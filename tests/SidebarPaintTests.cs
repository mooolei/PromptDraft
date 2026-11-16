using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using PromptDraft;
class SidebarPaintTests {
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
    static T Field<T>(MainForm f,string name){return (T)typeof(MainForm).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f);}
    static object Call(MainForm f,string name,params object[] args){return typeof(MainForm).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,args);}
    static void Paint(MainForm f,ListBox list,Graphics g,int index,int y,int height){Call(f,"DrawDraft",list,new DrawItemEventArgs(g,list.Font,new Rectangle(0,y,300,height),index,DrawItemState.None));}
    static int Dark(Bitmap b,Rectangle r){int n=0;r.Intersect(new Rectangle(Point.Empty,b.Size));for(int x=r.Left+8;x<r.Right-8;x++)for(int y=r.Top+6;y<r.Bottom-6;y++){Color c=b.GetPixel(x,y);if(c.R<160&&c.G<170&&c.B<170)n++;}return n;}
    static void Snapshot(MainForm form,string path){using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(path);}}
    static void InputsFit(MainForm form){foreach(string name in new[]{"titleBox","agentBox","search"}){var input=Field<TextBox>(form,name);Check(input.Top>0&&input.Bottom<input.Parent.ClientSize.Height&&input.Left>0&&input.Right<input.Parent.ClientSize.Width,name+" fits inside its frame at "+form.ClientSize);}}
    [STAThread]static int Main(string[] args){try{Run(args[0]);return 0;}catch(Exception e){Console.Error.WriteLine(e.GetType().Name+": "+e.Message);return 1;}}
    static void Run(string directory){
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Directory.CreateDirectory(directory);
        var state=new DraftState();for(int i=0;i<11;i++)state.Drafts.Add(new Draft{Title=i==0?"梳理项目需求，把下一轮要问的问题写清楚":"草稿 "+(i+1)+"：整理思路与待办事项",Body="先记录目标，再补充背景。\r\n\r\n- 明确需要解决的问题\r\n- 保留示例和限制条件\r\n- 写好后复制到对话",Agent="我的 Agent",Category=i<2?"工作":i<6?"学习":i<10?"写作":"",Pinned=i==0});state.SelectedId=state.Drafts[0].Id;
        using(var form=new MainForm(new DraftStore(Path.Combine(directory,Guid.NewGuid().ToString("N"))),state)){
            form.Opacity=0;form.ShowInTaskbar=false;form.Show();Application.DoEvents();var list=Field<ListBox>(form,"list");
            using(var bitmap=new Bitmap(300,600)){
                using(var g=Graphics.FromImage(bitmap)){g.Clear(Palette.Side);for(int i=0;i<5;i++)Paint(form,list,g,i,i*120,120);}
                for(int i=0;i<5;i++)Check(Dark(bitmap,new Rectangle(0,i*120,300,120))>15,"neighboring grouped rows remain painted "+i);
                Color before=bitmap.GetPixel(20,20);using(var g=Graphics.FromImage(bitmap))Paint(form,list,g,2,240,120);Check(bitmap.GetPixel(20,20)==before&&Dark(bitmap,new Rectangle(0,0,300,120))>15,"selection repaint does not erase other rows");
            }
            foreach(int top in new[]{0,4,8,0}){
                list.TopIndex=top;list.Invalidate();list.Update();Application.DoEvents();
                using(var bitmap=new Bitmap(list.Width,list.Height)){
                    list.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));int visible=0;
                    for(int i=0;i<list.Items.Count;i++){Rectangle row=list.GetItemRectangle(i);if(row.Top>=0&&row.Bottom<=list.ClientSize.Height){visible++;Check(Dark(bitmap,row)>8,"scroll paints full visible row "+i+" at top "+top);}}
                    Check(visible>=2,"multiple grouped rows visible after scroll");
                }
            }
            Call(form,"TogglePin");Check(list.Items.OfType<Draft>().Count()==11,"pin refresh retains every draft");
            var search=Field<TextBox>(form,"search");search.Text="草稿 11";Check(list.Items.OfType<Draft>().Count()==1,"grouped search filters correctly");search.Text="";
            var frame=Field<TextBox>(form,"titleBox").Parent;int oldRight=frame.Width-1;bool repaintedOldBorder=false;
            frame.Invalidated+=delegate(object s,InvalidateEventArgs e){if(e.InvalidRect.Contains(oldRight,1))repaintedOldBorder=true;};form.Width+=120;Application.DoEvents();
            Check(repaintedOldBorder,"growing the input frame invalidates the previous border position");
            for(int i=0;i<12;i++){form.Width=1120+i*13;Application.DoEvents();}InputsFit(form);
            form.ClientSize=new Size(1180,790);list.TopIndex=0;Application.DoEvents();Snapshot(form,Path.Combine(directory,"sidebar-default.png"));
            form.Size=form.MinimumSize;Application.DoEvents();InputsFit(form);Snapshot(form,Path.Combine(directory,"sidebar-minimum.png"));
            form.ClientSize=new Size(1320,920);Application.DoEvents();InputsFit(form);Snapshot(form,Path.Combine(directory,"sidebar-large.png"));
            Call(form,"RefreshList",state.Drafts[0].Id);Call(form,"TogglePin");Call(form,"ToggleAllGroups");Call(form,"ToggleGroup",list.Items.OfType<CategoryHeader>().Single(h=>h.Name=="工作"));
            form.ClientSize=new Size(1180,900);list.TopIndex=0;Field<TextBox>(form,"editor").Focus();Application.DoEvents();Snapshot(form,Path.Combine(directory,"sidebar-categories.png"));
            form.Scale(new SizeF(1.25F,1.25F));Application.DoEvents();InputsFit(form);Snapshot(form,Path.Combine(directory,"sidebar-scaled.png"));
            form.Close();
        }
    }
}
