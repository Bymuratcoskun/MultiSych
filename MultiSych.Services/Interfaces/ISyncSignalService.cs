using System.Threading;
using System.Threading.Tasks;

namespace MultiSych.Services.Interfaces;

public interface ISyncSignalService
{
    void TriggerSync();
    Task WaitAsync(CancellationToken cancellationToken);
}