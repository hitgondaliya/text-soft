using System.Text;

// skeptic <repo> <outDir> <mode> [args]: independent measurements of the Odia Shree-Lipi tables and keyboards, through the
// product's own ConfigLoader -> FontConverter / ConversionPipeline / TypingEngine of <repo>.
//   fwd | joins | words <seed,seed> <n> | rev | wordfinal | sweep | written <seed,seed> | paths | keys | random <n> <seed> | one <layout> <keys>
//   all = everything but one
static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        string repo = args[0], outDir = args[1], mode = args[2];
        K.Load(repo);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        void Run(string m, string[] a)
        {
            switch (m)
            {
                case "fwd": Measure.Fwd(outDir); break;
                case "joins": Measure.Joins(); break;
                case "words": Measure.Words(outDir, a[0].Split(',').Select(int.Parse).ToArray(), int.Parse(a[1])); break;
                case "rev": Measure.Reverse(outDir); break;
                case "wordfinal": Measure.WordFinal(outDir); break;
                case "sweep": Measure.Sweep(outDir); break;
                case "written": Measure.Written(a[0].Split(',').Select(int.Parse).ToArray()); break;
                case "paths": Measure.Paths(); break;
                case "keys": TypingAudit.Keys(outDir); break;
                case "random": TypingAudit.Random(outDir, int.Parse(a[0]), int.Parse(a[1])); break;
                case "one": TypingAudit.One(a[0], a[1]); break;
                case "docx": Docx.Make(a[0]); break;
                case "emptykey": TypingAudit.EmptyKey(outDir); break;
                case "oldread": Measure.OldRead(a[0]); break;
                default: throw new ArgumentException("mode " + m);
            }
            Console.WriteLine($"[{m} done at {sw.Elapsed.TotalSeconds:F0}s]");
        }
        if (mode == "all")
        {
            Run("fwd", null); Run("joins", null); Run("words", new[] { "4242,90001", "20000" }); Run("rev", null); Run("wordfinal", null);
            Run("sweep", null); Run("written", new[] { "4242,90001" }); Run("paths", null);
        }
        else Run(mode, args.Skip(3).ToArray());
        return 0;
    }
}
