using Grpc.Core;
using Grpc.Net.Client;
using NexusVault.Abstractions;

using var recordChannel = GrpcChannel.ForAddress("http://localhost:5240");
using var deviceChannel = GrpcChannel.ForAddress("http://localhost:5062");
var recordClient = new RecordCatalogService.RecordCatalogServiceClient(recordChannel);
var deviceClient = new DeviceRegistryService.DeviceRegistryServiceClient(deviceChannel);

Console.WriteLine("=== NexusVault Sandbox ===\n");

try
{
    var createRequest = new CreateRecordRequest
    {
        OwnerName = "Mona Youssef",
        Notes = "Leave with the building security if nobody answers.",
        TemperaturePayload = new TemperatureControlledPayload { CeilingCelsius = 4.0f }
    };
    createRequest.LineItems.Add(new RecordLineItem { ItemLabel = "Frozen Yogurt Tub", UnitCount = 12, MassKg = 6.5 });
    createRequest.LineItems.Add(new RecordLineItem { ItemLabel = "Ice Packs", UnitCount = 4, MassKg = 1.2 });
    createRequest.Metadata.Add("channel", "mobile-app");
    createRequest.Metadata.Add("promo_code", "SUMMER10");

    var created = await recordClient.CreateRecordAsync(createRequest);

    var recordId = created.Record.RecordId;

    Console.WriteLine($"[CreateRecord] Created {recordId} for {created.Record.OwnerName}");
    Console.WriteLine($"  State:             {created.Record.State}");
    Console.WriteLine($"  Submitted at:      {created.Record.SubmittedAt.ToDateTime():u}");
    Console.WriteLine($"  Estimated time:    {created.Record.EstimatedDuration.ToTimeSpan()}");
    Console.WriteLine($"  Payload (oneof):   {created.Record.PayloadKindCase} -> max {created.Record.TemperaturePayload?.CeilingCelsius}°C");
    Console.WriteLine($"  Line items:        {string.Join(", ", created.Record.LineItems.Select(i => $"{i.UnitCount}x {i.ItemLabel}"))}");
    Console.WriteLine($"  Metadata (map):    {string.Join(", ", created.Record.Metadata.Select(kv => $"{kv.Key}={kv.Value}"))}\n");

    var fetched = await recordClient.FetchRecordAsync(new FetchRecordRequest { RecordId = recordId });
    Console.WriteLine($"[FetchRecord] Fetched {fetched.Record.RecordId}, state = {fetched.Record.State}\n");

    var updated = await recordClient.UpdateProcessingStateAsync(new UpdateProcessingStateRequest
    {
        RecordId = recordId,
        NewState = ProcessingState.Active
    });
    Console.WriteLine($"[UpdateProcessingState] {updated.Record.RecordId} is now {updated.Record.State}\n");

    var listReply = await recordClient.ListRecordsAsync(new ListRecordsRequest
    {
        StateFilter = ProcessingState.Active
    });
    Console.WriteLine($"[ListRecords] {listReply.Records.Count} record(s) currently active:");
    foreach (var record in listReply.Records)
    {
        Console.WriteLine($"  - {record.RecordId} ({record.OwnerName})");
    }
    Console.WriteLine();

    var deviceReply = await deviceClient.EnrollDeviceAsync(new EnrollDeviceRequest
    {
        SerialCode = "GIZ-9911",
        Category = DeviceCategory.Portable,
        MaxLoadKg = 40f
    });
    Console.WriteLine($"[EnrollDevice] Enrolled {deviceReply.Device.DeviceId} ({deviceReply.Device.Category})\n");

    var seededDevice = await deviceClient.FetchDeviceAsync(new FetchDeviceRequest { DeviceId = "DEV-001" });
    Console.WriteLine($"[FetchDevice] DEV-001 -> {seededDevice.Device.SerialCode}, max load {seededDevice.Device.MaxLoadKg}kg\n");

    try
    {
        await recordClient.FetchRecordAsync(new FetchRecordRequest { RecordId = "REC-DOES-NOT-EXIST" });
    }
    catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
    {
        Console.WriteLine($"[FetchRecord] Expected failure handled cleanly: {ex.Status.Detail}\n");
    }
}
catch (RpcException ex)
{
    Console.WriteLine($"gRPC call failed: {ex.StatusCode} — {ex.Status.Detail}");
}

Console.WriteLine("=== Done ===");
Console.ReadKey();
