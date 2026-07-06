using NexusVault.Abstractions;

namespace NexusVault.DeviceService.Persistence;

public interface IDeviceCatalog
{
    RegisteredDevice Enroll(RegisteredDevice device);
    RegisteredDevice? Lookup(string deviceId);
}
