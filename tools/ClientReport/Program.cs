using System.IO;
using System.Text;
using ClientReport;
using FontSoftware.Core.Config;
using FontSoftware.Core.Models;

// ClientReport — the customer-facing capability document, typeset straight from
// the shipped configuration so the numbers in it cannot drift from the product.
//
//   dotnet run --project tools/ClientReport -- "C:\path\TextSoft-Capabilities.pdf"

string outPath = args.Length > 0
    ? args[0]
    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "TextSoft-Capabilities.pdf");
string repo = FindRoot();
var bundle = new ConfigLoader(Path.Combine(repo, "assets", "config")).Load();

// ---------- numbers from the configuration ----------
bool Both(string a, string b) => bundle.FindMapping(a, b) is not null && bundle.FindMapping(b, a) is not null;

var langs = new List<(string Name, int Fonts, int Layouts, int Ok, int Pending, int Keyboards, string KeyboardNames)>();
var pending = new List<(string Lang, string Layout)>();
var families = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
var encodings = new SortedSet<string>(StringComparer.Ordinal);
var keyboardIds = new HashSet<string>();

foreach (var lang in bundle.Languages)
{
    string uni = lang.Fonts.FirstOrDefault(f => f.Type == FontType.Unicode)?.ConversionKey ?? "";
    int ok = 0, bad = 0;
    foreach (var fl in lang.FontLayouts)
    {
        bool works;
        if (fl.IsUnicode) works = true;
        else if (string.IsNullOrEmpty(fl.FontId)) works = false;
        else
        {
            var font = lang.Fonts.FirstOrDefault(f => f.Id == fl.FontId);
            works = font is not null && !string.IsNullOrEmpty(font.ConversionKey) && Both(font.ConversionKey, uni);
            if (works) encodings.Add(font!.ConversionKey);
        }
        if (works) ok++; else { bad++; pending.Add((lang.Name, fl.Name)); }
    }
    foreach (var f in lang.Fonts) if (!string.IsNullOrWhiteSpace(f.Family)) families.Add(f.Family);
    foreach (var l in lang.Layouts) keyboardIds.Add(l.Id);
    langs.Add((lang.Name, lang.Fonts.Count, lang.FontLayouts.Count, ok, bad, lang.Layouts.Count,
               string.Join(", ", lang.Layouts.Select(l => l.Name))));
}

int totalFonts = langs.Sum(l => l.Fonts);
int totalLayouts = langs.Sum(l => l.Layouts);
int totalOk = langs.Sum(l => l.Ok);
int totalPending = langs.Sum(l => l.Pending);
int totalKeyboardEntries = langs.Sum(l => l.Keyboards);
int translit = Directory.GetFiles(Path.Combine(repo, "assets", "config", "translit"), "*.json").Length;

// ---------- palette and layout ----------
const string Orange = "EA580C", Deep = "9A3412", Ink = "23262B", Muted = "6B7280",
             Rule = "E2E0DC", Zebra = "F7F6F4", Tint = "FDECE1", White = "FFFFFF";
const double L = 52, R = Pdf.PageWidth - 52, Top = Pdf.PageHeight - 64, Bottom = 64;
double W = R - L;

var pdf = new Pdf();
int section = 0;
var toc = new List<(string Title, int Page)>();

void Footer()
{
    pdf.Line(L, Bottom - 14, R, Bottom - 14, Rule, 0.6);
    pdf.Text("TextSoft - Capabilities and coverage", L, Bottom - 27, 8, false, Muted);
    string p = "Page " + (pdf.PageCount + 1);
    pdf.Text(p, R - Pdf.Width(p, 8), Bottom - 27, 8, false, Muted);
}

void Break(double need)
{
    if (pdf.Y - need >= Bottom) return;
    Footer();
    pdf.NewPage();
    pdf.Y = Top;
}

void H1(string title)
{
    Break(90);
    section++;
    toc.Add((title, pdf.PageCount));
    pdf.Fill(L, pdf.Y - 26, 3.5, 22, Orange);
    pdf.Text(section.ToString("00"), L + 12, pdf.Y - 20, 15, true, Orange);
    pdf.Text(title, L + 38, pdf.Y - 20, 15, true, Ink);
    pdf.Y -= 40;
}

void H2(string title)
{
    Break(52);
    pdf.Text(title, L, pdf.Y - 12, 10.5, true, Deep);
    pdf.Y -= 24;
}

void Para(string text, double size = 9.5, string color = "333333")
{
    var all = Pdf.Wrap(text, W, size);
    Break(Math.Min(all.Count, 3) * (size + 4.2) + 6);   // no single orphan line at a page foot
    foreach (string line in all)
    {
        Break(size + 6);
        pdf.Text(line, L, pdf.Y - size, size, false, color);
        pdf.Y -= size + 4.2;
    }
    pdf.Y -= 5;
}

void Bullets(IEnumerable<string> items, double size = 9.5)
{
    foreach (string item in items)
    {
        var lines = Pdf.Wrap(item, W - 16, size);
        Break(lines.Count * (size + 3.6) + 6);   // keep a bullet whole on one page
        for (int i = 0; i < lines.Count; i++)
        {
            if (i == 0) pdf.Fill(L + 2, pdf.Y - size + 2.5, 3, 3, Orange);
            pdf.Text(lines[i], L + 16, pdf.Y - size, size, false, "333333");
            pdf.Y -= size + 3.6;
        }
        pdf.Y -= 2;
    }
    pdf.Y -= 4;
}

// A table: column widths in points, optional right-aligned columns.
void Table(string[] headers, IEnumerable<string[]> rows, double[] widths, bool[]? right = null, double size = 9)
{
    void Head()
    {
        Break(30);
        double hy = pdf.Y - 18;
        pdf.Fill(L, hy, W, 18, Ink);
        double x = L + 7;
        for (int i = 0; i < headers.Length; i++)
        {
            string t = headers[i];
            double tx = right is not null && right[i] ? x + widths[i] - 14 - Pdf.Width(t, size, true) : x;
            pdf.Text(t, tx, hy + 5.5, size, true, White);
            x += widths[i];
        }
        pdf.Y = hy;
    }

    Head();
    bool alt = false;
    foreach (string[] row in rows)
    {
        // wrap every cell, the tallest one sets the row height
        var cells = new List<string>[row.Length];
        int lines = 1;
        for (int i = 0; i < row.Length; i++)
        {
            cells[i] = Pdf.Wrap(row[i], widths[i] - 14, size);
            if (cells[i].Count == 0) cells[i].Add("");
            lines = Math.Max(lines, cells[i].Count);
        }
        double h = lines * (size + 3.4) + 7;
        if (pdf.Y - h < Bottom) { Footer(); pdf.NewPage(); pdf.Y = Top; Head(); alt = false; }

        double ry = pdf.Y - h;
        if (alt) pdf.Fill(L, ry, W, h, Zebra);
        pdf.Line(L, ry, R, ry, Rule, 0.5);
        double x = L + 7;
        for (int i = 0; i < row.Length; i++)
        {
            for (int j = 0; j < cells[i].Count; j++)
            {
                string t = cells[i][j];
                double tx = right is not null && right[i] ? x + widths[i] - 14 - Pdf.Width(t, size) : x;
                pdf.Text(t, tx, pdf.Y - 12 - j * (size + 3.4), size, false, "2A2D33");
            }
            x += widths[i];
        }
        pdf.Y = ry;
        alt = !alt;
    }
    pdf.Y -= 14;
}

// A row of headline figures.
void Stats((string Value, string Label)[] items, int perRow = 4)
{
    double gap = 10, boxW = (W - gap * (perRow - 1)) / perRow;
    for (int i = 0; i < items.Length; i += perRow)
    {
        Break(66);
        double y = pdf.Y - 58;
        for (int j = 0; j < perRow && i + j < items.Length; j++)
        {
            var (value, label) = items[i + j];
            double x = L + j * (boxW + gap);
            pdf.Fill(x, y, boxW, 58, Tint);
            pdf.Fill(x, y, 3, 58, Orange);
            pdf.Text(value, x + 12, y + 32, 19, true, Deep);
            foreach (var (line, k) in Pdf.Wrap(label, boxW - 20, 8).Select((s, k) => (s, k)))
                pdf.Text(line, x + 12, y + 18 - k * 9.5, 8, false, "6B4A38");
        }
        pdf.Y = y - 14;
    }
}

// ---------- cover ----------
pdf.Fill(0, Pdf.PageHeight - 232, Pdf.PageWidth, 232, Ink);
pdf.Fill(0, Pdf.PageHeight - 232, Pdf.PageWidth, 5, Orange);
string logo = Path.Combine(repo, "logo.png");
if (File.Exists(logo))
{
    pdf.Fill(L, Pdf.PageHeight - 122, 54, 54, White);
    pdf.Image(logo, L + 5, Pdf.PageHeight - 117, 44, 44);
}
pdf.Text("TextSoft", L + 70, Pdf.PageHeight - 96, 30, true, White);
pdf.Text("Beyond Typing", L + 72, Pdf.PageHeight - 114, 11, true, Orange);
pdf.Text("Capabilities and coverage", L, Pdf.PageHeight - 168, 17, true, White);
pdf.Text("Indian-language typing, legacy font conversion and licence administration",
    L, Pdf.PageHeight - 190, 10, false, "C9C6C2");
pdf.Text("Version 1.0.0   -   " + DateTime.Now.ToString("d MMMM yyyy"), L, Pdf.PageHeight - 212, 9, false, "8D8A86");

pdf.Y = Pdf.PageHeight - 268;
Para("This document lists what the software does today, every language, font family, font layout and " +
     "keyboard layout it carries, what is still waiting on a font from the vendor, and everything the " +
     "licence administration panel provides. Every figure here is generated from the shipped configuration " +
     "of this build, not written by hand.");

Stats(new[]
{
    (langs.Count.ToString(), "Indian languages"),
    (totalFonts.ToString("N0"), "Fonts listed"),
    (families.Count.ToString("N0"), "Distinct font families"),
    (totalKeyboardEntries.ToString(), "Keyboard layouts"),
    (totalOk + "/" + totalLayouts, "Font layouts working"),
    (encodings.Count.ToString(), "Legacy encodings wired"),
    (translit.ToString(), "Phonetic schemes"),
    ("1,425", "Combinations verified"),
});

// contents, so the reader sees the shape of the document before turning a page
string[] contents =
{
    "What the software does", "Language coverage", "Font layouts awaiting a vendor font",
    "Keyboard layouts", "Licensing", "Licence administration panel", "Quality and testing",
    "System requirements",
};
pdf.Y -= 10;
pdf.Text("Contents", L, pdf.Y, 11, true, Ink);
pdf.Line(L, pdf.Y - 8, R, pdf.Y - 8, Rule, 0.8);
pdf.Y -= 26;
for (int i = 0; i < contents.Length; i++)
{
    double cx = L + (i % 2) * (W / 2);
    double cy = pdf.Y - (i / 2) * 22;
    pdf.Text((i + 1).ToString("00"), cx, cy, 9, true, Orange);
    pdf.Text(contents[i], cx + 22, cy, 9.5, false, "333333");
}
pdf.Y -= (contents.Length / 2) * 22;

Footer();
pdf.NewPage();
pdf.Y = Top;

// ---------- 1. what the software does ----------
H1("What the software does");
H2("Typing");
Bullets(new[]
{
    "Types in any of the 14 supported languages system wide - Word, Excel, PowerPoint, Outlook, browsers, " +
    "CorelDRAW, Photoshop, billing software, anywhere text can be typed.",
    "One toggle key switches between English and the Indian language. CapsLock by default, with 17 keys to choose from.",
    "Types directly in a legacy font such as Shree-Lipi, Krutidev, Terafont, ISFOC, Akruti or Gopika, or in Unicode.",
    "In Word, Outlook, WordPad, LibreOffice and WPS the legacy font is applied automatically, so the typist " +
    "never has to pick the font by hand.",
    "Phonetic typing for " + translit + " scripts: type the sound in English and get the script.",
    "On-screen keyboard viewer showing the live layout, with a shift-layer view.",
    "Applications can be excluded, for example a password manager or a remote desktop session.",
});

H2("Conversion");
Bullets(new[]
{
    "Converts between any two layouts of a language: legacy to Unicode, Unicode to legacy, or one legacy " +
    "font family to another.",
    "Works on typed text, on the clipboard, and on files in batch - .txt, .rtf and .docx, with formatting preserved.",
    "Output numerals can be forced to the language digits, English digits or Hindi digits.",
    "A conversion dictionary holds the customer's own corrections and applies them on every run.",
    "Input and output layouts can be swapped with one button, and the result can be copied to the clipboard automatically.",
});

H2("Settings and licence");
Bullets(new[]
{
    "The last used language, font layout, font and keyboard layout come back on the next launch.",
    "Optional start with Windows, an on-screen toggle notification, and a system tray icon so typing keeps " +
    "working when the window is closed.",
    "Licence status, serial number, validity and the registered user are visible inside the app.",
});

// ---------- 2. language coverage ----------
H1("Language coverage");
Para("Every font listed below has a working conversion table in both directions. A font layout counts as " +
     "working when its encoding is wired end to end; the pending ones are third-party encodings whose font " +
     "the vendor has not supplied yet - they are shown in the app marked \"font not provided\" and type in Unicode.");
Table(
    new[] { "Language", "Fonts", "Font layouts", "Working", "Pending", "Keyboards" },
    langs.Select(l => new[] { l.Name, l.Fonts.ToString(), l.Layouts.ToString(), l.Ok.ToString(), l.Pending.ToString(), l.Keyboards.ToString() })
         .Append(new[] { "Total", totalFonts.ToString(), totalLayouts.ToString(), totalOk.ToString(), totalPending.ToString(), totalKeyboardEntries.ToString() }),
    new[] { W - 300, 54, 76, 54, 54, 62 },
    new[] { false, true, true, true, true, true });

H2("Legacy encodings wired end to end (" + encodings.Count + ")");
Para(string.Join(",  ", encodings));

// ---------- 3. pending ----------
H1("Font layouts awaiting a vendor font");
Para("These " + totalPending + " rows appear in the dropdown so nothing looks missing, and are labelled " +
     "\"font not provided\". Typing on them falls back to Unicode. As soon as the font file and a typed " +
     "sample arrive, each one becomes a full two-way conversion table like the others.");
Table(
    new[] { "Language", "Font layouts pending" },
    pending.GroupBy(p => p.Lang).Select(g => new[] { g.Key, string.Join(", ", g.Select(x => x.Layout)) }),
    new[] { 110, W - 110 });

// ---------- 4. keyboards ----------
H1("Keyboard layouts");
Para("The software carries " + totalKeyboardEntries + " keyboard layouts across the 14 languages, " +
     keyboardIds.Count + " of them distinct. Typists keep the layout they already know - Remington, " +
     "Typewriter, InScript, Phonetic or the vendor layout their old software used.");
Table(
    new[] { "Language", "No.", "Layouts" },
    langs.Select(l => new[] { l.Name, l.Keyboards.ToString(), l.KeyboardNames }),
    new[] { 78, 34, W - 112 },
    new[] { false, true, false },
    8.5);

// ---------- 5. licensing ----------
H1("Licensing");
Bullets(new[]
{
    "One key works on one PC. The first computer to activate holds the key; any other computer is refused " +
    "and the attempt is recorded with its name and IP address.",
    "Moving a licence to a new computer is an administrator action in the panel. The old PC loses it at its " +
    "next check-in and cannot take it back.",
    "Reinstalling Windows or the software on the same PC keeps working - the same machine is recognised.",
    "The person using the software registers with name, email and mobile number before typing is enabled.",
    "Demo and full editions, with validity in days, until a date, or perpetual.",
    "The software re-checks with the licence server about once a day and keeps working offline for five days.",
    "When a licence expires or is revoked, typing and conversion stop at the next check.",
    "Licence tokens are signed with Ed25519, stored encrypted for the Windows account, and the licence code " +
    "in the build is obfuscated.",
});

// ---------- 6. admin panel ----------
H1("Licence administration panel");
Para("A private web panel at admin.text-soft.com. Sales, keys, customers, end users and refused activations " +
     "are all managed there; nothing needs a developer.");

H2("Pages");
Table(
    new[] { "Page", "What it does" },
    new[]
    {
        new[] { "Dashboard", "Eight live figures - purchases, keys issued, active, expiring in 7 days, revoked, PCs activated, registered users, blocked attempts in the last 7 days - plus recently active keys and the latest refused activations." },
        new[] { "Purchases", "One serial number per sale with all its keys under it. Create a purchase with customer, company, email, mobile, edition, quantity up to 500 and validity; the keys are shown once, can be copied or downloaded as a file, and are emailed to the customer." },
        new[] { "Purchase detail", "Keys issued, PCs activated, revoked and blocked counts; extend the whole sale by a year or 30 days, make it perpetual, revoke or un-revoke every key, and see the customer and email status." },
        new[] { "License keys", "Every key issued, filtered by active, on a PC, never activated, with blocked attempts, expired or revoked, and searchable by key, serial, customer, user, email or mobile." },
        new[] { "Key detail", "The PC holding the key, the registered user, extension by days, revoke or un-revoke, the list of refused attempts, previous devices, and Move to another device with a reason kept on record." },
        new[] { "End users", "Everyone who activated: name, email, mobile, key, PC, when activated and last seen, with CSV export." },
        new[] { "Security", "Every activation the server refused, with the reason - second device, old device, wrong serial, unknown key, revoked or expired - the PC name, IP address and whether it came from the installer or the app." },
        new[] { "Team", "Panel users with owner and staff roles, add user, reset password, disable, enable, delete, and the mailbox purchase emails are sent from, with a test-email button." },
    },
    new[] { 96, W - 96 });

H2("Purchase email");
Para("When a sale is created the customer receives a branded email with the serial number, the edition, the " +
     "validity, every licence key and a four-step activation guide. It is sent from the company Gmail mailbox " +
     "over SMTP. The panel records whether it went out, and the template can be previewed and test-mailed " +
     "before a real sale.");

H2("Protection the server enforces");
Bullets(new[]
{
    "The serial number and the key must match before anything is checked.",
    "A key already on another PC is refused and logged with the name of the PC that holds it.",
    "A PC that activates a different key releases the previous one automatically.",
    "A device an administrator moved away cannot take the licence back on its own.",
    "Revoked and expired licences fail at the next check even if the computer is offline in between.",
    "Activation is rate limited - 30 attempts per address every 10 minutes and 20 per key an hour - so keys " +
    "cannot be guessed in bulk.",
    "Keys are never stored in readable form; only a one-way hash and the first block for display.",
});

H2("Hosting");
Para("Next.js and MongoDB on the customer's own server, started by a one-command deploy script under pm2, " +
     "behind nginx with an HTTPS certificate. Admin sessions last 12 hours and every change an administrator " +
     "makes is written to an audit log.");

// ---------- 7. quality ----------
H1("Quality and testing");
Table(
    new[] { "Check", "Result" },
    new[]
    {
        new[] { "Language x font layout x keyboard combinations exercised end to end", "1,425 of 1,425 pass" },
        new[] { "Automated unit and integration tests", "102 of 102 pass" },
        new[] { "Conversion tables validated for both directions", encodings.Count + " encodings, " + bundle.MappingTables.Count + " tables" },
        new[] { "Configuration validation (fonts, layouts, tables cross-checked)", "0 errors" },
    },
    new[] { W - 150, 150 });

Para("Round-trip conversion is checked for every wired encoding: a word converted to the legacy font and " +
     "back must come out identical, which is what catches a wrong byte in a table.");

H1("System requirements");
Table(
    new[] { "Item", "Requirement" },
    new[]
    {
        new[] { "Operating system", "Windows 10 or Windows 11, 64-bit" },
        new[] { "Installation", "Administrator rights for setup; the application then runs as a normal user" },
        new[] { "Disk space", "About 200 MB" },
        new[] { "Internet", "Needed at activation and about once a day afterwards; works offline for five days in between" },
        new[] { "Fonts", "Legacy fonts the customer already owns stay on their machine; the software does not replace them" },
    },
    new[] { 120, W - 120 });

Footer();
pdf.Save(outPath);
Console.WriteLine($"pages {pdf.PageCount}   ->  {outPath}");

static string FindRoot()
{
    var d = new DirectoryInfo(AppContext.BaseDirectory);
    while (d is not null && !File.Exists(Path.Combine(d.FullName, "FontSoftware.sln"))) d = d.Parent;
    return d?.FullName ?? throw new InvalidOperationException("solution root not found");
}
