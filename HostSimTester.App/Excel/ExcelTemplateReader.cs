using System.IO;
using System.Text;
using ExcelDataReader;

namespace HostSimTester.App.Excel;

/// <summary>
/// Reads CEID/DVID lists from the TSMC Define Event Report Excel template.
/// The template (e.g. tsmc_template_version1.xlsx) typically has sheets that include
/// a "CEID" / "DVID" column header. We scan all sheets and collect numeric IDs found
/// under any column whose header contains "CEID" / "DVID" (case-insensitive).
/// </summary>
public static class ExcelTemplateReader
{
    private static int _encodingRegistered;

    public sealed class TemplateContent
    {
        public List<uint> Ceids { get; } = new();
        public List<uint> Dvids { get; } = new();
        public List<uint> Rptids { get; } = new();
    }

    public static TemplateContent Read(string path)
    {
        EnsureEncodingProvider();

        var content = new TemplateContent();
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var dataset = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = false }
        });

        foreach (System.Data.DataTable table in dataset.Tables)
        {
            CollectFromTable(table, content);
        }

        Dedup(content.Ceids);
        Dedup(content.Dvids);
        Dedup(content.Rptids);
        return content;
    }

    private static void CollectFromTable(System.Data.DataTable table, TemplateContent content)
    {
        // Scan all rows for real data headers (CEID / DVID / RPTID),
        // and ignore setting rows such as CEIDType / VIDType / RPTIDType.
        for (int r = 0; r < table.Rows.Count; r++)
        {
            if (!TryGetHeaderMap(table.Rows[r], table.Columns.Count, out var headerByCol))
            {
                continue;
            }

            // Read data rows until the next header row.
            for (int dr = r + 1; dr < table.Rows.Count; dr++)
            {
                if (TryGetHeaderMap(table.Rows[dr], table.Columns.Count, out _))
                {
                    break;
                }

                var dataRow = table.Rows[dr];
                foreach (var (col, kind) in headerByCol)
                {
                    if (col >= table.Columns.Count) continue;
                    var raw = dataRow[col]?.ToString()?.Trim();
                    if (string.IsNullOrEmpty(raw)) continue;
                    if (!TryParseId(raw, out var id)) continue;

                    switch (kind)
                    {
                        case "CEID": content.Ceids.Add(id); break;
                        case "DVID": content.Dvids.Add(id); break;
                        case "RPTID": content.Rptids.Add(id); break;
                    }
                }
            }
        }
    }

    private static bool TryGetHeaderMap(System.Data.DataRow row, int columnCount, out Dictionary<int, string> headerByCol)
    {
        headerByCol = new Dictionary<int, string>();

        for (int c = 0; c < columnCount; c++)
        {
            var cell = row[c]?.ToString()?.Trim();
            if (string.IsNullOrEmpty(cell))
            {
                continue;
            }

            var normalized = NormalizeHeader(cell);
            switch (normalized)
            {
                case "CEID":
                    headerByCol[c] = "CEID";
                    break;
                case "DVID":
                case "VID":
                    headerByCol[c] = "DVID";
                    break;
                case "RPTID":
                    headerByCol[c] = "RPTID";
                    break;
            }
        }

        return headerByCol.Count > 0;
    }

    private static string NormalizeHeader(string value)
    {
        var upper = value.ToUpperInvariant();
        var chars = upper.Where(char.IsLetterOrDigit).ToArray();
        return new string(chars);
    }

    private static bool TryParseId(string raw, out uint id)
    {
        id = 0;
        if (uint.TryParse(raw, out id)) return true;
        // Some templates carry numbers like "101001.0" as Excel doubles.
        if (double.TryParse(raw, out var d) && d >= 0 && d <= uint.MaxValue && Math.Truncate(d) == d)
        {
            id = (uint)d;
            return true;
        }
        return false;
    }

    private static void Dedup(List<uint> list)
    {
        var seen = new HashSet<uint>();
        list.RemoveAll(x => !seen.Add(x));
    }

    private static void EnsureEncodingProvider()
    {
        if (Interlocked.Exchange(ref _encodingRegistered, 1) == 0)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
    }
}
