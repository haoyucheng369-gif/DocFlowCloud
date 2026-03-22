using Microsoft.AspNetCore.SignalR;

namespace DocFlowCloud.Api.Realtime;

// Job 状态更新 Hub：
// 浏览器连上这个 Hub 后，可以实时收到后端推送的 jobUpdated 事件。
// 这里故意保持很薄，只作为实时通信边界，不承载业务逻辑。
public sealed class JobUpdatesHub : Hub
{
}
