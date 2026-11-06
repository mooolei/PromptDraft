using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace PromptDraft {
    public class DraftViewState {
        public int Caret { get; set; }
        public int SelectionLength { get; set; }
        public int FirstVisibleChar { get; set; }
        public int PreviewScrollY { get; set; }
        public string Mode { get; set; }
        public double SplitRatio { get; set; }
    }
    public class WindowViewState {
        public bool HasBounds { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool Maximized { get; set; }
        public Dictionary<string,DraftViewState> Drafts { get; set; }
        public WindowViewState(){Drafts=new Dictionary<string,DraftViewState>();}
        public DraftViewState For(string id){DraftViewState value;if(!Drafts.TryGetValue(id,out value)||value==null){value=new DraftViewState();Drafts[id]=value;}return value;}
    }
    public class ViewStateStore {
        readonly string path; string savedJson;
        readonly JavaScriptSerializer json=new JavaScriptSerializer{MaxJsonLength=Int32.MaxValue};
        public ViewStateStore(string directory){path=Path.Combine(directory,"view-state.json");}
        public WindowViewState Load(){
            foreach(string file in new[]{path,path+".bak"}){
                try{if(!File.Exists(file))continue;var state=json.Deserialize<WindowViewState>(File.ReadAllText(file,Encoding.UTF8));if(state==null)continue;if(state.Drafts==null)state.Drafts=new Dictionary<string,DraftViewState>();savedJson=file==path?json.Serialize(state):null;return state;}catch{}
            }
            return new WindowViewState();
        }
        public bool Save(WindowViewState state){
            try{
                string text=json.Serialize(state);if(text==savedJson)return true;
                Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";
                byte[] bytes=new UTF8Encoding(false).GetBytes(text);
                using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);savedJson=text;return true;
            }catch{return false;}
        }
        public static Rectangle VisibleBounds(WindowViewState state,Rectangle[] areas,Size minimum){
            if(areas.Length==0)return new Rectangle(50,50,minimum.Width,minimum.Height);
            var stored=new Rectangle(state.X,state.Y,Math.Max(1,Math.Min(20000,state.Width)),Math.Max(1,Math.Min(20000,state.Height)));
            var area=areas.OrderByDescending(a=>{Rectangle i=Rectangle.Intersect(a,stored);return (long)i.Width*i.Height;}).First();
            int w=Math.Min(area.Width,Math.Max(minimum.Width,stored.Width));int h=Math.Min(area.Height,Math.Max(minimum.Height,stored.Height));
            int x=Math.Max(area.Left,Math.Min(stored.X,area.Right-w));int y=Math.Max(area.Top,Math.Min(stored.Y,area.Bottom-h));return new Rectangle(x,y,w,h);
        }
    }
    public static class EditorPosition {
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr handle,int message,IntPtr wParam,IntPtr lParam);
        public static int FirstVisibleChar(TextBox editor){int line=SendMessage(editor.Handle,0xCE,IntPtr.Zero,IntPtr.Zero).ToInt32();return Math.Max(0,SendMessage(editor.Handle,0xBB,(IntPtr)line,IntPtr.Zero).ToInt32());}
        public static void Restore(TextBox editor,DraftViewState state){
            int caret=Math.Max(0,Math.Min(state.Caret,editor.TextLength));editor.Select(caret,Math.Max(0,Math.Min(state.SelectionLength,editor.TextLength-caret)));
            int desired=editor.GetLineFromCharIndex(Math.Max(0,Math.Min(state.FirstVisibleChar,editor.TextLength)));
            int existing=SendMessage(editor.Handle,0xCE,IntPtr.Zero,IntPtr.Zero).ToInt32();SendMessage(editor.Handle,0xB6,IntPtr.Zero,(IntPtr)(desired-existing));
        }
    }
}
