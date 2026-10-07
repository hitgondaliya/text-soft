using FontSoftware.Engine;
using Xunit;

namespace FontSoftware.Tests;

/// <summary>
/// The hook used to share the UI thread, and Windows silently drops a hook that
/// answers too slowly - so a busy UI (a document converting) could leave every key
/// typing what its keycap says until a restart. It has a thread of its own now.
/// </summary>
[Collection("typing engine")]
public class KeyboardThreadTests
{
    [Fact]
    public void TheEngineRunsOnTheKeyboardThreadNotTheCallers()
    {
        using var keyboard = new KeyboardThread();
        int there = 0;
        keyboard.Invoke(() => there = Environment.CurrentManagedThreadId);
        Assert.NotEqual(0, there);
        Assert.NotEqual(Environment.CurrentManagedThreadId, there);
    }

    [Fact]
    public void TheHookGoesOnAndComesOffOnItsOwnThread()
    {
        var keyboard = new KeyboardThread();
        keyboard.Start();
        bool on = false;
        keyboard.Invoke(() => on = keyboard.Hook.IsInstalled);
        Assert.True(on);

        keyboard.Invoke(keyboard.Hook.Reinstall);         // what the watchdog does
        keyboard.Invoke(() => on = keyboard.Hook.IsInstalled);
        Assert.True(on);

        keyboard.Dispose();
        Assert.False(keyboard.Hook.IsInstalled);
    }

    [Fact]
    public void AFailureOnTheKeyboardThreadReachesTheCaller()
    {
        using var keyboard = new KeyboardThread();
        var ex = Assert.Throws<InvalidOperationException>(() => keyboard.Invoke(() => throw new FormatException("bad")));
        Assert.IsType<FormatException>(ex.InnerException);
        int after = 0;
        keyboard.Invoke(() => after = 1);                 // and the thread is still there
        Assert.Equal(1, after);
    }

    [Fact]
    public void TheKeyboardThreadAnswersWhileTheThreadThatMadeItIsBusy()
    {
        // The UI thread makes it, then spends two seconds converting a document.
        KeyboardThread? keyboard = null;
        var made = new ManualResetEventSlim();
        var ui = new Thread(() => { keyboard = new KeyboardThread(); keyboard.Start(); made.Set(); Thread.Sleep(2000); });
        ui.Start();
        Assert.True(made.Wait(5000));

        var answered = new ManualResetEventSlim();
        var asked = DateTime.UtcNow;
        new Thread(() => keyboard!.Invoke(answered.Set)).Start();
        Assert.True(answered.Wait(500), "the keyboard thread waited on the busy UI thread");
        Assert.True(DateTime.UtcNow - asked < TimeSpan.FromSeconds(1));

        ui.Join();
        keyboard!.Dispose();
    }
}
