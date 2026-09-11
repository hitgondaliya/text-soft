using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FontSoftware.Core.Config;

// FontSample — render the client fonts with real text into one PNG, to prove
// they load and render. The Shree-GujX legacy rows use our shree-gujx map to turn
// Unicode into legacy bytes first, proving the full legacy pipeline + font.

int rc = 0;
var th = new Thread(() => rc = Run());
th.SetApartmentState(ApartmentState.STA);
th.Start(); th.Join();
return rc;

static int Run()
{
    string config = @"C:\Users\harde\Desktop\Windows Font Software\assets\config";
    string clientDir = Path.Combine(config, "fonts", "client");
    var bundle = new ConfigLoader(config).Load();
    var toGujx = bundle.CreateConverterToLegacy("shree-gujx");

    string guText = "નમસ્તે ગુજરાત";
    string hiText = "नमस्ते भारत";
    string gujxLegacy = toGujx?.ConvertText(guText) ?? "(no map)";
    // all ASCII + high bytes, to see WHERE the Gujarati glyphs actually sit
    var sbLo = new System.Text.StringBuilder(); for (int c = 0x41; c <= 0x7A; c++) sbLo.Append((char)c);
    var sbHi = new System.Text.StringBuilder(); for (int c = 0xA1; c <= 0xF0; c++) sbHi.Append((char)c);
    string lo = sbLo.ToString(), hi = sbHi.ToString();

    // bundled reference SHREE-GUJX-0001 (the encoding our map targets)
    string shreeDir = Path.Combine(config, "fonts", "shree") + Path.DirectorySeparatorChar;
    var rows = new (string label, string dir, string file, string family, string text)[]
    {
        ("Aakar — Gujarati (client)",   clientDir+"\\", "Aakar.ttf",         "aakar",   guText),
        ("Shruti — Gujarati (client)",  clientDir+"\\", "SHRUTI.TTF",        "Shruti",  guText),
        ("Amita — Devanagari (client)", clientDir+"\\", "Amita-Regular.ttf", "Amita",   hiText),
        ("Biryani — Devanagari (client)",clientDir+"\\","Biryani-Regular.ttf","Biryani",hiText),
        ("Mukta — Devanagari (client)", clientDir+"\\", "Mukta-Regular.ttf", "Mukta",   hiText),
    };

    int W = 820, rowH = 76, H = rows.Length * rowH + 20;
    var dv = new DrawingVisual();
    var label = new Typeface("Segoe UI");
    using (var dc = dv.RenderOpen())
    {
        dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, W, H));
        double y = 12;
        foreach (var r in rows)
        {
            var lbl = new FormattedText(r.label, System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, label, 12, Brushes.Gray, 1.0);
            dc.DrawText(lbl, new Point(14, y + 4));

            var dir = new Uri(r.dir);
            var fam = new FontFamily(dir, "./#" + r.family);
            var tf = new Typeface(fam, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            var ft = new FormattedText(r.text, System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, tf, 22, Brushes.Black, 1.0);
            ft.MaxTextWidth = W - 300;
            dc.DrawText(ft, new Point(280, y));
            y += rowH;
        }
    }
    var rtb = new RenderTargetBitmap(W, H, 96, 96, PixelFormats.Pbgra32);
    rtb.Render(dv);
    string outp = @"C:\Users\harde\AppData\Local\Temp\claude\C--Users-harde-Desktop-Windows-Font-Software\950e994d-182b-4119-bc3b-a8bb7f55e31a\scratchpad\clientfonts.png";
    var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rtb));
    using (var fs = File.Create(outp)) enc.Save(fs);
    Console.WriteLine("legacy bytes for '" + guText + "' = '" + gujxLegacy + "'");
    Console.WriteLine("saved " + outp);
    return 0;
}
