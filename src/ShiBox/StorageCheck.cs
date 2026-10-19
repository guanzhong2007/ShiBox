using System.IO;
using System.Text.Json;

namespace ShiBox;
static class StorageCheck
{
    public static void Run(string root)
    {
        var run = Path.Combine(Path.GetFullPath(root), Guid.NewGuid().ToString("N"));
        var desktop = Path.Combine(run, "test-desktop");
        Directory.CreateDirectory(desktop);
        var storage = new CollectionStorage(Path.Combine(run, "collection"), new[] { desktop });
        void Require(bool value, string message) { if (!value) throw new Exception(message); }
        var file = Path.Combine(desktop, "receipt.json");
        File.WriteAllText(file, "test-payload");
        var receipt = storage.Collect(file);
        Require(Path.GetDirectoryName(receipt.Stored) == Path.Combine(run,"collection"), "flat storage path");
        Require(!File.Exists(file) && File.ReadAllText(receipt.Stored) == "test-payload", "file collection");
        Require(storage.Recover().Single() == receipt, "restart recovery");
        File.WriteAllText(file, "existing-file");
        bool refused = false;
        try { storage.Restore(receipt); } catch (IOException) { refused = true; }
        Require(refused && File.ReadAllText(file) == "existing-file" && File.ReadAllText(receipt.Stored) == "test-payload", "no overwrite on conflict");
        File.Move(file, Path.Combine(desktop, "existing-kept.json"));
        storage.Restore(receipt);
        Require(File.ReadAllText(file) == "test-payload" && !File.Exists(receipt.Stored), "file restore");
        var folder = Path.Combine(desktop, "sample-folder");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "child.txt"), "folder-payload");
        var folderReceipt = storage.Collect(folder);
        Require(!Directory.Exists(folder) && File.ReadAllText(Path.Combine(folderReceipt.Stored, "child.txt")) == "folder-payload", "folder collection");
        storage.Restore(folderReceipt);
        Require(File.ReadAllText(Path.Combine(folder, "child.txt")) == "folder-payload", "folder restore");
        var outside = Path.Combine(run, "outside.txt");
        File.WriteAllText(outside, "outside");
        refused = false;
        try { storage.Collect(outside); } catch (IOException) { refused = true; }
        Require(refused && File.Exists(outside), "scope boundary");
        var collectedAgain = storage.Collect(folder);
        var relocatedStorage = new CollectionStorage(Path.Combine(run, "new-collection"), new[] { desktop });
        var relocated = relocatedStorage.Relocate(collectedAgain, Path.Combine(run, "collection"));
        Require(!Directory.Exists(collectedAgain.Stored) && File.ReadAllText(Path.Combine(relocated.Stored, "child.txt")) == "folder-payload", "relocate preserves contents");
        Require(relocatedStorage.Recover().Single() == relocated, "relocate recovery receipt");
        relocatedStorage.Restore(relocated);
        Require(File.ReadAllText(Path.Combine(folder, "child.txt")) == "folder-payload", "restore after relocate");
        File.WriteAllText(Path.Combine(root, "result.json"), JsonSerializer.Serialize(new { passed = true, checks = new[] { "file collection", "restart recovery", "restore conflict preserves both files", "file restore", "folder collection", "folder restore", "non-desktop refused", "relocate preserves contents", "relocate recovery receipt", "restore after relocate" }, run }));
    }
}
