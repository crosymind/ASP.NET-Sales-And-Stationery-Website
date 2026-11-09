using Microsoft.AspNetCore.SignalR;

namespace QuanLyKho.Hubs
{
    /// <summary>
    /// Hub SignalR để push thông báo real-time về trình duyệt của tất cả client đang kết nối.
    /// </summary>
    public class NotificationHub : Hub
    {
        // Hub không cần logic phức tạp – server chủ động push qua IHubContext
    }
}
