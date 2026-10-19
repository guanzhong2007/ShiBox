using System.IO;
using System.Text.Json;

namespace ShiBox;

public sealed class StateStore(string configPath, string collectionRoot, CollectionStorage storage)
{
    public List<string> Warnings { get; } = new();
    bool primaryHealthy;

    public State Load()
    {
        Warnings.Clear();
        State? state = Read(configPath);
        primaryHealthy = state != null;
        state ??= Read(configPath + ".bak");
        state ??= new State();
        try
        {
            foreach (var receipt in storage.Recover())
            {
                state.Paths.RemoveAll(p => string.Equals(p, receipt.Original, StringComparison.OrdinalIgnoreCase));
                if (!state.Paths.Contains(receipt.Stored, StringComparer.OrdinalIgnoreCase)) state.Paths.Add(receipt.Stored);
                state.Originals[receipt.Stored] = receipt.Original;
            }
        }
        catch (Exception ex) { Warnings.Add("恢复凭据暂不可读：" + ex.Message); }
        // A collection is a real folder. Losing a config/receipt must never hide its contents.
        try
        {
            if (Directory.Exists(collectionRoot))
                foreach (var path in Directory.EnumerateFileSystemEntries(collectionRoot))
                    if (!state.Paths.Contains(path, StringComparer.OrdinalIgnoreCase)) state.Paths.Add(path);
        }
        catch (Exception ex) { Warnings.Add("收纳目录暂不可读：" + ex.Message); }
        state.Paths = state.Paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        state.Index = state.Paths.Count == 0 ? 0 : Math.Clamp(state.Index, 0, state.Paths.Count - 1);
        if (!double.IsFinite(state.X) || !double.IsFinite(state.Y)) { state.X = 100; state.Y = 160; }
        return state;
    }

    State? Read(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var state = JsonSerializer.Deserialize<State>(File.ReadAllText(path));
            if (state?.Paths == null || state.Originals == null || state.Paths.Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException("配置结构无效");
            return state;
        }
        catch (Exception ex) { Warnings.Add(Path.GetFileName(path) + " 读取失败：" + ex.Message); return null; }
    }

    public void Save(State state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        var temp = configPath + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(stream, state); stream.Flush(true); }
        if (File.Exists(configPath) && primaryHealthy) File.Replace(temp, configPath, configPath + ".bak");
        else
        {
            if (File.Exists(configPath)) File.Copy(configPath, configPath + ".unreadable-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"), false);
            File.Move(temp, configPath, true);
            if (!File.Exists(configPath + ".bak")) File.Copy(configPath, configPath + ".bak");
        }
        primaryHealthy = true;
    }
}
