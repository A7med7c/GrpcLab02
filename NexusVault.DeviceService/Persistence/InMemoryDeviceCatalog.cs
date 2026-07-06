using NexusVault.Abstractions;
using System.Collections.Concurrent;

namespace NexusVault.DeviceService.Persistence;

public class InMemoryDeviceCatalog : IDeviceCatalog
{
    private readonly ConcurrentDictionary<string, RegisteredDevice> _devices = new();

    public InMemoryDeviceCatalog()
    {
        Enroll(new RegisteredDevice
        {
            DeviceId = "DEV-001",
            SerialCode = "CAI-1234",
            Category = DeviceCategory.Compact,
            MaxLoadKg = 500f,
            IsOnline = true
        });

        Enroll(new RegisteredDevice
        {
            DeviceId = "DEV-002",
            SerialCode = "CAI-5678",
            Category = DeviceCategory.Heavy,
            MaxLoadKg = 2000f,
            IsOnline = true
        });
    }

    public RegisteredDevice Enroll(RegisteredDevice device)
    {
        _devices[device.DeviceId] = device;
        return device;
    }

    public RegisteredDevice? Lookup(string deviceId) =>
        _devices.TryGetValue(deviceId, out var device) ? device : null;
}
