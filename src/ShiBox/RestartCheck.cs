using System.IO;
using System.Text.Json;

namespace ShiBox;
static class RestartCheck
{
    public static void Run(string root)
    {
        var run = Path.Combine(Path.GetFullPath(root), "restart-" + Guid.NewGuid().ToString("N"));
        var collection = Path.Combine(run,"collection"); Directory.CreateDirectory(collection);
        var records = Path.Combine(run,"receipts"); Directory.CreateDirectory(records);
        var config = Path.Combine(run,"layout.json");
        var folder = Path.Combine(collection,"sample-folder"); Directory.CreateDirectory(folder);
        var file = Path.Combine(collection,"sample.txt"); File.WriteAllText(file,"payload");
        var original = Path.Combine(run,"desktop","sample-folder");
        File.WriteAllText(Path.Combine(records,"folder.json"),JsonSerializer.Serialize(new Receipt(original,folder)));
        var storage = new CollectionStorage(collection,Array.Empty<string>(),records);
        var store = new StateStore(config,collection,storage);
        void Require(bool value,string label){if(!value)throw new Exception(label);}
        var state=store.Load();
        Require(state.Paths.Count==2,"recover real files without layout");
        Require(state.Originals[folder]==original&&!state.Originals.ContainsKey(file),"recover only known restore paths");
        state.X=151;state.Y=829;state.Index=1;store.Save(state);
        var reloaded=new StateStore(config,collection,storage).Load();
        Require(reloaded.Paths.Count==2&&reloaded.X==151&&reloaded.Y==829&&reloaded.Index==1,"exact restart state");
        store.Save(state);File.WriteAllText(config,"{ incomplete");
        var recoveredStore=new StateStore(config,collection,storage);var recovered=recoveredStore.Load();
        Require(recovered.Paths.Count==2&&recovered.X==151&&recovered.Y==829,"corrupt primary uses backup");
        Require(recoveredStore.Warnings.Count>0,"load failure is visible");
        recoveredStore.Save(recovered);
        Require(Directory.EnumerateFiles(run,"layout.json.unreadable-*").Any(),"preserve damaged primary");
        Require(File.ReadAllText(config+".bak").Contains("151"),"do not overwrite healthy backup with damaged state");
        File.WriteAllText(config,"{ incomplete");File.WriteAllText(config+".bak","null");File.Delete(Path.Combine(records,"folder.json"));
        Require(new StateStore(config,collection,storage).Load().Paths.Count==2,"recover by directory when both configs and receipts fail");
        File.WriteAllText(Path.Combine(root,"restart-result.json"),JsonSerializer.Serialize(new{passed=true,checks=8,run}));
    }
}
