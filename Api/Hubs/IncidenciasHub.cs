using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Shared;
using System.Security.Claims;

namespace Api.Hubs;

[Authorize]
public class IncidenciasHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var rol    = Context.User?.FindFirstValue(ClaimTypes.Role);

        if (userId != null)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");

        if (rol is Roles.Admin or Roles.Tecnico)
            await Groups.AddToGroupAsync(Context.ConnectionId, "admins-tecnicos");

        await base.OnConnectedAsync();
    }
}
