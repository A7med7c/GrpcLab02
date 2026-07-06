using Grpc.Core;
using NexusVault.Abstractions;
using NexusVault.RecordService.Persistence;

namespace NexusVault.RecordService.Handlers;

public class ClientManagerHandler : ClientManagerService.ClientManagerServiceBase
{
    private readonly IClientRepository _repository;
    private readonly ILogger<ClientManagerHandler> _logger;

    public ClientManagerHandler(
        IClientRepository repository,
        ILogger<ClientManagerHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public override Task<ClientReply> RegisterClient(RegisterClientRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument, "full_name is required."));
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument, "email is required."));
        }

        var client = new VaultClient
        {
            ClientId = $"CLI-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            FullName = request.FullName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber ?? string.Empty,
            IsActive = true,
            RegisteredAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow)
        };

        _repository.Register(client);
        _logger.LogInformation("Registered client {ClientId}: {FullName} <{Email}>", client.ClientId, client.FullName, client.Email);

        return Task.FromResult(new ClientReply { Client = client });
    }

    public override Task<ClientReply> FetchClient(FetchClientRequest request, ServerCallContext context)
    {
        var client = _repository.Lookup(request.ClientId)
            ?? throw new RpcException(new Status(
                StatusCode.NotFound, $"Client '{request.ClientId}' was not found."));

        return Task.FromResult(new ClientReply { Client = client });
    }
}
