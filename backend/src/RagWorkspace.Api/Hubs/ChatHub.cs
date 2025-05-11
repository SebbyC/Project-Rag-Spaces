using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RagWorkspace.Api.Interfaces;

namespace RagWorkspace.Api.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(ILogger<ChatHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst("sub")?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
            _logger.LogInformation("User {UserId} connected to chat hub", userId);
        }
        
        await base.OnConnectedAsync();
    }

    public async Task JoinProjectGroup(string projectId)
    {
        if (string.IsNullOrEmpty(projectId))
        {
            throw new ArgumentException("Project ID cannot be empty", nameof(projectId));
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"project-{projectId}");
        _logger.LogInformation("User joined project group {ProjectId}", projectId);
    }

    public async Task LeaveProjectGroup(string projectId)
    {
        if (string.IsNullOrEmpty(projectId))
        {
            throw new ArgumentException("Project ID cannot be empty", nameof(projectId));
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"project-{projectId}");
        _logger.LogInformation("User left project group {ProjectId}", projectId);
    }
}