using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;
class BuildIcon {
    static GraphicsPath Round(float x,float y,float w,float h,float r) { var p=new GraphicsPath(); p.AddArc(x,y,r,r,180,90);p.AddArc(x+w-r,y,r,r,270,90);p.AddArc(x+w-r,y+h-r,r,r,0,90);p.AddArc(x,y+h-r,r,r,90,90);p.CloseFigure();return p; }
    static void Main(string[] args) {
        int[] sizes={16,24,32,48,64,128,256}; var images=new List<byte[]>();
        foreach(int size in sizes) using(var bitmap=new Bitmap(size,size)) {
            using(var g=Graphics.FromImage(bitmap)) {
                g.SmoothingMode=SmoothingMode.AntiAlias; g.ScaleTransform(size/256f,size/256f);g.Clear(Color.Transparent);
                using(var path=Round(8,8,240,240,58)) using(var b=new SolidBrush(Color.FromArgb(38,78,65))) g.FillPath(b,path);
                using(var path=Round(66,45,126,166,16)) using(var b=new SolidBrush(Color.FromArgb(249,248,239))) g.FillPath(b,path);
                using(var pen=new Pen(Color.FromArgb(55,106,86),12)) { pen.StartCap=pen.EndCap=LineCap.Round;g.DrawLine(pen,94,94,164,94);g.DrawLine(pen,94,126,164,126);g.DrawLine(pen,94,158,138,158); }
                using(var b=new SolidBrush(Color.FromArgb(220,174,98))) g.FillEllipse(b,171,168,42,42);
            }
            using(var ms=new MemoryStream()) { bitmap.Save(ms,ImageFormat.Png);images.Add(ms.ToArray()); }
            if(size==256) bitmap.Save(args[1],ImageFormat.Png);
        }
        using(var w=new BinaryWriter(File.Create(args[0]))) {
            w.Write((short)0);w.Write((short)1);w.Write((short)sizes.Length);int offset=6+16*sizes.Length;
            for(int i=0;i<sizes.Length;i++) { w.Write((byte)(sizes[i]==256?0:sizes[i]));w.Write((byte)(sizes[i]==256?0:sizes[i]));w.Write((byte)0);w.Write((byte)0);w.Write((short)1);w.Write((short)32);w.Write(images[i].Length);w.Write(offset);offset+=images[i].Length; }
            foreach(var bytes in images)w.Write(bytes);
        }
    }
}
