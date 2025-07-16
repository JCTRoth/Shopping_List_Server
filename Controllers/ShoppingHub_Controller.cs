using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ShoppingListServer.LiveUpdates
{
    [Authorize]
    public class UpdateHub_Controller : Hub
    {
        public UpdateHub_Controller()
        {
        }

        public override async Task OnConnectedAsync()
        {
            await Clients.Caller.SendAsync("ServerMessage", $"Connected to {Context.UserIdentifier}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            await Clients.Caller.SendAsync("ServerMessage", $"Disconnected {Context.UserIdentifier}");
            await base.OnDisconnectedAsync(exception);
            Console.Error.WriteLine("OnDisconnectAsync {0}", exception);
        }
    }
}
