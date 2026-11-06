using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SportShop.Contracts.Catalog;
using SportShop.Contracts.Common;

namespace SportShop.Mobile.Services;

public interface ICatalogApi
{
    Task<PagedResponse<ProductSummaryDto>> GetProductsAsync(string? query, int page, CancellationToken cancellationToken);
}

public sealed class CatalogApi(HttpClient http) : ICatalogApi
{
    // Gọi GET api/v1/products (ProductsController của API); không kết nối MySQL hoặc tự dựng dữ liệu sản phẩm trong app.
    public async Task<PagedResponse<ProductSummaryDto>> GetProductsAsync(
        string? query, int page, CancellationToken cancellationToken)
    {
        var path = $"api/v1/products?page={page}&pageSize=20&sort=new&inStockOnly=true";
        if (!string.IsNullOrWhiteSpace(query)) path += "&q=" + Uri.EscapeDataString(query.Trim());
        try
        {
            using var response = await http.GetAsync(path, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new CatalogException(response.StatusCode == HttpStatusCode.ServiceUnavailable
                    ? "Máy chủ chưa sẵn sàng. Vui lòng thử lại sau."
                    : "Máy chủ chưa xử lý được yêu cầu. Vui lòng thử lại.");
            var result = await response.Content.ReadFromJsonAsync<PagedResponse<ProductSummaryDto>>(cancellationToken);
            if (result is null || result.Items is null || result.Page != page || result.PageSize < 1 || result.TotalItems < 0)
                throw new CatalogException("Dữ liệu sản phẩm chưa hợp lệ. Vui lòng thử lại.");
            return result;
        }
        catch (HttpRequestException)
        {
            throw new CatalogException("Không kết nối được cửa hàng. Vui lòng kiểm tra kết nối và thử lại.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CatalogException("Kết nối mất quá nhiều thời gian. Vui lòng thử lại.");
        }
        catch (JsonException)
        {
            throw new CatalogException("Dữ liệu sản phẩm chưa hợp lệ. Vui lòng thử lại.");
        }
    }
}

public sealed class CatalogException(string message) : Exception(message);
