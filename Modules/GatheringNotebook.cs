using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using OmniToolbox.Common.Module.Abstractions;
using OmniToolbox.Common.Module.Enums;
using OmniToolbox.Common.Module.Models;
using OmniToolbox.Host;
using OmniToolbox.UI;
using OmniToolbox.UI.Controls;
using OmniToolbox.UI.Theme;
using OmenTools.Interop.Game.Lumina;
using OmenTools.OmenService;

namespace OmniToolbox.TreeHouseOnline;

[ModuleApiVersion(1)]
public sealed class GatheringNotebook : OmniModuleBase
{
    public override ModuleInfo Info { get; } = new()
    {
        Category = ModuleCategory.Item,
        Author = "WYJD",
        SupportUrls = ["https://github.com/wuyujindu"]
    };

    public override ModuleLocalization Localization { get; } = ModuleLocalization.Create(
        new Dictionary<string, string>
        {
            ["Title"] = "采集笔记图鉴",
            ["Description"] = "检测角色采集笔记的完成情况, 按采矿与园艺两个分支列出尚未采集过的采集物",
            ["Mining"] = "采矿",
            ["Botany"] = "园艺",
            ["MiningProgress"] = "采矿 已采集 {0} / {1}",
            ["BotanyProgress"] = "园艺 已采集 {0} / {1}",
            ["BranchTab"] = "{0} · 未采集 {1}",
            ["Refresh"] = "刷新",
            ["RefreshHelp"] = "重新读取采集笔记的完成情况",
            ["ShowCollected"] = "显示已采集",
            ["ShowCollectedHelp"] = "勾选后一并列出已经采集过的采集物",
            ["SearchHint"] = "搜索采集物名称",
            ["Name"] = "名称",
            ["Level"] = "等级",
            ["Stars"] = "星级",
            ["AllCollected"] = "这个分支的采集物已经全部采集完成",
            ["NoMatch"] = "没有符合条件的采集物",
            ["NotLoggedIn"] = "登录角色后显示采集笔记进度",
            ["DataLoading"] = "正在读取游戏数据…",
            ["DataUnavailable"] = "游戏数据尚未就绪, 请稍后重试"
        });

    private Settings config = new();
    private GatheringCatalog? catalog;
    private bool catalogUnavailable;
    private DateTime catalogRetryAfter = DateTime.MinValue;
    private DateTime lastRefresh = DateTime.MinValue;
    private string search = string.Empty;
    private readonly List<GatheringEntry> filteredEntries = [];

    public override bool HasSettings => true;

    public override bool DrawSettings()
    {
        if (!EnsureCatalog())
        {
            ImGui.TextUnformatted(Text(catalogUnavailable ? "DataUnavailable" : "DataLoading"));
            return false;
        }

        var loaded = DalamudServices.PlayerState.IsLoaded;
        if (!loaded)
        {
            ImGui.TextUnformatted(Text("NotLoggedIn"));
        }
        else if ((DateTime.UtcNow - lastRefresh).TotalSeconds >= 2d)
        {
            RefreshCollected();
        }

        var changed = DrawToolbar();
        DrawProgress();
        ImGui.Spacing();
        DrawBranchTabs();
        return changed;
    }

    public override bool ResetSettings()
    {
        if (!config.ShowCollected)
        {
            return false;
        }

        config.ShowCollected = false;
        return true;
    }

    private bool DrawToolbar()
    {
        var changed = false;

        using (ImRaii.Disabled(!DalamudServices.PlayerState.IsLoaded))
        {
            if (OmniControls.SmallButton(Text("Refresh") + "##gatheringNotebookRefresh", false))
            {
                RefreshCollected();
            }
        }

        OmniControls.HelpTooltip(Text("RefreshHelp"));

        ImGui.SameLine(0f, OmniTheme.Scale(12f));
        var showCollected = config.ShowCollected;
        if (OmniControls.Checkbox(Text("ShowCollected") + "##gatheringNotebookShowCollected", ref showCollected))
        {
            config.ShowCollected = showCollected;
            changed = true;
        }

        ImGui.SameLine(0f, OmniTheme.Scale(6f));
        OmniControls.HelpIcon(Text("ShowCollectedHelp"));

        ImGui.SameLine(0f, OmniTheme.Scale(12f));
        ImGui.SetNextItemWidth(OmniTheme.Scale(200f));
        var query = search;
        if (OmniControls.InputTextWithHint("##gatheringNotebookSearch", Text("SearchHint"), ref query, 64))
        {
            search = query;
        }

        return changed;
    }

    private void DrawProgress()
    {
        if (catalog == null)
        {
            return;
        }

        var miningCollected = catalog.Mining.Count(entry => entry.Collected);
        var botanyCollected = catalog.Botany.Count(entry => entry.Collected);
        ImGui.TextUnformatted(Text("MiningProgress", miningCollected, catalog.Mining.Count));
        ImGui.SameLine(0f, OmniTheme.Scale(18f));
        ImGui.TextUnformatted(Text("BotanyProgress", botanyCollected, catalog.Botany.Count));
    }

    private void DrawBranchTabs()
    {
        if (catalog == null)
        {
            return;
        }

        using var tabs = ImRaii.TabBar("##gatheringNotebookTabs");
        if (!tabs)
        {
            return;
        }

        using (var miningTab = ImRaii.TabItem(Text("BranchTab", Text("Mining"), CountPending(catalog.Mining))))
        {
            if (miningTab)
            {
                DrawBranch(catalog.Mining, "##gatheringNotebookMiningTable");
            }
        }

        using (var botanyTab = ImRaii.TabItem(Text("BranchTab", Text("Botany"), CountPending(catalog.Botany))))
        {
            if (botanyTab)
            {
                DrawBranch(catalog.Botany, "##gatheringNotebookBotanyTable");
            }
        }
    }

    private void DrawBranch(List<GatheringEntry> entries, string tableId)
    {
        filteredEntries.Clear();
        filteredEntries.AddRange(Filter(entries, config.ShowCollected, search));

        if (filteredEntries.Count == 0)
        {
            ImGui.TextUnformatted(Text(entries.Count > 0 && entries.All(entry => entry.Collected) ? "AllCollected" : "NoMatch"));
            return;
        }

        using var table = ImRaii.Table(
            tableId,
            3,
            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY,
            new Vector2(-1f, OmniTheme.Scale(340f)));
        if (!table)
        {
            return;
        }

        ImGui.TableSetupColumn(Text("Name"), ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn(Text("Level"), ImGuiTableColumnFlags.WidthFixed, OmniTheme.Scale(70f));
        ImGui.TableSetupColumn(Text("Stars"), ImGuiTableColumnFlags.WidthFixed, OmniTheme.Scale(70f));
        ImGui.TableHeadersRow();

        var iconSize = OmniTheme.Scale(22f);
        foreach (var entry in filteredEntries)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            var iconPosition = ImGui.GetCursorScreenPos();
            ImGui.Dummy(new Vector2(iconSize));
            ImGui.SameLine(0f, OmniTheme.Scale(6f));
            if (entry.IconId > 0 && ImageHelper.GetGameIcon(entry.IconId) is { } texture)
            {
                ImGui.GetWindowDrawList().AddImage(texture.Handle, iconPosition, iconPosition + new Vector2(iconSize));
            }

            ImGui.AlignTextToFramePadding();
            var color = entry.Collected ? new Vector4(0.62f, 0.62f, 0.66f, 1f) : OmniTheme.Tokens.Text;
            using (ImRaii.PushColor(ImGuiCol.Text, color, true))
            {
                ImGui.TextUnformatted(entry.Name);
            }

            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(entry.Level.ToString(CultureInfo.InvariantCulture));

            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(entry.Stars == 0 ? "—" : new string('★', entry.Stars));
        }
    }

    private static int CountPending(List<GatheringEntry> entries) => entries.Count(entry => !entry.Collected);

    private void RefreshCollected()
    {
        if (catalog == null)
        {
            return;
        }

        var loaded = DalamudServices.PlayerState.IsLoaded;
        foreach (var entry in catalog.Mining)
        {
            entry.Collected = loaded && QuestManager.IsGatheringItemGathered((ushort)entry.GatheringItemId);
        }

        foreach (var entry in catalog.Botany)
        {
            entry.Collected = loaded && QuestManager.IsGatheringItemGathered((ushort)entry.GatheringItemId);
        }

        lastRefresh = DateTime.UtcNow;
    }

    private bool EnsureCatalog()
    {
        if (catalog != null)
        {
            return true;
        }

        if (catalogUnavailable && DateTime.UtcNow < catalogRetryAfter)
        {
            return false;
        }

        try
        {
            catalog = GatheringCatalog.Build();
            catalogUnavailable = false;
            RefreshCollected();
            return true;
        }
        catch (Exception exception)
        {
            DalamudServices.PluginLog.Debug(exception, "[采集笔记图鉴] 读取游戏数据失败");
            catalogUnavailable = true;
            catalogRetryAfter = DateTime.UtcNow.AddSeconds(5);
            return false;
        }
    }

    internal static List<GatheringEntry> Filter(IEnumerable<GatheringEntry> entries, bool showCollected, string query)
    {
        var search = (query ?? string.Empty).Trim();
        var result = new List<GatheringEntry>();
        foreach (var entry in entries)
        {
            if (!showCollected && entry.Collected)
            {
                continue;
            }

            if (search.Length > 0 && entry.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            result.Add(entry);
        }

        return result;
    }

    internal static GatheringBranch ClassifyGatheringType(uint gatheringTypeId) => gatheringTypeId switch
    {
        0 or 1 => GatheringBranch.Mining,
        2 or 3 => GatheringBranch.Botany,
        _ => GatheringBranch.None
    };

    internal static int CompareEntries(GatheringEntry left, GatheringEntry right)
    {
        var byLevel = left.Level.CompareTo(right.Level);
        return byLevel != 0 ? byLevel : string.Compare(left.Name, right.Name, StringComparison.CurrentCulture);
    }

    internal string Text(string key, params object[] arguments)
    {
        var value = OmniLoc.Get(this, key);
        return arguments.Length == 0 ? value : string.Format(value, arguments);
    }

    [Serializable]
    internal sealed class Settings
    {
        public bool ShowCollected { get; set; }
    }

    internal sealed class GatheringCatalog
    {
        public List<GatheringEntry> Mining { get; } = [];
        public List<GatheringEntry> Botany { get; } = [];

        public static GatheringCatalog Build()
        {
            var result = new GatheringCatalog();
            var branches = new Dictionary<uint, GatheringBranch>();
            var gatheringItems = new Dictionary<uint, GatheringItem>();
            foreach (var row in LuminaGetter.Get<GatheringItem>())
            {
                gatheringItems[row.RowId] = row;
            }

            foreach (var pointBase in LuminaGetter.Get<GatheringPointBase>())
            {
                var branch = ClassifyGatheringType(pointBase.GatheringType.RowId);
                if (branch == GatheringBranch.None)
                {
                    continue;
                }

                for (var index = 0; index < pointBase.Item.Count; index++)
                {
                    var gatheringItemId = pointBase.Item[index].RowId;
                    if (gatheringItemId == 0 || gatheringItemId > ushort.MaxValue)
                    {
                        continue;
                    }

                    branches[gatheringItemId] = branches.TryGetValue(gatheringItemId, out var existing)
                        ? existing | branch
                        : branch;
                }
            }

            foreach (var (gatheringItemId, branch) in branches)
            {
                if (!gatheringItems.TryGetValue(gatheringItemId, out var gatheringItem))
                {
                    continue;
                }

                var itemId = gatheringItem.Item.RowId;
                if (itemId == 0)
                {
                    continue;
                }

                string name;
                uint iconId;
                if (gatheringItem.Item.TryGetValue<Item>(out var item))
                {
                    name = item.Name.ExtractText();
                    iconId = item.Icon;
                }
                else if (gatheringItem.Item.TryGetValue<EventItem>(out var eventItem))
                {
                    name = eventItem.Singular.ExtractText();
                    iconId = eventItem.Icon;
                }
                else
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                byte level = 0;
                byte stars = 0;
                if (gatheringItem.GatheringItemLevel.TryGetValue(out var itemLevel))
                {
                    level = itemLevel.GatheringItemLevel;
                    stars = itemLevel.Stars;
                }

                var entry = new GatheringEntry
                {
                    GatheringItemId = gatheringItemId,
                    ItemId = itemId,
                    IconId = iconId,
                    Name = name,
                    Level = level,
                    Stars = stars
                };

                if (branch.HasFlag(GatheringBranch.Mining))
                {
                    result.Mining.Add(entry);
                }

                if (branch.HasFlag(GatheringBranch.Botany))
                {
                    result.Botany.Add(entry);
                }
            }

            result.Mining.Sort(CompareEntries);
            result.Botany.Sort(CompareEntries);
            return result;
        }
    }
}

internal sealed class GatheringEntry
{
    public uint GatheringItemId { get; init; }
    public uint ItemId { get; init; }
    public uint IconId { get; init; }
    public string Name { get; init; } = string.Empty;
    public byte Level { get; init; }
    public byte Stars { get; init; }
    public bool Collected { get; set; }
}

[Flags]
internal enum GatheringBranch
{
    None = 0,
    Mining = 1,
    Botany = 2
}
