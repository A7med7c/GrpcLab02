using Grpc.Core;
using NexusVault.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NexusVault.RestApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecordsController : ControllerBase
{
    private readonly RecordCatalogService.RecordCatalogServiceClient _recordClient;
    private readonly ILogger<RecordsController> _logger;

    public RecordsController(
        RecordCatalogService.RecordCatalogServiceClient recordClient,
        ILogger<RecordsController> logger)
    {
        _recordClient = recordClient;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> ListRecords()
    {
        var records = await _recordClient.ListRecordsAsync(new ListRecordsRequest());
        return Ok(records);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> FetchRecordDetails(string id, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Authenticated user {User} is fetching record {RecordId}", User.Identity?.Name, id);

            var request = new FetchRecordRequest { RecordId = id };
            var response = await _recordClient.FetchRecordAsync(request, cancellationToken: cancellationToken);

            return Ok(new
            {
                response.Record.RecordId,
                response.Record.OwnerName,
                response.Record.State,
                SubmittedAt = response.Record.SubmittedAt.ToDateTime()
            });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            _logger.LogWarning("Record {RecordId} not found.", id);
            return NotFound($"Record {id} could not be found.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while fetching record {RecordId}", id);
            return StatusCode(500, "An internal error occurred.");
        }
    }

    [HttpGet("monitor/{id}")]
    public async Task<IActionResult> MonitorRecordState(string id, CancellationToken cancellationToken)
    {
        try
        {
            var request = new MonitorRecordRequest { RecordId = id };
            using var call = _recordClient.MonitorRecordState(request, cancellationToken: cancellationToken);

            await foreach (var response in call.ResponseStream.ReadAllAsync(cancellationToken))
            {
                _logger.LogInformation("Received state update for record {RecordId}: {State}", id, response.State);
            }

            return Ok($"Monitoring updates for record {id} completed.");
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            _logger.LogWarning("Record {RecordId} not found.", id);
            return NotFound($"Record {id} could not be found.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while monitoring record {RecordId}", id);
            return StatusCode(500, "An internal error occurred.");
        }
    }

    [HttpPut("bulk-update-state")]
    public async Task<IActionResult> UpdateBulkProcessingState(BulkUpdateProcessingStateDto stateDto, CancellationToken cancellationToken)
    {
        try
        {
            using var call = _recordClient.UpdateBulkProcessingState(cancellationToken: cancellationToken);

            foreach (var recordId in stateDto.RecordIds)
            {
                var request = new BulkUpdateProcessingStateRequest
                {
                    RecordId = recordId,
                    NewState = stateDto.NewState,
                };

                await call.RequestStream.WriteAsync(request);
            }

            await call.RequestStream.CompleteAsync();

            var response = await call.ResponseAsync;

            _logger.LogInformation("Updated state for {UpdatedCount} out of {TotalReceived} records to {NewState}.", response.UpdatedCount, response.TotalReceived, stateDto.NewState);

            return Ok($"Updated state for {response.UpdatedCount} records out of {response.TotalReceived} to {stateDto.NewState}.");
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            _logger.LogWarning("One or more records not found during bulk update.");
            return NotFound("One or more records could not be found.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while updating bulk record states.");
            return StatusCode(500, "An internal error occurred.");
        }
    }

    public record BulkUpdateProcessingStateDto(List<string> RecordIds, ProcessingState NewState);
}
