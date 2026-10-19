using static Common;

/// <summary>
/// The Shree-Lipi Telugu font families. The capture's family check (cap-tel\fonts\family-check.json: (3,1) cmap, glyph
/// id, advance and outline bytes of every installed face) found all ten full faces with the same 216 codes on the same
/// code points and glyph ids, and each 7 face identical to its 4/5/6 twin, outline bytes and advances, on 216 of 216
/// codes (M0900890 = M09000F0, N0902890 = N09020F0, M1642890 = M16420F0, N1644890 = N16440F0). So one table serves
/// Shree-Lipi 7.0 and 4.0/5.0/6.0. Drawn code by code (cap-tel\fonts\codes-*.png, r3\tbl-tel\round1\nga-*.png), the
/// faces mean the same at every code but one, 92:
///
///   shree-tel7   SHREE-TEL7-Apoorva-0900/Asmitha-1642 (M/N...890.TTF) and SHREE-TEL-Apoorva-0900/Asmitha-1642
///                (M/N...0F0.TTF): 92 is the rupee sign ₹. So ₹ is written 92; '*' (the composer's 92) has no
///                look-alike here and is left out, reported.
///   shree-tel7w  Shree-Tel7-001/002 (WTEL7001/7002.TTF), another design: 92 is a star ★, the composer's spelling of '*'.
///                No code draws ₹: it passes through as U+20B9, reported, and Word draws it in another font (P3b).
///
/// Itself: the ASCII punctuation every face draws as itself at its own code (r3\word\final-ascii.txt and the code sheets):
/// ! % ( ) + , . / : ; = ? - and the digits, which draw Latin digits (the composer writes the Telugu digits ౦-౯ there).
/// '-' 2D is a blank glyph: the hyphen is 26, the composer's spelling of '-'.
/// </summary>
public sealed record Fam(string Id, string Name, string Fonts, string FilePattern, bool RupeeAt92, string Itself)
{
    /// <summary>A truth spelling in this family's codes: the same codes in both families.</summary>
    public string Apply(string hex) => hex;

    public List<Truth> Truth(IEnumerable<Truth> truth) => truth.ToList();

    /// <summary>The faces' family names (the fonts' name table), as languages.json lists them.</summary>
    public string FamilyRegex => RupeeAt92 ? "^SHREE-TEL7?-" : "^Shree-Tel7-00[12]$";

    public static readonly Fam Tel7 = new("shree-tel7", "Shree-Lipi 7.0 and 4.0/5.0/6.0 Telugu",
        "SHREE-TEL7-Apoorva-0900/Asmitha-1642, SHREE-TEL-Apoorva-0900/Asmitha-1642", @"^[MN]\d{4}(890|0F0)\.TTF$", true, "!%()+,./:;=?");
    public static readonly Fam Tel7W = new("shree-tel7w", "Shree-Lipi 7.0 Telugu (Shree-Tel7-001/002)",
        "Shree-Tel7-001/002", @"^WTEL700[12]\.TTF$", false, "!%()+,./:;=?");

    public static readonly Fam[] All = { Tel7, Tel7W };

    public static Fam Get(string id) => All.FirstOrDefault(f => f.Id == id) ?? throw new ArgumentException("no family " + id);
}
