using System.Text.RegularExpressions;

using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

using NetshUi.App;
using NetshUi.Core;

namespace NetshUi.Pages;

/// <summary>
/// 端口转发管理：netsh interface portproxy 的查看 / 新增 / 编辑 / 删除。
/// 每行右侧有「备注 / 编辑 / 删除」三个入口，新增入口放在筛选行左侧。
/// </summary>
internal sealed class PortProxyPage : PageBase
{
    private static readonly Regex FamilyPattern = new("ipv[46]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly NoteStore _notes = NoteStore.Load();
    private readonly GridView _grid = new GridView().ZebraStriping(false).ShowGridLines(false);
    private readonly List<ProxyRow> _rows = [];
    // 右侧留出 22px 给清空按钮，文字不会压到它下面。
    private readonly TextBox _filter = Ui.Input("按端口、地址或备注过滤", "", 220).Padding(8, 4, 22, 4);
    private readonly Button _clearFilter = new Button().Content("✕").FontSize(ThemeFontSize.Small).MinWidth(0).Padding(4, 0, 4, 0);
    private readonly TextBlock _filterSummary = Ui.Muted(string.Empty).CenterVertical();

    public PortProxyPage()
    {
        var actionColumn = new GridViewColumn<ProxyRow>().Header("操作").Width(190);
        actionColumn.CellTemplate = new ActionCellTemplate(OpenNote, OpenEdit, Delete);

        _grid.Columns<ProxyRow>(
            new GridViewColumn<ProxyRow>().Header("类型").Width(90).Text(row => row.Kind),
            new GridViewColumn<ProxyRow>().Header("侦听地址").Width(130).Text(row => row.ListenAddress),
            new GridViewColumn<ProxyRow>().Header("侦听端口").Width(95).Text(row => row.ListenPort),
            new GridViewColumn<ProxyRow>().Header("目标地址").Width(155).Text(row => row.TargetAddress),
            new GridViewColumn<ProxyRow>().Header("目标端口").Width(95).Text(row => row.TargetPort),
            new GridViewColumn<ProxyRow>().Header("备注").StarWidth(1).Text(row => row.Note),
            actionColumn);

        // 双击整行等于点「编辑」
        _grid.ItemDoubleClicked += item =>
        {
            if (item is ProxyRow row) OpenEdit(row);
        };

        // 输入即筛选；有内容时框内右侧出现清空按钮（类似 Element 的 clearable）。
        _clearFilter.StyleName = BuiltInStyles.FlatButton;
        _clearFilter.ToolTip("清空过滤条件");
        _clearFilter.IsVisible = false;
        _clearFilter.OnClick(() => _filter.Text = string.Empty);

        _filter.TextChanged += _ =>
        {
            _clearFilter.IsVisible = _filter.Text.Length > 0;
            ApplyFilter();
        };

        _filter.KeyDown += e =>
        {
            if (e.Key != Key.Enter) return;
            ApplyFilter();
            e.Handled = true;
        };
    }

    protected override bool ScrollBody => false;

    protected override FrameworkElement BuildBody()
    {
        // 过滤框 + 叠在右侧的清空按钮
        var filterArea = new Grid().Children(
            _filter.StretchHorizontal(),
            _clearFilter.Right().CenterVertical().Margin(0, 0, 4, 0));

        var left = new StackPanel().Horizontal().Spacing(10).CenterVertical().Children(
            Ui.Action("添加转发", OpenAdd),
            filterArea,
            _filterSummary);

        var right = new StackPanel().Horizontal().Spacing(10).CenterVertical();
        if (!Elevation.IsElevated)
        {
            var elevate = Ui.Action("以管理员身份重启", () =>
            {
                if (Elevation.RestartElevated()) Application.Shutdown();
            });
            elevate.ToolTip("新增 / 修改 / 删除转发都需要管理员权限");
            right.Children(Ui.Muted("需管理员权限").CenterVertical(), elevate.CenterVertical());
        }

        right.Children(Ui.Action("刷新", () => _ = RefreshAsync()).CenterVertical());

        var bar = new DockPanel().Spacing(10);
        bar.LastChildFill = false;
        bar.Children(left.DockLeft(), right.DockRight());

        return new Grid().Rows("Auto, *").Children(
            new Border().Padding(20, 14, 20, 8).Child(bar),
            new Border().Padding(20, 0, 20, 14).Child(_grid).Row(1));
    }

    public override void Activate() => _ = RefreshAsync();

    /// <summary>关窗时把还没落盘的备注立即写掉，避免刚打完字就关掉丢内容。</summary>
    public void FlushNotes() => _notes.Save();

    private async Task RefreshAsync(string? preserveKey = null)
    {
        var result = await RunAsync("interface", "portproxy", "show", "all");
        if (result is null) return;

        _rows.Clear();
        foreach (var row in ParseShowAll(result.StdOut))
        {
            row.Note = _notes.Get(NoteStore.MakeKey(row.Kind, row.ListenAddress, row.ListenPort));
            _rows.Add(row);
        }

        // 系统里已经不存在的转发，把它的备注一起清掉，避免下次同名端口串到旧备注。
        if (result.Ok)
        {
            _notes.Prune(_rows.Select(row => NoteStore.MakeKey(row.Kind, row.ListenAddress, row.ListenPort)));
        }

        ApplyFilter(preserveKey);
    }

    private void ApplyFilter(string? preserveKey = null)
    {
        var filter = _filter.Text?.Trim() ?? string.Empty;
        var view = filter.Length == 0
            ? _rows.ToList()
            : _rows.Where(row =>
                row.ListenPort.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || row.ListenAddress.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || row.TargetAddress.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || row.TargetPort.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || row.Note.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

        _grid.ItemsSource(view);
        _filterSummary.Text = filter.Length == 0 ? $"共 {view.Count} 条" : $"筛选出 {view.Count} 条";

        if (preserveKey is null) return;

        int index = view.FindIndex(row =>
            string.Equals(
                NoteStore.MakeKey(row.Kind, row.ListenAddress, row.ListenPort),
                preserveKey,
                StringComparison.OrdinalIgnoreCase));

        if (index >= 0) _grid.SelectedIndex = index;
    }

    // ---- 行内操作 ----

    private void OpenAdd()
    {
        var dialog = new ForwardDialog();
        dialog.ShowDialog();
        if (dialog.Result is { } input) _ = AddAsync(input);
    }

    private void OpenEdit(ProxyRow row)
    {
        var dialog = new ForwardDialog(new ForwardInput(
            row.ListenAddress, row.ListenPort, row.TargetAddress, row.TargetPort, row.Note));

        dialog.ShowDialog();
        if (dialog.Result is { } input) _ = SaveEditAsync(row, input);
    }

    private void OpenNote(ProxyRow row)
    {
        var dialog = new NoteDialog(row.ListenPort, $"{row.TargetAddress}:{row.TargetPort}", row.Note);
        dialog.ShowDialog();
        if (dialog.Result is not { } note) return;

        var key = NoteStore.MakeKey(row.Kind, row.ListenAddress, row.ListenPort);
        _notes.Set(key, note);
        row.Note = note;

        // 当前还在按钮的点击回调里，等这一轮输入处理结束再重建列表，避免回收正在用的单元格。
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) ApplyFilter(key);
        else dispatcher.BeginInvoke(() => ApplyFilter(key));
    }

    private void Delete(ProxyRow row)
    {
        _ = DeleteAsync(row);
    }

    // ---- 实际操作 ----

    private async Task AddAsync(ForwardInput input)
    {
        await RunAsync("interface", "portproxy", "add", "v4tov4",
            "listenaddress=" + input.ListenAddress,
            "listenport=" + input.ListenPort,
            "connectaddress=" + input.TargetAddress,
            "connectport=" + input.TargetPort);

        var key = NoteStore.MakeKey("v4tov4", input.ListenAddress, input.ListenPort);
        _notes.Set(key, input.Note);

        await RefreshAsync(key);
    }

    private async Task SaveEditAsync(ProxyRow original, ForwardInput input)
    {
        var oldKey = NoteStore.MakeKey(original.Kind, original.ListenAddress, original.ListenPort);
        var newKey = NoteStore.MakeKey("v4tov4", input.ListenAddress, input.ListenPort);

        // 侦听地址/端口变了，旧的转发条目必须先删掉，否则会同时存在两条。
        bool listenChanged = !string.Equals(original.ListenAddress, input.ListenAddress, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(original.ListenPort, input.ListenPort, StringComparison.Ordinal);

        if (listenChanged)
        {
            await RunAsync("interface", "portproxy", "delete", original.Kind,
                "listenaddress=" + original.ListenAddress,
                "listenport=" + original.ListenPort);
        }

        await RunAsync("interface", "portproxy", "add", "v4tov4",
            "listenaddress=" + input.ListenAddress,
            "listenport=" + input.ListenPort,
            "connectaddress=" + input.TargetAddress,
            "connectport=" + input.TargetPort);

        if (!string.Equals(oldKey, newKey, StringComparison.OrdinalIgnoreCase)) _notes.Remove(oldKey);
        _notes.Set(newKey, input.Note);

        await RefreshAsync(newKey);
    }

    private async Task DeleteAsync(ProxyRow row)
    {
        if (!Confirm($"删除转发 {row.ListenAddress}:{row.ListenPort} → {row.TargetAddress}:{row.TargetPort}？"))
            return;

        await RunAsync("interface", "portproxy", "delete", row.Kind,
            "listenaddress=" + row.ListenAddress,
            "listenport=" + row.ListenPort);

        _notes.Remove(NoteStore.MakeKey(row.Kind, row.ListenAddress, row.ListenPort));
        Activate();
    }

    /// <summary>
    /// 解析 netsh interface portproxy show all：
    /// 输出按「侦听 ipv4: 连接到 ipv4:」分段，每段一张四列表（地址/端口/地址/端口）。
    /// </summary>
    internal static List<ProxyRow> ParseShowAll(string text)
    {
        var rows = new List<ProxyRow>();
        var kind = "v4tov4";

        foreach (var block in NetshParser.Parse(text))
        {
            if (block.FirstTable is { } table)
            {
                for (int i = 0; i < table.Rows.Count; i++)
                {
                    var listenPort = table.Cell(i, 1);
                    if (listenPort.Length == 0) continue;

                    rows.Add(new ProxyRow(
                        kind,
                        table.Cell(i, 0),
                        listenPort,
                        table.Cell(i, 2),
                        table.Cell(i, 3)));
                }

                continue;
            }

            // 方向标题行，例如「侦听 ipv4:   连接到 ipv6:」
            var label = block.Title + " " + string.Join(' ', block.Pairs.Select(pair => pair.Key + " " + pair.Value));
            var families = FamilyPattern.Matches(label);
            if (families.Count >= 2)
            {
                kind = $"v{families[0].Value[^1]}tov{families[1].Value[^1]}";
            }
        }

        return rows;
    }

    internal sealed class ProxyRow
    {
        public ProxyRow(string kind, string listenAddress, string listenPort, string targetAddress, string targetPort)
        {
            Kind = kind;
            ListenAddress = listenAddress;
            ListenPort = listenPort;
            TargetAddress = targetAddress;
            TargetPort = targetPort;
        }

        public string Kind { get; }

        public string ListenAddress { get; }

        public string ListenPort { get; }

        public string TargetAddress { get; }

        public string TargetPort { get; }

        public string Note { get; set; } = string.Empty;
    }

    /// <summary>操作列的单元格：三个小按钮，按钮点击时从 Tag 取当前行。</summary>
    private sealed class ActionCellTemplate : IDataTemplate<ProxyRow>
    {
        private readonly Action<ProxyRow> _note;
        private readonly Action<ProxyRow> _edit;
        private readonly Action<ProxyRow> _delete;

        public ActionCellTemplate(Action<ProxyRow> note, Action<ProxyRow> edit, Action<ProxyRow> delete)
        {
            _note = note;
            _edit = edit;
            _delete = delete;
        }

        public FrameworkElement Build(TemplateContext context)
        {
            var noteButton = SmallButton("备注");
            var editButton = SmallButton("编辑");
            var deleteButton = SmallButton("删除");

            var panel = new StackPanel().Horizontal().Spacing(4).CenterVertical()
                .Children(noteButton, editButton, deleteButton);

            noteButton.OnClick(() => Invoke(panel, _note));
            editButton.OnClick(() => Invoke(panel, _edit));
            deleteButton.OnClick(() => Invoke(panel, _delete));

            return panel;
        }

        public void Bind(FrameworkElement view, ProxyRow item, int index, TemplateContext context)
        {
            view.Tag = item;
        }

        void IDataTemplate.Bind(FrameworkElement view, object? item, int index, TemplateContext context)
        {
            if (item is ProxyRow row) Bind(view, row, index, context);
        }

        void IDataTemplate.Unbind(FrameworkElement view, object? item, int index, TemplateContext context)
        {
        }

        private static Button SmallButton(string text) =>
            new Button()
                .Content(text)
                .FontSize(ThemeFontSize.Small)
                .MinWidth(0)
                .Padding(new Thickness(8, 2, 8, 2));

        private static void Invoke(FrameworkElement panel, Action<ProxyRow> action)
        {
            if (panel.Tag is ProxyRow row) action(row);
        }
    }
}
