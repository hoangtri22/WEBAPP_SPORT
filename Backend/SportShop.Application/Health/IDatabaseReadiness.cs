namespace SportShop.Application.Health;

public interface IDatabaseReadiness
{
    Task CheckAsync(CancellationToken cancellationToken);
}
