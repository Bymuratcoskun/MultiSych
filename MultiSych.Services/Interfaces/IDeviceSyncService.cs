using System;
using System.Threading;
using System.Threading.Tasks;

namespace MultiSych.Services.Interfaces;

public record DeviceInfo(string DeviceId, string DeviceName, string Platform, DateTime LastSeen);

public interface IDeviceSyncService
{
    string LocalDeviceId { get; }
    Task RegisterDeviceAsync(CancellationToken ct = default);
    Task<DeviceInfo[]> GetPairedDevicesAsync(CancellationToken ct = default);
    Task ExportSnapshotAsync(string destinationPath, CancellationToken ct = default);
    Task ImportSnapshotAsync(string sourcePath, CancellationToken ct = default);
    Task<string> GetSyncStatusAsync();
}
