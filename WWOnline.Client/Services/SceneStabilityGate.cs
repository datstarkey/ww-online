using WWOnline.Data;

namespace WWOnline.Services;

/// <summary>
/// "Is it safe to touch save data right now?" for the sync services: Link exists, we're in
/// gameplay (not the title demo / file select, which run on an empty default save), no stage
/// change is pending (play.mNextStage.mEnable), and the stage name has been the same for
/// <see cref="RequiredTicks"/> consecutive checks. Stateful — one instance per caller, checked
/// once per tick.
/// </summary>
public class SceneStabilityGate
{
    public int RequiredTicks { get; }

    private string _lastStage = "";
    private int _stableTicks;

    public SceneStabilityGate(int requiredTicks) => RequiredTicks = requiredTicks;

    /// <summary>The stage name seen by the last <see cref="Check"/> ("" if none).</summary>
    public string Stage => _lastStage;

    public bool Check(IDolphinService dolphin)
    {
        if (dolphin.ReadScene() is not { LinkPresent: true, NextStagePending: false } scene)
        {
            _stableTicks = 0;
            return false;
        }

        var stage = scene.Stage;
        if (stage.Length == 0 || StageIDs.IsNonGameplayStage(stage))
        {
            // Title demo / file select: Link exists but the save is the empty default one.
            _lastStage = stage;
            _stableTicks = 0;
            return false;
        }
        if (stage != _lastStage)
        {
            _lastStage = stage;
            _stableTicks = 0;
            return false;
        }
        return ++_stableTicks >= RequiredTicks;
    }

    public void Reset()
    {
        _lastStage = "";
        _stableTicks = 0;
    }

    /// <summary>
    /// No event running (talks, shops, trades, cutscenes: play.mEvtCtrl.mMode != 0) and the pause menu is
    /// closed: safe to rewrite save data the game itself only changes from an event (a bag, a figurine).
    /// </summary>
    public static bool IsIdle(IDolphinService dolphin) => dolphin.ReadScene() is { IsIdle: true };
}
