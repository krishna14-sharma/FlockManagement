using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace FlockManagement.Common.Helpers
{
    /// <summary>Common description of a report to export (title, filter lines, table).</summary>
    public class ExportDocument
    {
        public string Title { get; set; }
        public IList<string> SubTitles { get; set; }
        public DataTable Table { get; set; }
        public string Footer { get; set; }

        public ExportDocument()
        {
            SubTitles = new List<string>();
        }
    }

    internal static class ExportFormat
    {
        public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static bool IsNumeric(Type t)
        {
            t = Nullable.GetUnderlyingType(t) ?? t;
            return t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte) ||
                   t == typeof(decimal) || t == typeof(double) || t == typeof(float);
        }

        public static bool IsInteger(Type t)
        {
            t = Nullable.GetUnderlyingType(t) ?? t;
            return t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte);
        }

        /// <summary>Display text for a cell (used by CSV and PDF).</summary>
        public static string Text(object value, Type type)
        {
            if (value == null || value is DBNull) return string.Empty;
            if (value is DateTime)
            {
                var d = (DateTime)value;
                return d.TimeOfDay == TimeSpan.Zero ? d.ToString("dd-MMM-yyyy", Inv) : d.ToString("dd-MMM-yyyy HH:mm", Inv);
            }
            if (value is decimal) return ((decimal)value).ToString("0.###", Inv);
            if (value is double) return ((double)value).ToString("0.###", Inv);
            if (value is bool) return (bool)value ? "Yes" : "No";
            return Convert.ToString(value, Inv);
        }
    }

    /// <summary>RFC 4180 CSV, UTF-8 with BOM so Excel opens it with the right encoding.</summary>
    public static class CsvWriter
    {
        public static byte[] Write(ExportDocument doc)
        {
            var sb = new StringBuilder();
            var t = doc.Table;
            sb.AppendLine(string.Join(",", t.Columns.Cast<DataColumn>().Select(c => Quote(c.ColumnName))));
            foreach (DataRow r in t.Rows)
            {
                sb.AppendLine(string.Join(",", t.Columns.Cast<DataColumn>()
                    .Select(c => Quote(r[c] is DateTime ? ((DateTime)r[c]).ToString("yyyy-MM-dd", ExportFormat.Inv) : ExportFormat.Text(r[c], c.DataType)))));
            }
            var preamble = Encoding.UTF8.GetPreamble();
            var body = Encoding.UTF8.GetBytes(sb.ToString());
            return preamble.Concat(body).ToArray();
        }

        private static string Quote(string s)
        {
            if (s == null) return string.Empty;
            // Neutralise spreadsheet formula injection
            if (s.Length > 0 && "=+-@".IndexOf(s[0]) >= 0 && !IsNumber(s)) s = "'" + s;
            return s.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        }

        private static bool IsNumber(string s)
        {
            decimal d;
            return decimal.TryParse(s, NumberStyles.Number, ExportFormat.Inv, out d);
        }
    }

    /// <summary>
    /// Minimal Office Open XML (.xlsx) writer - one sheet, title rows, bold header, number and date
    /// formats, column widths, frozen header and auto-filter. No third-party library needed.
    /// </summary>
    public static class XlsxWriter
    {
        public static byte[] Write(ExportDocument doc, string sheetName = "Report")
        {
            using (var ms = new MemoryStream())
            {
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    Add(zip, "[Content_Types].xml",
                        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                        "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                        "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                        "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                        "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                        "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
                        "</Types>");
                    Add(zip, "_rels/.rels",
                        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                        "</Relationships>");
                    Add(zip, "xl/workbook.xml",
                        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                        "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                        "<sheets><sheet name=\"" + Esc(SafeSheetName(sheetName)) + "\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
                        "<definedNames><definedName name=\"_xlnm._FilterDatabase\" localSheetId=\"0\" hidden=\"1\">" + FilterRef(doc) + "</definedName></definedNames>" +
                        "</workbook>");
                    Add(zip, "xl/_rels/workbook.xml.rels",
                        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                        "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
                        "</Relationships>");
                    Add(zip, "xl/styles.xml", Styles());
                    Add(zip, "xl/worksheets/sheet1.xml", Sheet(doc));
                }
                return ms.ToArray();
            }
        }

        // style indexes: 0 normal, 1 header, 2 decimal, 3 integer, 4 date, 5 title, 6 subtitle
        private static string Styles()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                   "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                   "<numFmts count=\"2\"><numFmt numFmtId=\"164\" formatCode=\"#,##0.###\"/><numFmt numFmtId=\"165\" formatCode=\"dd-mmm-yyyy\"/></numFmts>" +
                   "<fonts count=\"4\"><font><sz val=\"10\"/><name val=\"Calibri\"/></font>" +
                   "<font><b/><sz val=\"10\"/><color rgb=\"FFFFFFFF\"/><name val=\"Calibri\"/></font>" +
                   "<font><b/><sz val=\"14\"/><color rgb=\"FF1F5F4A\"/><name val=\"Calibri\"/></font>" +
                   "<font><i/><sz val=\"9\"/><color rgb=\"FF6C757D\"/><name val=\"Calibri\"/></font></fonts>" +
                   "<fills count=\"3\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill>" +
                   "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF1F5F4A\"/><bgColor indexed=\"64\"/></patternFill></fill></fills>" +
                   "<borders count=\"2\"><border/><border><left style=\"thin\"><color rgb=\"FFD9DEDC\"/></left><right style=\"thin\"><color rgb=\"FFD9DEDC\"/></right>" +
                   "<top style=\"thin\"><color rgb=\"FFD9DEDC\"/></top><bottom style=\"thin\"><color rgb=\"FFD9DEDC\"/></bottom></border></borders>" +
                   "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                   "<cellXfs count=\"7\">" +
                   "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyBorder=\"1\"/>" +
                   "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment wrapText=\"1\" vertical=\"center\"/></xf>" +
                   "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\"/>" +
                   "<xf numFmtId=\"3\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\"/>" +
                   "<xf numFmtId=\"165\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\"/>" +
                   "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
                   "<xf numFmtId=\"0\" fontId=\"3\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
                   "</cellXfs></styleSheet>";
        }

        private static int HeaderRow(ExportDocument doc)
        {
            return doc.SubTitles.Count + 3;  // title, subtitles, blank, header
        }

        private static string FilterRef(ExportDocument doc)
        {
            var cols = Math.Max(1, doc.Table.Columns.Count);
            var h = HeaderRow(doc);
            return "'" + "Report".Replace("'", "''") + "'!$A$" + h + ":$" + Col(cols - 1) + "$" + (h + Math.Max(1, doc.Table.Rows.Count));
        }

        private static string Sheet(ExportDocument doc)
        {
            var t = doc.Table;
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");

            var header = HeaderRow(doc);
            sb.Append("<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"" + header + "\" topLeftCell=\"A" + (header + 1) +
                      "\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");

            sb.Append("<cols>");
            for (var i = 0; i < t.Columns.Count; i++)
            {
                var c = t.Columns[i];
                var longest = c.ColumnName.Length;
                foreach (DataRow r in t.Rows)
                    longest = Math.Max(longest, ExportFormat.Text(r[c], c.DataType).Length);
                var width = Math.Min(45, Math.Max(8, longest + 2));
                sb.Append("<col min=\"" + (i + 1) + "\" max=\"" + (i + 1) + "\" width=\"" + width + "\" customWidth=\"1\"/>");
            }
            sb.Append("</cols><sheetData>");

            var row = 1;
            sb.Append("<row r=\"1\" ht=\"20\" customHeight=\"1\">" + StrCell("A1", doc.Title, 5) + "</row>");
            foreach (var s in doc.SubTitles)
            {
                row++;
                sb.Append("<row r=\"" + row + "\">" + StrCell("A" + row, s, 6) + "</row>");
            }
            row = header;
            sb.Append("<row r=\"" + row + "\">");
            for (var i = 0; i < t.Columns.Count; i++)
                sb.Append(StrCell(Col(i) + row, t.Columns[i].ColumnName, 1));
            sb.Append("</row>");

            foreach (DataRow r in t.Rows)
            {
                row++;
                sb.Append("<row r=\"" + row + "\">");
                for (var i = 0; i < t.Columns.Count; i++)
                {
                    var c = t.Columns[i];
                    var v = r[c];
                    var cellRef = Col(i) + row;
                    if (v == null || v is DBNull)
                        sb.Append("<c r=\"" + cellRef + "\" s=\"0\"/>");
                    else if (v is DateTime)
                        sb.Append("<c r=\"" + cellRef + "\" s=\"4\"><v>" + ((DateTime)v).ToOADate().ToString(ExportFormat.Inv) + "</v></c>");
                    else if (ExportFormat.IsNumeric(c.DataType))
                        sb.Append("<c r=\"" + cellRef + "\" s=\"" + (ExportFormat.IsInteger(c.DataType) ? 3 : 2) + "\"><v>" +
                                  Convert.ToDecimal(v, ExportFormat.Inv).ToString(ExportFormat.Inv) + "</v></c>");
                    else
                        sb.Append(StrCell(cellRef, ExportFormat.Text(v, c.DataType), 0));
                }
                sb.Append("</row>");
            }
            sb.Append("</sheetData>");
            if (t.Columns.Count > 0)
                sb.Append("<autoFilter ref=\"A" + header + ":" + Col(t.Columns.Count - 1) + (header + Math.Max(1, t.Rows.Count)) + "\"/>");
            sb.Append("<pageMargins left=\"0.4\" right=\"0.4\" top=\"0.5\" bottom=\"0.5\" header=\"0.3\" footer=\"0.3\"/>");
            sb.Append("<pageSetup orientation=\"landscape\" paperSize=\"9\" fitToWidth=\"1\" fitToHeight=\"0\"/>");
            sb.Append("</worksheet>");
            return sb.ToString();
        }

        private static string StrCell(string cellRef, string text, int style)
        {
            return "<c r=\"" + cellRef + "\" t=\"inlineStr\" s=\"" + style + "\"><is><t xml:space=\"preserve\">" + Esc(text) + "</t></is></c>";
        }

        public static string Col(int index)
        {
            var name = string.Empty;
            index++;
            while (index > 0)
            {
                var m = (index - 1) % 26;
                name = (char)('A' + m) + name;
                index = (index - m) / 26;
            }
            return name;
        }

        private static string SafeSheetName(string s)
        {
            var clean = new string((s ?? "Report").Where(ch => "[]:*?/\\".IndexOf(ch) < 0).ToArray());
            return clean.Length > 31 ? clean.Substring(0, 31) : (clean.Length == 0 ? "Report" : clean);
        }

        private static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s)
            {
                if (ch == '&') sb.Append("&amp;");
                else if (ch == '<') sb.Append("&lt;");
                else if (ch == '>') sb.Append("&gt;");
                else if (ch == '"') sb.Append("&quot;");
                else if (ch < 0x20 && ch != '\t' && ch != '\n' && ch != '\r') continue; // invalid in XML
                else sb.Append(ch);
            }
            return sb.ToString();
        }

        private static void Add(ZipArchive zip, string name, string content)
        {
            var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            using (var w = new StreamWriter(entry.Open(), new UTF8Encoding(false)))
            {
                w.Write(content);
            }
        }
    }

    /// <summary>
    /// Small dependency-free PDF writer for tabular reports: A4 landscape, Helvetica,
    /// repeated header row, zebra rows, status words coloured, "Page x of y" footer.
    /// </summary>
    public static class PdfTableWriter
    {
        private const float PageW = 842f, PageH = 595f, Margin = 28f;
        private static readonly Encoding Win1252 = GetWin1252();

        private static Encoding GetWin1252()
        {
            try { return Encoding.GetEncoding(1252); }
            catch (ArgumentException) { return Encoding.ASCII; }
        }

        public static byte[] Write(ExportDocument doc)
        {
            var t = doc.Table;
            var cols = t.Columns.Cast<DataColumn>().ToList();
            var cells = t.Rows.Cast<DataRow>().Select(r => cols.Select(c => ExportFormat.Text(r[c], c.DataType)).ToArray()).ToList();
            var numeric = cols.Select(c => ExportFormat.IsNumeric(c.DataType)).ToArray();

            // column widths from content length, scaled to the printable width
            var avail = PageW - 2 * Margin;
            float fontSize = 7.5f;
            var chars = cols.Select((c, i) => Math.Min(40, Math.Max(c.ColumnName.Length * 0.8f,
                cells.Count == 0 ? 4 : cells.Max(r => (float)r[i].Length)))).ToArray();
            var widths = chars.Select(ch => (ch + 1.5f) * fontSize * 0.52f).ToArray();
            var total = widths.Sum();
            if (total > avail)
            {
                var scale = avail / total;
                fontSize = Math.Max(5.5f, fontSize * scale);
                widths = chars.Select(ch => (ch + 1.5f) * fontSize * 0.52f).ToArray();
                total = widths.Sum();
                if (total > avail) widths = widths.Select(w => w * avail / total).ToArray();
            }
            else
            {
                widths = widths.Select(w => w * avail / total).ToArray(); // stretch to full width
            }

            var rowH = fontSize + 5f;
            var pages = new List<StringBuilder>();
            StringBuilder page = null;
            float y = 0;
            var rowIndex = 0;
            var statusCols = cols.Select(c => c.ColumnName.IndexOf("Status", StringComparison.OrdinalIgnoreCase) >= 0
                                           || c.ColumnName.IndexOf("Grade", StringComparison.OrdinalIgnoreCase) >= 0
                                           || c.ColumnName.Equals("Severity", StringComparison.OrdinalIgnoreCase)).ToArray();

            Action newPage = () =>
            {
                page = new StringBuilder();
                pages.Add(page);
                y = PageH - Margin;
                if (pages.Count == 1)
                {
                    Text(page, "F2", 14, Margin, y - 12, doc.Title ?? string.Empty, "0.122 0.373 0.29");
                    y -= 22;
                    foreach (var s in doc.SubTitles)
                    {
                        Text(page, "F1", 8, Margin, y - 8, s, "0.42 0.46 0.49");
                        y -= 11;
                    }
                    y -= 6;
                }
                // header row
                page.AppendFormat(ExportFormat.Inv, "0.122 0.373 0.29 rg {0:0.##} {1:0.##} {2:0.##} {3:0.##} re f\n", Margin, y - rowH - 2, avail, rowH + 2);
                var x = Margin;
                for (var i = 0; i < cols.Count; i++)
                {
                    var label = Fit(cols[i].ColumnName, widths[i] - 4, fontSize, true);
                    Text(page, "F2", fontSize, numeric[i] ? x + widths[i] - 2 - Width(label, fontSize, true) : x + 2, y - rowH + 2, label, "1 1 1");
                    x += widths[i];
                }
                y -= rowH + 2;
            };

            newPage();
            foreach (var r in cells)
            {
                if (y - rowH < Margin + 14) newPage();
                if (rowIndex % 2 == 1)
                    page.AppendFormat(ExportFormat.Inv, "0.955 0.965 0.96 rg {0:0.##} {1:0.##} {2:0.##} {3:0.##} re f\n", Margin, y - rowH, avail, rowH);
                var x = Margin;
                for (var i = 0; i < cols.Count; i++)
                {
                    var text = Fit(r[i], widths[i] - 4, fontSize, false);
                    var color = statusCols[i] ? StatusColor(r[i]) : "0.13 0.16 0.15";
                    var font = statusCols[i] && color != "0.13 0.16 0.15" ? "F2" : "F1";
                    Text(page, font, fontSize, numeric[i] ? x + widths[i] - 2 - Width(text, fontSize, false) : x + 2, y - rowH + 3, text, color);
                    x += widths[i];
                }
                page.AppendFormat(ExportFormat.Inv, "0.85 0.87 0.86 RG 0.4 w {0:0.##} {1:0.##} m {2:0.##} {1:0.##} l S\n", Margin, y - rowH, Margin + avail);
                y -= rowH;
                rowIndex++;
            }
            if (cells.Count == 0) Text(page, "F1", 9, Margin, y - 14, "No data for the selected filters.", "0.42 0.46 0.49");

            // footers
            for (var p = 0; p < pages.Count; p++)
            {
                var footer = string.Format(ExportFormat.Inv, "{0}   Page {1} of {2}", doc.Footer ?? string.Empty, p + 1, pages.Count).Trim();
                Text(pages[p], "F1", 7, Margin, Margin - 12, footer, "0.42 0.46 0.49");
            }
            return Assemble(pages);
        }

        private static string StatusColor(string s)
        {
            if (string.IsNullOrEmpty(s)) return "0.13 0.16 0.15";
            var v = s.ToLowerInvariant();
            if (v.Contains("critical") || v.Contains("poor")) return "0.86 0.21 0.27";
            if (v.Contains("below") || v.Contains("above") || v.Contains("warning")) return "0.70 0.53 0";
            if (v.Contains("within") || v.Contains("excellent") || v.Contains("good")) return "0.10 0.53 0.33";
            return "0.13 0.16 0.15";
        }

        private static float Width(string s, float size, bool bold)
        {
            float w = 0;
            foreach (var ch in s ?? string.Empty)
            {
                if (char.IsDigit(ch)) w += 0.556f;
                else if (ch == ' ' || ch == '.' || ch == ',' || ch == ':' || ch == 'i' || ch == 'l' || ch == '-') w += 0.28f;
                else if (char.IsUpper(ch) || ch == 'm' || ch == 'w') w += 0.70f;
                else w += 0.52f;
            }
            return w * size * (bold ? 1.06f : 1f);
        }

        private static string Fit(string s, float maxWidth, float size, bool bold)
        {
            if (string.IsNullOrEmpty(s) || Width(s, size, bold) <= maxWidth) return s ?? string.Empty;
            var cut = s;
            while (cut.Length > 1 && Width(cut + "..", size, bold) > maxWidth) cut = cut.Substring(0, cut.Length - 1);
            return cut + "..";
        }

        private static void Text(StringBuilder sb, string font, float size, float x, float y, string text, string rgb)
        {
            sb.AppendFormat(ExportFormat.Inv, "BT {0} rg /{1} {2:0.##} Tf {3:0.##} {4:0.##} Td ({5}) Tj ET\n", rgb, font, size, x, y, Escape(text));
        }

        private static string Escape(string s)
        {
            var sb = new StringBuilder();
            foreach (var ch in s ?? string.Empty)
            {
                if (ch == '\\' || ch == '(' || ch == ')') sb.Append('\\').Append(ch);
                else if (ch < 32) sb.Append(' ');
                else sb.Append(ch);
            }
            return sb.ToString();
        }

        private static byte[] Assemble(IList<StringBuilder> pages)
        {
            var objects = new List<byte[]>();
            // 1 catalog, 2 pages, 3 font regular, 4 font bold, then page/content pairs
            var kids = string.Join(" ", Enumerable.Range(0, pages.Count).Select(i => (5 + i * 2) + " 0 R"));
            objects.Add(Ascii("<< /Type /Catalog /Pages 2 0 R >>"));
            objects.Add(Ascii("<< /Type /Pages /Kids [" + kids + "] /Count " + pages.Count + " >>"));
            objects.Add(Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"));
            objects.Add(Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"));
            for (var i = 0; i < pages.Count; i++)
            {
                var contentId = 6 + i * 2;
                objects.Add(Ascii(string.Format(ExportFormat.Inv,
                    "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {0} {1}] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {2} 0 R >>",
                    PageW, PageH, contentId)));
                var body = Win1252.GetBytes(pages[i].ToString());
                var head = Ascii("<< /Length " + body.Length + " >>\nstream\n");
                var tail = Ascii("\nendstream");
                objects.Add(head.Concat(body).Concat(tail).ToArray());
            }

            using (var ms = new MemoryStream())
            {
                var offsets = new List<long>();
                Write(ms, Ascii("%PDF-1.4\n%\xE2\xE3\xCF\xD3\n"));
                for (var i = 0; i < objects.Count; i++)
                {
                    offsets.Add(ms.Position);
                    Write(ms, Ascii((i + 1) + " 0 obj\n"));
                    Write(ms, objects[i]);
                    Write(ms, Ascii("\nendobj\n"));
                }
                var xref = ms.Position;
                var sb = new StringBuilder();
                sb.Append("xref\n0 ").Append(objects.Count + 1).Append("\n0000000000 65535 f \n");
                foreach (var o in offsets) sb.Append(o.ToString("D10", ExportFormat.Inv)).Append(" 00000 n \n");
                sb.Append("trailer\n<< /Size ").Append(objects.Count + 1).Append(" /Root 1 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF\n");
                Write(ms, Ascii(sb.ToString()));
                return ms.ToArray();
            }
        }

        private static byte[] Ascii(string s)
        {
            return Win1252.GetBytes(s);
        }

        private static void Write(Stream s, byte[] b)
        {
            s.Write(b, 0, b.Length);
        }
    }
}
