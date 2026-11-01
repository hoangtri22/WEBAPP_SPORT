namespace SportShop.Application;

public sealed class DatabaseUnavailableException : Exception
{
    public DatabaseUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
