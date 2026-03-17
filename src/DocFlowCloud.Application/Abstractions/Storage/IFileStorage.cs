namespace DocFlowCloud.Application.Abstractions.Storage;

// 文件存储抽象：
// 业务层只关心 storage key，不关心底层是本地目录、NAS 还是 Azure Blob。
public interface IFileStorage
{
    Task<string> SaveAsync(
        string category,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken = default);

    Task<byte[]?> ReadAsync(string storageKey, CancellationToken cancellationToken = default);
}
