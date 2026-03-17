using Microsoft.AspNetCore.SignalR;

namespace DocFlowCloud.Api.Realtime;

// Job 状态更新 Hub：
// 浏览器连接到这个 Hub 后，可以实时收到后端推送的任务状态变化。
public sealed class JobUpdatesHub : Hub
{
}
