using System.IO;
using ClientReport;
using FontSoftware.Core.Config;

// WebsiteReview — the content changes text-soft.com needs so the site matches the
// software that actually ships. Reviewed pages: home, features, translator,
// converter, pricing, why-textsoft, about-us, blog, contact.
//
//   dotnet run --project tools/WebsiteReview -- "C:\path\TextSoft-Website-Changes.pdf"

string outPath = args.Length > 0
    ? args[0]
    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "TextSoft-Website-Changes.pdf");
string repo = FindRoot();
var bundle = new ConfigLoader(Path.Combine(repo, "assets", "config")).Load();

const string Orange = "EA580C", Deep = "9A3412", Ink = "23262B", Muted = "6B7280",
             Rule = "E2E0DC", Zebra = "F7F6F4", Tint = "FDECE1", White = "FFFFFF",
             Red = "B91C1C", Green = "047857";
const double L = 46, R = Pdf.PageWidth - 46, Top = Pdf.PageHeight - 60, Bottom = 62;
double W = R - L;

var pdf = new Pdf();
int section = 0;

void Footer()
{
    pdf.Line(L, Bottom - 14, R, Bottom - 14, Rule, 0.6);
    pdf.Text("TextSoft - website content changes", L, Bottom - 27, 8, false, Muted);
    string p = "Page " + (pdf.PageCount + 1);
    pdf.Text(p, R - Pdf.Width(p, 8), Bottom - 27, 8, false, Muted);
}

void Break(double need)
{
    if (pdf.Y - need >= Bottom) return;
    Footer(); pdf.NewPage(); pdf.Y = Top;
}

void H1(string title)
{
    Break(92); section++;
    pdf.Fill(L, pdf.Y - 26, 3.5, 22, Orange);
    pdf.Text(section.ToString("00"), L + 12, pdf.Y - 20, 15, true, Orange);
    pdf.Text(title, L + 38, pdf.Y - 20, 15, true, Ink);
    pdf.Y -= 40;
}

void H2(string title)
{
    Break(54);
    pdf.Text(title, L, pdf.Y - 12, 10.5, true, Deep);
    pdf.Y -= 24;
}

void Para(string text, double size = 9.5, string color = "333333")
{
    var all = Pdf.Wrap(text, W, size);
    Break(Math.Min(all.Count, 3) * (size + 4.2) + 6);
    foreach (string line in all) { Break(size + 6); pdf.Text(line, L, pdf.Y - size, size, false, color); pdf.Y -= size + 4.2; }
    pdf.Y -= 5;
}

void Bullets(IEnumerable<string> items, double size = 9.5, string bullet = Orange)
{
    foreach (string item in items)
    {
        var lines = Pdf.Wrap(item, W - 16, size);
        Break(lines.Count * (size + 3.6) + 6);
        for (int i = 0; i < lines.Count; i++)
        {
            if (i == 0) pdf.Fill(L + 2, pdf.Y - size + 2.5, 3, 3, bullet);
            pdf.Text(lines[i], L + 16, pdf.Y - size, size, false, "333333");
            pdf.Y -= size + 3.6;
        }
        pdf.Y -= 2;
    }
    pdf.Y -= 4;
}

void Table(string[] headers, IEnumerable<string[]> rows, double[] widths, double size = 8.8,
           string[]? colColors = null)
{
    void Head()
    {
        Break(30);
        double hy = pdf.Y - 18;
        pdf.Fill(L, hy, W, 18, Ink);
        double x = L + 7;
        for (int i = 0; i < headers.Length; i++) { pdf.Text(headers[i], x, hy + 5.5, size, true, White); x += widths[i]; }
        pdf.Y = hy;
    }
    Head();
    bool alt = false;
    foreach (string[] row in rows)
    {
        var cells = new List<string>[row.Length];
        int lines = 1;
        for (int i = 0; i < row.Length; i++)
        {
            cells[i] = Pdf.Wrap(row[i], widths[i] - 12, size);
            if (cells[i].Count == 0) cells[i].Add("");
            lines = Math.Max(lines, cells[i].Count);
        }
        double h = lines * (size + 3.2) + 7;
        if (pdf.Y - h < Bottom) { Footer(); pdf.NewPage(); pdf.Y = Top; Head(); alt = false; }
        double ry = pdf.Y - h;
        if (alt) pdf.Fill(L, ry, W, h, Zebra);
        pdf.Line(L, ry, R, ry, Rule, 0.5);
        double x = L + 7;
        for (int i = 0; i < row.Length; i++)
        {
            string c = colColors is not null && i < colColors.Length ? colColors[i] : "2A2D33";
            for (int j = 0; j < cells[i].Count; j++)
                pdf.Text(cells[i][j], x, pdf.Y - 12 - j * (size + 3.2), size, false, c);
            x += widths[i];
        }
        pdf.Y = ry;
        alt = !alt;
    }
    pdf.Y -= 14;
}

// A block of copy the client can paste as-is.
void Copy(string heading, IEnumerable<string> lines)
{
    var wrapped = new List<string>();
    foreach (string l in lines) wrapped.AddRange(l.Length == 0 ? new List<string> { "" } : Pdf.Wrap(l, W - 28, 9));
    double h = wrapped.Count * 12.6 + 34;
    Break(h + 10);
    double y = pdf.Y - h;
    pdf.Fill(L, y, W, h, Tint);
    pdf.Fill(L, y, 3, h, Orange);
    pdf.Text(heading, L + 14, pdf.Y - 16, 9, true, Deep);
    double ty = pdf.Y - 32;
    foreach (string l in wrapped) { pdf.Text(l, L + 14, ty, 9, false, "3A2A20"); ty -= 12.6; }
    pdf.Y = y - 14;
}

// ---------- cover ----------
pdf.Fill(0, Pdf.PageHeight - 210, Pdf.PageWidth, 210, Ink);
pdf.Fill(0, Pdf.PageHeight - 210, Pdf.PageWidth, 5, Orange);
string logo = Path.Combine(repo, "logo.png");
if (File.Exists(logo)) { pdf.Fill(L, Pdf.PageHeight - 110, 48, 48, White); pdf.Image(logo, L + 4, Pdf.PageHeight - 106, 40, 40); }
pdf.Text("TextSoft", L + 62, Pdf.PageHeight - 88, 26, true, White);
pdf.Text("Beyond Typing", L + 64, Pdf.PageHeight - 104, 10, true, Orange);
pdf.Text("Website content changes", L, Pdf.PageHeight - 150, 17, true, White);
pdf.Text("text-soft.com reviewed against the software that ships today", L, Pdf.PageHeight - 172, 10, false, "C9C6C2");
pdf.Text(DateTime.Now.ToString("d MMMM yyyy"), L, Pdf.PageHeight - 192, 9, false, "8D8A86");

pdf.Y = Pdf.PageHeight - 240;
Para("The site reads well, but several claims on it do not match the product, and a few of the strongest " +
     "things the software does are not mentioned at all. This document lists every change, page by page, " +
     "with wording that can be pasted straight in. Pages reviewed: home, features, translator, converter, " +
     "pricing, why-textsoft, about-us, blog and contact.");

Para("Nothing here is about design. The layout, colours and structure are fine; only the words need work.");

H1("Fix these first");
Para("Six items matter more than the rest. Two are promises the software cannot keep, and a customer will " +
     "notice within the first week.");
Bullets(new[]
{
    "\"Internet is needed only once, to activate\" - the licence re-checks about once a day. A PC that stays " +
    "offline keeps working for five days and then asks for a connection. Saying otherwise creates a support " +
    "call we cannot answer.",
    "\"Tampering is reported to us instantly by email\" - no email is sent. Refused activations are recorded " +
    "in our licence panel with the PC name, IP address and reason. Say that instead; it is still strong.",
    "\"Windows 7, 8, 10, 11\" and \"the installer picks 32-bit or 64-bit\" - the build needs Windows 10 or 11 " +
    "and is 64-bit only. A Windows 7 buyer will ask for a refund.",
    "\"~18 MB\" - the installer is about 59 MB because the runtime is bundled. Keep \"no separate .NET " +
    "download\", correct the size.",
    "The Translator page says the software translates one language into another. It does not translate; it " +
    "types the script from English spelling. Call it phonetic typing.",
    "The language list carries scripts the product does not have - Arabic, Urdu, Sindhi, Nepali, Konkani, " +
    "Maithili, Bodo and English - and leaves out Manipuri, which it does have.",
});

// ---------- corrections ----------
H1("Corrections, page by page");
Para("Left column is what the site says now; right column is what it should say. Where the reason is not " +
     "obvious it is written into the replacement.");

H2("Home page");
Table(new[] { "Where", "It says now", "Change to" },
    new[]
    {
        new[] { "Top strip", "Windows 7-11 Pro & Mac (soon) - One-time lifetime licence",
                "Windows 10 & 11, 64-bit - one-time licence. Drop Mac until there is a date to announce." },
        new[] { "Hero buttons", "Download now / Try the translator",
                "Download now / Try phonetic typing" },
        new[] { "Capability card", "Works online & offline. An internet connection is only needed once, to activate your licence.",
                "Works offline. Typing and conversion never leave your PC. The licence checks in about once a day; offline, it keeps working for five days." },
        new[] { "Capability card", "Tamper-proof licensing ... reported to us instantly by email.",
                "Device-bound licensing. Each key works on one PC. Every refused activation is recorded in our licence panel with the PC name and IP address." },
        new[] { "Capability card", "On-screen keyboard ... with Shift and AltGr states",
                "On-screen keyboard with the Shift layer. There is no AltGr layer in the layouts we ship." },
        new[] { "Demo caption", "A taste of the Translator engine - language-to-language conversion",
                "Phonetic typing: write the sound in English letters and the script appears as you type." },
        new[] { "Languages grid", "21 tiles including Arabic, Urdu, Sindhi, Nepali, Konkani, Maithili, Bodo, English",
                "List only what ships: Hindi, Marathi, Sanskrit, Gujarati, Punjabi, Bangla, Assamese, Manipuri, Odia, Tamil, Telugu, Kannada, Malayalam." },
        new[] { "Download block", "v1.0 - ~18 MB - installer detects 32-bit or 64-bit automatically",
                "v1.0 - about 59 MB - 64-bit - everything bundled, no separate .NET download." },
    },
    new[] { 74, (W - 74) * 0.44, (W - 74) * 0.56 },
    8.5, new[] { Ink, Red, Green });

H2("Features page");
Para("Same eight cards as the home page, so the same three corrections apply: offline wording, the email " +
     "claim and AltGr. Two cards can be added while you are in there - see section 04.");

H2("Translator page");
Table(new[] { "Where", "It says now", "Change to" },
    new[]
    {
        new[] { "Page title", "Translator - Language to Language", "Phonetic typing - type the sound, get the script" },
        new[] { "Heading", "Language-to-language conversion", "Type in English letters, write in your script" },
        new[] { "Body", "The Translator turns text from one language into another.",
                "Type namaste and get the script as you type, in any app. Available for nine scripts: Devanagari, Gujarati, Bangla, Punjabi, Tamil, Telugu, Kannada, Malayalam and Odia." },
        new[] { "Menu / footer", "Translator", "Phonetic typing" },
    },
    new[] { 74, (W - 74) * 0.44, (W - 74) * 0.56 }, 8.5, new[] { Ink, Red, Green });

H2("Converter page");
Table(new[] { "Where", "It says now", "Change to" },
    new[]
    {
        new[] { "Bulk card", "run whole documents and databases through the same engine",
                "convert whole files in one go - .txt, .rtf and .docx - with the formatting kept. Drop databases; the product does not read them." },
        new[] { "Comparison", "Translator - one language to another (Roman input to Devanagari script)",
                "Phonetic typing - type the sound in English letters and get the script." },
        new[] { "Missing", "-", "Add: a correction dictionary the customer fills in, and a choice of output digits - language, English or Hindi." },
    },
    new[] { 74, (W - 74) * 0.44, (W - 74) * 0.56 }, 8.5, new[] { Ink, Red, Green });

H2("Pricing page");
Table(new[] { "Where", "It says now", "Change to" },
    new[]
    {
        new[] { "Feature list", "Keyboard Generator",
                "Remove it. There is no keyboard generator in the product; custom layouts are something we build for a customer, so it belongs under Custom Solutions." },
        new[] { "Feature list", "Custom toggle key & per-app typing profiles",
                "Custom toggle key, and an exclude list for apps where typing should pass through untouched." },
        new[] { "Feature list", "Works fully offline - one-time online activation",
                "Works offline day to day - activation online, then a quiet check about once a day." },
        new[] { "Contradiction", "FAQ says lifetime updates; the box says AMC applies after 1 year",
                "Pick one and repeat it in both places. Suggested: one-time purchase, one year of updates and support included, optional annual maintenance after that." },
        new[] { "Plan name", "Lifetime licence",
                "Keep it only if the key you issue is perpetual. The panel can also issue keys that run for a set number of days, which is not a lifetime licence." },
    },
    new[] { 74, (W - 74) * 0.44, (W - 74) * 0.56 }, 8.5, new[] { Ink, Red, Green });

H2("Why TextSoft, About us, Blog");
Table(new[] { "Where", "It says now", "Change to" },
    new[]
    {
        new[] { "Why - card", "Private & offline ... the internet is used only once to activate",
                "Same offline wording as above." },
        new[] { "Why - card", "Impossible to crack ... auto-reported to us by email",
                "Hard to copy, easy to manage: one key, one PC, signed licences, and every refused activation on record in our panel. Avoid \"impossible to crack\" - no software can promise that, and it invites someone to try." },
        new[] { "About us", "type any language on any Windows or Mac system",
                "Windows today; say Mac only when it is being built." },
        new[] { "Blog post", "\"Software that can't be cracked - by design ... reports tampering automatically\"",
                "Same correction as the Why page. The post is otherwise good; fix the claim and keep it." },
    },
    new[] { 74, (W - 74) * 0.44, (W - 74) * 0.56 }, 8.5, new[] { Ink, Red, Green });

// ---------- copy blocks ----------
H1("Copy you can paste");
Para("Written to match the product exactly. Short sentences, no numbers that will go stale.");

Copy("Hero", new[]
{
    "Type every Indian script. Keep the fonts you already use.",
    "",
    "TextSoft types Hindi, Gujarati, Marathi, Tamil and ten more scripts directly inside Word, Excel,",
    "CorelDRAW, your billing software or a browser - one key turns it on. It also converts text between",
    "legacy fonts and Unicode, both ways, on text or whole files.",
    "",
    "Download now      See how conversion works",
});

Copy("Capability card - offline", new[]
{
    "Works offline",
    "Typing and conversion happen on your PC and nothing is sent anywhere. Activation needs the internet",
    "once; after that the licence checks in quietly about once a day, and a machine that stays offline keeps",
    "working for five days.",
});

Copy("Capability card - licensing", new[]
{
    "One key, one PC",
    "Every licence is tied to the computer it is activated on and signed so it cannot be copied to another",
    "machine. If a key is tried somewhere else it is refused, and the attempt is recorded in our licence",
    "panel with the PC name and IP address. Changing machines is a support request, and it takes a minute.",
});

Copy("Capability card - font follows the typist", new[]
{
    "Your font, applied for you",
    "Choose a legacy font once in TextSoft and type. In Word, Outlook, WordPad, LibreOffice and WPS the",
    "font is applied to what you type, so you never have to set it in the font box first.",
});

Copy("Capability card - files", new[]
{
    "Convert whole files, not just snippets",
    "Drop in .txt, .rtf and .docx files and convert them in one run, formatting kept. Add your own",
    "corrections to the dictionary and every future conversion follows them.",
});

Copy("Download block", new[]
{
    "Download TextSoft for Windows 10 and 11, activate your licence, and start typing in minutes.",
    "",
    "v1.0 - about 59 MB - 64-bit - everything bundled, no separate .NET download.",
});

// ---------- add these ----------
H1("Say these things too");
Para("All of this is in the product and none of it is on the site. Each one answers a question a buyer " +
     "actually has.");
Bullets(new[]
{
    "The font is applied automatically in Word, Outlook, WordPad, LibreOffice and WPS - the single most " +
    "asked-about thing for anyone moving from Shree-Lipi.",
    "Whole-file conversion for .txt, .rtf and .docx, with the formatting preserved.",
    "A correction dictionary: the customer's own word fixes, applied on every conversion.",
    "Output digits can be forced to the language, to English or to Hindi digits.",
    "Both directions between legacy families, not only legacy to Unicode - for example one Gujarati font " +
    "family to another.",
    "The app remembers the language, font and keyboard you last used, and can start with Windows and sit " +
    "in the tray.",
    "A demo edition exists, so a customer can try before paying.",
    "Licences are managed from our own panel: extend, revoke, move a licence to a new PC, see who activated " +
    "and from which machine.",
    "The keys and the invoice reach the customer by email the moment a purchase is created.",
});

// ---------- languages and keyboards ----------
H1("Languages and keyboard layouts");
Para("Replace the languages grid with exactly these, in this order. Manipuri is missing from the site today; " +
     "Arabic, Urdu, Sindhi, Nepali, Konkani, Maithili, Bodo and English are on the site but not in the " +
     "product, so they have to come off until they are.");

var langs = bundle.Languages.Where(l => l.Id != "dia").ToList();
Para(string.Join(",  ", langs.Select(l => l.Name)));

H2("Keyboard layouts per language");
Para("This is worth putting on the Languages page. A typist who has used Remington or a vendor layout for " +
     "twenty years wants to see the name of their layout before they buy.");
Table(new[] { "Language", "No.", "Layouts" },
    langs.Select(l => new[] { l.Name, l.Layouts.Count.ToString(), string.Join(", ", l.Layouts.Select(x => x.Name)) }),
    new[] { 70, 30, W - 100 }, 8.3);
Para("A separate Diacritical layout for transliteration work also ships with the product.");

// ---------- faq ----------
H1("FAQ, rewritten");
Copy("Does TextSoft work offline?", new[]
{
    "Yes, for the work itself. Typing and conversion run on your PC and your text never leaves it. The",
    "licence is activated online once, then checks in about once a day. If the computer is offline it keeps",
    "working for five days before it asks for a connection.",
});
Copy("Can the software be copied to another computer?", new[]
{
    "No. A key activates on one PC and is refused anywhere else, and the attempt is logged with the PC name",
    "and IP address. If you replace the machine, tell us and we move the licence across in a minute.",
});
Copy("Which languages are supported?", new[]
{
    "Hindi, Marathi, Sanskrit, Gujarati, Punjabi, Bangla, Assamese, Manipuri, Odia, Tamil, Telugu, Kannada",
    "and Malayalam, each with the keyboard layouts typists already know, and legacy font conversion both",
    "ways.",
});
Copy("Which operating systems?", new[]
{
    "Windows 10 and Windows 11, 64-bit. The installer carries everything it needs - there is no separate",
    ".NET download. A Mac version is not available.",
});
Copy("What does it cost?", new[]
{
    "A one-time purchase of Rs 4,999 including 18% GST, with a GST invoice. One year of updates and support",
    "is included; annual maintenance after that is optional.",
});
Copy("Do you build custom solutions?", new[]
{
    "Yes. Custom keyboard layouts, mapping tables for a font we have not seen before, bulk conversion of an",
    "archive, and organisation-wide rollouts. Send us the fonts and a sample and we will scope it.",
});

// ---------- housekeeping ----------
H1("Housekeeping");
Bullets(new[]
{
    "Footer links to Partnerships, Careers, Magazine and Feedback: keep them only if there is a page behind " +
    "them. An empty page costs more trust than a missing link.",
    "Privacy Policy, Terms and Refund Policy must exist before you take payments online - the refund policy " +
    "especially, since the licence is device-bound.",
    "The review quote on the home page names a real publishing house. Get their written consent, or change " +
    "it to an unnamed customer.",
    "Every page repeats the download strip, so the Windows version and size are wrong in eight places. Fix " +
    "it in the shared component once.",
    "Screenshots would carry this site: the typing window with the on-screen keyboard, the converter with " +
    "before and after text, and the licence panel. There are none today.",
});

Footer();
pdf.Save(outPath);
Console.WriteLine($"pages {pdf.PageCount}   ->  {outPath}");

static string FindRoot()
{
    var d = new DirectoryInfo(AppContext.BaseDirectory);
    while (d is not null && !File.Exists(Path.Combine(d.FullName, "FontSoftware.sln"))) d = d.Parent;
    return d?.FullName ?? throw new InvalidOperationException("solution root not found");
}
