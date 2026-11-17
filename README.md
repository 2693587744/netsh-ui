# NetshUi

一个轻量的 Windows 桌面小工具，用界面管理 `netsh interface portproxy` 的端口转发规则，不用再手敲命令、也不用记参数顺序。

基于 [MewUI](https://www.nuget.org/packages/Aprillz.MewUI.Windows)（Direct2D 渲染）编写，可编译为 Native AOT 单文件程序。

## 功能

- 列表展示系统里所有 portproxy 转发（`netsh interface portproxy show all`）
- 新增 / 编辑 / 删除 `v4tov4` 转发，编辑时若侦听地址或端口变了会自动先删旧条目，避免出现两条
- 每行可写**备注**，portproxy 本身不支持备注，这里存在本机 JSON 文件里，不写进系统
- 按端口、地址或备注过滤
- 双击整行等同点「编辑」
- 非管理员启动时，顶部显示状态并可一键「以管理员身份重启」
- 每次执行的真实 netsh 命令和输出都会回显在底部「命令输出」区，方便核对

## 环境要求

- Windows 10 及以上
- 开发构建需要 [.NET 10 SDK](https://dotnet.microsoft.com/download)
- 用发布出来的 AOT 单文件程序时，目标机器**不需要**安装 .NET 运行时

## 运行

```bash
dotnet run
```

新增 / 删除转发需要管理员权限，普通权限启动时用界面顶部的「以管理员身份重启」即可。

## 发布

框架依赖发布：

```bash
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```

Native AOT 单文件发布（不需要 .NET 运行时）：

```bash
dotnet publish -c Release -r win-x64 -p:PublishAot=true -o publish
```

产物里的 `NetshUi.pdb` 是调试符号，不需要调试时可以直接删掉。

## 备注文件位置

备注按 `方向|侦听地址|侦听端口` 作为键存储（例如 `v4tov4|0.0.0.0|58088`），读取顺序：

1. 程序所在目录的 `portproxy-notes.json`（把整个目录拷到别的机器，备注跟着走）
2. `%APPDATA%\NetshUi\portproxy-notes.json`（上面那份不存在时用这个）

读到哪一份就写回哪一份。转发被删除、或系统里已经不存在时，对应备注会被自动清理。

## 项目结构

```
Program.cs              入口：注册渲染后端、创建主窗口
App/
  MainView.cs           主界面骨架：顶部状态栏 + 页面
  PageBase.cs           页面基类：netsh 调用与输出回显
  Ui.cs                 复用的布局小件与页面骨架
  Dialogs.cs            新增 / 编辑 / 备注三个模态小窗
Pages/
  PortProxyPage.cs      端口转发页：列表、筛选、增删改、备注
Core/
  NetshRunner.cs        调用 netsh，处理 GBK / UTF-8 输出编码
  NetshParser.cs        解析 netsh 的表格输出（按列位置，不依赖语言）
  NoteStore.cs          备注的本地存取
  Elevation.cs          管理员权限检测与提权重启
```

## 说明

- netsh 的输出会随系统语言本地化，解析一律按列位置而不是表头文字。
- 通过管道调用 netsh 时输出是 GBK，直接 `cmd /c` 调用是英文，所以输出按「先严格 UTF-8、失败回退 OEM 代码页」解码。
- 项目关闭了 NuGet 漏洞审计（`<NuGetAudit>false</NuGetAudit>`），因为它会让 restore 从 1.6 秒涨到 49 秒；需要检查漏洞时用 `dotnet restore -p:NuGetAudit=true`。
