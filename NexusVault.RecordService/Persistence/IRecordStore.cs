using NexusVault.Abstractions;

namespace NexusVault.RecordService.Persistence;

public interface IRecordStore
{
    VaultRecord Insert(VaultRecord record);
    VaultRecord Revise(VaultRecord record);
    VaultRecord? Lookup(string recordId);
    IReadOnlyCollection<VaultRecord> ListAll();
}
