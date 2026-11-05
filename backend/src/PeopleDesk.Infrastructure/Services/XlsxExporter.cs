using ClosedXML.Excel;
using PeopleDesk.Application.Common;

namespace PeopleDesk.Infrastructure.Services;

public class XlsxExporter : IXlsxExporter
{
    public byte[] Export(string sheetName, IReadOnlyList<string> headers, IEnumerable<IEnumerable<object?>> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(string.IsNullOrWhiteSpace(sheetName) ? "Export" : sheetName.Length <= 31 ? sheetName : sheetName[..31]);

        for (var c = 0; c < headers.Count; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0F172A");
            cell.Style.Font.FontColor = XLColor.White;
        }

        var r = 2;
        foreach (var row in rows)
        {
            var c = 1;
            foreach (var cellValue in row)
            {
                var cell = ws.Cell(r, c);
                switch (cellValue)
                {
                    case null: break;
                    case int i: cell.Value = i; break;
                    case long l: cell.Value = l; break;
                    case decimal d: cell.Value = d; cell.Style.NumberFormat.Format = "#,##0.00"; break;
                    case double db: cell.Value = db; cell.Style.NumberFormat.Format = "#,##0.00"; break;
                    case DateTime dt: cell.Value = dt; cell.Style.DateFormat.Format = "yyyy-MM-dd HH:mm"; break;
                    case DateOnly dOnly: cell.Value = dOnly.ToDateTime(TimeOnly.MinValue); cell.Style.DateFormat.Format = "yyyy-MM-dd"; break;
                    case bool bo: cell.Value = bo ? "Yes" : "No"; break;
                    default: cell.Value = cellValue.ToString() ?? string.Empty; break;
                }
                c++;
            }
            r++;
        }

        ws.Columns().AdjustToContents();
        ws.RangeUsed()?.SetAutoFilter();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
