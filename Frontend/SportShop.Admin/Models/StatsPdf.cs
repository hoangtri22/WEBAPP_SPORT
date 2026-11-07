using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SportShop.Admin.Controllers;

namespace SportShop.Admin.Models;

// Dựng file PDF báo cáo doanh thu bằng QuestPDF.
public static class StatsPdf
{
    const string HeadColor = "#d6ebe7";
    const string LineColor = "#e7ecf0";

    // Dựng file PDF báo cáo (khổ A4 ngang) từ các bảng trong StatsVm của StatsController.Build(); gọi từ StatsController.Pdf.
    public static byte[] Build(StatsVm vm, string kindText) => Document.Create(doc => doc.Page(page =>
    {
        page.Size(PageSizes.A4.Landscape());
        page.Margin(28);
        page.DefaultTextStyle(s => s.FontFamily("Arial").FontSize(9).FontColor("#17222c"));
        page.Header().PaddingBottom(10).Column(head =>
        {
            head.Item().Text("SPORTSHOP – BÁO CÁO THỐNG KÊ DOANH THU").FontSize(15).Bold().FontColor("#0f766e");
            head.Item().Text($"{kindText} · Từ ngày {vm.From:dd/MM/yyyy} đến ngày {vm.To:dd/MM/yyyy}");
            head.Item().Text($"Ngày lập: {DateTime.Now:dd/MM/yyyy HH:mm} · Số hóa đơn: {vm.Invoices:N0} · Tổng tiền hóa đơn: {Fmt.Money(vm.Total)}");
        });
        page.Content().Column(body =>
        {
            body.Spacing(14);
            foreach (var t in vm.Tables) body.Item().Element(c => Table(c, t));
        });
        page.Footer().AlignCenter().Text(x => { x.CurrentPageNumber(); x.Span(" / "); x.TotalPages(); });
    })).GeneratePdf();

    // Vẽ một StatTable: tiêu đề, dòng dữ liệu và dòng Tổng cộng nếu có; bảng nhiều cột thì thu nhỏ chữ.
    static void Table(IContainer container, StatTable t) => container.Column(block =>
    {
        float size = t.Cols.Count > 8 ? 6.5f : 9;
        block.Item().PaddingBottom(4).Text(t.Title).FontSize(11).Bold();
        block.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(t.Cols.Count > 8 ? 1.3f : 2);
                foreach (var _ in t.Cols) c.RelativeColumn();
            });
            table.Header(h =>
            {
                Head(h.Cell(), t.First, size, false);
                foreach (var c in t.Cols) Head(h.Cell(), c.Name, size, true);
            });
            foreach (var r in t.Rows) Line(table, t, r.Label, r.Values, size, false);
            if (t.Total.Length > 0) Line(table, t, "Tổng cộng", t.Total, size, true);
        });
    });

    // Vẽ một ô tiêu đề bảng nền xanh nhạt.
    static void Head(IContainer cell, string text, float size, bool right)
    {
        var box = cell.Background(HeadColor).Padding(3);
        (right ? box.AlignRight() : box).Text(text).FontSize(size).Bold();
    }

    // Vẽ một dòng dữ liệu: nhãn và các giá trị theo định dạng cột trong StatTable.Cols.
    static void Line(TableDescriptor table, StatTable t, string label, decimal[] values, float size, bool bold)
    {
        Body(table.Cell(), label, size, bold, false);
        for (int i = 0; i < t.Cols.Count; i++)
            Body(table.Cell(), t.Cols[i].Money ? Fmt.Money(values[i]) : values[i].ToString("N0"), size, bold, true);
    }

    // Vẽ một ô dữ liệu có viền dưới; cột số canh phải.
    static void Body(IContainer cell, string text, float size, bool bold, bool right)
    {
        var box = cell.BorderBottom(1).BorderColor(LineColor).Padding(3);
        var span = (right ? box.AlignRight() : box).Text(text).FontSize(size);
        if (bold) span.Bold();
    }
}
