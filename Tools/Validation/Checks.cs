using System.Reflection;
using System.Numerics;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using Newtonsoft.Json;
using OmniToolbox.Common.Module.Abstractions;
using OmniToolbox.Config;
using OmniToolbox.Host;
using OmniToolbox.TreeHouseOnline;
using OmniToolbox.UI;
using TreeTools.Lifecycle;

internal static class Checks
{
    private static int passed;

    public static async Task Run()
    {
        var module = new CustomHotbar();
        var example = new OnlineIconExample();
        var register = typeof(OmniLoc).GetMethod("RegisterModule", BindingFlags.Static | BindingFlags.NonPublic)!;
        register.Invoke(null, [module]);
        register.Invoke(null, [example]);
        typeof(OmniLoc).GetMethod("ApplyLanguage", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [UILanguage.SimplifiedChinese]);
        var contract = typeof(OmniModuleBase).Assembly.GetType("OmniToolbox.Common.Module.Runtime.ModuleContract", true)!;
        var validate = contract.GetMethod("ValidateType", BindingFlags.Static | BindingFlags.NonPublic)!;

        Check("host API 1 contract", () =>
        {
            validate.Invoke(null, [typeof(CustomHotbar)]);
            validate.Invoke(null, [typeof(OnlineIconExample)]);
            Expect(typeof(CustomHotbar).GetCustomAttribute<ModuleApiVersionAttribute>()?.Version == 1);
        });
        Check("complete Chinese catalogs and command descriptions", () =>
        {
            foreach (var owner in new OmniModuleBase[] { module, example })
            {
                var source = owner.Localization.Catalogs[UILanguage.SimplifiedChinese];
                var traditional = owner.Localization.Catalogs[UILanguage.TraditionalChinese];
                Expect(source.Count == traditional.Count && source.Count >= 6);
                foreach (var key in source.Keys)
                {
                    Expect(!string.IsNullOrWhiteSpace(OmniLoc.Get(owner, key, UILanguage.SimplifiedChinese)));
                    Expect(!string.IsNullOrWhiteSpace(OmniLoc.Get(owner, key, UILanguage.TraditionalChinese)));
                    _ = string.Format(source[key], 1, 2, 3);
                }
                foreach (var command in owner.Info.Commands ?? [])
                    Expect(source.ContainsKey(command.DescriptionKey));
            }
        });
        Check("manifest schema, API, metadata and Git LF SHA256", () =>
        {
            using var manifest = JsonDocument.Parse(File.ReadAllBytes("TreeHouseModules.json"));
            Expect(manifest.RootElement.GetProperty("SchemaVersion").GetInt32() == 1);
            foreach (var item in manifest.RootElement.GetProperty("Modules").EnumerateArray())
            {
                Expect(item.GetProperty("ApiVersion").GetInt32() == 1);
                Expect(Version.Parse(item.GetProperty("MinimumOmniVersion").GetString()!) >= new Version(1, 1, 8, 2));
                var uri = new Uri(item.GetProperty("RepositoryURL").GetString()!);
                Expect(uri.Scheme == "https" && uri.Host == "github.com" && uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Length == 2);
                Expect(!string.IsNullOrWhiteSpace(item.GetProperty("Author").GetString()));
                var file = item.GetProperty("File").GetString()!;
                // Git stores these sources as LF; Windows working copies may be CRLF.
                var bytes = Encoding.UTF8.GetBytes(File.ReadAllText(file).Replace("\r\n", "\n"));
                Expect(Convert.ToHexString(SHA256.HashData(bytes)) == item.GetProperty("Sha256").GetString());
                var source = module.Localization.Catalogs[UILanguage.SimplifiedChinese];
                var localization = item.GetProperty("Localization").GetProperty("Source");
                Expect(localization.GetProperty("Title").GetString() == source["Title"]);
                Expect(localization.GetProperty("Description").GetString() == source["Description"]);
            }
        });
        Check("legacy JSON and replacement config binding", () =>
        {
            const string legacy = """
                {"Bars":[{"Name":"旧配置 热键栏","Visible":true,"Position":{"X":640,"Y":480},
                "PositionRatio":{"X":0.3,"Y":0.4},"GlobalScale":1.5,"Opacity":0.7,"Locked":true,
                "Layout":1,"Slots":[{"IconID":123,"Tooltip":"说明","Command":"/echo test"}]}]}
                """;
            var config = JsonConvert.DeserializeObject<CustomHotbar.Settings>(legacy)!;
            Bind(module, config);
            module.NormalizeConfig();
            var bar = config.Bars.Single();
            Expect(bar.Position == new Vector2(640, 480) && bar.PositionRatio == new Vector2(0.3f, 0.4f));
            Expect(bar.Slots.Count == 12 && bar.Slots[0].IconID == 123 && bar.Locked);
            Expect(module.TryHandleCommand("CustomHotbar", "toggle 旧配置 热键栏") && !bar.Visible);
            Expect(module.TryHandleCommand("toggle 1") && bar.Visible);
            Expect(!module.TryHandleCommand("unknown") && !module.TryHandleCommand(""));
            using var saved = JsonDocument.Parse(CustomHotbarSharing.Export(config.Bars));
            Expect(saved.RootElement.GetProperty("Bars")[0].GetProperty("Slots")[0].GetProperty("IconID").GetUInt32() == 123);
        });
        Check("normalization boundaries and layouts", () =>
        {
            var bar = new CustomHotbarBarConfig { GlobalScale = 99, Opacity = 2, Layout = (CustomHotbarLayout)99 };
            Expect(CustomHotbar.NormalizeBar(bar));
            Expect(bar.GlobalScale == 3 && bar.Opacity == 1 && bar.Layout == CustomHotbarLayout.TwelveByOne);
            Expect(bar.Slots.Count == 12 && !CustomHotbar.NormalizeBar(bar));
            bar.Slots.Add(new());
            Expect(CustomHotbar.NormalizeBar(bar) && bar.Slots.Count == 12);
            bar.GlobalScale = -1;
            bar.Opacity = 0;
            CustomHotbar.NormalizeBar(bar);
            Expect(bar.GlobalScale == 1 && bar.Opacity == 0.85f);
            foreach (var layout in Enum.GetValues<CustomHotbarLayout>())
                Expect(layout.Columns() * layout.Rows() == 12);
        });
        Check("command normalization", () =>
        {
            foreach (var pair in new[] { (" echo test; ", "/echo test"), ("／echo a", "/echo a"), ("＼echo a", "\\echo a"), ("\\echo a", "\\echo a") })
                Expect(CustomHotbar.TryNormalizeCommand(pair.Item1, out var value) && value == pair.Item2);
            Expect(!CustomHotbar.TryNormalizeCommand(" ; ", out _));
            Expect(!CustomHotbar.TryNormalizeCommand(null, out _));
        });
        Check("sharing round trip and duplicate names", () =>
        {
            var config = new CustomHotbar.Settings();
            var source = new CustomHotbarBarConfig { Name = "测试栏", Position = new(500, 600) };
            CustomHotbar.NormalizeBar(source);
            source.Slots[0] = new() { IconID = 42, Tooltip = "tip", Command = "/echo a\n/echo b" };
            var json = CustomHotbarSharing.Export([source]);
            Expect(CustomHotbarSharing.TryImport(json, config, module, out var count, out _) && count == 1);
            Expect(config.Bars[0].Position == source.Position && config.Bars[0].Slots[0].Command == source.Slots[0].Command);
            Expect(CustomHotbarSharing.TryImport(json, config, module, out _, out _));
            Expect(config.Bars[1].Name == "测试栏 (导入 1)");
            Expect(CustomHotbarSharing.TryImport("{\"Bars\":[{}]}", config, module, out _, out _));
            Expect(config.Bars[2].Name == module.Text("ImportedBarName"));
        });
        Check("invalid imports are atomic", () =>
        {
            var config = new CustomHotbar.Settings { Bars = [new() { Name = "保留" }] };
            string[] invalid = ["", "{", "null", "{}", "{\"Bars\":[]}", "{\"Bars\":[null]}",
                "{\"Bars\":[{\"Slots\":[null]}]}",
                "{\"Bars\":[{\"Name\":\"有效\"},{\"Slots\":[null]}]}",
                "{\"Bars\":[{\"Slots\":[" + string.Join(',', Enumerable.Repeat("{}", 13)) + "]}]}",
                "{\"Bars\":[{\"GlobalScale\":1e39}]}" ];
            foreach (var json in invalid)
            {
                Expect(!CustomHotbarSharing.TryImport(json, config, module, out var count, out var error));
                Expect(count == 0 && config.Bars.Count == 1 && config.Bars[0].Name == "保留");
                Expect(!string.IsNullOrEmpty(module.Text(error)));
            }
        });
        Check("reset reports actual changes", () =>
        {
            Bind(module, new());
            Expect(module.ResetSettings() && !module.ResetSettings());
            var settings = (CustomHotbar.Settings)ConfigField.GetValue(module)!;
            settings.Bars[0].Slots[0].IconID = 10;
            Expect(module.ResetSettings() && !module.ResetSettings());
            Expect(!example.ResetSettings());
        });

        var commandService = typeof(DalamudServices).GetProperty("CommandManager")!;
        var original = commandService.GetValue(null);
        var manager = DispatchProxy.Create<ICommandManager, CommandProxy>();
        var proxy = (CommandProxy)(object)manager;
        commandService.SetValue(null, manager);
        var registerCommands = typeof(CustomHotbar).GetMethod("RegisterCommands", BindingFlags.Instance | BindingFlags.NonPublic)!;
        try
        {
            for (var i = 0; i < 2; i++)
            {
                await using var activation = new FeatureLifetime();
                registerCommands.Invoke(module, [activation]);
                Expect(proxy.Handlers.ContainsKey("/customhotbar"));
                await activation.DisposeAsync();
                Expect(proxy.Handlers.Count == 0);
            }
            await using var occupied = new FeatureLifetime();
            var other = new CommandInfo((_, _) => { });
            proxy.Handlers.Add("/customhotbar", other);
            try
            {
                registerCommands.Invoke(module, [occupied]);
                throw new Exception("Expected registration conflict");
            }
            catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { }
            await occupied.DisposeAsync();
            Expect(ReferenceEquals(proxy.Handlers["/customhotbar"], other));
            Console.WriteLine("PASS standalone command re-enable, cleanup and conflict ownership");
            passed++;
        }
        finally
        {
            commandService.SetValue(null, original);
        }
        Console.WriteLine($"PASS {passed} test groups against real Omni API 1 dependencies.");
    }

    private static readonly FieldInfo ConfigField = typeof(CustomHotbar).GetField("config", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static void Bind(CustomHotbar module, CustomHotbar.Settings config) => ConfigField.SetValue(module, config);
    private static void Expect(bool success)
    {
        if (!success) throw new Exception("Assertion failed");
    }
    private static void Check(string name, Action action)
    {
        action();
        Console.WriteLine("PASS " + name);
        passed++;
    }
}
