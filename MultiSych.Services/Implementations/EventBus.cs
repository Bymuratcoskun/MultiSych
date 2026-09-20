using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using MultiSych.Services.Interfaces;
using Serilog;

namespace MultiSych.Services.Implementations;

public sealed class EventBus : IEventBus, IDisposable
{
    private readonly ILogger _logger = Log.ForContext<EventBus>();
    private readonly ConcurrentDictionary<Type, List<object>> _handlers = new();
    private bool _disposed;

    public void Publish<TEvent>(TEvent @event) where TEvent : class
    {
        if (_disposed || @event == null) return;

        var eventType = typeof(TEvent);
        if (!_handlers.TryGetValue(eventType, out var handlers)) return;

        List<object> snapshot;
        lock (handlers) snapshot = new List<object>(handlers);

        foreach (var handler in snapshot)
        {
            try
            {
                switch (handler)
                {
                    case Func<TEvent, Task> asyncHandler:
                        _ = Task.Run(() => asyncHandler(@event));
                        break;
                    case Action<TEvent> syncHandler:
                        syncHandler(@event);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "EventBus handler threw for event {Event}", eventType.Name);
            }
        }
    }

    public IDisposable Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : class
    {
        return AddHandler(typeof(TEvent), handler);
    }

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class
    {
        return AddHandler(typeof(TEvent), handler);
    }

    private IDisposable AddHandler(Type eventType, object handler)
    {
        var handlers = _handlers.GetOrAdd(eventType, _ => new List<object>());
        lock (handlers) handlers.Add(handler);
        return new Subscription(() =>
        {
            if (_handlers.TryGetValue(eventType, out var h))
                lock (h) h.Remove(handler);
        });
    }

    public void Dispose()
    {
        _disposed = true;
        _handlers.Clear();
    }

    private sealed class Subscription(Action onDispose) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            onDispose();
        }
    }
}
