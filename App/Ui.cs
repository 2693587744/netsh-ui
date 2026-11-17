using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

namespace NetshUi.App;

/// <summary>页面里复用的布局小件，统一视觉与间距。</summary>
internal static class Ui
{
    public const int LabelWidth = 152;

    public static TextBlock Heading(string text) =>
        new TextBlock().Text(text).FontSize(ThemeFontSize.Medium).SemiBold();

    public static TextBlock Muted(string text) =>
        new TextBlock()
            .Text(text)
            .TextWrapping(TextWrapping.Wrap)
            .WithTheme((theme, element) => element.Foreground(theme.Palette.DisabledText));

    /// <summary>带标题的卡片容器。</summary>
    public static Border Card(string? title, FrameworkElement body, string? description = null)
    {
        var stack = new StackPanel().Vertical().Spacing(10);
        if (!string.IsNullOrEmpty(title)) stack.Children(Heading(title));
        if (!string.IsNullOrEmpty(description)) stack.Children(Muted(description!));
        stack.Children(body);

        return new Border()
            .BorderThickness(new Thickness(1))
            .CornerRadius(8)
            .Padding(14)
            .WithTheme((theme, border) =>
            {
                border.BorderBrush(theme.Palette.ControlBorder);
                border.Background(theme.Palette.ContainerBackground);
            })
            .Child(stack);
    }

    /// <summary>「标签 + 控件」一行。</summary>
    public static FrameworkElement Field(string label, FrameworkElement control) =>
        new Grid()
            .Columns($"{LabelWidth}, *")
            .Children(
                new TextBlock().Text(label).CenterVertical(),
                control.Column(1));

    public static StackPanel Form(params FrameworkElement[] rows) =>
        new StackPanel().Vertical().Spacing(9).Children(rows);

    public static StackPanel Row(params FrameworkElement[] children) =>
        new StackPanel().Horizontal().Spacing(8).Children(children);

    public static StackPanel Column(params FrameworkElement[] children) =>
        new StackPanel().Vertical().Spacing(14).Children(children);

    public static FrameworkElement Scrollable(FrameworkElement body) =>
        new ScrollViewer().AutoVerticalScroll().NoHorizontalScroll().Padding(20, 4, 20, 16).Content(body);

    public static TextBox Input(string placeholder, string value = "", double width = 240) =>
        new TextBox().Placeholder(placeholder).Text(value).Width(width);

    public static TextBox WideInput(string placeholder, string value = "") =>
        new TextBox().Placeholder(placeholder).Text(value).StretchHorizontal();

    public static ComboBox Choice(IEnumerable<string> items, int selectedIndex = 0, double width = 180) =>
        new ComboBox().Items(items.ToArray()).SelectedIndex(selectedIndex).Width(width);

    public static Button Primary(string text, Action onClick) =>
        new Button().Content(text).OnClick(onClick);

    public static Button Action(string text, Action onClick) =>
        new Button().Content(text).OnClick(onClick);

    public static MultiLineTextBox OutputBox()
    {
        var box = new MultiLineTextBox()
            .IsReadOnly(true)
            .Wrap(true)
            .FontSize(ThemeFontSize.Small)
            .StretchHorizontal()
            .StretchVertical();
        box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        return box;
    }

    /// <summary>命令输出页：整页铺满的 netsh 输出（放在独立标签页里）。</summary>
    public static FrameworkElement OutputPage(MultiLineTextBox box, Action onClear) =>
        new Grid().Rows("Auto, *").Children(
            new DockPanel().Spacing(10).Padding(20, 10, 20, 8).Children(
                new Button().Content("清空").OnClick(onClear).DockRight(),
                Muted("netsh 命令输出").CenterVertical()),
            new Border().Padding(20, 0, 20, 16).Child(box).Row(1));

    /// <summary>页面骨架：内容区（标题由窗口标题栏承担，页内不再重复）。</summary>
    public static FrameworkElement Shell(FrameworkElement body, bool scrollBody) =>
        scrollBody ? Scrollable(body) : body;

}
