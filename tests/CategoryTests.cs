using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using PromptDraft;

class CategoryTests {
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
    static T Field<T>(MainForm f,string name){return (T)typeof(MainForm).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f);}
    static object Call(MainForm f,string name,params object[] args){return typeof(MainForm).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,args);}
    static CategoryHeader Header(MainForm f,string name){return Field<ListBox>(f,"list").Items.OfType<CategoryHeader>().Single(h=>CategoryNames.Same(h.Name,name));}
    [STAThread]static int Main(string[] args){try{Run(args[0]);return 0;}catch(Exception e){Console.Error.WriteLine(e.GetType().Name+": "+e.Message);return 1;}}
    static void Run(string directory){
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        string oldDir=Path.Combine(directory,"legacy-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(oldDir);
        string oldJson="{\"Version\":1,\"SelectedId\":\"old\",\"Drafts\":[{\"Id\":\"old\",\"Title\":\"old\",\"Body\":\"line1\\nline2\",\"Kind\":\"prompt\"}]}";
        File.WriteAllText(Path.Combine(oldDir,"drafts.json"),oldJson);var legacy=new DraftStore(oldDir).Load();
        Check(legacy.Drafts[0].Category==""&&legacy.Categories.Count==0&&legacy.Drafts[0].Body=="line1\nline2","old schema loads into unclassified without changing content");
        Check(File.ReadAllText(Path.Combine(oldDir,"drafts.json"))==oldJson,"legacy load does not rewrite the data file");
        var store=new DraftStore(Path.Combine(directory,Guid.NewGuid().ToString("N")));var state=new DraftState();
        var a=new Draft{Title="Work one",Body="keep A",Category="工作"};var b=new Draft{Title="Work pinned",Body="keep B",Category="工作",Pinned=true};
        var c=new Draft{Title="Study",Body="keep C",Category="学习"};var trash=new Draft{Body="keep trash",Category="工作",Deleted=true};var clip=new Draft{Body="keep clip",Category="工作",Kind="clip",Archived=true};
        state.Drafts.AddRange(new[]{a,b,c,trash,clip});state.SelectedId=a.Id;store.Save(state);
        using(var f=new MainForm(store,state)){
            var list=Field<ListBox>(f,"list");Check(Header(f,"工作").Count==2&&Header(f,"学习").Count==1,"category counts respect section and archive/trash filters");
            Check(list.Items.OfType<Draft>().First(d=>d.Category=="工作")==b,"pinning remains first inside its category");
            Call(f,"ToggleGroup",Header(f,"工作"));Check(Header(f,"工作").Collapsed&&!list.Items.OfType<Draft>().Contains(a)&&Field<Draft>(f,"current")==a,"collapsing hides rows while preserving the current editor");
            var search=Field<TextBox>(f,"search");search.Text="Work";Check(list.Items.OfType<Draft>().Count()==2&&!Header(f,"工作").Collapsed,"search finds content inside collapsed categories");search.Text="";Check(Header(f,"工作").Collapsed,"clearing search restores collapse preference");
            Call(f,"SaveNow");using(var reopened=new MainForm(store,store.Load()))Check(Header(reopened,"工作").Collapsed,"collapsed categories survive restart");
            Call(f,"AddCategory","新项目");Check(Header(f,"新项目").Count==0,"empty custom category is retained");Call(f,"NewDraft");var fresh=Field<Draft>(f,"current");Check(fresh.Category=="新项目"&&Header(f,"新项目").Count==1,"new draft uses the selected category");
            var combo=Field<ComboBox>(f,"categoryBox");combo.Text="学习";Call(f,"CommitCategory");Check(fresh.Category=="学习"&&Header(f,"学习").Count==2,"changing category moves only the current record");
            combo.Text="需要保存";Call(f,"SaveNow");Check(store.Load().Drafts.Single(d=>d.Id==fresh.Id).Category=="需要保存","save commits a typed category before focus leaves the input");
            Call(f,"RenameCategory","工作","项目 A");Check(new[]{a,b,trash,clip}.All(d=>d.Category=="项目 A"),"rename covers active, archived and deleted records across both sections");
            Call(f,"RenameCategory","项目 A","");Check(new[]{a,b,trash,clip}.All(d=>d.Category=="")&&a.Body=="keep A"&&b.Pinned&&trash.Deleted&&clip.Archived,"removing category keeps content, pin and lifecycle flags");
            Call(f,"SwitchCategory","clip");Call(f,"SetArchiveView",true);Check(Header(f,"").Count==1&&list.Items.OfType<Draft>().Contains(clip),"collections keep independent archive filtering");
            Call(f,"SwitchCategory","prompt");Call(f,"RefreshList",a.Id);combo.Text="临时分类";Call(f,"CommitCategory");Call(f,"DeleteCurrent");
            typeof(MainForm).GetField("showTrash",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(f,true);Call(f,"RefreshList",a.Id);
            Check(!combo.Enabled&&Field<TextBox>(f,"editor").ReadOnly&&a.Category=="临时分类","trash preserves category and disables category editing");Call(f,"ArchiveCurrent");Check(!a.Deleted&&a.Category=="临时分类","restoring a record restores its category");
            Call(f,"SaveNow");var saved=store.Load();Check(saved.Categories.Contains("新项目")&&saved.Drafts.Count==6,"empty categories and every record persist");
        }
    }
}
