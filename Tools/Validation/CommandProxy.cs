using System.Collections.ObjectModel;
using System.Reflection;
using Dalamud.Game.Command;

public class CommandProxy : DispatchProxy
{
    public Dictionary<string, IReadOnlyCommandInfo> Handlers { get; } = new();
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
    {
        "get_Commands" => new ReadOnlyDictionary<string, IReadOnlyCommandInfo>(Handlers),
        "AddHandler" => Handlers.TryAdd((string)args![0]!, (CommandInfo)args[1]!),
        "RemoveHandler" => Handlers.Remove((string)args![0]!),
        _ => throw new NotSupportedException(method.Name)
    };
}
