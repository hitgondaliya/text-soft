using FontSoftware.Core.Config;

// ConvTest — live end-to-end exercise of the converter through the REAL asset
// bundle (the app's exact code path). Round-trips Unicode -> legacy -> Unicode
// for every wired script and reports any mismatch, so table/reorder bugs surface.

var bundle = new ConfigLoader(Path.Combine(AppContext.BaseDirectory, "config")).Load();

// (encoding, unicodeFontId, sample words)
var scripts = new (string Enc, string Uni, string[] Words)[]
{
    ("shree-devx", "mangal",  new[] { "कमल", "नमस्ते", "हिंदी", "विद्यालय", "भारत", "श्री", "कर्म", "सूर्य", "धर्म" }),
    ("shree-gujx", "shruti",  new[] { "કમલ", "નમસ્તે", "ભારત", "વિદ્યા", "શ્રી", "કર્મ" }),
    ("shree-banx", "vrinda",  new[] { "কমল", "নমস্তে", "ভারত", "বিদ্যা", "কর্ম" }),
    ("shree-telx", "gautami", new[] { "కమల", "భారత", "విద్య" }),
    ("shree-punx", "raavi",   new[] { "ਕਮਲ", "ਭਾਰਤ", "ਵਿਦਿਆ" }),
    ("shree-kanx", "tunga",   new[] { "ಕಮಲ", "ಭಾರತ", "ವಿದ್ಯಾ" }),
    ("shree-malx", "kartika", new[] { "കമല", "ഭാരതം", "വിദ്യ" }),
    ("shree-orix", "kalinga", new[] { "କମଳ", "ଭାରତ", "ବିଦ୍ୟା" }),
    ("shree-tamx", "latha",   new[] { "கமலம்", "பாரதம்", "வித்தை" }),
};

// Words per script block, so any encoding wired later is exercised too without
// having to be listed here by hand.
// The second half of each list is the characters a full-matrix run once found
// missing — the danda, Gujarati's independent vowels, Tamil ள, the nuktas. They
// are ordinary letters a customer types on day one, so they stay here as a guard.
var wordsByBlock = new Dictionary<int, string[]>
{
    [0x0900] = new[] { "कमल", "नमस्ते", "विद्यालय", "कर्म", "राम।" },
    [0x0980] = new[] { "কমল", "নমস্তে", "বিদ্যা", "কর্ম", "ভারত৷" },
    [0x0A00] = new[] { "ਕਮਲ", "ਭਾਰਤ", "ਵਿਦਿਆ", "ਭਾਸ਼ਾ", "ਖ਼ਬਰ" },
    [0x0A80] = new[] { "કમલ", "નમસ્તે", "વિદ્યા", "ક્ષત્રિય", "આ", "એ", "ઐ", "ઓ", "ઔ", "કો", "કૌ", "ક઼" },
    [0x0B00] = new[] { "କମଳ", "ଭାରତ", "ବିଦ୍ୟା", "ଅଛି", "ଆମେ", "ୱ" },
    [0x0B80] = new[] { "கமலம்", "பாரதம்", "வித்தை", "ளி" },
    [0x0C00] = new[] { "కమల", "భారత", "విద్య" },
    [0x0C80] = new[] { "ಕಮಲ", "ಭಾರತ", "ವಿದ್ಯಾ", "ಕನ್ನಡ" },
    [0x0D00] = new[] { "കമല", "ഭാരതം", "വിദ്യ", "നല്ല", "ഈ", "ഊ", "ഐ", "ഓ", "ഔ" },
};

// every legacy encoding actually wired in languages.json, paired with its language's
// first Unicode font — this is what the app itself offers the user
var listed = new HashSet<string>(scripts.Select(s => s.Enc), StringComparer.Ordinal);
var discovered = new List<(string Enc, string Uni, string[] Words)>();
foreach (var lang in bundle.Languages)
{
    var uni = lang.Fonts.FirstOrDefault(f => f.Type == FontSoftware.Core.Models.FontType.Unicode);
    if (uni is null) continue;
    int block = lang.Sample.Length > 0 && lang.Sample[0] >= 0x0900 && lang.Sample[0] <= 0x0D7F
        ? (lang.Sample[0] - 0x0900) / 0x80 * 0x80 + 0x0900
        : 0;
    if (!wordsByBlock.TryGetValue(block, out var words)) continue;
    foreach (var enc in lang.Fonts.Where(f => f.Type == FontSoftware.Core.Models.FontType.Legacy)
                                  .Select(f => f.ConversionKey).Distinct(StringComparer.Ordinal))
    {
        if (!listed.Add(enc)) continue;
        if (bundle.FindMapping(uni.ConversionKey, enc) is null) continue;
        discovered.Add((enc, uni.ConversionKey, words));
    }
}
scripts = scripts.Concat(discovered).ToArray();
Console.WriteLine($"testing {scripts.Length} encodings ({discovered.Count} discovered from config)");

int total = 0, pass = 0;
var problems = new List<string>();

foreach (var s in scripts)
{
    var toLegacy = bundle.CreateConverter(s.Uni, s.Enc);
    var toUnicode = bundle.CreateConverter(s.Enc, s.Uni);
    if (toLegacy is null || toUnicode is null)
    {
        problems.Add($"[{s.Enc}] MISSING converter (fwd={toUnicode is not null}, rev={toLegacy is not null})");
        continue;
    }

    Console.WriteLine($"\n== {s.Enc} <-> {s.Uni} ==");
    foreach (var word in s.Words)
    {
        total++;
        var legacy = toLegacy.Convert(word);
        var back = toUnicode.Convert(legacy.Text);
        // a nukta letter can come back precomposed (ਸ਼) where the input was
        // decomposed (ਸ + ਼) — same text, different normalisation
        // Unmapped characters pass through untouched, so a round trip can "match"
        // while the legacy text is really the original letters in a legacy font —
        // i.e. garbage on screen. Treat any unmapped character as a failure.
        bool ok = back.Text.Normalize(System.Text.NormalizationForm.FormC) == word.Normalize(System.Text.NormalizationForm.FormC)
                  && legacy.UnmappedCharacters.Count == 0;
        if (ok) pass++;
        else problems.Add($"[{s.Enc}] FAIL: '{word}' -> legacy '{legacy.Text}' -> '{back.Text}'" + (legacy.UnmappedCharacters.Count > 0 ? $" (unmapped: {string.Join("", legacy.UnmappedCharacters)})" : ""));

        string flag = ok ? "ok  " : "FAIL";
        string unmappedFwd = legacy.UnmappedCharacters.Count > 0 ? $"  ip-unmapped[{string.Join("", legacy.UnmappedCharacters)}]" : "";
        string unmappedBack = back.UnmappedCharacters.Count > 0 ? $"  op-unmapped[{string.Join("", back.UnmappedCharacters)}]" : "";
        Console.WriteLine($"  {flag}  {word,-12} -> {legacy.Text,-14} -> {back.Text}{unmappedFwd}{unmappedBack}");
    }
}

Console.WriteLine($"\n==== round-trip {pass}/{total} passed ====");
if (problems.Count > 0)
{
    Console.WriteLine($"\n{problems.Count} problem(s):");
    foreach (var p in problems) Console.WriteLine("  " + p);
}
return problems.Count == 0 ? 0 : 1;
