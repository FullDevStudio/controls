using System.Collections.Concurrent;
using Microsoft.Maui.Dispatching;

namespace Shiny.Maui.Controls.Tests;

/// <summary>
/// A real UI thread for tests that care which thread work lands on. <see cref="TestDispatcher"/> runs
/// everything inline, so it cannot tell "called on the UI thread" apart from "called from the thread
/// pool" - and that difference is exactly where a cross-thread bug lives. This one owns a dedicated
/// thread with its own synchronization context (so an await on it comes back to it, as on a device),
/// reports <see cref="IsDispatchRequired"/> honestly, and only hands itself out on that thread, as the
/// platform dispatchers do.
/// </summary>
sealed class ThreadBoundDispatcher : IDispatcher, IDispatcherProvider, IDisposable
{
    readonly BlockingCollection<Action> work = new();
    readonly Thread thread;

    public ThreadBoundDispatcher()
    {
        this.thread = new Thread(this.Run) { IsBackground = true, Name = "Test UI thread" };
        this.thread.Start();
    }

    /// <summary>
    /// Whatever escaped a dispatched action. Kept rather than rethrown: an exception on this thread
    /// would end it, and every test after it would hang instead of saying what went wrong.
    /// </summary>
    public ConcurrentQueue<Exception> Unhandled { get; } = new();

    public bool IsDispatchRequired => Environment.CurrentManagedThreadId != this.thread.ManagedThreadId;

    public bool Dispatch(Action action)
    {
        try
        {
            this.work.Add(action);
            return true;
        }
        catch (InvalidOperationException)
        {
            // disposed - the platform dispatchers answer false once their thread is gone too
            return false;
        }
    }

    public bool DispatchDelayed(TimeSpan delay, Action action)
    {
        _ = Task.Delay(delay).ContinueWith(_ => this.Dispatch(action), TaskScheduler.Default);
        return true;
    }

    public IDispatcherTimer CreateTimer() => new TestDispatcher.ControllableTimer();

    IDispatcher? IDispatcherProvider.GetForCurrentThread() => this.IsDispatchRequired ? null : this;

    /// <summary>Makes this the process-wide provider. Undo it with <see cref="TestDispatcherProvider.Install"/>.</summary>
    public void Install() => DispatcherProvider.SetCurrent(this);

    /// <summary>
    /// Throws when called off the UI thread - the headless stand-in for the platform rejecting a view
    /// it is handed from another thread (Android's CalledFromWrongThreadException, WinUI's
    /// RPC_E_WRONG_THREAD, UIKit's thread check).
    /// </summary>
    public void VerifyAccess()
    {
        if (this.IsDispatchRequired)
            throw new InvalidOperationException("A view was added to the page off the UI thread.");
    }

    public Task<T> InvokeAsync<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var queued = this.Dispatch(() =>
        {
            try
            {
                tcs.SetResult(func());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        if (!queued)
            tcs.SetException(new ObjectDisposedException(nameof(ThreadBoundDispatcher)));

        return tcs.Task;
    }

    public Task InvokeAsync(Action action) => this.InvokeAsync(() =>
    {
        action();
        return true;
    });

    void Run()
    {
        SynchronizationContext.SetSynchronizationContext(new UiContext(this));

        foreach (var action in this.work.GetConsumingEnumerable())
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                this.Unhandled.Enqueue(ex);
            }
        }
    }

    public void Dispose()
    {
        this.work.CompleteAdding();
        this.thread.Join(TimeSpan.FromSeconds(5));
    }


    sealed class UiContext(ThreadBoundDispatcher owner) : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) => owner.Dispatch(() => d(state));

        public override void Send(SendOrPostCallback d, object? state)
        {
            if (owner.IsDispatchRequired)
                owner.InvokeAsync(() => d(state)).GetAwaiter().GetResult();
            else
                d(state);
        }

        public override SynchronizationContext CreateCopy() => this;
    }
}
