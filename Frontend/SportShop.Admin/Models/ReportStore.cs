using System.Text.Json;

namespace SportShop.Admin.Models;

// Một báo cáo thống kê đã tạo (file Excel hoặc PDF) trong lịch sử báo cáo.
public record ReportEntry(string Id, string Name, string Kind, string TypeLabel, string Format, string File,
    DateTime From, DateTime To, string CreatedBy, DateTime CreatedAt, int Downloads);

// Lịch sử các báo cáo thống kê đã tạo: file báo cáo và danh sách (index.json) nằm trong thư mục Uploads/reports của hai web, không dùng database.
public class ReportStore
{
    readonly string _dir;
    readonly object _lock = new();

    public ReportStore(IConfiguration config, IWebHostEnvironment env)
        => _dir = Path.Combine(Path.GetFullPath(Path.Combine(env.ContentRootPath, config["Paths:Uploads"] ?? "../Uploads")), "reports");

    string IndexPath => Path.Combine(_dir, "index.json");

    // Đọc danh sách từ index.json (mới nhất lên đầu); chưa có file hoặc file hỏng thì trả danh sách rỗng.
    List<ReportEntry> Load()
    {
        try { return JsonSerializer.Deserialize<List<ReportEntry>>(File.ReadAllText(IndexPath)) ?? []; }
        catch (Exception e) when (e is IOException or JsonException) { return []; }
    }

    // Ghi danh sách vào index.json.
    void Save(List<ReportEntry> list)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(IndexPath, JsonSerializer.Serialize(list));
    }

    // Toàn bộ báo cáo đã tạo, mới nhất trước.
    public List<ReportEntry> All()
    {
        lock (_lock) return Load().OrderByDescending(r => r.CreatedAt).ToList();
    }

    // Lưu file báo cáo vừa dựng vào thư mục reports và ghi một dòng vào lịch sử.
    public ReportEntry Add(string name, string kind, string typeLabel, string format, byte[] bytes, DateTime from, DateTime to, string createdBy)
    {
        lock (_lock)
        {
            Directory.CreateDirectory(_dir);
            string id = Guid.NewGuid().ToString("N")[..12];
            string file = id + (format == "PDF" ? ".pdf" : ".xlsx");
            File.WriteAllBytes(Path.Combine(_dir, file), bytes);
            var entry = new ReportEntry(id, name, kind, typeLabel, format, file, from, to, createdBy, DateTime.Now, 0);
            var list = Load();
            list.Add(entry);
            Save(list);
            return entry;
        }
    }

    // Mở một báo cáo để tải xuống (chỉ theo mã có trong lịch sử, không nhận đường dẫn từ người dùng): tăng số lượt tải và trả về tên, đường dẫn file.
    public (ReportEntry Entry, string Path)? Open(string id)
    {
        lock (_lock)
        {
            var list = Load();
            int i = list.FindIndex(r => r.Id == id);
            if (i < 0) return null;
            string path = Path.Combine(_dir, list[i].File);
            if (!File.Exists(path)) return null;
            list[i] = list[i] with { Downloads = list[i].Downloads + 1 };
            Save(list);
            return (list[i], path);
        }
    }
}
