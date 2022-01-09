using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using ShoppingListServer.Entities;
using ShoppingListServer.Helpers;
using ShoppingListServer.LiveUpdates;
using ShoppingListServer.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShoppingListServer.Services
{
    public class UserHubService : IUserHub
    {
        private readonly IHubContext<UpdateHub_Controller> _hubContext;
        private readonly Lazy<IUserService> _userService;

        public UserHubService(
            IServiceProvider services,
            IHubContext<UpdateHub_Controller> hubContext)
        {
            _userService = new Lazy<IUserService>(() => (IUserService)services.GetService(typeof(IUserService)));
            _hubContext = hubContext;
        }

        public async Task SendUserVerified(User user)
        {
            await _hubContext.Clients.User(user.Id).SendAsync("UserEMailVerified");
        }
    }
}
