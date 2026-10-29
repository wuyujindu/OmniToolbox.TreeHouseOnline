using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Lumina.Excel;
using OmniToolbox.Commands;
using OmniToolbox.Common.Module.Abstractions;
using OmniToolbox.Common.Module.Enums;
using OmniToolbox.Common.Module.Models;
using OmniToolbox.Host;
using OmniToolbox.UI;
using OmniToolbox.UI.Controls;
using OmniToolbox.UI.Theme;
using TreeTools.Lifecycle;
using OmenTools;
using OmenTools.Interop.Game.Lumina;
using OmenTools.OmenService;
using LuminaAction = Lumina.Excel.Sheets.Action;
using LuminaCompanion = Lumina.Excel.Sheets.Companion;
using LuminaEmote = Lumina.Excel.Sheets.Emote;
using LuminaGeneralAction = Lumina.Excel.Sheets.GeneralAction;
using LuminaItem = Lumina.Excel.Sheets.Item;
using LuminaMount = Lumina.Excel.Sheets.Mount;

namespace OmniToolbox.TreeHouseOnline;

[ModuleApiVersion(1)]
public sealed class CustomHotbar : OmniModuleBase
{
    public override ModuleInfo Info { get; } = new()
    {
        Category = ModuleCategory.Interface,
        Author = "WYJD",
        SupportUrls = ["https://github.com/wuyujindu"],
        Commands = [new("ToggleBar", OmniCommandCatalog.FormatModule(nameof(CustomHotbar), "toggle 1"))]
    };

    public override ModuleLocalization Localization { get; } = ModuleLocalization.Create(
        new Dictionary<string, string>
        {
            ["Title"] = "自定义热键栏",
            ["Description"] = "在屏幕上放置一组可自定义图标、悬浮说明与指令的热键栏, 点击图标即可执行对应的游戏或插件指令",
            ["ToggleBar"] = "切换指定热键栏的显示/隐藏（名称或序号）",
            ["StandaloneHelp"] = "切换热键栏显示/隐藏: /customhotbar toggle <名称或序号>",
            ["CommandUsage"] = "[自定义热键栏] 用法: {0} <名称或序号>",
            ["BarNotFound"] = "[自定义热键栏] 未找到热键栏 \"{0}\", 用法: {1} <名称或序号>",
            ["RegisterCommandFailed"] = "无法注册 /customhotbar：该指令已被占用",
            ["UnregisterCommandFailed"] = "无法注销 /customhotbar 指令",
            ["DefaultBarName"] = "热键栏 {0}",
            ["ImportedBarName"] = "导入的热键栏",
            ["ImportedNameSuffix"] = "{0} (导入 {1})",
            ["ImportEmpty"] = "剪贴板为空或配置文本过长",
            ["ImportInvalidJson"] = "剪贴板不是有效的热键栏配置",
            ["ImportNoBars"] = "配置中没有热键栏",
            ["ImportInvalidBar"] = "配置含有无效热键栏、格子或数值（每栏最多 12 格）",
            ["Imported"] = "已追加导入 {0} 个热键栏",
            ["ImportFailed"] = "导入失败：{0}",
            ["RowColumn"] = "第{0}行 第{1}列",
            ["MultilinePreview"] = "{0} …(共 {1} 行)",
            ["IconId"] = "图标ID: {0}",
            ["ObjectNotFound"] = "未找到 \"{0}\", 名称需与游戏内完全一致",
            ["DraggingSlot"] = "拖动: 第 {0} 格",
            ["Action"] = "技能",
            ["Emote"] = "情感动作",
            ["Item"] = "物品",
            ["Mount"] = "坐骑",
            ["Companion"] = "宠物",
            ["GeneralAction"] = "通用技能",
            ["NoBars"] = "暂无热键栏, 点击\"新增热键栏\"创建",
            ["AddBar"] = "新增热键栏",
            ["RemoveBarHelp"] = "删除此热键栏 (不可恢复, 该热键栏的所有格子配置都会丢失)",
            ["ExportCurrent"] = "导出当前热键栏",
            ["ExportAll"] = "导出全部",
            ["Import"] = "从剪贴板导入",
            ["ImportHelp"] = "读取剪贴板中的热键栏配置并追加，同名热键栏自动重命名",
            ["Exported"] = "已复制热键栏配置到剪贴板",
            ["ExportFailed"] = "导出失败：配置过大或含有无效数值",
            ["Name"] = "名称",
            ["Visible"] = "显示",
            ["LockPosition"] = "锁定位置",
            ["LockPositionHelp"] = "锁定后无法拖动热键栏窗口, 防止误触移动",
            ["HideEmptySlots"] = "隐藏空格",
            ["HideEmptySlotsHelp"] = "不渲染没有图标的格子, 热键栏会自动收缩; 只配了指令没配图标的格子同样会被隐藏",
            ["Layout"] = "布局",
            ["LayoutHelp"] = "热键栏格子的排列方式, 与游戏内热键栏的 12 格布局一致",
            ["Scale"] = "缩放",
            ["Opacity"] = "不透明度",
            ["Position"] = "位置",
            ["Icon"] = "图标",
            ["Tooltip"] = "悬浮说明",
            ["Command"] = "执行指令",
            ["TooltipHint"] = "鼠标悬浮时显示的说明文本",
            ["CommandHint"] = "如 /ac 技能名 或 /p 文本",
            ["Edit"] = "编辑",
            ["EditCommandHelp"] = "展开多行编辑, 每行一条指令",
            ["CommandEditorHelp"] = "执行指令 (每行一条, 点击图标时按顺序执行)",
            ["Confirm"] = "确定",
            ["Cancel"] = "取消",
            ["OpenIconBrowser"] = "打开图标浏览器",
            ["PickObjectIconHelp"] = "按游戏内名称从技能/物品/坐骑等对象获取图标",
            ["ObjectIconHelp"] = "从游戏对象获取图标 (名称需与游戏内完全一致)",
            ["ObjectCategory"] = "类别",
            ["ObjectNameHint"] = "如 铁壁 / 惊讶 / 蒸馏水",
            ["ResolveObject"] = "解析",
            ["ObjectNameRequired"] = "请输入对象名称",
            ["ReorderSlotHelp"] = "按住拖动以调整格子位置"
        });

    public const int SlotCount = 12;
    private const string STANDALONE_COMMAND = "/customhotbar";
    private Settings config = new();
    private CustomHotbarPanel? panel;
    private CustomHotbarOverlay? overlay;

    public override bool HasSettings => true;

    public override bool DrawSettings()
    {
        panel ??= new CustomHotbarPanel(this);
        var changed = panel.Draw(config, OpenIconBrowser);
        if (changed)
        {
            NormalizeConfig();
        }

        return changed;
    }

    public override bool ResetSettings()
    {
        if (config.Bars.Count == 1)
        {
            var bar = config.Bars[0];
            if (bar.Name == Text("DefaultBarName", 1) && bar.Visible && !bar.HideEmptySlots &&
                bar.Position == new Vector2(400f, 300f) && bar.PositionRatio == new Vector2(-1f, -1f) &&
                bar.GlobalScale == 1f && bar.Opacity == 0.85f && !bar.Locked &&
                bar.Layout == CustomHotbarLayout.TwelveByOne && bar.Slots.Count == SlotCount &&
                bar.Slots.All(slot => slot.IconID == 0 && slot.Tooltip.Length == 0 && slot.Command.Length == 0))
            {
                return false;
            }
        }

        config.Bars = [new CustomHotbarBarConfig { Name = Text("DefaultBarName", 1) }];
        NormalizeConfig();
        return true;
    }

    protected override void OnStart(FeatureLifetime activation)
    {
        NormalizeConfig();
        RegisterCommands(activation);

        var created = new CustomHotbarOverlay(config);
        var windowManager = WindowManager.Instance();
        _ = windowManager.WindowSystem;
        activation.OnStopping(() => windowManager.PostDraw -= created.Draw);
        windowManager.PostDraw += created.Draw;
        overlay = created;
    }

    protected override void OnStop()
    {
        overlay = null;
        panel = null;
    }

    private void RegisterCommands(FeatureLifetime activation)
    {
        var commandManager = DalamudServices.CommandManager;
        var standalone = new CommandInfo(OnCommand)
        {
            HelpMessage = Text("StandaloneHelp")
        };
        activation.OnStopping(() =>
        {
            if (commandManager.Commands.TryGetValue(STANDALONE_COMMAND, out var current) &&
                ReferenceEquals(current, standalone) && !commandManager.RemoveHandler(STANDALONE_COMMAND))
            {
                throw new InvalidOperationException(Text("UnregisterCommandFailed"));
            }
        });
        if (!commandManager.AddHandler(STANDALONE_COMMAND, standalone))
        {
            throw new InvalidOperationException(Text("RegisterCommandFailed"));
        }
    }

    public override bool TryHandleCommand(string arguments)
    {
        var parts = arguments.Trim().Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !string.Equals(parts[0], "toggle", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return ToggleBarVisibility(parts.Length > 1 ? parts[1] : string.Empty);
    }

    private void OnCommand(string command, string args)
    {
        if (!TryHandleCommand(args))
        {
            DalamudServices.ChatGUI.Print(Text("CommandUsage", OmniCommandCatalog.FormatModule(nameof(CustomHotbar), "toggle")));
        }
    }

    private bool ToggleBarVisibility(string arguments)
    {
        var argument = arguments.Trim();
        if (argument.Length == 0)
        {
            DalamudServices.ChatGUI.Print(Text("CommandUsage", OmniCommandCatalog.FormatModule(nameof(CustomHotbar), "toggle")));
            return true;
        }

        CustomHotbarBarConfig? target = null;
        foreach (var bar in config.Bars)
        {
            if (string.Equals(bar.Name, argument, StringComparison.Ordinal))
            {
                target = bar;
                break;
            }
        }

        if (target == null && int.TryParse(argument, out var index) &&
            index >= 1 && index <= config.Bars.Count)
        {
            target = config.Bars[index - 1];
        }

        if (target == null)
        {
            DalamudServices.ChatGUI.Print(Text("BarNotFound", argument, OmniCommandCatalog.FormatModule(nameof(CustomHotbar), "toggle")));
            return true;
        }

        target.Visible = !target.Visible;
        return true;
    }

    internal bool NormalizeConfig()
    {
        var changed = false;
        config.Bars ??= [];

        for (var index = 0; index < config.Bars.Count; index++)
        {
            var bar = config.Bars[index];
            if (string.IsNullOrWhiteSpace(bar.Name))
            {
                bar.Name = Text("DefaultBarName", index + 1);
                changed = true;
            }

            changed |= NormalizeBar(bar);
        }

        return changed;
    }

    internal static bool NormalizeBar(CustomHotbarBarConfig bar)
    {
        var changed = false;

        var clampedScale = Math.Clamp(bar.GlobalScale <= 0f ? 1f : bar.GlobalScale, 0.25f, 3f);
        if (MathF.Abs(clampedScale - bar.GlobalScale) > 0.0001f)
        {
            bar.GlobalScale = clampedScale;
            changed = true;
        }

        var clampedOpacity = Math.Clamp(bar.Opacity <= 0f ? 0.85f : bar.Opacity, 0.05f, 1f);
        if (MathF.Abs(clampedOpacity - bar.Opacity) > 0.0001f)
        {
            bar.Opacity = clampedOpacity;
            changed = true;
        }

        if (!Enum.IsDefined(bar.Layout))
        {
            bar.Layout = CustomHotbarLayout.TwelveByOne;
            changed = true;
        }

        bar.Slots ??= [];
        while (bar.Slots.Count < SlotCount)
        {
            bar.Slots.Add(new());
            changed = true;
        }

        if (bar.Slots.Count > SlotCount)
        {
            bar.Slots.RemoveRange(SlotCount, bar.Slots.Count - SlotCount);
            changed = true;
        }

        foreach (var slot in bar.Slots)
        {
            slot.Tooltip = (slot.Tooltip ?? string.Empty).Trim();
            slot.Command = (slot.Command ?? string.Empty).Trim();
        }

        return changed;
    }

    internal static bool TryNormalizeCommand(string? value, out string command)
    {
        command = (value ?? string.Empty).Trim().TrimEnd(';').Trim();
        if (command.Length == 0)
        {
            return false;
        }

        if (command[0] == '／')
        {
            command = $"/{command[1..]}";
        }
        else if (command[0] == '＼')
        {
            command = $"\\{command[1..]}";
        }
        else if (command[0] is not ('/' or '\\'))
        {
            command = $"/{command}";
        }

        return true;
    }

    internal static void ExecuteCommand(string command)
    {
        foreach (var line in command.Split('\n'))
        {
            if (!TryNormalizeCommand(line, out var normalized))
            {
                continue;
            }

            if (!DalamudServices.CommandManager.ProcessCommand(normalized))
            {
                ChatManager.Instance().SendMessage(normalized);
            }
        }
    }
    internal string Text(string key, params object[] arguments)
    {
        var value = OmniLoc.Get(this, key);
        return arguments.Length == 0 ? value : string.Format(value, arguments);
    }

    [Serializable]
    internal sealed class Settings
    {
        public List<CustomHotbarBarConfig> Bars { get; set; } = [];
    }

}

public enum CustomHotbarLayout
{
    TwelveByOne,
    SixByTwo,
    FourByThree,
    ThreeByFour,
    TwoBySix,
    OneByTwelve
}

internal static class CustomHotbarLayoutExtensions
{
    public static int Columns(this CustomHotbarLayout layout) => layout switch
    {
        CustomHotbarLayout.TwelveByOne => 12,
        CustomHotbarLayout.SixByTwo => 6,
        CustomHotbarLayout.FourByThree => 4,
        CustomHotbarLayout.ThreeByFour => 3,
        CustomHotbarLayout.TwoBySix => 2,
        CustomHotbarLayout.OneByTwelve => 1,
        _ => 12
    };

    public static int Rows(this CustomHotbarLayout layout) => CustomHotbar.SlotCount / layout.Columns();

    public static string DisplayName(this CustomHotbarLayout layout) => $"{layout.Columns()} × {layout.Rows()}";
}

[Serializable]
public sealed class CustomHotbarSlot
{
    public uint IconID { get; set; }
    public string Tooltip { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
}

[Serializable]
public sealed class CustomHotbarBarConfig
{
    public const float DefaultSlotSize = 40f;
    public const float DefaultSlotSpacing = 4f;

    public string Name { get; set; } = string.Empty;
    public bool Visible { get; set; } = true;
    public bool HideEmptySlots { get; set; }
    public Vector2 Position { get; set; } = new(400f, 300f);
    public Vector2 PositionRatio { get; set; } = new(-1f, -1f);
    public float GlobalScale { get; set; } = 1f;
    public float Opacity { get; set; } = 0.85f;
    public bool Locked { get; set; }
    public CustomHotbarLayout Layout { get; set; } = CustomHotbarLayout.TwelveByOne;
    public List<CustomHotbarSlot> Slots { get; set; } = [];

    public float EffectiveScale => MathF.Max(0.01f, GlobalScale);
    public float ScaleValue => OmniTheme.ScaleValue * EffectiveScale;
    public float Scale(float value) => value * ScaleValue;
    public Vector2 Scale(Vector2 value) => value * ScaleValue;
}

internal static class CustomHotbarSharing
{
    private const int MAX_CLIPBOARD_LENGTH = 4 * 1024 * 1024;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        IncludeFields = true,
        IgnoreReadOnlyProperties = true,
        WriteIndented = true
    };

    public static string Export(IEnumerable<CustomHotbarBarConfig> bars)
    {
        var text = JsonSerializer.Serialize(new CustomHotbar.Settings { Bars = bars.ToList() }, SerializerOptions);
        if (text.Length > MAX_CLIPBOARD_LENGTH)
        {
            throw new JsonException("Clipboard size limit exceeded.");
        }

        return text;
    }

    public static bool TryImport(string text, CustomHotbar.Settings config, CustomHotbar module, out int count, out string error)
    {
        count = 0;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(text) || text.Length > MAX_CLIPBOARD_LENGTH)
        {
            error = "ImportEmpty";
            return false;
        }

        CustomHotbar.Settings? imported;
        try
        {
            imported = JsonSerializer.Deserialize<CustomHotbar.Settings>(text, SerializerOptions);
        }
        catch (JsonException)
        {
            error = "ImportInvalidJson";
            return false;
        }

        if (imported?.Bars == null || imported.Bars.Count == 0)
        {
            error = "ImportNoBars";
            return false;
        }

        var names = new HashSet<string>(config.Bars.Select(bar => bar.Name), StringComparer.Ordinal);
        foreach (var bar in imported.Bars)
        {
            if (bar == null || bar.Slots?.Any(slot => slot == null) == true ||
                bar.Slots?.Count > CustomHotbar.SlotCount ||
                !float.IsFinite(bar.GlobalScale) || !float.IsFinite(bar.Opacity) ||
                !float.IsFinite(bar.Position.X) || !float.IsFinite(bar.Position.Y) ||
                !float.IsFinite(bar.PositionRatio.X) || !float.IsFinite(bar.PositionRatio.Y))
            {
                error = "ImportInvalidBar";
                return false;
            }

            CustomHotbar.NormalizeBar(bar);
            var baseName = string.IsNullOrWhiteSpace(bar.Name) ? module.Text("ImportedBarName") : bar.Name.Trim();
            var name = baseName;
            var suffix = 1;
            while (!names.Add(name))
            {
                name = module.Text("ImportedNameSuffix", baseName, suffix++);
            }

            bar.Name = name;
        }

        config.Bars.AddRange(imported.Bars);
        count = imported.Bars.Count;
        return true;
    }
}

internal sealed class CustomHotbarOverlay(CustomHotbar.Settings config)
{
    private readonly CustomHotbarSlotDragState slotDrag = new();

    public void Draw()
    {
        using var font = FontManager.Instance().UIFont.Push();

        for (var index = 0; index < config.Bars.Count; index++)
        {
            var bar = config.Bars[index];
            if (bar.Visible)
            {
                DrawBar(bar, index);
            }
        }

        UpdateSlotDrag();
    }

    private void DrawBar(CustomHotbarBarConfig bar, int index)
    {
        var columns = bar.Layout.Columns();
        var slotSize = bar.Scale(CustomHotbarBarConfig.DefaultSlotSize);
        var spacing = bar.Scale(CustomHotbarBarConfig.DefaultSlotSpacing);

        ImGui.SetNextWindowPos(bar.Position, ImGuiCond.FirstUseEver);

        var flags = ImGuiWindowFlags.NoSavedSettings |
                    ImGuiWindowFlags.NoTitleBar |
                    ImGuiWindowFlags.NoScrollbar |
                    ImGuiWindowFlags.NoResize |
                    ImGuiWindowFlags.AlwaysAutoResize |
                    ImGuiWindowFlags.NoDocking |
                    ImGuiWindowFlags.NoFocusOnAppearing;
        if (bar.Locked)
        {
            flags |= ImGuiWindowFlags.NoMove;
        }

        using var styles = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, bar.Scale(new Vector2(6f)))
            .Push(ImGuiStyleVar.WindowRounding, bar.Scale(6f))
            .Push(ImGuiStyleVar.WindowBorderSize, 0f);
        using var colors = ImRaii.PushColor(ImGuiCol.WindowBg, new Vector4(0.045f, 0.047f, 0.052f, bar.Opacity));

        if (ImGui.Begin($"{bar.Name}###OmniCustomHotbar{index}", flags))
        {
            ImGui.SetWindowFontScale(bar.EffectiveScale);
            var drawList = ImGui.GetWindowDrawList();
            var slots = bar.Slots;
            var slotCount = Math.Min(slots.Count, CustomHotbar.SlotCount);
            Span<int> visibleSlots = stackalloc int[CustomHotbar.SlotCount];
            var visibleCount = 0;
            for (var i = 0; i < slotCount; i++)
            {
                if (!bar.HideEmptySlots || slots[i].IconID > 0)
                {
                    visibleSlots[visibleCount++] = i;
                }
            }

            for (var visibleIndex = 0; visibleIndex < visibleCount; visibleIndex++)
            {
                var slotIndex = visibleSlots[visibleIndex];
                if (visibleIndex % columns > 0)
                {
                    ImGui.SameLine(0f, spacing);
                }

                var slot = slots[slotIndex];
                var slotPosition = ImGui.GetCursorScreenPos();
                var slotSizeVector = new Vector2(slotSize);

                if (ImGui.InvisibleButton($"##omniCustomHotbarSlot{slotIndex}", slotSizeVector) &&
                    !string.IsNullOrWhiteSpace(slot.Command))
                {
                    CustomHotbar.ExecuteCommand(slot.Command);
                }

                if (slotDrag.SourceIndex < 0 && ImGui.IsItemHovered() && ImGui.IsMouseDragging(ImGuiMouseButton.Right))
                {
                    slotDrag.BarIndex = index;
                    slotDrag.SourceIndex = slotIndex;
                    slotDrag.TargetIndex = slotIndex;
                    slotDrag.GrabOffset = ImGui.GetMousePos() - slotPosition;
                }

                if (slotDrag.BarIndex == index && slotDrag.SourceIndex >= 0 &&
                    IsMouseOverSlot(slotPosition, slotSizeVector, spacing * 0.5f))
                {
                    slotDrag.TargetIndex = slotIndex;
                }

                if (ImGui.IsItemHovered() && slotDrag.SourceIndex < 0)
                {
                    drawList.AddRectFilled(
                        slotPosition,
                        slotPosition + slotSizeVector,
                        ImGui.GetColorU32(ImGui.IsItemActive() ? ImGuiCol.HeaderActive : ImGuiCol.HeaderHovered));
                    if (!string.IsNullOrWhiteSpace(slot.Tooltip))
                    {
                        OmniControls.HelpTooltip(slot.Tooltip);
                    }
                }

                var isDragTarget = slotDrag.BarIndex == index && slotDrag.SourceIndex >= 0 &&
                                   slotDrag.TargetIndex == slotIndex && slotDrag.TargetIndex != slotDrag.SourceIndex;
                DrawSlotIcon(drawList, isDragTarget ? 0u : slot.IconID, slotPosition, slotSizeVector, bar.EffectiveScale);
            }

            if (slotDrag.BarIndex == index && slotDrag.SourceIndex >= 0 && slotDrag.SourceIndex < slots.Count)
            {
                var floatingPosition = ImGui.GetMousePos() - slotDrag.GrabOffset;
                DrawSlotIcon(ImGui.GetForegroundDrawList(), slots[slotDrag.SourceIndex].IconID, floatingPosition, new Vector2(slotSize), bar.EffectiveScale);
            }

            UpdateWindowGeometry(bar);
        }

        ImGui.End();
    }

    private static void DrawSlotIcon(ImDrawListPtr drawList, uint iconID, Vector2 position, Vector2 size, float scale)
    {
        var rounding = size.X * 0.08f;
        drawList.AddRectFilled(position, position + size, 0x99000000, rounding);
        if (iconID > 0 && ImageHelper.GetGameIcon(iconID) is { } texture)
        {
            drawList.AddImage(texture.Handle, position, position + size);
        }
    }

    private void UpdateWindowGeometry(CustomHotbarBarConfig bar)
    {
        var viewport = ImGui.GetMainViewport();
        var viewportSize = viewport.WorkSize;
        if (viewportSize.X < 8f || viewportSize.Y < 8f)
        {
            return;
        }

        var viewportPos = viewport.WorkPos;
        if (bar.PositionRatio.X < 0f || bar.PositionRatio.Y < 0f)
        {
            bar.PositionRatio = new Vector2(
                (bar.Position.X - viewportPos.X) / viewportSize.X,
                (bar.Position.Y - viewportPos.Y) / viewportSize.Y);
        }

        var position = ImGui.GetWindowPos();
        var userDragging = !bar.Locked &&
                           ImGui.IsWindowFocused() &&
                           ImGui.IsMouseDown(ImGuiMouseButton.Left);
        if (userDragging)
        {
            bar.Position = position;
            bar.PositionRatio = new Vector2(
                (position.X - viewportPos.X) / viewportSize.X,
                (position.Y - viewportPos.Y) / viewportSize.Y);
            return;
        }

        var windowSize = ImGui.GetWindowSize();
        var target = Vector2.Clamp(
            viewportPos + bar.PositionRatio * viewportSize,
            viewportPos,
            viewportPos + Vector2.Max(Vector2.Zero, viewportSize - windowSize));
        if (Vector2.DistanceSquared(position, target) > 0.25f)
        {
            bar.Position = target;
            ImGui.SetWindowPos(target);
        }
    }

    private void UpdateSlotDrag()
    {
        if (slotDrag.SourceIndex < 0)
        {
            return;
        }

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Right))
        {
            CommitSlotDrag();
        }
        else if (!ImGui.IsMouseDown(ImGuiMouseButton.Right))
        {
            slotDrag.Cancel();
        }
    }

    private void CommitSlotDrag()
    {
        var barIndex = slotDrag.BarIndex;
        var sourceIndex = slotDrag.SourceIndex;
        var targetIndex = slotDrag.TargetIndex;
        slotDrag.Cancel();

        if (barIndex < 0 || barIndex >= config.Bars.Count)
        {
            return;
        }

        var slots = config.Bars[barIndex].Slots;
        if (sourceIndex < 0 || sourceIndex >= slots.Count ||
            targetIndex < 0 || targetIndex >= slots.Count ||
            sourceIndex == targetIndex)
        {
            return;
        }

        (slots[sourceIndex], slots[targetIndex]) = (slots[targetIndex], slots[sourceIndex]);
    }

    private static bool IsMouseOverSlot(Vector2 position, Vector2 size, float halfGap)
    {
        var mouse = ImGui.GetMousePos();
        return mouse.X >= position.X - halfGap && mouse.X < position.X + size.X + halfGap &&
               mouse.Y >= position.Y - halfGap && mouse.Y < position.Y + size.Y + halfGap;
    }
}

internal sealed class CustomHotbarSlotDragState
{
    public int BarIndex = -1;
    public int SourceIndex = -1;
    public int TargetIndex = -1;
    public Vector2 GrabOffset;

    public void Cancel()
    {
        BarIndex = -1;
        SourceIndex = -1;
        TargetIndex = -1;
        GrabOffset = Vector2.Zero;
    }
}

internal sealed class CustomHotbarPanel(CustomHotbar module)
{
    private const string SlotReorderPayload = "OmniCustomHotbarSlotReorder";

    private readonly (CustomHotbarLayout Layout, string Name)[] LayoutOptions =
    [
        (CustomHotbarLayout.TwelveByOne, "12 × 1"),
        (CustomHotbarLayout.SixByTwo, "6 × 2"),
        (CustomHotbarLayout.FourByThree, "4 × 3"),
        (CustomHotbarLayout.ThreeByFour, "3 × 4"),
        (CustomHotbarLayout.TwoBySix, "2 × 6"),
        (CustomHotbarLayout.OneByTwelve, "1 × 12")
    ];

    private float IconPreviewSize => OmniTheme.Scale(24f);

    private int selectedBarIndex;
    private int draggedSlotBarIndex = -1;
    private int draggedSlotIndex = -1;
    private bool reorderDirty;
    private CustomHotbarSlot? editingSlot;
    private string editBuffer = string.Empty;
    private string transferMessage = string.Empty;

    private static readonly string[] GameObjectCategoryKeys =
    [
        "Action",
        "Emote",
        "Item",
        "Mount",
        "Companion",
        "GeneralAction"
    ];

    private CustomHotbarSlot? iconPickerSlot;
    private int iconPickerCategory;
    private string iconPickerName = string.Empty;
    private uint iconPickerResolvedIcon;
    private string? iconPickerError;

    public bool Draw(CustomHotbar.Settings config, Action<Action<uint>> openIconBrowser)
    {
        var changed = false;

        changed |= DrawBarSelector(config);
        changed |= DrawTransferControls(config);

        if (config.Bars.Count == 0)
        {
            ImGui.TextUnformatted(module.Text("NoBars"));
            return changed;
        }

        selectedBarIndex = Math.Clamp(selectedBarIndex, 0, config.Bars.Count - 1);
        var bar = config.Bars[selectedBarIndex];

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        changed |= DrawBarEditor(bar, selectedBarIndex, openIconBrowser);
        return changed;
    }

    private bool DrawBarSelector(CustomHotbar.Settings config)
    {
        var changed = false;
        var width = ImGui.GetContentRegionAvail().X * 0.5f;

        var preview = config.Bars.Count > 0 && selectedBarIndex < config.Bars.Count
            ? config.Bars[selectedBarIndex].Name
            : string.Empty;
        if (OmniControls.BeginCombo("##customHotbarBarSelector", preview, width))
        {
            for (var index = 0; index < config.Bars.Count; index++)
            {
                if (ImGui.Selectable($"{config.Bars[index].Name}##bar{index}", index == selectedBarIndex))
                {
                    selectedBarIndex = index;
                }
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine();
        if (OmniControls.IconButton("customHotbarAddBar", FontAwesomeIcon.Plus, false, module.Text("AddBar")))
        {
            var offset = 30f * config.Bars.Count;
            config.Bars.Add(new CustomHotbarBarConfig
            {
                Name = module.Text("DefaultBarName", config.Bars.Count + 1),
                Position = new Vector2(400f + offset, 300f + offset)
            });
            selectedBarIndex = config.Bars.Count - 1;
            changed = true;
        }

        if (config.Bars.Count > 0 && selectedBarIndex < config.Bars.Count)
        {
            ImGui.SameLine();
            if (OmniControls.IconButton("customHotbarRemoveBar", FontAwesomeIcon.Trash, false, module.Text("RemoveBarHelp")))
            {
                config.Bars.RemoveAt(selectedBarIndex);
                selectedBarIndex = Math.Max(0, selectedBarIndex - 1);
                changed = true;
            }
        }

        return changed;
    }

    private bool DrawTransferControls(CustomHotbar.Settings config)
    {
        var changed = false;
        using (ImRaii.Disabled(config.Bars.Count == 0))
        {
            if (OmniControls.SmallButton(module.Text("ExportCurrent") + "##customHotbarExportCurrent", false))
            {
                var index = Math.Clamp(selectedBarIndex, 0, config.Bars.Count - 1);
                ExportToClipboard([config.Bars[index]]);
            }

            ImGui.SameLine();
            if (OmniControls.SmallButton(module.Text("ExportAll") + "##customHotbarExportAll", false))
            {
                ExportToClipboard(config.Bars);
            }
        }

        ImGui.SameLine();
        if (OmniControls.SmallButton(module.Text("Import") + "##customHotbarImport", false))
        {
            var firstImportedIndex = config.Bars.Count;
            if (CustomHotbarSharing.TryImport(ImGui.GetClipboardText(), config, module, out var count, out var error))
            {
                selectedBarIndex = firstImportedIndex;
                transferMessage = module.Text("Imported", count);
                changed = true;
            }
            else
            {
                transferMessage = module.Text("ImportFailed", module.Text(error));
            }
        }

        OmniControls.HelpTooltip(module.Text("ImportHelp"));
        if (transferMessage.Length > 0)
        {
            ImGui.TextUnformatted(transferMessage);
        }

        return changed;
    }

    private void ExportToClipboard(IEnumerable<CustomHotbarBarConfig> bars)
    {
        try
        {
            ImGui.SetClipboardText(CustomHotbarSharing.Export(bars));
            transferMessage = module.Text("Exported");
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            transferMessage = module.Text("ExportFailed");
        }
    }

    private bool DrawBarEditor(CustomHotbarBarConfig bar, int barIndex, Action<Action<uint>> openIconBrowser)
    {
        var changed = false;

        var name = bar.Name;
        ImGui.SetNextItemWidth(OmniTheme.Scale(160f));
        if (ImGui.InputText(module.Text("Name") + "##customHotbarBarName", ref name, 64))
        {
            bar.Name = name;
        }

        changed |= ImGui.IsItemDeactivatedAfterEdit();

        ImGui.SameLine(0f, OmniTheme.Scale(12f));
        var visible = bar.Visible;
        if (OmniControls.Checkbox(module.Text("Visible") + "##customHotbarBarVisible", ref visible))
        {
            bar.Visible = visible;
            changed = true;
        }

        ImGui.SameLine(0f, OmniTheme.Scale(12f));
        var locked = bar.Locked;
        if (OmniControls.Checkbox(module.Text("LockPosition") + "##customHotbarLocked", ref locked))
        {
            bar.Locked = locked;
            changed = true;
        }

        ImGui.SameLine(0f, OmniTheme.Scale(6f));
        OmniControls.HelpIcon(module.Text("LockPositionHelp"));

        ImGui.SameLine(0f, OmniTheme.Scale(12f));
        var hideEmpty = bar.HideEmptySlots;
        if (OmniControls.Checkbox(module.Text("HideEmptySlots") + "##customHotbarHideEmpty", ref hideEmpty))
        {
            bar.HideEmptySlots = hideEmpty;
            changed = true;
        }

        ImGui.SameLine(0f, OmniTheme.Scale(6f));
        OmniControls.HelpIcon(module.Text("HideEmptySlotsHelp"));

        if (OmniControls.BeginCombo(module.Text("Layout") + "##customHotbarLayout", bar.Layout.DisplayName(), OmniTheme.Scale(120f)))
        {
            foreach (var (layout, layoutName) in LayoutOptions)
            {
                if (ImGui.Selectable($"{layoutName}##customHotbarLayout{layout}", bar.Layout == layout))
                {
                    bar.Layout = layout;
                    changed = true;
                }
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine(0f, OmniTheme.Scale(6f));
        OmniControls.HelpIcon(module.Text("LayoutHelp"));

        ImGui.SameLine(0f, OmniTheme.Scale(12f));
        var scale = bar.GlobalScale;
        if (OmniControls.DragFloat(module.Text("Scale") + "##customHotbarScale", ref scale, 0.05f, 0.25f, 3f, "%.2f", OmniTheme.Scale(110f), ImGuiSliderFlags.AlwaysClamp))
        {
            bar.GlobalScale = scale;
        }

        changed |= ImGui.IsItemDeactivatedAfterEdit();

        ImGui.SameLine(0f, OmniTheme.Scale(12f));
        var opacity = bar.Opacity;
        if (OmniControls.DragFloat(module.Text("Opacity") + "##customHotbarOpacity", ref opacity, 0.01f, 0.05f, 1f, "%.2f", OmniTheme.Scale(110f), ImGuiSliderFlags.AlwaysClamp))
        {
            bar.Opacity = opacity;
        }

        changed |= ImGui.IsItemDeactivatedAfterEdit();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        changed |= DrawSlotsTable(bar, barIndex, openIconBrowser);
        return changed;
    }

    private bool DrawSlotsTable(CustomHotbarBarConfig bar, int barIndex, Action<Action<uint>> openIconBrowser)
    {
        var changed = false;
        if (reorderDirty && ImGui.GetDragDropPayload().IsNull)
        {
            reorderDirty = false;
            draggedSlotBarIndex = -1;
            draggedSlotIndex = -1;
            changed = true;
        }

        var columns = bar.Layout.Columns();

        using var table = ImRaii.Table("##customHotbarSlotsV2", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, new Vector2(-1f, OmniTheme.Scale(320f)));
        if (!table)
        {
            return false;
        }

        ImGui.TableSetupColumn("##reorder", ImGuiTableColumnFlags.WidthFixed, OmniTheme.Scale(40f));
        ImGui.TableSetupColumn(module.Text("Position"), ImGuiTableColumnFlags.WidthFixed, OmniTheme.Scale(88f));
        ImGui.TableSetupColumn(module.Text("Icon"), ImGuiTableColumnFlags.WidthFixed, OmniTheme.Scale(200f));
        ImGui.TableSetupColumn(module.Text("Tooltip"), ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn(module.Text("Command"), ImGuiTableColumnFlags.WidthStretch);

        ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
        string[] headers = ["", module.Text("Position"), module.Text("Icon"), module.Text("Tooltip"), module.Text("Command")];
        for (var column = 0; column < headers.Length; column++)
        {
            ImGui.TableSetColumnIndex(column);
            if (headers[column].Length == 0)
            {
                ImGui.TableHeader(string.Empty);
                continue;
            }

            var headerWidth = ImGui.GetContentRegionAvail().X;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + MathF.Max(0f, (headerWidth - ImGui.CalcTextSize(headers[column]).X) * 0.5f));
            ImGui.TableHeader(headers[column]);
        }

        var rowHeight = MathF.Max(IconPreviewSize, ImGui.GetFrameHeight()) + ImGui.GetStyle().CellPadding.Y * 2f;
        for (var index = 0; index < bar.Slots.Count && index < CustomHotbar.SlotCount; index++)
        {
            var slot = bar.Slots[index];
            ImGui.PushID(index);
            ImGui.TableNextRow(ImGuiTableRowFlags.None, rowHeight);
            if (draggedSlotBarIndex == barIndex && draggedSlotIndex == index)
            {
                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(ImGuiCol.HeaderHovered));
            }

            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + MathF.Max(0f, (ImGui.GetContentRegionAvail().X - ImGui.GetFrameHeight()) * 0.5f));
            DrawSlotReorderHandle(barIndex, bar.Slots, index);

            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            var positionText = module.Text("RowColumn", index / columns + 1, index % columns + 1);
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + MathF.Max(0f, (ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(positionText).X) * 0.5f));
            ImGui.TextUnformatted(positionText);

            ImGui.TableNextColumn();
            changed |= DrawIconCell(slot, openIconBrowser);

            ImGui.TableNextColumn();
            var tooltip = slot.Tooltip;
            ImGui.SetNextItemWidth(-1f);
            if (OmniControls.InputTextWithHint("##tooltip", module.Text("TooltipHint"), ref tooltip, 128))
            {
                slot.Tooltip = tooltip;
            }

            changed |= ImGui.IsItemDeactivatedAfterEdit();

            ImGui.TableNextColumn();
            var command = slot.Command;
            if (command.Contains('\n'))
            {
                ImGui.AlignTextToFramePadding();
                var lineCount = 1;
                foreach (var c in command)
                {
                    if (c == '\n')
                    {
                        lineCount++;
                    }
                }

                ImGui.TextUnformatted(module.Text("MultilinePreview", command.Split('\n', 2)[0], lineCount));
            }
            else
            {
                var style = ImGui.GetStyle();
                var editButtonWidth = ImGui.CalcTextSize(module.Text("Edit")).X + style.FramePadding.X * 2f + style.ItemSpacing.X + style.CellPadding.X * 2f + style.ScrollbarSize;
                ImGui.SetNextItemWidth(MathF.Max(OmniTheme.Scale(120f), ImGui.GetContentRegionAvail().X - editButtonWidth));
                if (OmniControls.InputTextWithHint("##command", module.Text("CommandHint"), ref command, 128))
                {
                    slot.Command = command;
                }

                changed |= ImGui.IsItemDeactivatedAfterEdit();
            }

            ImGui.SameLine();
            if (OmniControls.SmallButton(module.Text("Edit") + "##editCommand", false))
            {
                editingSlot = slot;
                editBuffer = slot.Command;
                ImGui.OpenPopup("##customHotbarCommandEditor");
            }

            OmniControls.HelpTooltip(module.Text("EditCommandHelp"));

            using (var popup = ImRaii.Popup("##customHotbarCommandEditor"))
            {
                if (popup)
                {
                    DrawCommandEditorPopup(slot, ref changed);
                }
            }

            ImGui.PopID();
        }

        return changed;
    }

    private void DrawCommandEditorPopup(CustomHotbarSlot slot, ref bool changed)
    {
        if (editingSlot != slot)
        {
            return;
        }

        ImGui.TextUnformatted(module.Text("CommandEditorHelp"));
        var buffer = editBuffer;
        if (ImGui.InputTextMultiline("##customHotbarCommandEditorInput", ref buffer, 2048, new Vector2(OmniTheme.Scale(460f), OmniTheme.Scale(150f))))
        {
            editBuffer = buffer;
        }

        if (OmniControls.SmallButton(module.Text("Confirm") + "##confirmCommand", false))
        {
            slot.Command = editBuffer.Trim();
            editingSlot = null;
            ImGui.CloseCurrentPopup();
            changed = true;
        }

        ImGui.SameLine();
        if (OmniControls.SmallButton(module.Text("Cancel") + "##cancelCommand", false))
        {
            editingSlot = null;
            ImGui.CloseCurrentPopup();
        }
    }

    private bool DrawIconCell(CustomHotbarSlot slot, Action<Action<uint>> openIconBrowser)
    {
        var changed = false;

        var previewSize = IconPreviewSize;
        var previewX = ImGui.GetCursorScreenPos().X;
        ImGui.Dummy(new Vector2(previewSize));
        ImGui.SameLine();

        var iconText = slot.IconID.ToString(CultureInfo.InvariantCulture);
        ImGui.SetNextItemWidth(OmniTheme.Scale(60f));
        if (ImGui.InputText("##iconId", ref iconText, 16, ImGuiInputTextFlags.CharsDecimal) &&
            uint.TryParse(iconText, out var iconID))
        {
            slot.IconID = iconID;
        }

        changed |= ImGui.IsItemDeactivatedAfterEdit();

        var inputMin = ImGui.GetItemRectMin();
        var inputMax = ImGui.GetItemRectMax();
        var previewPosition = new Vector2(previewX, inputMin.Y + MathF.Max(0f, (inputMax.Y - inputMin.Y - previewSize) * 0.5f));
        if (slot.IconID > 0 && ImageHelper.GetGameIcon(slot.IconID) is { } texture)
        {
            ImGui.GetWindowDrawList().AddImage(texture.Handle, previewPosition, previewPosition + new Vector2(previewSize));
        }
        else
        {
            ImGui.GetWindowDrawList().AddRect(previewPosition, previewPosition + new Vector2(previewSize), 0xFF808080, OmniTheme.Scale(2f));
        }

        ImGui.SameLine();
        if (OmniControls.IconButton("##pickIcon", FontAwesomeIcon.Image, false, module.Text("OpenIconBrowser")))
        {
            openIconBrowser(iconID => slot.IconID = iconID);
        }

        ImGui.SameLine();
        if (OmniControls.IconButton("##pickGameObjectIcon", FontAwesomeIcon.Search, false, module.Text("PickObjectIconHelp")))
        {
            iconPickerSlot = slot;
            iconPickerCategory = 0;
            iconPickerName = string.Empty;
            iconPickerResolvedIcon = 0;
            iconPickerError = null;
            ImGui.OpenPopup("##customHotbarGameObjectIcon");
        }

        using (var popup = ImRaii.Popup("##customHotbarGameObjectIcon"))
        {
            if (popup)
            {
                DrawGameObjectIconPopup(slot, ref changed);
            }
        }

        return changed;
    }

    private void DrawGameObjectIconPopup(CustomHotbarSlot slot, ref bool changed)
    {
        if (iconPickerSlot != slot)
        {
            return;
        }

        ImGui.TextUnformatted(module.Text("ObjectIconHelp"));

        if (OmniControls.BeginCombo(module.Text("ObjectCategory") + "##customHotbarGameObjectCategory", module.Text(GameObjectCategoryKeys[iconPickerCategory]), OmniTheme.Scale(110f)))
        {
            for (var i = 0; i < GameObjectCategoryKeys.Length; i++)
            {
                if (ImGui.Selectable($"{module.Text(GameObjectCategoryKeys[i])}##customHotbarGameObjectCategory{i}", iconPickerCategory == i))
                {
                    iconPickerCategory = i;
                    iconPickerResolvedIcon = 0;
                    iconPickerError = null;
                }
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine(0f, OmniTheme.Scale(8f));
        var name = iconPickerName;
        ImGui.SetNextItemWidth(OmniTheme.Scale(180f));
        if (OmniControls.InputTextWithHint("##customHotbarGameObjectName", module.Text("ObjectNameHint"), ref name, 64))
        {
            iconPickerName = name;
            iconPickerResolvedIcon = 0;
            iconPickerError = null;
        }

        ImGui.SameLine(0f, OmniTheme.Scale(8f));
        if (OmniControls.SmallButton(module.Text("ResolveObject") + "##resolveGameObjectIcon", false))
        {
            if (TryResolveGameObjectIcon(iconPickerCategory, iconPickerName, out var resolved, out var error))
            {
                iconPickerResolvedIcon = resolved;
                iconPickerError = null;
            }
            else
            {
                iconPickerResolvedIcon = 0;
                iconPickerError = error;
            }
        }

        if (iconPickerResolvedIcon > 0)
        {
            var rowStartY = ImGui.GetCursorPosY();
            if (ImageHelper.GetGameIcon(iconPickerResolvedIcon) is { } texture)
            {
                ImGui.Image(texture.Handle, new Vector2(IconPreviewSize));
                ImGui.SameLine();
                ImGui.SetCursorPosY(rowStartY + MathF.Max(0f, (IconPreviewSize - ImGui.GetTextLineHeight()) * 0.5f));
            }

            ImGui.TextUnformatted(module.Text("IconId", iconPickerResolvedIcon));
        }
        else if (iconPickerError != null)
        {
            ImGui.TextUnformatted(iconPickerError);
        }

        using (ImRaii.Disabled(iconPickerResolvedIcon == 0))
        {
            if (OmniControls.SmallButton(module.Text("Confirm") + "##confirmGameObjectIcon", false))
            {
                slot.IconID = iconPickerResolvedIcon;
                iconPickerSlot = null;
                ImGui.CloseCurrentPopup();
                changed = true;
            }
        }

        ImGui.SameLine();
        if (OmniControls.SmallButton(module.Text("Cancel") + "##cancelGameObjectIcon", false))
        {
            iconPickerSlot = null;
            ImGui.CloseCurrentPopup();
        }
    }

    private bool TryResolveGameObjectIcon(int categoryIndex, string name, out uint iconID, out string error)
    {
        iconID = 0;
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            error = module.Text("ObjectNameRequired");
            return false;
        }

        var found = categoryIndex switch
        {
            0 => TryFindIcon(LuminaGetter.Get<LuminaAction>(), trimmed, row => row.Name.ExtractText(), row => row.Icon, out iconID),
            1 => TryFindIcon(LuminaGetter.Get<LuminaEmote>(), trimmed, row => row.Name.ExtractText(), row => row.Icon, out iconID),
            2 => TryFindIcon(LuminaGetter.Get<LuminaItem>(), trimmed, row => row.Name.ExtractText(), row => row.Icon, out iconID),
            3 => TryFindIcon(LuminaGetter.Get<LuminaMount>(), trimmed, row => row.Singular.ExtractText(), row => row.Icon, out iconID),
            4 => TryFindIcon(LuminaGetter.Get<LuminaCompanion>(), trimmed, row => row.Singular.ExtractText(), row => row.Icon, out iconID),
            _ => TryFindIcon(LuminaGetter.Get<LuminaGeneralAction>(), trimmed, row => row.Name.ExtractText(), row => (uint)row.Icon, out iconID)
        };

        error = found ? string.Empty : module.Text("ObjectNotFound", trimmed);
        return found;
    }

    private bool TryFindIcon<T>(ExcelSheet<T> sheet, string name, Func<T, string> getName, Func<T, uint> getIcon, out uint iconID)
        where T : struct, IExcelRow<T>
    {
        foreach (var row in sheet)
        {
            if (!string.Equals(getName(row), name, StringComparison.Ordinal))
            {
                continue;
            }

            iconID = getIcon(row);
            return iconID > 0;
        }

        iconID = 0;
        return false;
    }

    private void DrawSlotReorderHandle(int barIndex, List<CustomHotbarSlot> slots, int index)
    {
        OmniControls.IconButton($"##customHotbarSlotReorder{barIndex}_{index}", FontAwesomeIcon.Bars, false, module.Text("ReorderSlotHelp"));

        using (var source = ImRaii.DragDropSource())
        {
            if (source)
            {
                if (ImGui.SetDragDropPayload(SlotReorderPayload, []))
                {
                    draggedSlotBarIndex = barIndex;
                    draggedSlotIndex = index;
                }

                ImGui.TextUnformatted(module.Text("DraggingSlot", index + 1));
            }
        }

        using var target = ImRaii.DragDropTarget();
        if (!target)
        {
            return;
        }

        var payload = ImGui.AcceptDragDropPayload(SlotReorderPayload);
        if (payload.IsNull ||
            draggedSlotBarIndex != barIndex ||
            draggedSlotIndex < 0 ||
            draggedSlotIndex == index)
        {
            return;
        }

        (slots[draggedSlotIndex], slots[index]) = (slots[index], slots[draggedSlotIndex]);
        draggedSlotIndex = index;
        reorderDirty = true;
    }

}
