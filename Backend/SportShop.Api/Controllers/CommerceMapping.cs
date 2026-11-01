using SportShop.Api.Media;
using SportShop.Application.Catalog;
using SportShop.Contracts.Commerce;
using SportShop.CustomerWeb.Models;

namespace SportShop.Api.Controllers;

internal static class CommerceMapping
{
    internal static CartDto Cart(IReadOnlyList<CartLine> lines, MediaUrlResolver media) => new(
        lines.Select(line => new CartItemDto(line.VariantId, line.ProductId, line.Name, line.Size, line.Color,
            media.Resolve(line.Image), line.Price, line.Qty, line.Stock, line.Amount, line.Enough)).ToArray(),
        lines.Sum(line => line.Qty), lines.Sum(line => line.Amount));
    internal static EligibleVoucherDto Voucher(UsablePromo item, ICatalogClock clock) => new(item.Promo.Code, item.Promo.Name,
        item.Promo.Percent, item.Promo.MaxDiscount, item.Promo.Tier, clock.WithOffset(item.Promo.End), item.Discount);
    internal static OrderDto Order(MyOrder order, MediaUrlResolver media, ICatalogClock clock) => new(order.Id, order.Code, clock.WithOffset(order.Date),
        order.Status, order.StatusText, order.PayStatus, order.PayMethod,
        new(order.Receiver, order.Phone, order.Address), order.CancelReason, order.Carrier, order.Tracking,
        order.Subtotal, order.Discount, order.Ship, order.Total, order.Invoice, order.CanCancel,
        order.Lines.Select(line => new OrderItemDto(line.ProductId, line.Name, line.Label, line.Qty,
            line.Price, line.Amount, media.Resolve(line.Image))).ToArray());
}
