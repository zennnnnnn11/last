<div align="center">

<img src="assets/icon.svg" width="76" height="76" alt="last logo" />

# last

现代、轻量、极简的英雄联盟大乱斗助手

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C# 14](https://img.shields.io/badge/C%23-14-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![Avalonia 12](https://img.shields.io/badge/Avalonia-12.1-8b5cf6)](https://avaloniaui.net/)
[![Design](https://img.shields.io/badge/Design-Linear%20Style-black?logo=linear&logoColor=white)](https://linear.app/style-guide)
[![Native AOT](https://img.shields.io/badge/AOT-win--x64-blue)](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)

[下载安装包](https://github.com/zennnnnnn11/last/releases)

</div>

---

### 界面预览

<p align="center">
  <img src="assets/preview-main.png?v=4" alt="主界面" width="58%" />
</p>
<p align="center">
  <img src="assets/preview-detail.png?v=4" alt="对局详情" width="100%" />
</p>

### 核心特性

- **自动接受对局**：匹配成功后自动确认接受。
- **对局自动秒开**：结算返回房间后自动开启下一局匹配。
- **选人自动置顶**：进入英雄选择界面时窗口自动弹出并置顶。
- **队友战绩查询**：对局就绪后展示队友近期战绩、常用英雄与胜率。
- **大乱斗长凳换人**：极地大乱斗模式下一键交换长凳英雄。
- **对局战况详情**：赛后查看 KDA、MVP/SVP、输出伤害、经济与出装数据。
- **托盘静默运行**：关闭窗口收起至系统托盘，随时可呼出。

### 安装与运行

前往 [Releases](https://github.com/zennnnnnn11/last/releases) 页面获取程序：

- **安装引导版**：下载 `last-setup-x64.exe` 安装，自动创建桌面与开始菜单快捷方式。
- **绿色便携版**：下载 `last-win-x64-portable.zip` 解压即用。

#### 本地构建

```powershell
git clone https://github.com/zennnnnnn11/last.git
cd last
pwsh tools/build_dist.ps1
```
