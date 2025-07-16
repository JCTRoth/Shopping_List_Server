using ShoppingListServer.Entities;
using System.Threading.Tasks;

namespace ShoppingListServer.Services.Interfaces
{
    public interface IEMailVerificationService
    {
        Task<bool> SendEMailVerificationCodeAndAddToken(string userId);

        // Verifies a user by email verification code and removes the users email verification tokens.
        // Also clears all remaining EMail verification tokens if the user is already registered.
        // Returns the verified user or if verification wasn't successful null (e.g. there is user assigned to the code or the user is already verified).
        // Throws an exception if the code has expired.
        User VerifyEMailUrlCode(string urlCode);
    }
}
