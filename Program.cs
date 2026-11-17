using System.Text;

using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

using NetshUi.App;

// netsh 在非 UTF-8 代码页的机器上会输出 GBK 等编码，需要 CodePages 提供程序兜底。
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

Win32Platform.Register();
Direct2DBackend.Register();

var view = new MainView();

var window = new Window()
    .Padding(0)
    .Resizable(1180, 760, minWidth: 900, minHeight: 560)
    .StartCenterScreen()
    .Content(view)
    .OnLoaded(view.Start);

window.Title = "端口转发管理 · netsh portproxy";
window.Closed += () =>
{
    view.Flush();
    Application.Shutdown();
};

Application.Run(window);
