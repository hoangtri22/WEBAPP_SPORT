namespace SportShop.Application.Health;

// Kiểm tra CSDL sẵn sàng, cài đặt ở DatabaseReadiness.
public interface IDatabaseReadiness
{
    Task CheckAsync(CancellationToken cancellationToken);
}
