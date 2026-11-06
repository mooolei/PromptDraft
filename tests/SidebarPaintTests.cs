using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using PromptDraft;
class SidebarPaintTests {
    static void Check(bool ok,string name){Console.WriteLine((ok?"PASS ":"FAIL ")+name);if(!ok)throw new Exception(name);}
    static void Paint(MainForm f,ListBox list,Graphics g,int index,int y,bool selected){typeof(MainForm).GetMethod("DrawDraft",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,new object[]{list,new DrawItemEventArgs(g,list.Font,new Rectangle(0,y,240,92),index,selected?DrawItemState.Selected:DrawItemState.None)});}
    static int Dark(Bitmap b,int y){int n=0;for(int xx=10;xx<220;xx++)for(int yy=y+10;yy<y+32;yy++){Color c=b.GetPixel(xx,yy);if(c.R<160&&c.G<170&&c.B<170)n++;}return n;}
    static object Call(MainForm f,string name,params object[] args){return typeof(MainForm).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,args);}
    static ListBox List(MainForm f){return (ListBox)typeof(MainForm).GetField("list",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f);}
    [STAThread]static void Main(string[] args){
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Directory.CreateDirectory(args[0]);
        var state=new DraftState();for(int i=0;i<11;i++)state.Drafts.Add(new Draft{Title="收藏 "+(i+1)+"：整理计算与写入流程",Body="第 "+(i+1)+" 条内容保持独立保存",Agent="我的 Agent",Kind="clip",Pinned=i==3});state.SelectedId=state.Drafts[0].Id;
        using(var f=new MainForm(new DraftStore(Path.Combine(args[0],Guid.NewGuid().ToString("N"))),state)){
            f.Opacity=0;f.ShowInTaskbar=false;f.Show();Application.DoEvents();var list=List(f);
            using(var b=new Bitmap(240,460)){
                using(var g=Graphics.FromImage(b)){g.Clear(Palette.Side);for(int i=0;i<5;i++)Paint(f,list,g,i,i*92,i==2);}
                b.Save(Path.Combine(args[0],"five-rows.png"));
                for(int i=0;i<5;i++)Check(Dark(b,i*92)>20,"all visible rows remain painted: row "+i);
                Color before=b.GetPixel(20,20);using(var g=Graphics.FromImage(b))Paint(f,list,g,2,184,false);Check(b.GetPixel(20,20)==before&&Dark(b,0)>20&&Dark(b,368)>20,"selection repaint preserves neighboring rows");
            }
            foreach(int top in new[]{0,4,7,0}){
                list.TopIndex=top;list.SelectedIndex=Math.Min(top+1,10);list.Invalidate();list.Update();Application.DoEvents();
                using(var b=new Bitmap(list.Width,list.Height)){
                    list.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));int full=Math.Min(list.Items.Count-list.TopIndex,list.Height/list.ItemHeight);int visible=0;
                    for(int i=0;i<full;i++)if(Dark(b,i*list.ItemHeight)>20)visible++;
                    b.Save(Path.Combine(args[0],"scroll-"+top+".png"));Check(visible==full&&full>=2,"scrolling renders every visible row at top "+top);
                }
            }
            Call(f,"TogglePin");Check(list.Items.Count==11&&((Draft)list.Items[0]).Pinned,"pin refresh retains all entries");
            var search=(TextBox)typeof(MainForm).GetField("search",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f);search.Text="收藏 11";Check(list.Items.Count==1,"search filters correctly");search.Text="";Check(list.Items.Count==11,"clearing search restores every entry");
            Call(f,"SetView","edit");Application.DoEvents();using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(Point.Empty,f.Size));b.Save(Path.Combine(args[0],"sidebar-fixed.png"));}
            f.Close();
        }
    }
}
