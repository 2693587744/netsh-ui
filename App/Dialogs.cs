using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

namespace NetshUi.App;

/// <summary>新增 / 编辑转发时收集的字段。</summary>
internal sealed record ForwardInput(
    string ListenAddress,
    string ListenPort,
    string TargetAddress,
    string TargetPort,
    string Note);

/// <summary>新增或编辑一条端口转发的模态小窗口。</summary>
internal sealed class ForwardDialog : Window
{
    private readonly TextBox _listenAddress = Ui.Input("侦听地址", "0.0.0.0", 200);
    private readonly TextBox _listenPort = Ui.Input("如 58088", "", 200);
    private readonly TextBox _targetAddress = Ui.Input("目标地址", "", 200);
    private readonly TextBox _targetPort = Ui.Input("如 58088", "", 200);
    private readonly TextBox _note = Ui.Input("这条转发是干什么用的，可留空", "", 200);

    public ForwardDialog(ForwardInput? initial = null)
    {
        Title = initial is null ? "新增转发" : "编辑转发";
        Padding = new Thickness(18);
        StartupLocation = WindowStartupLocation.CenterOwner;
        WindowSize = WindowSize.FitContentSize(430, 400);

        if (initial is not null)
        {
            _listenAddress.Text = initial.ListenAddress;
            _listenPort.Text = initial.ListenPort;
            _targetAddress.Text = initial.TargetAddress;
            _targetPort.Text = initial.TargetPort;
            _note.Text = initial.Note;
        }

        Content = new StackPanel().Vertical().Spacing(10).Children(
            Ui.Field("侦听地址", _listenAddress),
            Ui.Field("侦听端口", _listenPort),
            Ui.Field("目标地址", _targetAddress),
            Ui.Field("目标端口", _targetPort),
            Ui.Field("备注", _note),
            new StackPanel().Horizontal().Spacing(8).Children(
                new Button().Content("保存").MinWidth(80).OnClick(Accept),
                new Button().Content("取消").MinWidth(80).OnClick(Close)));
    }

    /// <summary>点「保存」后的结果；用户取消或直接关窗时为 null。</summary>
    public ForwardInput? Result { get; private set; }

    private void Accept()
    {
        if (!Check(_listenPort.Text, "侦听端口")) return;
        if (!Check(_targetAddress.Text, "目标地址")) return;
        if (!Check(_targetPort.Text, "目标端口")) return;

        Result = new ForwardInput(
            string.IsNullOrWhiteSpace(_listenAddress.Text) ? "0.0.0.0" : _listenAddress.Text!.Trim(),
            _listenPort.Text!.Trim(),
            _targetAddress.Text!.Trim(),
            _targetPort.Text!.Trim(),
            _note.Text?.Trim() ?? string.Empty);

        Close();
    }

    private bool Check(string? value, string field)
    {
        if (!string.IsNullOrWhiteSpace(value)) return true;
        MessageBox.Notify($"请填写「{field}」。", owner: this);
        return false;
    }
}

/// <summary>编辑某条转发备注的模态小窗口。</summary>
internal sealed class NoteDialog : Window
{
    private readonly TextBox _note = Ui.Input("这条转发是干什么用的", "", 320);

    public NoteDialog(string listenPort, string target, string current)
    {
        Title = $"备注 · 侦听端口 {listenPort}";
        Padding = new Thickness(18);
        StartupLocation = WindowStartupLocation.CenterOwner;
        WindowSize = WindowSize.FitContentSize(420, 230);

        _note.Text = current;

        Content = new StackPanel().Vertical().Spacing(10).Children(
            Ui.Muted($"转发到 {target}"),
            _note,
            new StackPanel().Horizontal().Spacing(8).Children(
                new Button().Content("保存").MinWidth(80).OnClick(Accept),
                new Button().Content("清空").MinWidth(80).OnClick(() =>
                {
                    _note.Text = string.Empty;
                    Accept();
                }),
                new Button().Content("取消").MinWidth(80).OnClick(Close)));
    }

    /// <summary>保存后的备注；用户取消时为 null。</summary>
    public string? Result { get; private set; }

    private void Accept()
    {
        Result = _note.Text?.Trim() ?? string.Empty;
        Close();
    }
}
