namespace FontSoftware.App.Services;

/// <summary>One physical key: its Windows virtual-key code and English label.</summary>
public readonly record struct PhysicalKey(int VirtualKey, string Label);

/// <summary>
/// The physical US keyboard layout used by the on-screen viewer (FR-2). This is
/// hardware geometry, not language data, so it is fixed. The character shown on
/// each key comes from the active <c>KeyboardLayout</c> at runtime.
/// </summary>
public static class PhysicalKeyboard
{
    // OEM virtual-key codes for the punctuation keys.
    private const int OemTilde = 0xC0, OemMinus = 0xBD, OemPlus = 0xBB;
    private const int OemOpen = 0xDB, OemClose = 0xDD, OemPipe = 0xDC;
    private const int OemSemicolon = 0xBA, OemQuote = 0xDE;
    private const int OemComma = 0xBC, OemPeriod = 0xBE, OemSlash = 0xBF;

    public static IReadOnlyList<IReadOnlyList<PhysicalKey>> Rows { get; } = new[]
    {
        NumberRow(),
        Row("QWERTYUIOP", new[] { OemOpen, OemClose, OemPipe }, "[]\\"),
        Row("ASDFGHJKL", new[] { OemSemicolon, OemQuote }, ";'"),
        Row("ZXCVBNM", new[] { OemComma, OemPeriod, OemSlash }, ",./")
    };

    private static IReadOnlyList<PhysicalKey> NumberRow()
    {
        var keys = new List<PhysicalKey> { new(OemTilde, "`") };
        foreach (char c in "1234567890") // digit virtual keys equal their ASCII code
            keys.Add(new PhysicalKey(c, c.ToString()));
        keys.Add(new PhysicalKey(OemMinus, "-"));
        keys.Add(new PhysicalKey(OemPlus, "="));
        return keys;
    }

    // Letter/OEM row: A..Z virtual keys equal their ASCII code; trailing OEM keys follow.
    private static IReadOnlyList<PhysicalKey> Row(string letters, int[] extraVks, string extraLabels)
    {
        var keys = new List<PhysicalKey>();
        foreach (char c in letters)
            keys.Add(new PhysicalKey(c, c.ToString()));
        for (int i = 0; i < extraVks.Length; i++)
            keys.Add(new PhysicalKey(extraVks[i], extraLabels[i].ToString()));
        return keys;
    }
}
