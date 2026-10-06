using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace EasyControl.Interop;

/// <summary>
/// Ensures only one EasyControl instance runs. A second launch signals the
/// first instance to surface its window, then exits.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private const string MutexName = "EasyControl.SingleInstance.Mutex";
    private const string EventName = "EasyControl.SingleInstance.Activate";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activateEvent;
    private readonly Thread _listener;
    private volatile bool _stopping;

    public bool IsFirstInstance { get; }

    public event Action? Activated;

    public SingleInstance()
    {
        _mutex = new Mutex(true, MutexName, out var isFirst);
        IsFirstInstance = isFirst;

        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);

        if (!IsFirstInstance)
        {
            _activateEvent.Set();
            _listener = null!;
            return;
        }

        _listener = new Thread(Listen)
        {
            IsBackground = true,
            Name = "EasyControl.SingleInstance"
        };
        _listener.Start();
    }

    private void Listen()
    {
        while (!_stopping)
        {
            if (_activateEvent.WaitOne(500))
            {
                if (_stopping)
                {
                    return;
                }

                Activated?.Invoke();
            }
        }
    }

    public static void SignalExistingInstance()
    {
        try
        {
            using var handle = EventWaitHandle.OpenExisting(EventName);
            handle.Set();
        }
        catch
        {
            // no existing instance
        }
    }

    public void Dispose()
    {
        _stopping = true;
        _activateEvent.Set();
        _mutex.Dispose();
        _activateEvent.Dispose();

        if (IsFirstInstance)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch
            {
                // ignored
            }
        }
    }
}
