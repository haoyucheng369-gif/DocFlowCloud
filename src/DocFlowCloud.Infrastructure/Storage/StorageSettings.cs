namespace DocFlowCloud.Infrastructure.Storage;

// 存储配置：
// 当前默认使用 Local，共享目录适合本地联调；
// 后续上云时可以把 Provider 切到 AzureBlob。
public sealed class StorageSettings
{
    public const string SectionName = "Storage";

    public string Provider { get; set; } = "Local";

    public LocalStorageSettings Local { get; set; } = new();

    public AzureBlobStorageSettings AzureBlob { get; set; } = new();
}

public sealed class LocalStorageSettings
{
    public string RootPath { get; set; } = "../../shared-storage";
}

public sealed class AzureBlobStorageSettings
{
    public string ConnectionString { get; set; } = string.Empty;

    public string ContainerName { get; set; } = "docflow-files";
}
