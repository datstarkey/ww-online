using System.Globalization;
using System.Text;
using WWOnline.Data;

namespace WWOnline.Services;

/// <summary>
/// Player names above puppets: what the client writes into the puppet REL's names block
/// (GameMod/src/puppet_link/puppet_nametag.c; layout in puppet_shared.h PUPPET_NAMES_*).
///
/// The REL allocates the block on the game heap the first time a puppet is created, publishes its
/// address at PUPPET_NAMES_PTR_ADDR and never frees it (the next REL instance adopts it), so the
/// client may write it whenever the pointer is in MEM1, the block's magic matches and its boot stamp
/// is this boot's __OSStartTime (a soft reset leaves the pointer but recreates the heap). The client
/// compares the block with what it wants each tick and writes only what differs: the SHOW flag
/// ("Show player names") and one NUL-terminated name per puppet slot. The REL draws the names in the
/// game's message font, which only has the characters the game's English text uses, so names are
/// sanitised to printable ASCII (no Shift-JIS lead bytes can reach the font).
/// </summary>
public static class PuppetNameTags
{
    /// <summary>MEM1 ends here with use_extra_memory.asm (48MB); the block must lie below it.</summary>
    public const uint Mem1End = BootStampedBlock.Mem1End;

    /// <summary>
    /// A name as the game can draw it: accents dropped (é -> e), whitespace runs as one space,
    /// control characters removed, anything else outside printable ASCII as '?', trimmed and cut to
    /// <see cref="PuppetLayout.PUPPET_NAME_MAX_CHARS"/>. Empty when nothing readable (no letter or
    /// digit) is left.
    /// </summary>
    public static string Sanitize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";

        var sb = new StringBuilder(PuppetLayout.PUPPET_NAME_MAX_CHARS);
        bool space = false;
        string decomposed = name.Normalize(NormalizationForm.FormD);
        for (int i = 0; i < decomposed.Length && sb.Length < PuppetLayout.PUPPET_NAME_MAX_CHARS; i++)
        {
            char c = decomposed[i];
            if (char.IsWhiteSpace(c))
            {
                space = sb.Length > 0;
                continue;
            }
            if (char.IsControl(c) || char.IsLowSurrogate(c) || IsCombiningMark(c))
                continue;

            string text = c is >= '!' and <= '~' ? c.ToString() : Fold(c);
            if (char.IsHighSurrogate(c) && i + 1 < decomposed.Length && char.IsLowSurrogate(decomposed[i + 1]))
                i++; // one '?' per astral character (emoji), not two

            if (space)
            {
                sb.Append(' ');
                space = false;
            }
            foreach (char o in text)
            {
                if (sb.Length < PuppetLayout.PUPPET_NAME_MAX_CHARS)
                    sb.Append(o);
            }
        }

        string result = sb.ToString().TrimEnd();
        return result.Any(char.IsAsciiLetterOrDigit) ? result : "";
    }

    /// <summary>The name shown above a slot's puppet: the sanitised name, or "Player N" when none is readable.</summary>
    public static string DisplayName(string? name, int slot)
    {
        string sanitized = Sanitize(name);
        return sanitized.Length > 0 ? sanitized : $"Player {slot + 1}";
    }

    /// <summary>A slot's PUPPET_NAME_BYTES: the ASCII name, NUL-padded (always NUL-terminated).</summary>
    public static byte[] Encode(string? sanitized)
    {
        var bytes = new byte[PuppetLayout.PUPPET_NAME_BYTES];
        if (string.IsNullOrEmpty(sanitized))
            return bytes;
        int n = Math.Min(sanitized.Length, PuppetLayout.PUPPET_NAME_BYTES - 1);
        for (int i = 0; i < n; i++)
        {
            char c = sanitized[i];
            bytes[i] = c is >= ' ' and <= '~' ? (byte)c : (byte)'?';
        }
        return bytes;
    }

    /// <summary>Is <paramref name="address"/> a plausible names block (whole block in MEM1, word aligned)?</summary>
    public static bool IsValidBlockAddress(uint address) =>
        BootStampedBlock.IsValidAddress(address, PuppetLayout.PUPPET_NAMES_BLOCK_SIZE);

    /// <summary>What one <see cref="Publish"/> found and wrote.</summary>
    /// <param name="Block">The names block's address.</param>
    /// <param name="FlagsWritten">The SHOW flag differed and was written.</param>
    /// <param name="SlotsWritten">Bit i set: slot i's name differed and was written.</param>
    public readonly record struct PublishResult(uint Block, bool FlagsWritten, int SlotsWritten);

    /// <summary>
    /// Bring the names block in line with <paramref name="show"/> and <paramref name="names"/> (one
    /// already-sanitised name per slot, "" = none), writing only what differs. Null when there is no
    /// usable block (no puppet created since boot, a bad pointer / magic, or a block from before a reboot),
    /// in which case nothing is written.
    /// </summary>
    public static PublishResult? Publish(IDolphinService dolphin, bool show, IReadOnlyList<string> names)
    {
        // Only a block made in this boot (BootStampedBlock): after a soft reset the old one is someone
        // else's memory, and writing it would corrupt the game.
        if (BootStampedBlock.Read(dolphin, PuppetLayout.PUPPET_NAMES_PTR_ADDR, PuppetLayout.PUPPET_NAMES_MAGIC,
                PuppetLayout.PUPPET_NAMES_BLOCK_SIZE) is not { } found)
            return null;
        var (block, current) = found;

        uint wantFlags = show ? PuppetLayout.PUPPET_NAMES_FLAG_SHOW : 0u;
        bool flagsWritten = false;
        if (ReadU32(current, PuppetLayout.PUPPET_NAMES_OFF_FLAGS) != wantFlags)
        {
            dolphin.WriteMemory(block + PuppetLayout.PUPPET_NAMES_OFF_FLAGS,
                [(byte)(wantFlags >> 24), (byte)(wantFlags >> 16), (byte)(wantFlags >> 8), (byte)wantFlags]);
            flagsWritten = true;
        }

        int slotsWritten = 0;
        for (int i = 0; i < PuppetLayout.PUPPET_MAX_SLOTS; i++)
        {
            byte[] want = Encode(i < names.Count ? names[i] : "");
            int offset = PuppetLayout.PUPPET_NAMES_OFF_NAME0 + i * PuppetLayout.PUPPET_NAME_BYTES;
            if (current.AsSpan(offset, PuppetLayout.PUPPET_NAME_BYTES).SequenceEqual(want))
                continue;
            dolphin.WriteMemory(block + (uint)offset, want);
            slotsWritten |= 1 << i;
        }

        return new PublishResult(block, flagsWritten, slotsWritten);
    }

    private static bool IsCombiningMark(char c) => CharUnicodeInfo.GetUnicodeCategory(c) is
        UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;

    /// <summary>Latin letters that don't decompose into ASCII + accent; everything else is '?'.</summary>
    private static string Fold(char c) => c switch
    {
        'ß' => "ss",
        'æ' => "ae",
        'Æ' => "AE",
        'œ' => "oe",
        'Œ' => "OE",
        'ø' => "o",
        'Ø' => "O",
        'đ' => "d",
        'Đ' => "D",
        'ł' => "l",
        'Ł' => "L",
        'ı' => "i",
        'þ' => "th",
        'Þ' => "Th",
        _ => "?",
    };

    private static uint ReadU32(byte[] b, int offset) =>
        (uint)(b[offset] << 24 | b[offset + 1] << 16 | b[offset + 2] << 8 | b[offset + 3]);
}
