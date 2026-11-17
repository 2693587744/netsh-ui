using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace NetshUi.Core;

/// <summary>一次 netsh 调用的结果。</summary>
public sealed record NetshResult(string Arguments, string StdOut, string StdErr, int ExitCode, TimeSpan Elapsed)
{
    public bool Ok => ExitCode == 0;

    /// <summary>命令回显（可直接粘到 cmd 里重放）。</summary>
    public string CommandLine => "netsh " + Arguments;

    public string Output
    {
        get
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(StdOut)) sb.Append(StdOut.TrimEnd());
            if (!string.IsNullOrWhiteSpace(StdErr))
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(StdErr.TrimEnd());
            }
            if (sb.Length == 0) sb.Append("(无输出)");
            return sb.ToString();
        }
    }

    public string Summary =>
        (Ok ? "[成功] " : $"[失败 {ExitCode}] ") + CommandLine;
}

/// <summary>执行 netsh 并把每次调用记录到 <see cref="CommandLog"/>。</summary>
public static class Netsh
{
    // 严格 UTF-8：非法字节会抛异常，从而回退到系统 OEM 代码页。
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static Task<NetshResult> RunAsync(params string[] args) => RunAsync(args, CancellationToken.None);

    public static async Task<NetshResult> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("netsh")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in args) startInfo.ArgumentList.Add(arg);

        var stopwatch = Stopwatch.StartNew();
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("无法启动 netsh.exe");

        var stdoutTask = ReadAllAsync(process.StandardOutput.BaseStream, cancellationToken);
        var stderrTask = ReadAllAsync(process.StandardError.BaseStream, cancellationToken);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var result = new NetshResult(
            string.Join(' ', args),
            Decode(await stdoutTask.ConfigureAwait(false)),
            Decode(await stderrTask.ConfigureAwait(false)),
            process.ExitCode,
            stopwatch.Elapsed);

        CommandLog.Add(result);
        return result;
    }

    private static async Task<byte[]> ReadAllAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    /// <summary>
    /// netsh 的输出编码跟随控制台代码页：UTF-8 环境下是 UTF-8，简体中文默认环境下是 GBK。
    /// 先按严格 UTF-8 解码，失败则回退到 OEM 代码页。
    /// </summary>
    public static string Decode(byte[] bytes)
    {
        if (bytes.Length == 0) return string.Empty;
        try
        {
            return Normalize(StrictUtf8.GetString(bytes));
        }
        catch (DecoderFallbackException)
        {
            // 落到下面的 OEM 代码页
        }

        try
        {
            return Normalize(Encoding.GetEncoding((int)GetOEMCP()).GetString(bytes));
        }
        catch (Exception)
        {
            return Normalize(Encoding.Latin1.GetString(bytes));
        }
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');

    [DllImport("kernel32.dll")]
    private static extern uint GetOEMCP();
}

/// <summary>全局命令历史，供「命令控制台」页显示。</summary>
public static class CommandLog
{
    private static readonly object Gate = new();
    private static readonly List<NetshResult> Entries = [];

    /// <summary>每次命令执行完成后触发（可能在 UI 线程，也可能在后台线程）。</summary>
    public static event Action<NetshResult>? Added;

    public static int Count
    {
        get { lock (Gate) return Entries.Count; }
    }

    public static void Add(NetshResult result)
    {
        lock (Gate)
        {
            Entries.Add(result);
            if (Entries.Count > 500) Entries.RemoveAt(0);
        }

        Added?.Invoke(result);
    }

    public static IReadOnlyList<NetshResult> Snapshot()
    {
        lock (Gate) return Entries.ToArray();
    }

    public static void Clear()
    {
        lock (Gate) Entries.Clear();
    }
}
