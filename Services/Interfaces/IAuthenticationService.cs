using System;
using System.Net.Http;
using System.Threading.Tasks;
using ShoppingListServer.Models;

namespace ShoppingListServer.Services.Interfaces
{
    public interface IAuthenticationService
    {
        Task<HttpResponseMessage> AuthenticateFacebook(FacebookProfile facebookProfile, string accessToken);

        Task<HttpResponseMessage> AuthenticateFacebookWithException(FacebookProfile facebookProfile, string accessToken);

        Task<HttpResponseMessage> AuthenticateGoogle(GoogleUser googleUser, string accessToken);

        Task<HttpResponseMessage> AuthenticateGoogleWithException(GoogleUser googleUser, string accessToken);

        Task<HttpResponseMessage> AuthenticateApple(AppleAccount appleAccount);

        Task<HttpResponseMessage> AuthenticateAppleWithException(AppleAccount appleAccount);

    }
}
