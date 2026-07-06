using NexusVault.Abstractions;

namespace NexusVault.RecordService.Persistence;

public interface IClientRepository
{
    VaultClient Register(VaultClient client);
    VaultClient? Lookup(string clientId);
}
