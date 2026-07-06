using NexusVault.Abstractions;
using System.Collections.Concurrent;

namespace NexusVault.RecordService.Persistence;

public class InMemoryRecordStore : IRecordStore
{
    private readonly ConcurrentDictionary<string, VaultRecord> _records;

    public InMemoryRecordStore()
    {
        _records = BootstrapSampleRecords();
    }

    public VaultRecord Insert(VaultRecord record)
    {
        _records[record.RecordId] = record;
        return record;
    }

    public VaultRecord? Lookup(string recordId) =>
        _records.TryGetValue(recordId, out var record) ? record : null;

    public VaultRecord Revise(VaultRecord record)
    {
        _records[record.RecordId] = record;
        return record;
    }

    public IReadOnlyCollection<VaultRecord> ListAll() => _records.Values.ToList();

    private static ConcurrentDictionary<string, VaultRecord> BootstrapSampleRecords()
    {
        var records = new ConcurrentDictionary<string, VaultRecord>();

        Enumerable.Range(1, 10).ToList().ForEach(i =>
        {
            var record = new VaultRecord
            {
                RecordId = i.ToString(),
                OwnerName = $"Owner {i}",
                State = ProcessingState.Queued,
                SubmittedAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow),
                EstimatedDuration = Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan(TimeSpan.FromHours(2)),
                Notes = $"Notes for record {i}",
                Metadata = { { "Key1", "Value1" }, { "Key2", "Value2" } }
            };
            records[record.RecordId] = record;
        });

        return records;
    }
}
