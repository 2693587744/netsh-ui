using System.Text;

namespace NetshUi.Core;

/// <summary>netsh 输出里的一个按虚线列宽切分出来的表格。</summary>
public sealed class NetshTable
{
    public NetshTable(IReadOnlyList<string> headers, IReadOnlyList<string[]> rows)
    {
        Headers = headers;
        Rows = rows;
    }

    public IReadOnlyList<string> Headers { get; }

    public IReadOnlyList<string[]> Rows { get; }

    public int ColumnCount => Headers.Count;

    public string Cell(int rowIndex, int columnIndex) =>
        rowIndex >= 0 && rowIndex < Rows.Count && columnIndex >= 0 && columnIndex < Rows[rowIndex].Length
            ? Rows[rowIndex][columnIndex]
            : string.Empty;

    /// <summary>按表头关键字取单元格（表头做包含匹配，避免依赖具体语言）。</summary>
    public string Cell(int rowIndex, params string[] headerHints)
    {
        int index = FindColumn(headerHints);
        return index < 0 ? string.Empty : Cell(rowIndex, index);
    }

    public int FindColumn(params string[] headerHints)
    {
        foreach (var hint in headerHints)
        {
            var normalizedHint = Normalize(hint);
            for (int i = 0; i < Headers.Count; i++)
            {
                if (Normalize(Headers[i]).Contains(normalizedHint, StringComparison.Ordinal))
                    return i;
            }
        }

        return -1;
    }

    internal static string Normalize(string value) =>
        string.Join(' ', value.Trim().TrimEnd(':').Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
}

/// <summary>netsh 输出里以空行分隔的一段（标题 + 键值对 + 表格）。</summary>
public sealed class NetshBlock
{
    private readonly List<KeyValuePair<string, string>> _pairs = [];
    private readonly List<NetshTable> _tables = [];

    public string Title { get; set; } = string.Empty;

    public IReadOnlyList<KeyValuePair<string, string>> Pairs => _pairs;

    public IReadOnlyList<NetshTable> Tables => _tables;

    public NetshTable? FirstTable => _tables.Count > 0 ? _tables[0] : null;

    public bool IsEmpty => Title.Length == 0 && _pairs.Count == 0 && _tables.Count == 0;

    internal void AddPair(string key, string value) => _pairs.Add(new KeyValuePair<string, string>(key, value));

    internal void AppendToLastValue(string extra)
    {
        if (_pairs.Count == 0) return;
        var last = _pairs[^1];
        _pairs[^1] = new KeyValuePair<string, string>(last.Key, last.Value + "\n" + extra);
    }

    internal void AddTable(NetshTable table) => _tables.Add(table);

    /// <summary>按关键字取字段值；关键字忽略大小写与多余空格，做双向包含匹配。</summary>
    public string? Get(params string[] keyHints)
    {
        for (int i = 0; i < _pairs.Count; i++)
        {
            var key = NetshTable.Normalize(_pairs[i].Key);
            foreach (var hint in keyHints)
            {
                if (key.Length == 0) continue;
                if (key.Contains(NetshTable.Normalize(hint), StringComparison.Ordinal))
                    return _pairs[i].Value.Trim();
            }
        }

        return null;
    }

    public bool TryGet(out string value, params string[] keyHints)
    {
        var found = Get(keyHints);
        value = found ?? string.Empty;
        return found is not null;
    }

    public bool Flag(params string[] keyHints)
    {
        var value = Get(keyHints);
        if (value is null) return false;
        return value.StartsWith("yes", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("enable", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("on", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("是", StringComparison.Ordinal);
    }
}

/// <summary>
/// netsh 文本输出的通用解析器。
/// 说明行（"Configuration for interface ..."）、键值行（"State   OFF" / "DHCP enabled:  No"）、
/// 以及用虚线标出列宽的表格都能识别；不依赖具体界面语言。
/// </summary>
public static class NetshParser
{
    public static IReadOnlyList<NetshBlock> Parse(string text)
    {
        var lines = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var blocks = new List<NetshBlock>();
        NetshBlock current = new();

        void Flush()
        {
            if (current.IsEmpty) return;
            if (current.FirstTable is not null && current.Pairs.Count == 0 && current.Title.Length == 0)
            {
                // 纯表格块保留
            }

            blocks.Add(current);
            current = new NetshBlock();
        }

        for (int i = 0; i < lines.Length; i++)
        {
            var raw = lines[i];
            var line = raw.TrimEnd();

            if (line.Length == 0)
            {
                Flush();
                continue;
            }

            if (IsDashLine(line)) continue;

            // 表格：当前行是表头，下一行是虚线（至少两列）
            if (i + 1 < lines.Length && IsDashLine(lines[i + 1]))
            {
                var spans = DashSpans(lines[i + 1]);
                if (spans.Count >= 2)
                {
                    Flush();
                    var headers = SplitBySpans(line, spans);
                    var rows = new List<string[]>();
                    i += 2;
                    for (; i < lines.Length; i++)
                    {
                        var rowLine = lines[i];
                        if (rowLine.Trim().Length == 0) { i--; break; }
                        if (IsDashLine(rowLine)) continue;
                        if (i + 1 < lines.Length && IsDashLine(lines[i + 1])) { i--; break; }

                        var cells = SplitBySpans(rowLine, spans);
                        if (cells.All(c => c.Length == 0)) { i--; break; }
                        rows.Add(cells.ToArray());
                    }

                    current.AddTable(new NetshTable(headers.ToArray(), rows));
                    Flush();
                    continue;
                }
            }

            if (TryParsePair(line, out var key, out var value))
            {
                current.AddPair(key, value);
                continue;
            }

            // 续行：缩进较深且不像键值，说明是上一个键值的多行取值（例如多个 DNS 服务器）
            int indent = raw.Length - raw.TrimStart().Length;
            if (indent >= 4 && current.Pairs.Count > 0)
            {
                current.AppendToLastValue(line.Trim());
                continue;
            }

            if (current.Title.Length == 0) current.Title = line.Trim();
            else current.Title += " " + line.Trim();
        }

        Flush();
        return blocks;
    }

    /// <summary>只取第一张表。</summary>
    public static NetshTable? FirstTable(string text)
    {
        foreach (var block in Parse(text))
        {
            if (block.FirstTable is not null) return block.FirstTable;
        }

        return null;
    }

    /// <summary>把所有表格按出现顺序取出。</summary>
    public static IReadOnlyList<NetshTable> AllTables(string text)
    {
        var tables = new List<NetshTable>();
        foreach (var block in Parse(text)) tables.AddRange(block.Tables);
        return tables;
    }

    public static bool IsDashLine(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length < 3) return false;
        int dashes = 0;
        foreach (var c in trimmed)
        {
            if (c == '-') dashes++;
            else if (c != ' ' && c != '=') return false;
        }

        return dashes >= 3;
    }

    private static List<(int Start, int Length)> DashSpans(string dashLine)
    {
        var spans = new List<(int, int)>();
        int i = 0;
        while (i < dashLine.Length)
        {
            if (dashLine[i] != '-') { i++; continue; }
            int start = i;
            while (i < dashLine.Length && dashLine[i] == '-') i++;
            spans.Add((start, i - start));
        }

        return spans;
    }

    private static List<string> SplitBySpans(string line, List<(int Start, int Length)> spans)
    {
        var cells = new List<string>(spans.Count);
        foreach (var (start, length) in spans)
        {
            if (start >= line.Length)
            {
                cells.Add(string.Empty);
                continue;
            }

            int take = Math.Min(length, line.Length - start);
            cells.Add(line.Substring(start, take).Trim());
        }

        return cells;
    }

    /// <summary>识别 "标签: 值" 与 "标签      值"（两个以上空格对齐）两种写法。</summary>
    public static bool TryParsePair(string line, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;

        var trimmed = line.Trim();
        if (trimmed.Length == 0) return false;

        // 形如 "DHCP enabled:   No"
        int colon = trimmed.IndexOf(':');
        if (colon > 0 && trimmed.IndexOf(':', colon + 1) < 0)
        {
            var left = trimmed[..colon].Trim();
            if (left.Length > 0 && left.Length <= 60 && left.All(c => c != '：'))
            {
                key = left;
                value = trimmed[(colon + 1)..].Trim();
                return true;
            }
        }

        // 形如 "State                    OFF"
        int bestIndex = -1;
        int bestLength = 0;
        for (int i = 1; i < trimmed.Length - 1; i++)
        {
            if (trimmed[i] != ' ' || trimmed[i - 1] != ' ') continue;
            int runStart = i - 1;
            while (runStart > 0 && trimmed[runStart - 1] == ' ') runStart--;
            int runEnd = i;
            while (runEnd + 1 < trimmed.Length && trimmed[runEnd + 1] == ' ') runEnd++;
            int runLength = runEnd - runStart + 1;
            if (runLength < 2) continue;
            if (runStart == 0 || runEnd >= trimmed.Length - 1) continue;
            if (runLength > bestLength)
            {
                bestLength = runLength;
                bestIndex = runStart;
            }

            i = runEnd;
        }

        if (bestIndex <= 0 || bestLength < 2) return false;

        var candidateKey = trimmed[..bestIndex].Trim();
        var candidateValue = trimmed[(bestIndex + bestLength)..].Trim();
        if (candidateKey.Length == 0 || candidateKey.Length > 60) return false;
        if (candidateValue.Length == 0) return false;

        key = candidateKey;
        value = candidateValue;
        return true;
    }

    /// <summary>批量整理键值对为可读文本（用于只读展示）。</summary>
    public static string ToLines(NetshBlock block)
    {
        var sb = new StringBuilder();
        foreach (var pair in block.Pairs) sb.Append(pair.Key).Append(": ").Append(pair.Value).Append('\n');
        return sb.ToString().TrimEnd();
    }
}
