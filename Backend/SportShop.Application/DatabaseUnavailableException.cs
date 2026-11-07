namespace SportShop.Application;

// Báo CSDL không kết nối được; ApiExceptionHandler đổi thành lỗi 503.
public sealed class DatabaseUnavailableException : Exception
{
    // Tạo exception với thông báo và lỗi gốc.
    public DatabaseUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
