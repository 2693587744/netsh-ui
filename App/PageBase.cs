using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

using NetshUi.Core;

namespace NetshUi.App;

/// <summary>所有功能页的基类：统一骨架 + netsh 调用与输出回显。</summary>
internal abstract class PageBase : UserControl
{
    protected PageBase()
    {
        Output = Ui.OutputBox();
    }

    protected MultiLineTextBox Output { get; }

    /// <summary>命令输出的控件，供主界面放进「命令行输出」标签页。</summary>
    public MultiLineTextBox OutputBox => Output;

    /// <summary>清空命令输出。</summary>
    public void ClearOutput() => Output.Text = string.Empty;

    /// <summary>内容区是否放进 ScrollViewer（表格页通常关掉，让表格自己占满）。</summary>
    protected virtual bool ScrollBody => true;

    protected abstract FrameworkElement BuildBody();

    protected override Element? OnBuild() => Ui.Shell(BuildBody(), ScrollBody);

    /// <summary>页面被选中或需要刷新时调用。</summary>
    public virtual void Activate()
    {
    }

    protected void Reload() => Activate();

    /// <summary>往输出框追加一段文本（自动裁剪过长内容）。</summary>
    protected void Append(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var normalized = text!.Replace("\r\n", "\n").TrimEnd();
        var current = Output.Text ?? string.Empty;
        var next = current.Length == 0 ? normalized : current + "\n" + normalized;
        if (next.Length > 60_000) next = next[^40_000..];
        Output.Text = next;
    }

    /// <summary>执行一条 netsh 命令（参数自动按参数列表传递，路径含空格也安全），并回显输出。</summary>
    protected async Task<NetshResult?> RunAsync(params string[] args)
    {
        Append("> netsh " + string.Join(' ', args));
        try
        {
            var result = await Netsh.RunAsync(args);
            Append(result.Output);
            return result;
        }
        catch (Exception ex)
        {
            Append("执行失败：" + ex.Message);
            return null;
        }
    }

    /// <summary>执行会改动系统的命令，先确认，成功后刷新页面。</summary>
    protected async Task RunConfirmedAsync(string question, params string[] args)
    {
        if (!MessageBox.Confirm(question)) return;

        var result = await RunAsync(args);
        if (result is { Ok: true }) Activate();
    }

    protected bool Confirm(string question) => MessageBox.Confirm(question);

    protected void Notify(string message, string? detail = null) => MessageBox.Notify(message, detail: detail);

    /// <summary>参数是否为空，用于表单校验。</summary>
    protected bool Require(string? value, string fieldName)
    {
        if (!string.IsNullOrWhiteSpace(value)) return true;
        Notify("请填写「" + fieldName + "」。");
        return false;
    }
}
