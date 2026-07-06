using NexusVault.Abstractions;
using System.Collections.Concurrent;

namespace NexusVault.RecordService.Persistence;

public class InMemoryClientRepository : IClientRepository
{
    private readonly ConcurrentDictionary<string, VaultClient> _clients = new();

    public InMemoryClientRepository()
    {
        Register(new VaultClient
        {
            ClientId = "CLI-001",
            FullName = "Alice Johnson",
            Email = "alice@example.com",
            PhoneNumber = "+1-555-0101",
            IsActive = true,
            RegisteredAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow)
        });

        Register(new VaultClient
        {
            ClientId = "CLI-002",
            FullName = "Bob Smith",
            Email = "bob@example.com",
            PhoneNumber = "+1-555-0102",
            IsActive = true,
            RegisteredAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow)
        });
    }

    public VaultClient Register(VaultClient client)
    {
        _clients[client.ClientId] = client;
        return client;
    }

    public VaultClient? Lookup(string clientId) =>
        _clients.TryGetValue(clientId, out var client) ? client : null;
}
