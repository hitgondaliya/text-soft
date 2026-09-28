using System.Windows.Forms;

/// <summary>
/// A window of this tool's own to type into: one text box, on its own thread with
/// its own message loop. Notepad was the old target, and every run left a window
/// (Windows 11 Notepad reopens its unsaved tabs) - this one is gone when the test
/// ends. TextSoft's hook is system-wide, so it types here exactly as anywhere else.
/// </summary>
sealed class Pad
{
    private readonly Form _form;
    private readonly TextBox _box;
    private readonly ManualResetEventSlim _ready = new(false);

    public IntPtr Handle { get; private set; }

    public Pad()
    {
        _form = new Form { Text = "TypeProbe pad", Width = 700, Height = 260, StartPosition = FormStartPosition.CenterScreen, TopMost = true };
        _box = new TextBox { Multiline = true, Dock = DockStyle.Fill, Font = new System.Drawing.Font("Nirmala UI", 20) };
        _form.Controls.Add(_box);
        _form.Shown += (_, _) => { Handle = _form.Handle; _box.Focus(); _ready.Set(); };
        var ui = new Thread(() => Application.Run(_form)) { IsBackground = true };
        ui.SetApartmentState(ApartmentState.STA);
        ui.Start();
        _ready.Wait(10000);
    }

    public string Text => (string)_form.Invoke(() => _box.Text);

    public void Close() => _form.Invoke(() => _form.Close());
}
