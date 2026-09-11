using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

// PdfRender — render PDF pages to PNG using the built-in Windows PDF engine.
//   dotnet run --project tools/PdfRender -- "<in.pdf>" "<outPrefix>" [maxPages]

if (args.Length < 2) { Console.Error.WriteLine("usage: pdfrender <in.pdf> <outPrefix> [maxPages]"); return 1; }
int maxPages = args.Length >= 3 ? int.Parse(args[2]) : 3;

var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(args[0]));
var doc = await PdfDocument.LoadFromFileAsync(file);
int n = Math.Min((int)doc.PageCount, maxPages);
for (uint i = 0; i < n; i++)
{
    using var page = doc.GetPage(i);
    var opt = new PdfPageRenderOptions { DestinationWidth = 1600 };
    string outp = $"{args[1]}_{i + 1}.png";
    using var ras = new InMemoryRandomAccessStream();
    await page.RenderToStreamAsync(ras, opt);
    using var fs = File.Create(outp);
    ras.Seek(0);
    using var input = ras.AsStreamForRead();
    input.CopyTo(fs);
    Console.WriteLine($"page {i + 1} -> {outp}");
}
Console.WriteLine($"total pages in pdf: {doc.PageCount}");
return 0;
