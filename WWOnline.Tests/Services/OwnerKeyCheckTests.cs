using WWOnline.Server.Hubs;
using Xunit;
using Outcome = WWOnline.Server.Hubs.OwnerKeyCheck.Outcome;

namespace WWOnline.Tests.Services;

/// <summary>The owner key / host token check behind GameHub.ClaimRoomOwner.</summary>
public class OwnerKeyCheckTests
{
    private static OwnerKeyCheck With(string? key)
    {
        var check = new OwnerKeyCheck();
        check.Configure(key);
        return check;
    }

    [Fact]
    public void RightKey_IsAccepted()
    {
        var check = With("s3cret-key");
        Assert.True(check.IsConfigured);
        Assert.Equal(Outcome.Accepted, check.Check("c1", "s3cret-key"));
        Assert.Equal(Outcome.Accepted, check.Check("c1", "s3cret-key")); // re-claims keep working
    }

    [Theory]
    [InlineData("wrong")]
    [InlineData("s3cret-ke")]      // prefix
    [InlineData("s3cret-key ")]    // not trimmed by the server
    [InlineData("S3CRET-KEY")]     // case-sensitive
    [InlineData("")]
    [InlineData(null)]
    public void OtherTokens_AreRejected(string? token) =>
        Assert.Equal(Outcome.Rejected, With("s3cret-key").Check("c1", token));

    [Fact]
    public void UnicodeKeys_CompareByUtf8()
    {
        var check = With("clé-ünïcode-🗝");
        Assert.Equal(Outcome.Accepted, check.Check("c1", "clé-ünïcode-🗝"));
        Assert.Equal(Outcome.Rejected, check.Check("c2", "cle-unicode-🗝"));
    }

    [Fact]
    public void NoKeyConfigured_RejectsEverything()
    {
        var check = With(null);
        Assert.False(check.IsConfigured);
        Assert.Equal(Outcome.Rejected, check.Check("c1", "anything"));
        Assert.Equal(Outcome.Rejected, check.Check("c1", ""));
    }

    [Fact]
    public void OverlongToken_IsRejected_EvenIfItWouldMatch()
    {
        var longKey = new string('k', OwnerKeyCheck.MaxKeyLength + 1);
        Assert.Equal(Outcome.Rejected, With(longKey).Check("c1", longKey));
        var maxKey = new string('k', OwnerKeyCheck.MaxKeyLength);
        Assert.Equal(Outcome.Accepted, With(maxKey).Check("c1", maxKey));
    }

    [Fact]
    public void AfterTooManyFailures_TheConnectionIsIgnored_EvenWithTheRightKey()
    {
        var check = With("right");
        for (int i = 1; i < OwnerKeyCheck.MaxFailedAttempts; i++)
            Assert.Equal(Outcome.Rejected, check.Check("guesser", $"guess{i}"));
        Assert.Equal(Outcome.RejectedLastAttempt, check.Check("guesser", "last guess"));
        Assert.Equal(Outcome.Ignored, check.Check("guesser", "another"));
        Assert.Equal(Outcome.Ignored, check.Check("guesser", "right"));

        // Other connections are unaffected.
        Assert.Equal(Outcome.Accepted, check.Check("owner", "right"));
    }

    [Fact]
    public void ASuccess_ResetsTheFailureCount()
    {
        var check = With("right");
        for (int i = 1; i < OwnerKeyCheck.MaxFailedAttempts; i++)
            check.Check("c1", "typo");
        Assert.Equal(Outcome.Accepted, check.Check("c1", "right"));
        Assert.Equal(Outcome.Rejected, check.Check("c1", "typo")); // counting from zero again
    }

    [Fact]
    public void ForgetAndReconfigure_ClearFailures()
    {
        var check = With("right");
        for (int i = 0; i < OwnerKeyCheck.MaxFailedAttempts; i++)
            check.Check("c1", "wrong");
        Assert.Equal(Outcome.Ignored, check.Check("c1", "right"));

        check.Forget("c1");
        Assert.Equal(Outcome.Accepted, check.Check("c1", "right"));

        for (int i = 0; i < OwnerKeyCheck.MaxFailedAttempts; i++)
            check.Check("c2", "wrong");
        check.Configure("new-key");
        Assert.Equal(Outcome.Accepted, check.Check("c2", "new-key"));
        Assert.Equal(Outcome.Rejected, check.Check("c3", "right")); // the old key is gone
    }
}
