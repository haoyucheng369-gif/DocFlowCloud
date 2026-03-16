namespace DocFlowCloud.Api.Contracts;

// 文档转 PDF 的表单请求模型。
// 用独立模型承载 IFormFile，Swagger/OpenAPI 对 multipart/form-data 的生成更稳定。
public sealed class CreateDocumentToPdfForm
{
    public IFormFile File { get; set; } = default!;
    public string? Name { get; set; }
}
