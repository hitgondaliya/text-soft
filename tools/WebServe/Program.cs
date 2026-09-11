// Minimal static file server for the FR-7 marketing website (local preview/demo).
// Serves ../../website on http://localhost:5080.
using Microsoft.Extensions.FileProviders;

var siteRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "website"));
if (!Directory.Exists(siteRoot))
{
    Console.Error.WriteLine($"Website folder not found at: {siteRoot}");
    return 1;
}

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5080");
var app = builder.Build();

var files = new PhysicalFileProvider(siteRoot);
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
app.UseStaticFiles(new StaticFileOptions { FileProvider = files, ServeUnknownFileTypes = true });

Console.WriteLine($"Serving {siteRoot} at http://localhost:5080");
app.Run();
return 0;
