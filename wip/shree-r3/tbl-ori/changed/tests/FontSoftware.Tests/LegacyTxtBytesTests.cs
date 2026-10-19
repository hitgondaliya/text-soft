using System.Text;
using FontSoftware.App.Services;
using FontSoftware.Core.Config;
using FontSoftware.Core.Conversion;
using Xunit;
using FontFamily = System.Windows.Media.FontFamily;

namespace FontSoftware.Tests;

/// <summary>
/// A .txt in an 8-bit legacy font (FileConversion.WriteTxt) carries the very code points the converter
/// wrote, one byte each, as typing, RTF and .docx do: U+0000-U+00FF as their own byte - the C1 codes
/// U+0080-U+009F included, which Encoding.GetEncoding(1252) turned into '?' (U+009E, ৱ in the Bangla
/// Shree fonts, came out 3F) - and Windows-1252's own characters (– “ ” ƒ š ...) as theirs. A character
/// no byte can hold (one a table passed through, which the converter reports - ₹ in a Shree font, P3b)
/// is left out: never '?', which the font would draw as a question mark where a letter was.
/// </summary>
public class LegacyTxtBytesTests
{
    [Fact]
    public void EveryCodePointUpToFFIsItsOwnByte()
    {
        string all = new(Enumerable.Range(1, 0xFF).Select(i => (char)i).ToArray());
        Assert.Equal(Enumerable.Range(1, 0xFF).Select(i => (byte)i).ToArray(), FileConversion.LegacyBytes(all));
        // the ones Windows-1252 alone made '?'
        Assert.Equal(new byte[] { 0x80, 0x9E, 0x9F, 0x82, 0x95 }, FileConversion.LegacyBytes("\u0080\u009E\u009F\u0082\u0095"));
    }

    [Fact]
    public void Windows1252CharactersAreTheirBytes()
    {
        Assert.Equal(new byte[] { 0x96, 0x97, 0x91, 0x92, 0x93, 0x94, 0x8C, 0x9C, 0x8A, 0x9A, 0x9F, 0x83, 0x85, 0x95, 0x80, 0x99, 0x9E },
            FileConversion.LegacyBytes("–—‘’“”ŒœŠšŸƒ…•€™ž"));
    }

    [Fact]
    public void ACharacterNoByteHoldsIsLeftOut()
    {
        // ৱ (passed through by the Bangla tables, reported), ₹ (passed through by the Shree tables, P3b), an
        // emoji, ā (never its "best fit" a, a letter of the script in a legacy font): left out, never '?'
        Assert.Equal(new byte[] { 0x41, 0x42, 0x43, 0x44, 0x3F }, FileConversion.LegacyBytes("AৱB\U0001F600C₹āD?"));
    }

    // The line and paragraph separators U+2028 U+2029 are line breaks (the tables leave them as they are, as
    // they do \n): a Windows-1252 file has no byte for them, so they are its CR LF - never a silent '?'.
    [Fact]
    public void LineAndParagraphSeparatorsAreALineBreak()
    {
        string text = "A" + (char)0x2028 + "B" + (char)0x2029 + "C";
        Assert.Equal(new byte[] { 0x41, 0x0D, 0x0A, 0x42, 0x0D, 0x0A, 0x43 }, FileConversion.LegacyBytes(text));

        // ...and through the Bangla table and FileConversion.ConvertFile: a Unicode space is the fonts' space 20.
        var p = new ConversionPipeline(new ConfigLoader(Path.Combine(AppContext.BaseDirectory, "config")).Load());
        var key = LayoutKey.Legacy("shree-ban7");
        string uniText = "ক" + (char)0x2003 + "খ" + (char)0x2028 + "গ";
        string dir = Path.Combine(Path.GetTempPath(), "txsf-txt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string uni = Path.Combine(dir, "in.txt"), outPath = Path.Combine(dir, "out", "in.txt");
            File.WriteAllText(uni, uniText, new UTF8Encoding(true));
            FileConversion.ConvertFile(uni, outPath, t => p.Convert(t, LayoutKey.Unicode, key).Text, true, false, new FontFamily("Arial"), "Legacy", false);
            Assert.Equal(new byte[] { 0x41, 0xA1, 0x20, 0x4A, 0x0D, 0x0A, 0x4B }, File.ReadAllBytes(outPath));
        }
        finally { Directory.Delete(dir, true); }
    }

    // End to end, through FileConversion.ConvertFile: the file holds the converter's own code points, byte for byte.
    [Theory]
    [InlineData("shree-ban456", "বাংলা ভাষা “উক্তি” কর্ম ধ্বনি ১২-০৮")]
    [InlineData("shree-ban7", "প্রকাশ বিদ্যার্থী ব্ঢ চ্ঞা ক্র্র")]
    [InlineData("shree-ass7", "অসমীয়া ৰাজ্য ৱাৰিষ")]
    [InlineData("shree-ass456", "কৰ্ম প্ৰকাশ ৱা")]
    [InlineData("shree-guj7", "ગુજરાતી – ભાષા — ક્ષ")]
    [InlineData("shree-dev456", "हिन्दी – भाषा — क्ष")]
    public void TxtOutputIsTheConvertersCodePointsAsBytes(string enc, string text)
    {
        var p = new ConversionPipeline(new ConfigLoader(Path.Combine(AppContext.BaseDirectory, "config")).Load());
        var key = LayoutKey.Legacy(enc);
        string legacy = p.Convert(text, LayoutKey.Unicode, key).Text;
        Assert.DoesNotContain('?', legacy);

        string dir = Path.Combine(Path.GetTempPath(), "txsf-txt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string uni = Path.Combine(dir, "in.txt"), outPath = Path.Combine(dir, "out", "in.txt");
            File.WriteAllText(uni, text, new UTF8Encoding(true));
            FileConversion.ConvertFile(uni, outPath, t => p.Convert(t, LayoutKey.Unicode, key).Text, true, false, new FontFamily("Arial"), "Legacy", false);
            byte[] got = File.ReadAllBytes(outPath);
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var cp1252 = Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            byte[] want = legacy.SelectMany(c => c <= 'ÿ' ? new[] { (byte)c } : cp1252.GetBytes(c.ToString())).ToArray();
            Assert.Equal(want, got);
            Assert.Equal(legacy.Length, got.Length);
            Assert.DoesNotContain((byte)'?', got);
        }
        finally { Directory.Delete(dir, true); }
    }
}
