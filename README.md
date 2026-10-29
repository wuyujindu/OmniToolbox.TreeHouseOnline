# OmniToolbox.TreeHouseOnline

Omni Toolbox 自定义热键栏在线模块仓库。模块使用 **API 1**，需要 Omni **1.1.8.2 或更新版本**。

## 安装与更新

在“插件设置 → 在线模块”中填写仓库地址：

```text
https://github.com/wuyujindu/OmniToolbox.TreeHouseOnline
```

Omni 读取默认分支根目录的 `TreeHouseModules.json`，下载并校验模块。首次安装后默认关闭，在“树树妙妙屋 → 在线”中手动启用。

GitHub API 加载清单失败时，宿主会自动回退到 raw 地址，无需重复添加备用链接。仓库必须公开；无法确认仓库公开状态时，宿主会阻止本次加载并提示重试，保留文件与用户配置。

在线模块拥有与 Omni 相同的运行权限，请仅安装可信来源。更新时同时发布模块文件与清单，递增模块版本并重新计算 SHA256；只修改摘要不会触发已安装模块的版本更新。用户配置保留在宿主的 `TreeHouse` 存储中。

## 自定义热键栏

`CustomHotbar` 支持多组 12 格热键栏，可设置图标、悬浮说明、多行指令、布局与外观。

启用模块后，用以下指令切换指定热键栏的显示/隐藏。序号从 `1` 开始，也可填写完整名称（允许空格）：

```text
/omni customhotbar toggle 1
/omni customhotbar toggle 热键栏 1
/customhotbar toggle 1
```

`/omni customhotbar` 通过宿主的模块命令路由处理；独立的 `/customhotbar` 在模块启用时注册，停用时移除。`/omni toggle` 用于 Omni 自身的模块开关。

在模块设置中点击“导出当前热键栏”或“导出全部”，将 JSON 配置复制到剪贴板。点击“从剪贴板导入”会追加为新热键栏并选中第一组新栏；现有热键栏保留，同名时自动加上导入后缀。

导出包含图标、说明、指令、位置、布局和外观设置。导入只保存配置，点击热键栏图标时才执行其中的指令。每栏最多 12 格；无效内容会显示错误，不会部分导入。

2.0.0 保留此前发布的 `Bars` 及各热键栏、格子的 JSON 配置键，配置类型改为模块内嵌的 `Settings`。界面和命令说明通过 `ModuleLocalization.Create` 提供简体源文，繁体由宿主生成；其他语言需提供完整词库后才开放对应入口。

## 模块组织与维护

```text
OmniToolbox.TreeHouseOnline/
  Modules/
    CustomHotbar.cs
    OnlineIconExample.cs
  README.md
  TreeHouseModules.json
```

清单只发布 `CustomHotbar`；`OnlineIconExample.cs` 是与新版接口同步的教学示例。

一个仓库可包含多个模块，每个发布的 `.cs` 或 `.dll` 只能有一个可实例化模块。源码模式按文件独立编译，辅助类型和词条放在模块文件内；需要跨文件组织时使用 DLL。多个模块仍可共用一个维护帖子。

模块继承 `OmniModuleBase` 并声明 `[ModuleApiVersion(1)]`；事件在 `OnStart(FeatureLifetime activation)` 中接入，并通过 `activation.OnStopping` 登记解绑。不要打包宿主提供的 Common、TreeTools、TreeToolKit 等公共依赖。完整契约和示例见[上游 TreeHouseOnline README](https://github.com/YouShux/OmniToolbox.TreeHouseOnline/blob/main/README.md)。

发布前使用当前宿主依赖编译、自测，检查启停、命令、配置读写与导入导出。发布后继续跟进宿主接口变化和模块反馈。

## 编译与自测

需要 .NET 10 SDK，以及已安装的当前 Omni 和 Dalamud 依赖。从仓库根目录执行，替换两个宿主路径：

```powershell
$omniPath = '<Omni 插件版本目录>'
$dalamudPath = '<Dalamud addon\Hooks\dev 目录>'
rtk proxy dotnet build Tools/Validation/Validation.csproj -c Release -p:ModuleName=CustomHotbar "-p:OmniPath=$omniPath" "-p:DalamudPath=$dalamudPath"
rtk proxy dotnet build Tools/Validation/Validation.csproj -c Release -p:ModuleName=OnlineIconExample "-p:OmniPath=$omniPath" "-p:DalamudPath=$dalamudPath"
rtk proxy dotnet run --project Tools/Validation/Validation.csproj -c Release "-p:OmniPath=$omniPath" "-p:DalamudPath=$dalamudPath"
```

前两条命令按文件独立编译；最后一条运行宿主 API 契约、简繁词库、旧 JSON 配置、命令切换、导入导出边界、重置和独立命令清理测试。测试中的命令管理器使用替身，不向游戏发送指令；测试程序集不加入发布清单。

本次使用 Omni 1.1.8.7 / Dalamud 15.0.3.9 的真实程序集验证。游戏内的绘制、拖动、图标选择与宿主下载、重启后的配置恢复仍需在 Omni 中验收。

## 发布校验

最终模块文件的 SHA256 必须与清单一致，修改代码、编码、换行或重新编译后均需重新计算：

本仓库源码在 Git 中存储为 LF，Windows 工作副本可为 CRLF；清单摘要按 Git 发布的 LF 内容计算：

```powershell
rtk proxy python -c "from pathlib import Path; import hashlib; print(hashlib.sha256(Path('Modules/CustomHotbar.cs').read_bytes().replace(b'\r\n', b'\n')).hexdigest().upper())"
```

清单中的 `ApiVersion` 为 `1`，`MinimumOmniVersion` 为 `1.1.8.2`，`RepositoryURL` 必须与清单和模块文件所属仓库一致，`Localization.Source` 提供非空的 `Title`、`Description`。

上传后在 Omni 中刷新仓库，验证下载、加载及配置保存。Git 可能转换换行符，远端校验应以清单指向的原始文件字节为准。
