using System.IO;
using System.Text.Json;

namespace ShiBox;

public record Receipt(string Original, string Stored);

// Receipts are written before moving anything, so collection paths remain recoverable
// even if the app exits before layout.json has been updated.
public sealed class CollectionStorage(string root, IEnumerable<string> desktopRoots, string? receiptsRoot = null)
{
    readonly string rootPath = Path.GetFullPath(root);
    readonly string records = Path.GetFullPath(receiptsRoot ?? root + ".records");
    readonly HashSet<string> desktops = desktopRoots.Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public bool IsDesktopItem(string source) => desktops.Contains(Path.GetDirectoryName(Path.GetFullPath(source)) ?? "");

    public Receipt Collect(string source)
    {
        source = Path.GetFullPath(source);
        if (!IsDesktopItem(source)) throw new IOException("仅收纳桌面上的直接项目；其他目录只添加引用。");
        if (!File.Exists(source) && !Directory.Exists(source)) throw new IOException("原项目不存在。");
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("此项目是链接目录，请改为拖入普通快捷方式。");
        var stored = FreePath(Path.GetFileName(source));
        var receipt = new Receipt(source, stored);
        Record(receipt);
        Move(source, stored);
        return receipt;
    }

    public IEnumerable<Receipt> Recover()
    {
        if (!Directory.Exists(records)) yield break;
        foreach (var file in Directory.EnumerateFiles(records, "*.json"))
        {
            Receipt? receipt = null;
            try { receipt = JsonSerializer.Deserialize<Receipt>(File.ReadAllText(file)); } catch { }
            if (receipt != null && Path.GetDirectoryName(Path.GetFullPath(receipt.Stored))!.Equals(rootPath,StringComparison.OrdinalIgnoreCase)
                && (File.Exists(receipt.Stored) || Directory.Exists(receipt.Stored))) yield return receipt;
        }
    }

    string FreePath(string name)
    {
        Directory.CreateDirectory(rootPath);
        var target = Path.GetFullPath(Path.Combine(rootPath,name));
        if (!string.Equals(Path.GetDirectoryName(target),rootPath,StringComparison.OrdinalIgnoreCase)) throw new IOException("收纳路径无效。");
        if (!File.Exists(target) && !Directory.Exists(target)) return target;
        var stem = Path.GetFileNameWithoutExtension(name); var ext=Path.GetExtension(name);
        for(int n=2;;n++){target=Path.Combine(rootPath,$"{stem} ({n}){ext}");if(!File.Exists(target)&&!Directory.Exists(target))return target;}
    }
    void Record(Receipt receipt)
    {
        Directory.CreateDirectory(records);
        // A restored item may leave a historical receipt for this now-free name.
        // Do not let that receipt claim a different item subsequently using the name.
        foreach(var file in Directory.EnumerateFiles(records,"*.json"))
        {
            Receipt? old=null;
            try { old=JsonSerializer.Deserialize<Receipt>(File.ReadAllText(file)); } catch { }
            if(old!=null && string.Equals(old.Stored,receipt.Stored,StringComparison.OrdinalIgnoreCase)) File.Delete(file);
        }
        File.WriteAllText(Path.Combine(records,Guid.NewGuid().ToString("N")+".json"),JsonSerializer.Serialize(receipt));
    }

    public Receipt Relocate(Receipt source, string previousRoot)
    {
        var oldRoot = Path.GetFullPath(previousRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var oldPath = Path.GetFullPath(source.Stored);
        if (!oldPath.StartsWith(oldRoot, StringComparison.OrdinalIgnoreCase)) throw new IOException("迁移源不属于旧收纳目录。");
        var target = FreePath(Path.GetFileName(oldPath));
        var receipt = new Receipt(source.Original, target);
        Record(receipt);
        Move(oldPath, target);
        return receipt;
    }

    public void Restore(Receipt receipt)
    {
        if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(receipt.Stored)),rootPath,StringComparison.OrdinalIgnoreCase))
            throw new IOException("项目不属于拾匣收纳目录。");
        if (File.Exists(receipt.Original) || Directory.Exists(receipt.Original))
            throw new IOException("原位置已有同名项目，未覆盖任何文件。请先改名或移开同名项目，再放回。");
        Directory.CreateDirectory(Path.GetDirectoryName(receipt.Original)!);
        Move(receipt.Stored, receipt.Original);
    }

    static void Move(string source, string target)
    {
        if (Directory.Exists(source)) Directory.Move(source, target);
        else File.Move(source, target);
    }
}
