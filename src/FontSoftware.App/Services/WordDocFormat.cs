using System.Diagnostics;
using System.IO;

namespace FontSoftware.App.Services;

/// <summary>
/// The old Word format. A .doc is not a zip of XML like a .docx - it is a compound
/// binary file - so nothing in the OpenXML library can read it. Word itself can,
/// and every customer who has .doc files has Word, so the conversion borrows it:
/// Word opens the file, hands its XML over, takes the converted XML back and saves
/// the file where it already is. It is never asked to change a file's format,
/// because that is a "Save as" - and a Save as can stop on a save dialog that,
/// with Word hidden, nobody can see or answer.
///
/// Word is asked for nothing else: no window, no dialogs, no recent-files entry,
/// and it is always closed again. It is also never waited on for ever: Word can
/// stop on a dialog nobody can see - a repair prompt, a stale lock left by a Word
/// that was killed - and a converter that hangs is worse than one that says why.
/// </summary>
internal static class WordDocFormat
{
    private const int WdDoNotSaveChanges = 0;

    /// <summary>How long Word gets before we give up on it.</summary>
    public static TimeSpan Patience { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>True when Word is installed and can be asked to do this.</summary>
    public static bool Available => Type.GetTypeFromProgID("Word.Application") is not null;

    /// <summary>
    /// Open <paramref name="file"/> in Word, hand its XML to <paramref name="transform"/>,
    /// put the answer back and save it where it already is. Word is never asked to
    /// change the file's format, so it has no reason to open a save dialog.
    /// </summary>
    public static void ConvertInPlace(string file, Func<string, string> transform)
    {
        if (Type.GetTypeFromProgID("Word.Application") is null)
            throw new InvalidOperationException(
                "Converting a .doc file needs Microsoft Word on this PC. Save it as .docx in Word and convert that.");

        var before = WordProcessIds();
        Exception? error = null;
        // COM wants an apartment of its own, and a thread of our own is also what
        // lets us walk away when Word stops answering.
        var worker = new Thread(() => { try { Borrow(file, transform); } catch (Exception e) { error = e; } })
        {
            IsBackground = true,
            Name = "word-doc-format",
        };
        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();

        if (!worker.Join(Patience))
        {
            CloseTheWordWeStarted(before);
            throw new TimeoutException(
                $"Word did not answer within {Patience.TotalMinutes:0} minutes - it may be waiting on a dialog. " +
                "Open Word once by hand, close it, and try again; or save the file as .docx and convert that.");
        }
        if (error is not null) throw error;
    }

    private static void Borrow(string file, Func<string, string> transform)
    {
        Type progId = Type.GetTypeFromProgID("Word.Application")!;
        dynamic? app = null;
        dynamic? open = null;
        try
        {
            app = Activator.CreateInstance(progId)!;
            app.Visible = false;
            app.DisplayAlerts = 0;              // never stop on a dialog: nobody is watching
            open = app.Documents.Open(
                FileName: Path.GetFullPath(file),
                ConfirmConversions: false,
                ReadOnly: false,
                AddToRecentFiles: false,
                Visible: false);
            // Word hands a whole document over as one XML string and takes it back the
            // same way, which keeps every bit of formatting we do not touch.
            string converted = transform(open.Content.WordOpenXML);
            open.Content.InsertXML(converted);
            open.Save();
        }
        finally
        {
            try { open?.Close(WdDoNotSaveChanges); } catch { }
            try { app?.Quit(WdDoNotSaveChanges); } catch { }
            if (app is not null) try { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(app); } catch { }
        }
    }

    private static HashSet<int> WordProcessIds()
    {
        try { return Process.GetProcessesByName("WINWORD").Select(p => p.Id).ToHashSet(); }
        catch { return new HashSet<int>(); }
    }

    /// <summary>
    /// Close the copy of Word this conversion started, and only that one: it is the
    /// one that was not running before and has no window of its own. Somebody else's
    /// Word, with their unsaved work in it, is left alone.
    /// </summary>
    private static void CloseTheWordWeStarted(HashSet<int> before)
    {
        try
        {
            foreach (var p in Process.GetProcessesByName("WINWORD"))
            {
                if (before.Contains(p.Id) || p.MainWindowHandle != IntPtr.Zero) continue;
                try { p.Kill(); } catch { }
            }
        }
        catch { /* the conversion already failed; this is only tidying up */ }
    }
}
