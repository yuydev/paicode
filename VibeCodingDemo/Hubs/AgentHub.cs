using Microsoft.AspNetCore.SignalR;
using VibeCodingDemo.Services;

namespace VibeCodingDemo.Hubs;

public class AgentHub : Hub
{
    public async Task SendMessage(AgentMessage message)
    {
        await Clients.All.SendAsync("ReceiveMessage", message);
    }
}
