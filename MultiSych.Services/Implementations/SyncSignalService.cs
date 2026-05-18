using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using MultiSych.Services.Interfaces;

namespace MultiSych.Services.Implementations;

public class SyncSignalService : ISyncSignalService
{
    private readonly Channel<bool> _channel;

    public SyncSignalService()
    {
        // Kuyrukta en fazla 1 istek tutulur, yeni istekler eskisinin yerine geçer (Spam engelleme)
        var options = new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        };
        _channel = Channel.CreateBounded<bool>(options);
    }

    public void TriggerSync()
    {
        _channel.Writer.TryWrite(true);
    }

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        await _channel.Reader.ReadAsync(cancellationToken);
    }
}