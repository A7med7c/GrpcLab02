using Grpc.Core;
using NexusVault.Abstractions;
using NexusVault.DeviceService.Persistence;

namespace NexusVault.DeviceService.Handlers;

public class DeviceRegistryHandler : DeviceRegistryService.DeviceRegistryServiceBase
{
    private readonly IDeviceCatalog _catalog;
    private readonly ILogger<DeviceRegistryHandler> _logger;

    public DeviceRegistryHandler(
        IDeviceCatalog catalog,
        ILogger<DeviceRegistryHandler> logger)
    {
        _catalog = catalog;
        _logger = logger;
    }

    public override Task<DeviceReply> EnrollDevice(EnrollDeviceRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.SerialCode))
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument, "serial_code is required."));
        }

        var device = new RegisteredDevice
        {
            DeviceId = $"DEV-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            SerialCode = request.SerialCode,
            Category = request.Category,
            MaxLoadKg = request.MaxLoadKg,
            IsOnline = true
        };

        _catalog.Enroll(device);
        return Task.FromResult(new DeviceReply { Device = device });
    }

    public override Task<DeviceReply> FetchDevice(FetchDeviceRequest request, ServerCallContext context)
    {
        var device = _catalog.Lookup(request.DeviceId)
            ?? throw new RpcException(new Status(
                StatusCode.NotFound, $"Device '{request.DeviceId}' was not found."));

        return Task.FromResult(new DeviceReply { Device = device });
    }

    public override async Task StreamDeviceMetrics(IAsyncStreamReader<DeviceMetrics> requestStream, IServerStreamWriter<DeviceMetricsAck> responseStream, ServerCallContext context)
    {
        await foreach (var metrics in requestStream.ReadAllAsync())
        {
            if (!context.CancellationToken.IsCancellationRequested)
            {
                break;
            }

            _logger.LogInformation("Received metrics from device {DeviceId}: Lat={Latitude}, Lon={Longitude}, Velocity={VelocityKmh} km/h, CapturedAt={CapturedAt}",
                metrics.DeviceId, metrics.Latitude, metrics.Longitude, metrics.VelocityKmh, metrics.CapturedAt);

            var ack = new DeviceMetricsAck
            {
                DeviceId = metrics.DeviceId,
                CapturedAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow),
                Accepted = true
            };

            await responseStream.WriteAsync(ack);
        }
    }
}
