// 画像コンバーター: Windows 11 新コンテキストメニュー用 IExplorerCommand (アウトプロセス COM サーバー)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ImageConverter
{
    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItem
    {
        void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [ComImport, Guid("B63EA76D-1F85-456F-A19C-48159EFA858B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItemArray
    {
        void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppvOut);
        void GetPropertyStore(int flags, ref Guid riid, out IntPtr ppv);
        void GetPropertyDescriptionList(IntPtr keyType, ref Guid riid, out IntPtr ppv);
        void GetAttributes(int attribFlags, uint sfgaoMask, out uint psfgaoAttribs);
        void GetCount(out uint pdwNumItems);
        void GetItemAt(uint dwIndex, out IShellItem ppsi);
        void EnumItems(out IntPtr ppenumShellItems);
    }

    [ComImport, Guid("A08CE4D0-FA25-44AB-B57C-C7B1C323E0B9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IExplorerCommand
    {
        [PreserveSig] int GetTitle(IShellItemArray psiItemArray, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        [PreserveSig] int GetIcon(IShellItemArray psiItemArray, [MarshalAs(UnmanagedType.LPWStr)] out string ppszIcon);
        [PreserveSig] int GetToolTip(IShellItemArray psiItemArray, [MarshalAs(UnmanagedType.LPWStr)] out string ppszInfotip);
        [PreserveSig] int GetCanonicalName(out Guid pguidCommandName);
        [PreserveSig] int GetState(IShellItemArray psiItemArray, [MarshalAs(UnmanagedType.Bool)] bool fOkToBeSlow, out uint pCmdState);
        [PreserveSig] int Invoke(IShellItemArray psiItemArray, IntPtr pbc);
        [PreserveSig] int GetFlags(out uint pFlags);
        [PreserveSig] int EnumSubCommands(out IEnumExplorerCommand ppEnum);
    }

    [ComImport, Guid("A88826F8-186F-4987-AADE-EA0CEF8FBFE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IEnumExplorerCommand
    {
        [PreserveSig] int Next(uint celt, IntPtr rgelt, out uint pceltFetched);
        [PreserveSig] int Skip(uint celt);
        [PreserveSig] int Reset();
        [PreserveSig] int Clone(out IEnumExplorerCommand ppenum);
    }

    [ComImport, Guid("00000001-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IClassFactory
    {
        [PreserveSig] int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject);
        [PreserveSig] int LockServer([MarshalAs(UnmanagedType.Bool)] bool fLock);
    }

    static class Hr
    {
        public const int S_OK = 0;
        public const int S_FALSE = 1;
        public const int E_NOTIMPL = unchecked((int)0x80004001);
        public const int E_NOINTERFACE = unchecked((int)0x80004002);
        public const int CLASS_E_NOAGGREGATION = unchecked((int)0x80040110);
    }

    static class Converter
    {
        public static string ExtOf(string fmt)
        {
            switch (fmt) { case "jpg": return ".jpg"; case "tiff": return ".tif"; default: return "." + fmt; }
        }

        public static void Convert(string path, string fmt)
        {
            string dir = Path.GetDirectoryName(path);
            string baseName = Path.GetFileNameWithoutExtension(path);
            string ext = ExtOf(fmt);
            string dest = Path.Combine(dir, baseName + ext);
            for (int i = 1; File.Exists(dest); i++)
                dest = Path.Combine(dir, string.Format("{0} ({1}){2}", baseName, i, ext));

            using (Image img = Image.FromFile(path))
            using (Bitmap bmp = new Bitmap(img.Width, img.Height))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    if (fmt == "jpg" || fmt == "bmp") g.Clear(Color.White);
                    g.DrawImage(img, 0, 0, img.Width, img.Height);
                }
                switch (fmt)
                {
                    case "png": bmp.Save(dest, ImageFormat.Png); break;
                    case "bmp": bmp.Save(dest, ImageFormat.Bmp); break;
                    case "gif": bmp.Save(dest, ImageFormat.Gif); break;
                    case "tiff": bmp.Save(dest, ImageFormat.Tiff); break;
                    case "jpg":
                        ImageCodecInfo codec = ImageCodecInfo.GetImageEncoders().First(c => c.MimeType == "image/jpeg");
                        using (EncoderParameters p = new EncoderParameters(1))
                        {
                            p.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 95L);
                            bmp.Save(dest, codec, p);
                        }
                        break;
                }
            }
        }
    }

    [ComVisible(true)]
    class FormatCommand : IExplorerCommand
    {
        readonly string fmt, title;
        public FormatCommand(string fmt, string title) { this.fmt = fmt; this.title = title; }

        public int GetTitle(IShellItemArray a, out string n) { n = title; return Hr.S_OK; }
        public int GetIcon(IShellItemArray a, out string i) { i = null; return Hr.E_NOTIMPL; }
        public int GetToolTip(IShellItemArray a, out string t) { t = null; return Hr.E_NOTIMPL; }
        public int GetCanonicalName(out Guid g) { g = Guid.Empty; return Hr.E_NOTIMPL; }
        public int GetState(IShellItemArray a, bool slow, out uint s) { s = 0; return Hr.S_OK; }
        public int GetFlags(out uint f) { f = 0; return Hr.S_OK; }
        public int EnumSubCommands(out IEnumExplorerCommand e) { e = null; return Hr.E_NOTIMPL; }

        public int Invoke(IShellItemArray items, IntPtr pbc)
        {
            var paths = new List<string>();
            if (items != null)
            {
                uint count; items.GetCount(out count);
                for (uint i = 0; i < count; i++)
                {
                    IShellItem it; items.GetItemAt(i, out it);
                    string p; it.GetDisplayName(0x80058000, out p); // SIGDN_FILESYSPATH
                    if (!string.IsNullOrEmpty(p)) paths.Add(p);
                }
            }
            Server.Busy();
            Task.Run(() =>
            {
                var errors = new List<string>();
                foreach (string p in paths)
                {
                    try { Converter.Convert(p, fmt); }
                    catch (Exception ex) { errors.Add(Path.GetFileName(p) + ": " + ex.Message); }
                }
                if (errors.Count > 0)
                    MessageBox.Show("変換に失敗しました。\n\n" + string.Join("\n", errors), "画像コンバーター", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Server.Idle();
            });
            return Hr.S_OK;
        }
    }

    [ComVisible(true)]
    class SubEnum : IEnumExplorerCommand
    {
        readonly List<IExplorerCommand> list;
        int pos;
        public SubEnum(List<IExplorerCommand> l, int p) { list = l; pos = p; }

        public int Next(uint celt, IntPtr rgelt, out uint fetched)
        {
            uint n = 0;
            while (n < celt && pos < list.Count)
            {
                IntPtr unk = Marshal.GetComInterfaceForObject(list[pos], typeof(IExplorerCommand));
                Marshal.WriteIntPtr(rgelt, (int)n * IntPtr.Size, unk);
                n++; pos++;
            }
            fetched = n;
            return n == celt ? Hr.S_OK : Hr.S_FALSE;
        }
        public int Skip(uint celt) { pos += (int)celt; return pos <= list.Count ? Hr.S_OK : Hr.S_FALSE; }
        public int Reset() { pos = 0; return Hr.S_OK; }
        public int Clone(out IEnumExplorerCommand e) { e = new SubEnum(list, pos); return Hr.S_OK; }
    }

    [ComVisible(true)]
    class RootCommand : IExplorerCommand
    {
        public int GetTitle(IShellItemArray a, out string n) { n = "画像コンバーター"; return Hr.S_OK; }
        public int GetIcon(IShellItemArray a, out string i)
        {
            i = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "imageres.dll") + ",-68";
            return Hr.S_OK;
        }
        public int GetToolTip(IShellItemArray a, out string t) { t = null; return Hr.E_NOTIMPL; }
        public int GetCanonicalName(out Guid g) { g = Guid.Empty; return Hr.E_NOTIMPL; }
        public int GetState(IShellItemArray a, bool slow, out uint s) { s = 0; return Hr.S_OK; }
        public int GetFlags(out uint f) { f = 1; return Hr.S_OK; } // ECF_HASSUBCOMMANDS
        public int Invoke(IShellItemArray a, IntPtr pbc) { return Hr.E_NOTIMPL; }
        public int EnumSubCommands(out IEnumExplorerCommand e)
        {
            e = new SubEnum(new List<IExplorerCommand>
            {
                new FormatCommand("png", "PNG に変換"),
                new FormatCommand("jpg", "JPEG に変換"),
                new FormatCommand("bmp", "BMP に変換"),
                new FormatCommand("gif", "GIF に変換"),
                new FormatCommand("tiff", "TIFF に変換"),
            }, 0);
            return Hr.S_OK;
        }
    }

    [ComVisible(true)]
    class Factory : IClassFactory
    {
        public int CreateInstance(IntPtr outer, ref Guid riid, out IntPtr obj)
        {
            obj = IntPtr.Zero;
            if (outer != IntPtr.Zero) return Hr.CLASS_E_NOAGGREGATION;
            Server.Busy(); Server.Idle();
            IntPtr unk = Marshal.GetIUnknownForObject(new RootCommand());
            try { return Marshal.QueryInterface(unk, ref riid, out obj); }
            finally { Marshal.Release(unk); }
        }
        public int LockServer(bool l) { Server.Busy(); Server.Idle(); return Hr.S_OK; }
    }

    static class Server
    {
        public static Guid Clsid = new Guid("6F1B7D52-3C0E-4A53-9B1D-2A8E5C4D7F10");
        static int active;
        static long lastTick = Environment.TickCount;

        public static void Busy() { Interlocked.Increment(ref active); lastTick = Environment.TickCount; }
        public static void Idle() { Interlocked.Decrement(ref active); lastTick = Environment.TickCount; }

        [DllImport("ole32.dll")]
        static extern int CoRegisterClassObject(ref Guid rclsid, IntPtr pUnk, uint dwClsContext, uint flags, out uint cookie);
        [DllImport("ole32.dll")]
        static extern int CoRevokeClassObject(uint cookie);

        [MTAThread]
        static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                MessageBox.Show("このプログラムは右クリックメニューから使用されます。\n画像ファイルを右クリックして「画像コンバーター」を選んでください。", "画像コンバーター");
                return 0;
            }
            IntPtr unk = Marshal.GetIUnknownForObject(new Factory());
            uint cookie;
            int hr = CoRegisterClassObject(ref Clsid, unk, 4 /*LOCAL_SERVER*/, 1 /*MULTIPLEUSE*/, out cookie);
            if (hr != 0) return hr;
            // 最後の操作から10分間何もなければ終了
            while (Interlocked.CompareExchange(ref active, 0, 0) > 0 || Environment.TickCount - lastTick < 10 * 60 * 1000)
                Thread.Sleep(2000);
            CoRevokeClassObject(cookie);
            return 0;
        }
    }
}
