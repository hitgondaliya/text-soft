using System.IO;

namespace FontSoftware.App.Services;

/// <summary>
/// The old Word format. A .doc is not a zip of XML like a .docx - it is a compound
/// binary file - so nothing in the OpenXML library can read it. Word itself can,
/// and every customer who has .doc files has Word, so the conversion borrows it:
/// Word saves the document as a .docx, we convert that the way we convert any
/// .docx, and Word saves the result back as a .doc.
///
/// Word is asked for nothing else: no window, no dialogs, no recent-files entry,
/// and it is always closed again.
/// </summary>
internal static class WordDocFormat
{
    private const int WdFormatDocument97 = 0;     // .doc
    private const int WdFormatXmlDocument = 12;   // .docx
    private const int WdDoNotSaveChanges = 0;

    /// <summary>True when Word is installed and can be asked to do this.</summary>
    public static bool Available => Type.GetTypeFromProgID("Word.Application") is not null;

    /// <summary>
    /// Save <paramref name="doc"/> in the other Word format. Used both ways: a .doc
    /// in to get a .docx to work on, and the converted .docx back out as a .doc.
    /// </summary>
    public static void SaveAs(string doc, string target, bool asDocx)
    {
        Type? progId = Type.GetTypeFromProgID("Word.Application")
            ?? throw new InvalidOperationException(
                "Converting a .doc file needs Microsoft Word on this PC. Save it as .docx in Word and convert that.");

        dynamic? app = null;
        dynamic? open = null;
        try
        {
            app = Activator.CreateInstance(progId)!;
            app.Visible = false;
            app.DisplayAlerts = 0;              // never stop on a dialog: nobody is watching
            open = app.Documents.Open(
                FileName: Path.GetFullPath(doc),
                ConfirmConversions: false,
                ReadOnly: true,
                AddToRecentFiles: false,
                Visible: false);
            open.SaveAs2(Path.GetFullPath(target), asDocx ? WdFormatXmlDocument : WdFormatDocument97);
        }
        finally
        {
            try { open?.Close(WdDoNotSaveChanges); } catch { }
            try { app?.Quit(WdDoNotSaveChanges); } catch { }
            if (app is not null) try { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(app); } catch { }
        }
    }
}
