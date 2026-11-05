using System.Globalization;
using System.Text;
using SportShop.Contracts.Commerce;

namespace SportShop.Mobile.Services;

public sealed record SearchFieldOption(string Key, string Label, string Kind);
public sealed record RelationalOperatorOption(string Key, string Label);
public sealed record LogicalOperatorOption(string Key, string Label);

public sealed record OrderSearchCondition(
    string Join,        // "and" ("Và") hoặc "or" ("Hoặc")
    string FieldKey,    // "code", "total", "date", "status", "payStatus", "receiver", "product", "itemCount"
    string OperatorKey, // "contains", "notcontains", "eq", "neq", "starts", "gt", "gte", "lt", "lte"
    string Value);

// Bộ công cụ Tìm kiếm nâng cao cho Đơn hàng & Hóa đơn trên Mobile App (đồng bộ 100% logic với wwwroot/js/search-kit.js của Web):
// 1. Bỏ dấu tiếng Việt và không phân biệt hoa thường (Fold).
// 2. Toán tử quan hệ theo kiểu dữ liệu: chữ (chứa, không chứa, bằng, khác, bắt đầu bằng), số / tiền / ngày (=, ≠, >, ≥, <, ≤).
// 3. Toán tử logic: nối nhiều điều kiện bằng Và (AND) / Hoặc (OR), trong đó Và được gom nhóm ưu tiên tính trước Hoặc.
public static class AdvancedOrderSearch
{
    public static readonly IReadOnlyList<SearchFieldOption> Fields =
    [
        new("code", "Mã đơn / Số HĐ", "text"),
        new("total", "Tổng tiền (₫)", "money"),
        new("date", "Ngày đặt (dd/MM/yyyy)", "date"),
        new("status", "Trạng thái đơn", "text"),
        new("payStatus", "Thanh toán", "text"),
        new("receiver", "Người nhận / SĐT", "text"),
        new("product", "Tên sản phẩm", "text"),
        new("itemCount", "Tổng số lượng món", "number")
    ];

    public static readonly IReadOnlyList<LogicalOperatorOption> LogicalOperators =
    [
        new("and", "Và (AND)"),
        new("or", "Hoặc (OR)")
    ];

    public static readonly IReadOnlyList<RelationalOperatorOption> TextOperators =
    [
        new("contains", "chứa"),
        new("notcontains", "không chứa"),
        new("eq", "= (bằng)"),
        new("neq", "≠ (khác)"),
        new("starts", "bắt đầu bằng")
    ];

    public static readonly IReadOnlyList<RelationalOperatorOption> CompareOperators =
    [
        new("eq", "= (bằng)"),
        new("neq", "≠ (khác)"),
        new("gt", "> (lớn hơn)"),
        new("gte", "≥ (lớn hơn hoặc bằng)"),
        new("lt", "< (nhỏ hơn)"),
        new("lte", "≤ (nhỏ hơn hoặc bằng)")
    ];

    public static IReadOnlyList<RelationalOperatorOption> OperatorsForField(string fieldKey)
    {
        var kind = Fields.FirstOrDefault(f => f.Key == fieldKey)?.Kind ?? "text";
        return kind is "money" or "number" or "date" ? CompareOperators : TextOperators;
    }

    // Chữ thường, bỏ dấu tiếng Việt ("đ" -> "d"), gộp khoảng trắng: "Giày  Chạy Đỏ" -> "giay chay do".
    public static string Fold(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var normalized = s.ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        var prevSpace = false;
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsWhiteSpace(ch))
            {
                if (!prevSpace && sb.Length > 0) { sb.Append(' '); prevSpace = true; }
            }
            else
            {
                sb.Append(ch);
                prevSpace = false;
            }
        }
        return sb.ToString().Trim().Normalize(NormalizationForm.FormC);
    }

    // Chuỗi văn bản khớp khi chứa đủ mọi từ khóa trong query (không phân biệt thứ tự, hoa thường, dấu).
    public static bool MatchesKeyword(string? text, string? query)
    {
        var words = Fold(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return true;
        var folded = Fold(text);
        return words.All(w => folded.Contains(w, StringComparison.Ordinal));
    }

    // Lọc danh sách đơn hàng trong bộ nhớ đệm theo từ khóa chung, trạng thái tab, và danh sách điều kiện nâng cao (Và / Hoặc).
    public static IReadOnlyList<OrderDto> Filter(
        IEnumerable<OrderDto> orders,
        string? keyword,
        string? statusFilter,
        IEnumerable<OrderSearchCondition>? conditions)
    {
        var activeConds = (conditions ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c.Value))
            .ToList();

        return orders.Where(order =>
        {
            if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter != "all" && order.Status != statusFilter)
                return false;

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var summaryText = $"{order.Code} {order.InvoiceNumber} {order.Receiver.Name} {order.Receiver.Phone} {order.Receiver.Address} {order.StatusLabel} {order.PaymentMethod} " +
                                  string.Join(" ", order.Items.Select(i => $"{i.Name} {i.Variant}"));
                if (!MatchesKeyword(summaryText, keyword))
                    return false;
            }

            return MatchesConditions(order, activeConds);
        }).ToArray();
    }

    // Tách danh sách điều kiện thành các nhóm tại mỗi toán tử "or" (Hoặc); trong mỗi nhóm phải thỏa tất cả ("and" - Và), chỉ cần 1 nhóm đúng là thỏa ("or" - Hoặc).
    public static bool MatchesConditions(OrderDto order, IReadOnlyList<OrderSearchCondition> activeConds)
    {
        if (activeConds.Count == 0) return true;

        var groups = new List<List<OrderSearchCondition>>();
        List<OrderSearchCondition>? current = null;
        foreach (var cond in activeConds)
        {
            if (current is null || string.Equals(cond.Join, "or", StringComparison.OrdinalIgnoreCase))
            {
                current = [];
                groups.Add(current);
            }
            current.Add(cond);
        }

        return groups.Any(group => group.All(cond => EvaluateSingle(order, cond)));
    }

    public static bool EvaluateSingle(OrderDto order, OrderSearchCondition cond)
    {
        var kind = Fields.FirstOrDefault(f => f.Key == cond.FieldKey)?.Kind ?? "text";
        var op = cond.OperatorKey;
        var rawVal = cond.Value.Trim();

        if (kind is "money" or "number")
        {
            decimal left = cond.FieldKey == "itemCount"
                ? order.Items.Sum(i => i.Quantity)
                : order.Total;
            if (!TryParseDecimal(rawVal, out var right))
                return op == "neq";
            return Compare(op, left, right);
        }

        if (kind == "date")
        {
            var leftDate = order.OrderedAt.LocalDateTime.Date;
            if (!TryParseDate(rawVal, out var rightDate))
                return op == "neq";
            return Compare(op, leftDate.Ticks, rightDate.Date.Ticks);
        }

        string text = cond.FieldKey switch
        {
            "code" => $"{order.Code} {order.InvoiceNumber}",
            "status" => $"{order.StatusLabel} {order.Status}",
            "payStatus" => $"{(order.PaymentStatus == "da_thanh_toan" ? "Đã thanh toán" : "Chưa thanh toán")} {order.PaymentStatus} {order.PaymentMethod}",
            "receiver" => $"{order.Receiver.Name} {order.Receiver.Phone} {order.Receiver.Address}",
            "product" => string.Join(" ", order.Items.Select(i => $"{i.Name} {i.Variant}")),
            _ => order.Code
        };

        var fText = Fold(text);
        var fVal = Fold(rawVal);
        return op switch
        {
            "notcontains" => !MatchesKeyword(text, rawVal),
            "eq" => fText == fVal,
            "neq" => fText != fVal,
            "starts" => fText.StartsWith(fVal, StringComparison.Ordinal),
            _ => MatchesKeyword(text, rawVal)
        };
    }

    public static bool Compare<T>(string op, T a, T b) where T : IComparable<T>
    {
        int cmp = a.CompareTo(b);
        return op switch
        {
            "eq" => cmp == 0,
            "neq" => cmp != 0,
            "gt" => cmp > 0,
            "gte" => cmp >= 0,
            "lt" => cmp < 0,
            "lte" => cmp <= 0,
            _ => cmp == 0
        };
    }

    private static bool TryParseDecimal(string text, out decimal result)
    {
        var cleaned = new string(text.Where(ch => char.IsDigit(ch) || ch == '-' || ch == ',').ToArray()).Replace(',', '.');
        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryParseDate(string text, out DateTime result)
    {
        string[] formats = ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy"];
        return DateTime.TryParseExact(text.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out result)
               || DateTime.TryParse(text.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }
}
