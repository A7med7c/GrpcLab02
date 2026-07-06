using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using NexusVault.Abstractions;
using NexusVault.RecordService.Persistence;
using Microsoft.AspNetCore.Authorization;

namespace NexusVault.RecordService.Handlers;

[Authorize]
public class RecordCatalogHandler : RecordCatalogService.RecordCatalogServiceBase
{
    private readonly IRecordStore _store;
    private readonly ILogger<RecordCatalogHandler> _logger;

    public RecordCatalogHandler(
        IRecordStore store,
        ILogger<RecordCatalogHandler> logger)
    {
        _store = store;
        _logger = logger;
    }

    [Authorize(Roles = "Admin")]
    public override Task<RecordReply> CreateRecord(CreateRecordRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.OwnerName))
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument, "owner_name is required."));
        }

        if (request.LineItems.Count == 0)
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument, "A record needs at least one line item."));
        }

        var record = new VaultRecord
        {
            RecordId = $"REC-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            OwnerName = request.OwnerName,
            State = ProcessingState.Queued,
            SubmittedAt = Timestamp.FromDateTime(DateTime.UtcNow),
            EstimatedDuration = Duration.FromTimeSpan(TimeSpan.FromHours(2))
        };

        record.LineItems.AddRange(request.LineItems);
        record.Metadata.Add(request.Metadata);

        if (request.Notes is not null)
        {
            record.Notes = request.Notes;
        }

        switch (request.PayloadKindCase)
        {
            case CreateRecordRequest.PayloadKindOneofCase.SensitivePayload:
                record.SensitivePayload = request.SensitivePayload;
                break;
            case CreateRecordRequest.PayloadKindOneofCase.TemperaturePayload:
                record.TemperaturePayload = request.TemperaturePayload;
                break;
            case CreateRecordRequest.PayloadKindOneofCase.DefaultPayload:
            case CreateRecordRequest.PayloadKindOneofCase.None:
            default:
                record.DefaultPayload = new DefaultPayload();
                break;
        }

        _store.Insert(record);
        _logger.LogInformation("Created record {RecordId} for {OwnerName}", record.RecordId, record.OwnerName);

        return Task.FromResult(new RecordReply { Record = record });
    }

    public override Task<RecordReply> FetchRecord(FetchRecordRequest request, ServerCallContext context)
    {
        var record = _store.Lookup(request.RecordId)
            ?? throw new RpcException(new Status(
                StatusCode.NotFound, $"Record '{request.RecordId}' was not found."));

        return Task.FromResult(new RecordReply { Record = record });
    }

    public override Task<RecordReply> UpdateProcessingState(UpdateProcessingStateRequest request, ServerCallContext context)
    {
        var record = _store.Lookup(request.RecordId)
            ?? throw new RpcException(new Status(
                StatusCode.NotFound, $"Record '{request.RecordId}' was not found."));

        if (request.NewState == ProcessingState.Unknown)
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument, "new_state must be a real state, not UNKNOWN."));
        }

        record.State = request.NewState;
        _store.Revise(record);

        return Task.FromResult(new RecordReply { Record = record });
    }

    [Authorize(Roles = "Admin")]
    public override Task<ListRecordsReply> ListRecords(ListRecordsRequest request, ServerCallContext context)
    {
        var all = _store.ListAll();

        var filtered = request.StateFilter == ProcessingState.Unknown
            ? all
            : all.Where(r => r.State == request.StateFilter);

        var reply = new ListRecordsReply();
        reply.Records.AddRange(filtered);
        return Task.FromResult(reply);
    }

    public override async Task MonitorRecordState(MonitorRecordRequest request, IServerStreamWriter<RecordStateNotification> responseStream, ServerCallContext context)
    {
        _logger.LogInformation("Monitoring record {RecordId} for state changes", request.RecordId);

        var states = new[]
        {
            ProcessingState.Active,
            ProcessingState.Completed
        };

        foreach (var state in states)
        {
            await responseStream.WriteAsync(new RecordStateNotification
            {
                RecordId = request.RecordId,
                State = state,
                Summary = $"Record is now {state}",
                ChangedAt = Timestamp.FromDateTime(DateTime.UtcNow)
            });

            await Task.Delay(2500, context.CancellationToken);
        }
    }

    public override async Task<BulkUpdateProcessingStateReply> UpdateBulkProcessingState(IAsyncStreamReader<BulkUpdateProcessingStateRequest> requestStream, ServerCallContext context)
    {
        var updatedCount = 0;
        var totalReceived = 0;
        var notifications = new List<RecordStateNotification>();

        await foreach (var request in requestStream.ReadAllAsync())
        {
            totalReceived++;
            var record = _store.Lookup(request.RecordId);
            if (record is not null && request.NewState != ProcessingState.Unknown && request.NewState != record.State)
            {
                record.State = request.NewState;
                _store.Revise(record);
                updatedCount++;
                notifications.Add(new RecordStateNotification
                {
                    RecordId = record.RecordId,
                    State = record.State,
                    Summary = $"Record state updated to {record.State}",
                    ChangedAt = Timestamp.FromDateTime(DateTime.UtcNow)
                });
            }
        }

        var reply = new BulkUpdateProcessingStateReply
        {
            UpdatedCount = updatedCount,
            TotalReceived = totalReceived,
        };

        reply.Notifications.AddRange(notifications);

        return reply;
    }
}
