using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

using NetshUi.Pages;

namespace NetshUi.App;

/// <summary>主界面：端口转发页 + 命令行输出页，用标签页切换。</summary>
internal sealed class MainView : UserControl
{
    private readonly PortProxyPage _page = new();
    private readonly TabControl _tabs = new();

    public void Start() => _page.Activate();

    /// <summary>关窗前收尾（把备注写盘）。</summary>
    public void Flush() => _page.FlushNotes();

    protected override Element? OnBuild()
    {
        _tabs
            .Tab("端口转发", _page)
            .Tab("命令行输出", Ui.OutputPage(_page.OutputBox, _page.ClearOutput))
            .StretchHorizontal()
            .StretchVertical();

        return _tabs;
    }
}
