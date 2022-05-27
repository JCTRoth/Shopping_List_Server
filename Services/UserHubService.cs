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

        /// <summary>
        /// Send a message that a contact was added for the given user.
        /// </summary>
        /// <returns></returns>
        public async Task SendContactAdded(string currentUserId, User contactUser)
        {
            string contactJson = contactUser == null ? "" : JsonConvert.SerializeObject(contactUser.WithoutPassword());
            await _hubContext.Clients.User(currentUserId).SendAsync("ContactAdded", contactJson);
        }
    }
}
