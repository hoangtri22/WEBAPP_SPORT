using System.Net;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using SportShop.Contracts.Catalog;
using SportShop.Contracts.Common;

namespace SportShop.Mobile.Services;

// Các API đọc sản phẩm.
public interface ICatalogApi
{
    Task<PagedResponse<ProductSummaryDto>> GetProductsAsync(string? query, int page, CancellationToken cancellationToken);
    Task<PagedResponse<ProductSummaryDto>> GetProductsAsync(string? query, int page, int? categoryId,
        string sort, bool inStockOnly, CancellationToken cancellationToken)
        => GetProductsAsync(query, page, cancellationToken);
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CategoryDto>>([]);
    Task<PagedResponse<ProductSummaryDto>> GetProductsAsync(string? query, int page, int? categoryId,
        string sort, bool inStockOnly, decimal? minPrice, decimal? maxPrice,
        IReadOnlyList<string> sizes, IReadOnlyList<string> colors, CancellationToken cancellationToken)
        => GetProductsAsync(query, page, categoryId, sort, inStockOnly, cancellationToken);
    Task<ProductFilterOptionsDto> GetFilterOptionsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new ProductFilterOptionsDto([], []));
    Task<IReadOnlyList<ProductSummaryDto>> GetRelatedProductsAsync(int productId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ProductSummaryDto>>([]);
    Task<ProductDetailDto> GetProductAsync(int productId, CancellationToken cancellationToken = default)
        => Task.FromException<ProductDetailDto>(new CatalogException("Không tìm thấy sản phẩm."));
}

// Cài đặt ICatalogApi bằng HttpClient.
public sealed class CatalogApi(HttpClient http) : ICatalogApi
{
    // Gọi GET api/v1/products (ProductsController của API); không kết nối MySQL hoặc tự dựng dữ liệu sản phẩm trong app.
    public Task<PagedResponse<ProductSummaryDto>> GetProductsAsync(
        string? query, int page, CancellationToken cancellationToken)
        => GetProductsAsync(query, page, null, "new", true, cancellationToken);

    // Giữ nguyên điều kiện lọc và sắp xếp trên mỗi trang kết quả.
    public Task<PagedResponse<ProductSummaryDto>> GetProductsAsync(
        string? query, int page, int? categoryId, string sort, bool inStockOnly, CancellationToken cancellationToken)
        => GetProductsAsync(query, page, categoryId, sort, inStockOnly, null, null, [], [], cancellationToken);

    // Gửi bộ lọc biến thể lên backend; số thập phân độc lập ngôn ngữ của thiết bị.
    public async Task<PagedResponse<ProductSummaryDto>> GetProductsAsync(
        string? query, int page, int? categoryId, string sort, bool inStockOnly, decimal? minPrice,
        decimal? maxPrice, IReadOnlyList<string> sizes, IReadOnlyList<string> colors, CancellationToken cancellationToken)
    {
        var path = $"api/v1/products?page={page}&pageSize=20&sort={Uri.EscapeDataString(sort)}&inStockOnly={inStockOnly.ToString().ToLowerInvariant()}";
        if (categoryId is > 0) path += "&categoryId=" + categoryId;
        if (!string.IsNullOrWhiteSpace(query)) path += "&q=" + Uri.EscapeDataString(query.Trim());
        if (minPrice is { } minimum) path += "&minPrice=" + minimum.ToString(CultureInfo.InvariantCulture);
        if (maxPrice is { } maximum) path += "&maxPrice=" + maximum.ToString(CultureInfo.InvariantCulture);
        foreach (var size in sizes) path += "&sizes=" + Uri.EscapeDataString(size);
        foreach (var color in colors) path += "&colors=" + Uri.EscapeDataString(color);
        var result = await GetAsync<PagedResponse<ProductSummaryDto>>(path, cancellationToken);
        if (result.Items is null || result.Page != page || result.PageSize < 1 || result.TotalItems < 0
            || result.Items.Any(item => item is null || item.Id <= 0))
            throw new CatalogException("Dữ liệu sản phẩm chưa hợp lệ. Vui lòng thử lại.");
        return result;
    }

    // Danh sách lựa chọn lấy từ toàn bộ catalog công khai, không suy ra từ trang kết quả hiện tại.
    public async Task<ProductFilterOptionsDto> GetFilterOptionsAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetAsync<ProductFilterOptionsDto>("api/v1/products/filter-options?inStockOnly=false", cancellationToken);
        if (result.Sizes is null || result.Colors is null
            || result.Sizes.Concat(result.Colors).Any(string.IsNullOrWhiteSpace))
            throw new CatalogException("Dữ liệu bộ lọc chưa hợp lệ. Vui lòng thử lại.");
        return result;
    }

    // Backend xác định danh mục và loại sản phẩm hiện tại trước khi giới hạn số gợi ý.
    public async Task<IReadOnlyList<ProductSummaryDto>> GetRelatedProductsAsync(int productId, CancellationToken cancellationToken = default)
    {
        if (productId <= 0) throw new CatalogException("Không tìm thấy sản phẩm.");
        var result = await GetAsync<ProductSummaryDto[]>($"api/v1/products/{productId}/related?limit=8", cancellationToken);
        if (result.Any(item => item is null || item.Id <= 0))
            throw new CatalogException("Dữ liệu sản phẩm liên quan chưa hợp lệ. Vui lòng thử lại.");
        return result;
    }

    // Đọc danh mục từ CatalogController; lỗi danh mục không thay thế danh sách sản phẩm.
    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetAsync<CategoryDto[]>("api/v1/categories", cancellationToken);
        if (result.Any(category => category is null || category.Id <= 0))
            throw new CatalogException("Dữ liệu danh mục chưa hợp lệ. Vui lòng thử lại.");
        return result;
    }

    // Đọc chi tiết và các biến thể từ ProductsController.
    public async Task<ProductDetailDto> GetProductAsync(int productId, CancellationToken cancellationToken = default)
    {
        if (productId <= 0) throw new CatalogException("Không tìm thấy sản phẩm.");
        var result = await GetAsync<ProductDetailDto>($"api/v1/products/{productId}", cancellationToken);
        if (result.Id != productId || result.Variants is null || result.Variants.Any(variant => variant is null
                || variant.Id <= 0 || variant.Price < 0 || variant.Stock < 0))
            throw new CatalogException("Dữ liệu sản phẩm chưa hợp lệ. Vui lòng thử lại.");
        return result;
    }

    // Đọc JSON với thông báo lỗi thống nhất, giữ nguyên cancellation của màn hình.
    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.GetAsync(path, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new CatalogException(response.StatusCode switch
                {
                    HttpStatusCode.NotFound => "Không tìm thấy sản phẩm hoặc nội dung này.",
                    HttpStatusCode.ServiceUnavailable => "Máy chủ chưa sẵn sàng. Vui lòng thử lại sau.",
                    _ => "Máy chủ chưa xử lý được yêu cầu. Vui lòng thử lại."
                });
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
                ?? throw new CatalogException("Dữ liệu sản phẩm chưa hợp lệ. Vui lòng thử lại.");
        }
        catch (Exception ex) when (ex is HttpRequestException or WebException or IOException)
        {
            cancellationToken.ThrowIfCancellationRequested();
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

// Lỗi khi tải sản phẩm.
public sealed class CatalogException(string message) : Exception(message);
