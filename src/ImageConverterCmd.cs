// 画像コンバーター: Windows 11 新コンテキストメニュー用 IExplorerCommand (アウトプロセス COM サーバー)
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        // EXIF Orientation (0x0112) を読み、見た目を正しい向きにするための回転/反転を返す
        static RotateFlipType OrientationOf(Image img)
        {
            const int OrientationId = 0x0112;
            if (Array.IndexOf(img.PropertyIdList, OrientationId) < 0) return RotateFlipType.RotateNoneFlipNone;
            byte[] v = img.GetPropertyItem(OrientationId).Value;
            if (v == null || v.Length < 2) return RotateFlipType.RotateNoneFlipNone;
            switch (v[0] | v[1]) // 値は 1〜8 なので、どちらのバイトが下位でも読める
            {
                case 2: return RotateFlipType.RotateNoneFlipX;
                case 3: return RotateFlipType.Rotate180FlipNone;
                case 4: return RotateFlipType.RotateNoneFlipY;
                case 5: return RotateFlipType.Rotate90FlipX;
                case 6: return RotateFlipType.Rotate90FlipNone;
                case 7: return RotateFlipType.Rotate270FlipX;
                case 8: return RotateFlipType.Rotate270FlipNone;
                default: return RotateFlipType.RotateNoneFlipNone;
            }
        }

        static ImageCodecInfo EncoderOf(ImageFormat f)
        {
            return ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == f.Guid);
        }

        // 現在のフレームを 32bpp ARGB に描画して返す。
        // メタデータは変換先に引き継がれないため、EXIF の向きは画素に反映し、解像度(DPI)は引き継ぐ。
        static Bitmap Render(Image img, bool whiteBackground)
        {
            float hr = img.HorizontalResolution, vr = img.VerticalResolution;
            RotateFlipType rot = OrientationOf(img);
            if (rot != RotateFlipType.RotateNoneFlipNone)
            {
                img.RotateFlip(rot);
                if (((int)rot & 1) == 1) { float t = hr; hr = vr; vr = t; } // 90°/270° 回転なら縦横を入れ替える
            }

            Bitmap bmp = new Bitmap(img.Width, img.Height);
            try
            {
                if (hr > 0 && vr > 0) bmp.SetResolution(hr, vr);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    if (whiteBackground) g.Clear(Color.White);
                    g.DrawImage(img, new Rectangle(0, 0, img.Width, img.Height), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel);
                }
                return bmp;
            }
            catch { bmp.Dispose(); throw; }
        }

        // GIF は 256 色まで。GDI+ に任せると固定のハーフトーンパレットになって劣化が大きいので、
        // 画像に合わせたパレット(メディアンカット)で 8bpp にする。不透明度 128 未満の画素は透過色にする。
        static Bitmap ToIndexed(Bitmap src)
        {
            int w = src.Width, h = src.Height;
            byte[] px = new byte[w * h * 4]; // B, G, R, A の順
            BitmapData bd = src.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < h; y++)
                    Marshal.Copy(IntPtr.Add(bd.Scan0, y * bd.Stride), px, y * w * 4, w * 4);
            }
            finally { src.UnlockBits(bd); }

            // 各チャンネル上位 5bit (32768 通り) のヒストグラム。平均色の計算用に実際の値も合計しておく
            int[] count = new int[32768];
            long[] sumR = new long[32768], sumG = new long[32768], sumB = new long[32768];
            bool hasAlpha = false;
            for (int i = 0; i < px.Length; i += 4)
            {
                if (px[i + 3] < 128) { hasAlpha = true; continue; }
                int b = px[i], g = px[i + 1], r = px[i + 2];
                int bin = ((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3);
                count[bin]++; sumR[bin] += r; sumG[bin] += g; sumB[bin] += b;
            }

            List<int> used = new List<int>();
            for (int bin = 0; bin < count.Length; bin++)
                if (count[bin] > 0) used.Add(bin);

            // メディアンカット: 画素数が最大の箱を、幅が最大のチャンネルの中央値で割る
            int maxColors = hasAlpha ? 255 : 256; // 透過色用に 1 つ空ける
            List<int[]> boxes = new List<int[]>();
            if (used.Count > 0) boxes.Add(used.ToArray());
            while (boxes.Count < maxColors)
            {
                int pick = -1;
                long best = 0;
                for (int i = 0; i < boxes.Count; i++)
                {
                    if (boxes[i].Length < 2) continue;
                    long pop = 0;
                    foreach (int bin in boxes[i]) pop += count[bin];
                    if (pop > best) { best = pop; pick = i; }
                }
                if (pick < 0) break;

                int[] box = boxes[pick];
                int minR = 31, maxR = 0, minG = 31, maxG = 0, minB = 31, maxB = 0;
                foreach (int bin in box)
                {
                    int r = bin >> 10, g = (bin >> 5) & 31, b = bin & 31;
                    if (r < minR) minR = r;
                    if (r > maxR) maxR = r;
                    if (g < minG) minG = g;
                    if (g > maxG) maxG = g;
                    if (b < minB) minB = b;
                    if (b > maxB) maxB = b;
                }
                int rangeR = maxR - minR, rangeG = maxG - minG, rangeB = maxB - minB;
                int shift = (rangeR >= rangeG && rangeR >= rangeB) ? 10 : (rangeG >= rangeB ? 5 : 0);
                Array.Sort(box, (p, q) => ((p >> shift) & 31).CompareTo((q >> shift) & 31));

                long half = best / 2, acc = 0;
                int cut = 1;
                for (int i = 0; i < box.Length - 1; i++)
                {
                    acc += count[box[i]];
                    cut = i + 1;
                    if (acc >= half) break;
                }
                boxes[pick] = box.Take(cut).ToArray();
                boxes.Add(box.Skip(cut).ToArray());
            }

            Bitmap dst = new Bitmap(w, h, PixelFormat.Format8bppIndexed);
            try
            {
                int[] lut = new int[32768]; // ヒストグラムの bin -> パレット番号
                ColorPalette pal = dst.Palette;
                for (int i = 0; i < boxes.Count; i++)
                {
                    long n = 0, sr = 0, sg = 0, sb = 0;
                    foreach (int bin in boxes[i])
                    {
                        n += count[bin]; sr += sumR[bin]; sg += sumG[bin]; sb += sumB[bin];
                        lut[bin] = i;
                    }
                    pal.Entries[i] = Color.FromArgb((int)(sr / n), (int)(sg / n), (int)(sb / n));
                }
                int transparent = boxes.Count;
                if (hasAlpha) pal.Entries[transparent] = Color.FromArgb(0, 0, 0, 0);
                dst.Palette = pal;

                byte[] idx = new byte[w * h];
                for (int i = 0, o = 0; i < px.Length; i += 4, o++)
                    idx[o] = (byte)(px[i + 3] < 128 ? transparent : lut[((px[i + 2] >> 3) << 10) | ((px[i + 1] >> 3) << 5) | (px[i] >> 3)]);

                BitmapData od = dst.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
                try
                {
                    for (int y = 0; y < h; y++)
                        Marshal.Copy(idx, y * w, IntPtr.Add(od.Scan0, y * od.Stride), w);
                }
                finally { dst.UnlockBits(od); }

                if (src.HorizontalResolution > 0 && src.VerticalResolution > 0)
                    dst.SetResolution(src.HorizontalResolution, src.VerticalResolution);
                return dst;
            }
            catch { dst.Dispose(); throw; }
        }

        // アニメーション GIF / 複数ページ TIFF などの総フレーム数と、そのフレーム軸を返す
        static int FrameCount(Image img, out FrameDimension dim)
        {
            dim = FrameDimension.Page;
            foreach (Guid id in img.FrameDimensionsList)
            {
                FrameDimension d = new FrameDimension(id);
                int n = img.GetFrameCount(d);
                if (n > 1) { dim = d; return n; }
            }
            return 1;
        }

        static void SaveFrame(Bitmap bmp, string fmt, Stream s)
        {
            switch (fmt)
            {
                case "png": bmp.Save(s, ImageFormat.Png); break;
                case "bmp": bmp.Save(s, ImageFormat.Bmp); break;
                case "tiff": bmp.Save(s, ImageFormat.Tiff); break;
                case "gif":
                    using (Bitmap indexed = ToIndexed(bmp)) indexed.Save(s, ImageFormat.Gif);
                    break;
                case "jpg":
                    using (EncoderParameters p = new EncoderParameters(1))
                    {
                        p.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 95L);
                        bmp.Save(s, EncoderOf(ImageFormat.Jpeg), p);
                    }
                    break;
                default: throw new ArgumentException("未対応の形式です: " + fmt);
            }
        }

        // 全フレームを保持して GIF (アニメーション) / TIFF (複数ページ) に書き出す。
        // 1 フレームずつ処理するので、ページ数が多くてもメモリは 2 フレーム分で済む。
        static void SaveFrames(Image img, FrameDimension dim, int count, string fmt, Stream s)
        {
            bool gif = fmt == "gif";
            ImageCodecInfo codec = EncoderOf(gif ? ImageFormat.Gif : ImageFormat.Tiff);
            EncoderValue axis = gif ? EncoderValue.FrameDimensionTime : EncoderValue.FrameDimensionPage;
            Bitmap first = null;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    img.SelectActiveFrame(dim, i);
                    Bitmap frame = Render(img, false);
                    if (gif)
                    {
                        Bitmap indexed = ToIndexed(frame);
                        frame.Dispose();
                        frame = indexed;
                    }
                    if (i == 0)
                    {
                        first = frame;
                        if (gif) // 各フレームの表示時間(0x5100)とループ回数(0x5101)
                            foreach (int id in new int[] { 0x5100, 0x5101 })
                                if (Array.IndexOf(img.PropertyIdList, id) >= 0) first.SetPropertyItem(img.GetPropertyItem(id));
                        using (EncoderParameters p = new EncoderParameters(1))
                        {
                            p.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.SaveFlag, (long)EncoderValue.MultiFrame);
                            first.Save(s, codec, p);
                        }
                    }
                    else
                    {
                        using (frame)
                        using (EncoderParameters p = new EncoderParameters(1))
                        {
                            p.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.SaveFlag, (long)axis);
                            first.SaveAdd(frame, p);
                        }
                    }
                }
                using (EncoderParameters p = new EncoderParameters(1))
                {
                    p.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.SaveFlag, (long)EncoderValue.Flush);
                    first.SaveAdd(p);
                }
            }
            finally { if (first != null) first.Dispose(); }
        }

        // 出力先を「作成できた」ことで確保する。File.Exists で調べてから書く方式だと、
        // 調べた後に同名ファイルができたとき上書きしてしまうため。
        static FileStream CreateUnique(string dir, string baseName, string ext, out string dest)
        {
            for (int i = 0; ; i++)
            {
                dest = Path.Combine(dir, i == 0 ? baseName + ext : string.Format("{0} ({1}){2}", baseName, i, ext));
                try { return new FileStream(dest, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None); }
                catch (IOException) { if (!File.Exists(dest)) throw; } // 同名ファイルがあれば次の番号へ
            }
        }

        public static void Convert(string path, string fmt)
        {
            string dir = Path.GetDirectoryName(path);
            string baseName = Path.GetFileNameWithoutExtension(path);
            string ext = ExtOf(fmt);

            using (Image img = Image.FromFile(path))
            {
                FrameDimension dim;
                int frames = FrameCount(img, out dim);

                string dest;
                FileStream fs = CreateUnique(dir, baseName, ext, out dest);
                bool done = false;
                try
                {
                    if (frames > 1 && (fmt == "gif" || fmt == "tiff"))
                        SaveFrames(img, dim, frames, fmt, fs);
                    else // 1 枚しか持てない形式 (PNG/JPEG/BMP) へは先頭フレームのみ
                        using (Bitmap bmp = Render(img, fmt == "jpg" || fmt == "bmp")) SaveFrame(bmp, fmt, fs);
                    fs.Dispose();
                    done = true;
                }
                finally
                {
                    if (!done)
                    {
                        fs.Dispose();
                        // 失敗したら、途中までのファイルを残さない
                        try { File.Delete(dest); }
                        catch (IOException) { }
                        catch (UnauthorizedAccessException) { }
                    }
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
                try
                {
                    var errors = new List<string>();
                    foreach (string p in paths)
                    {
                        try { Converter.Convert(p, fmt); }
                        catch (Exception ex) { errors.Add(Path.GetFileName(p) + ": " + ex.Message); }
                    }
                    if (errors.Count > 0)
                        MessageBox.Show("変換に失敗しました。\n\n" + string.Join("\n", errors), "画像コンバーター", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally { Server.Idle(); }
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
        // Environment.TickCount は約24.9日で負に巻き戻るため、単調増加する Stopwatch で経過時間を測る
        static readonly Stopwatch clock = Stopwatch.StartNew();
        static long lastTick;

        public static void Busy() { Interlocked.Increment(ref active); Touch(); }
        public static void Idle() { Interlocked.Decrement(ref active); Touch(); }
        static void Touch() { Interlocked.Exchange(ref lastTick, clock.ElapsedMilliseconds); }

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
            while (Interlocked.CompareExchange(ref active, 0, 0) > 0 || clock.ElapsedMilliseconds - Interlocked.Read(ref lastTick) < 10 * 60 * 1000)
                Thread.Sleep(2000);
            CoRevokeClassObject(cookie);
            return 0;
        }
    }
}
