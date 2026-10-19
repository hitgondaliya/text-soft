using System.Text;

namespace WordVis;

/// The test lines as an RTF document whose test runs are the product's RichTextClipboard.BuildRtf body, for Word's RTF reader.
public static class RtfDoc
{
    public static void Run(string[] args)
    {
        foreach (var fam in args)
        {
            string face = (string)Lines.Base(fam)["repFace"]!;
            var lines = Lines.Load(fam);
            var sb = new StringBuilder();
            sb.Append(@"{\rtf1\ansi\ansicpg1252\deff0{\fonttbl{\f0\fnil ").Append(face).Append(@";}{\f1\fswiss Arial;}}");
            sb.Append(@"{\colortbl;\red255\green0\blue0;\red0\green0\blue255;}");
            sb.Append(@"\paperw12240\paperh15840\margl720\margr720\margt720\margb720\uc1");
            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0 && i % Lines.PerPage == 0) sb.Append(@"\page");
                sb.Append(@"\pard\plain\sl1440\slmult0 ");
                sb.Append(@"{\f1\fs16\cf2 ").Append(Program.Hex(lines[i].Cp)).Append("  }");
                sb.Append(@"{\f1\fs48\cf1 H    }");
                // the product's run: \plain\rtlch\fcs1\af0\ltrch\fcs0\f0 then the text as BuildRtf writes it
                sb.Append(@"{\plain\rtlch\fcs1\af0\ltrch\fcs0\f0\fs48 ");
                string body = RichEditTest.ProductRtf(char.ConvertFromUtf32(lines[i].Cp), face);
                int k = body.IndexOf(@"\f0 ", body.IndexOf(@"\ltrch", StringComparison.Ordinal), StringComparison.Ordinal) + 4;
                sb.Append(body[k..^1]).Append('}');
                sb.Append(@"{\f1\fs48\cf1     H}\par");
            }
            sb.Append('}');
            string path = Path.Combine(Program.Root, "fam", "rtf-" + fam + ".rtf");
            File.WriteAllText(path, sb.ToString(), Encoding.ASCII);
            Console.WriteLine($"{path}: {lines.Count} lines");
        }
    }
}
