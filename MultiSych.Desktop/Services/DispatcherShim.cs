using System;
using System.Threading.Tasks;

namespace Avalonia.Threading;

public static class Dispatcher
{
    public static UIThreadShim UIThread { get; } = new();

    public class UIThreadShim
    {
        public void Post(Action action)
        {
            GLib.Functions.IdleAdd(0, () =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "Error executing posted UI thread action");
                }
                return false;
            });
        }

        public Task InvokeAsync(Action action)
        {
            var tcs = new TaskCompletionSource();
            Post(() =>
            {
                try
                {
                    action();
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return tcs.Task;
        }

        public Task<T> InvokeAsync<T>(Func<T> func)
        {
            var tcs = new TaskCompletionSource<T>();
            Post(() =>
            {
                try
                {
                    var result = func();
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return tcs.Task;
        }
    }
}

public class DispatcherTimer
{
    private readonly System.Threading.Timer _timer;
    private TimeSpan _interval;
    private bool _isRunning;

    public event EventHandler? Tick;

    public TimeSpan Interval
    {
        get => _interval;
        set
        {
            _interval = value;
            if (_isRunning)
            {
                _timer.Change(_interval, _interval);
            }
        }
    }

    public DispatcherTimer()
    {
        _timer = new System.Threading.Timer(OnTimerTick, null, System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
    }

    public void Start()
    {
        _isRunning = true;
        _timer.Change(_interval, _interval);
    }

    public void Stop()
    {
        _isRunning = false;
        _timer.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
    }

    private void OnTimerTick(object? state)
    {
        GLib.Functions.IdleAdd(0, () =>
        {
            try
            {
                Tick?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error during DispatcherTimer tick");
            }
            return false;
        });
    }
}
