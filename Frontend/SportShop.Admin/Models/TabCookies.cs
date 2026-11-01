using System.Text.RegularExpressions;
using Microsoft.Extensions.Primitives;

namespace SportShop.Admin.Models;

/// <summary>
/// Cho phép mở nhiều tab, mỗi tab một tài khoản. Trình duyệt dùng chung cookie cho mọi tab nên ở đây "ảo hóa" cookie theo mã tab:
/// cookie do ứng dụng đặt (đăng nhập, chống giả mạo...) được đổi tên thành "tên.t&lt;mã tab&gt;" khi gửi về trình duyệt,
/// và khi nhận về chỉ giữ cookie của đúng tab đang gọi rồi trả lại tên gốc. Phần còn lại của ứng dụng không cần biết gì về tab.
/// Mã tab do wwwroot/js/tab-session.js tạo, gửi bằng header X-Tab-Id (fetch) hoặc cookie <c>tabCookie</c> (mở trang);
/// không có mã tab (chưa chạy JS) thì dùng cookie như bình thường.
/// </summary>
public class TabCookieMiddleware(RequestDelegate next, string tabCookie)
{
    static readonly Regex TabIdFormat = new("^[0-9a-f]{32}$", RegexOptions.Compiled);
    static readonly Regex TabSuffix = new(@"\.t[0-9a-f]{32}$", RegexOptions.Compiled);

    public async Task Invoke(HttpContext ctx)
    {
        string tab = TabId(ctx.Request);
        string suffix = tab.Length > 0 ? ".t" + tab : "";
        ctx.Request.Headers.Cookie = KeepOwnCookies(ctx.Request.Headers.Cookie, suffix);

        if (suffix.Length > 0)
            ctx.Response.OnStarting(() =>
            {
                var set = ctx.Response.Headers.SetCookie;
                if (set.Count > 0) ctx.Response.Headers.SetCookie = new StringValues(set.Select(c => AddSuffix(c, suffix)).ToArray());
                return Task.CompletedTask;
            });

        await next(ctx);
    }

    // Mã tab hợp lệ (32 ký tự hex) lấy từ header X-Tab-Id, không có thì từ cookie mã tab; không hợp lệ thì coi như không có tab.
    string TabId(HttpRequest request)
    {
        string? id = request.Headers["X-Tab-Id"].FirstOrDefault();
        if (string.IsNullOrEmpty(id))
            id = Pairs(request.Headers.Cookie).Where(p => p.Name == tabCookie).Select(p => p.Pair[(p.Name.Length + 1)..]).FirstOrDefault();
        return id != null && TabIdFormat.IsMatch(id) ? id : "";
    }

    // Giữ cookie của tab này (đổi về tên gốc); bỏ cookie của tab khác và cookie mã tab. Không có tab thì giữ cookie không gắn mã tab.
    StringValues KeepOwnCookies(StringValues header, string suffix)
    {
        var kept = new List<string>();
        foreach (var (name, pair) in Pairs(header))
        {
            if (name == tabCookie) continue;
            if (suffix.Length > 0) { if (name.EndsWith(suffix, StringComparison.Ordinal)) kept.Add(name[..^suffix.Length] + pair[name.Length..]); }
            else if (!TabSuffix.IsMatch(name)) kept.Add(pair);
        }
        return kept.Count == 0 ? StringValues.Empty : new StringValues(string.Join("; ", kept));
    }

    // "name=value" thêm hậu tố mã tab vào tên: "name.t<mã>=value; path=/; ..."
    string AddSuffix(string setCookie, string suffix)
    {
        int eq = setCookie.IndexOf('=');
        return eq <= 0 || setCookie[..eq] == tabCookie ? setCookie : setCookie[..eq] + suffix + setCookie[eq..];
    }

    // Tách header Cookie thành các cặp "tên=giá trị" (giữ nguyên giá trị gốc)
    static IEnumerable<(string Name, string Pair)> Pairs(StringValues header)
    {
        foreach (var line in header)
            foreach (var segment in (line ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                int eq = segment.IndexOf('=');
                if (eq > 0) yield return (segment[..eq], segment);
            }
    }
}
