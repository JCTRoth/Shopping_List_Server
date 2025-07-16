namespace ShoppingListServer.Models
{
    /// <summary>
    /// Default: neither list sharing nore ignored
    /// AllowSharing: allow the user to share lists
    /// Ignored: ignore the user. Ignore all lists that the ignored user owns.
    /// </summary>
    public enum UserContactType
    {
        Default, AllowSharing, Ignored
    }
}
