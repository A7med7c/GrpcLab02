using NexusVault.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace NexusVault.RestApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DevicesController : ControllerBase
{
    private readonly DeviceRegistryService.DeviceRegistryServiceClient _deviceClient;
    private readonly ILogger<DevicesController> _logger;

    public DevicesController(
        DeviceRegistryService.DeviceRegistryServiceClient deviceClient,
        ILogger<DevicesController> logger)
    {
        _deviceClient = deviceClient;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> EnrollDevice([FromBody] EnrollDeviceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _deviceClient.EnrollDeviceAsync(request, cancellationToken: cancellationToken);
            return Ok(response.Device);
        }
        catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            return BadRequest(ex.Status.Detail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enrolling device");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("stream-metrics")]
    public async Task<IActionResult> StreamDeviceMetrics([FromBody] IEnumerable<DeviceMetricsDto> metricsData, CancellationToken cancellationToken)
    {
        try
        {
            using var call = _deviceClient.StreamDeviceMetrics(cancellationToken: cancellationToken);

            foreach (var sample in metricsData)
            {
                var metricsRequest = new DeviceMetrics
                {
                    DeviceId = sample.DeviceId,
                    Latitude = sample.Latitude,
                    Longitude = sample.Longitude,
                    VelocityKmh = sample.VelocityKmh,
                    CapturedAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(sample.CapturedAt.ToUniversalTime())
                };

                await call.RequestStream.WriteAsync(metricsRequest);
                await Task.Delay(6000);
            }

            await call.RequestStream.CompleteAsync();

            while (await call.ResponseStream.MoveNext(cancellationToken))
            {
                var response = call.ResponseStream.Current;
                _logger.LogInformation("Received metrics ack for device {DeviceId} at {CapturedAt}: Accepted={Accepted}", response.DeviceId, response.CapturedAt.ToDateTime(), response.Accepted);
            }

            return Ok("Metrics streamed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming device metrics");
            return StatusCode(500, "Internal server error");
        }
    }
}

public record DeviceMetricsDto
{
    public string DeviceId { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public float VelocityKmh { get; init; }
    public DateTime CapturedAt { get; init; }
}
