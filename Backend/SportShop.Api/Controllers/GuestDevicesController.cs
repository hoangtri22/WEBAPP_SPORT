using Microsoft.AspNetCore.Mvc;
using SportShop.Api.Auth;
using SportShop.Contracts.Customer;

namespace SportShop.Api.Controllers;

[ApiController, Route("api/v1/guest-devices")]
public sealed class GuestDevicesController(GuestDeviceStore devices) : ControllerBase
{
    /// <summary>Ghi nhận thiết bị vào app với tư cách khách (không cần token) qua GuestDeviceStore.SaveAsync(); Web Admin hiển thị ở trang Quản lý khách hàng.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Register(GuestDeviceRequest request, CancellationToken cancellationToken)
    {
        await devices.SaveAsync(request, cancellationToken);
        return NoContent();
    }
}
