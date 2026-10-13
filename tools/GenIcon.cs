// Builds the NightShift icons from icon\owl-source.png (the logo image).
// - trims white margins and strips any screenshot frame
// - flood-fills the OUTER white to transparency; interior whites (the eyes) are
//   re-colored with the "interior" color so the owl reads in both polarities
// - optional thin ring just outside the silhouette (used for the inverted active state)
//
// usage: genicon <source.png> <out.ico> <ink r,g,b> <interior r,g,b> <ring r,g,b|none> [out256.png]
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

class GenIcon {
  [STAThread]
  static int Main(string[] args) {
    if (args.Length < 5) {
      Console.Error.WriteLine("usage: genicon <source.png> <out.ico> <ink r,g,b> <interior r,g,b> <ring r,g,b|none> [out256.png]");
      return 1;
    }
    string src = args[0], outIco = args[1];
    Color ink = ParseColor(args[2]);
    Color interior = ParseColor(args[3]);
    bool hasRing = !string.Equals(args[4], "none", StringComparison.OrdinalIgnoreCase);
    Color ring = hasRing ? ParseColor(args[4]) : Color.Empty;
    string outPng = args.Length > 5 ? args[5] : null;
    try {
      using (Bitmap source = new Bitmap(src)) {
        int w = source.Width, h = source.Height;
        Console.WriteLine("source: " + w + "x" + h);
        byte[] lum = new byte[w * h];
        for (int y = 0; y < h; y++) {
          for (int x = 0; x < w; x++) {
            Color p = source.GetPixel(x, y);
            lum[y * w + x] = (byte)((p.R * 299 + p.G * 587 + p.B * 114) / 1000);
          }
        }
        // Kill any screenshot frame remnants hugging the image edges (the owl starts
        // far inside, so a thin border band is safe to blank).
        for (int i = 0; i < 3; i++) {
          for (int x = 0; x < w; x++) { lum[i * w + x] = 255; lum[(h - 1 - i) * w + x] = 255; }
          for (int y = 0; y < h; y++) { lum[y * w + i] = 255; lum[y * w + (w - 1 - i)] = 255; }
        }

        Rectangle content = Trim(lum, w, h, 245);
        if (content.IsEmpty) { Console.Error.WriteLine("nothing but white in the source image"); return 1; }
        content = RemoveFrame(lum, w, h, content);
        content = Trim(lum, w, h, 245, content);
        Console.WriteLine("content: " + content);

        // Render the cut-out at native resolution, then pad to a square.
        int pad = 2;
        Rectangle r = Rectangle.FromLTRB(
          Math.Max(0, content.Left - pad), Math.Max(0, content.Top - pad),
          Math.Min(w, content.Right + pad), Math.Min(h, content.Bottom + pad));
        // Flood the outer white over the PADDED rect so its margin turns transparent
        // too (flooding only the content rect left opaque white padding lines).
        bool[] background = FloodBackground(lum, w, h, r);
        using (Bitmap region = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb)) {
          for (int y = 0; y < r.Height; y++) {
            for (int x = 0; x < r.Width; x++) {
              int idx = (y + r.Top) * w + (x + r.Left);
              Color c;
              if (background[idx]) c = Color.FromArgb(0, ink);
              else if (lum[idx] >= 200) c = Color.FromArgb(255, interior);
              else {
                int a = 255 - lum[idx];
                if (a < 20) a = 0;
                c = Color.FromArgb(a, ink);
              }
              region.SetPixel(x, y, c);
            }
          }
          int side = (int)Math.Ceiling(Math.Max(r.Width, r.Height) * 1.06);
          Console.WriteLine("icon side: " + side);
          Bitmap square = new Bitmap(side, side, PixelFormat.Format32bppArgb);
          using (Graphics g = Graphics.FromImage(square)) {
            g.CompositingMode = CompositingMode.SourceCopy; // keep alpha exact, no blending
            g.DrawImage(region, (side - r.Width) / 2, (side - r.Height) / 2);
          }

          int[] sizes = new int[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
          byte[][] images = new byte[sizes.Length][];
          for (int i = 0; i < sizes.Length; i++) {
            int s = sizes[i];
            using (Bitmap b = new Bitmap(s, s, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(b)) {
              g.InterpolationMode = InterpolationMode.HighQualityBicubic;
              g.PixelOffsetMode = PixelOffsetMode.HighQuality;
              g.DrawImage(square, new Rectangle(0, 0, s, s), new Rectangle(0, 0, side, side), GraphicsUnit.Pixel);
              if (hasRing) AddOutline(b, s, ring);
              images[i] = EncodeBmp(b);
              if (s == 256 && outPng != null) b.Save(outPng, ImageFormat.Png);
            }
          }
          square.Dispose();

          using (FileStream fs = new FileStream(outIco, FileMode.Create))
          using (BinaryWriter wr = new BinaryWriter(fs)) {
            wr.Write((ushort)0);
            wr.Write((ushort)1);
            wr.Write((ushort)images.Length);
            int offset = 6 + 16 * images.Length;
            for (int i = 0; i < sizes.Length; i++) {
              int s = sizes[i];
              wr.Write((byte)(s >= 256 ? 0 : s));
              wr.Write((byte)(s >= 256 ? 0 : s));
              wr.Write((byte)0);
              wr.Write((byte)0);
              wr.Write((ushort)1);
              wr.Write((ushort)32);
              wr.Write((uint)images[i].Length);
              wr.Write((uint)offset);
              offset += images[i].Length;
            }
            for (int i = 0; i < images.Length; i++) wr.Write(images[i]);
          }
          Console.WriteLine("written: " + Path.GetFullPath(outIco) + (outPng != null ? " + " + Path.GetFullPath(outPng) : ""));
        }
      }
      return 0;
    } catch (Exception ex) {
      Console.Error.WriteLine("genicon failed: " + ex);
      return 1;
    }
  }

  static Color ParseColor(string s) {
    string[] p = s.Split(',');
    return Color.FromArgb(int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]));
  }

  // A thin ring just outside the silhouette, added per target size, so the inverted
  // owl stays readable on the same background it is drawn against.
  static void AddOutline(Bitmap b, int s, Color ring) {
    int r = Math.Max(1, (int)Math.Round(s * 0.05));
    bool[] ink = new bool[s * s];
    int minX = s, minY = s, maxX = -1, maxY = -1;
    BitmapData bd = b.LockBits(new Rectangle(0, 0, s, s), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    byte[] px = new byte[s * s * 4];
    Marshal.Copy(bd.Scan0, px, 0, s * s * 4);
    b.UnlockBits(bd);
    for (int y = 0; y < s; y++) {
      for (int x = 0; x < s; x++) {
        if (px[(y * s + x) * 4 + 3] > 40) {
          ink[y * s + x] = true;
          if (x < minX) minX = x;
          if (x > maxX) maxX = x;
          if (y < minY) minY = y;
          if (y > maxY) maxY = y;
        }
      }
    }
    if (maxX < minX) return;
    int x0 = Math.Max(0, minX - r), x1 = Math.Min(s - 1, maxX + r);
    int y0 = Math.Max(0, minY - r), y1 = Math.Min(s - 1, maxY + r);
    for (int y = y0; y <= y1; y++) {
      for (int x = x0; x <= x1; x++) {
        int idx = y * s + x;
        if (ink[idx]) continue;
        bool near = false;
        for (int dy = -r; dy <= r && !near; dy++) {
          int ny = y + dy;
          if (ny < 0 || ny >= s) continue;
          for (int dx = -r; dx <= r; dx++) {
            if (dx * dx + dy * dy > r * r) continue;
            int nx = x + dx;
            if (nx < 0 || nx >= s) continue;
            if (ink[ny * s + nx]) { near = true; break; }
          }
        }
        if (near) {
          int p = idx * 4;
          px[p] = ring.B; px[p + 1] = ring.G; px[p + 2] = ring.R; px[p + 3] = 255; // BGRA
        }
      }
    }
    bd = b.LockBits(new Rectangle(0, 0, s, s), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
    Marshal.Copy(px, 0, bd.Scan0, s * s * 4);
    b.UnlockBits(bd);
  }

  static Rectangle Trim(byte[] lum, int w, int h, byte thr) {
    return Trim(lum, w, h, thr, new Rectangle(0, 0, w, h));
  }

  static Rectangle Trim(byte[] lum, int w, int h, byte thr, Rectangle bounds) {
    int minX = bounds.Right, minY = bounds.Bottom, maxX = bounds.Left - 1, maxY = bounds.Top - 1;
    for (int y = bounds.Top; y < bounds.Bottom; y++) {
      for (int x = bounds.Left; x < bounds.Right; x++) {
        if (lum[y * w + x] < thr) {
          if (x < minX) minX = x;
          if (x > maxX) maxX = x;
          if (y < minY) minY = y;
          if (y > maxY) maxY = y;
        }
      }
    }
    if (maxX < minX) return Rectangle.Empty;
    return Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
  }

  // A screenshot frame shows up as thin mostly-ink lines hugging the content.
  static Rectangle RemoveFrame(byte[] lum, int w, int h, Rectangle content) {
    int top = FrameRun(lum, w, h, content, 0);
    int bottom = FrameRun(lum, w, h, content, 1);
    int left = FrameRun(lum, w, h, content, 2);
    int right = FrameRun(lum, w, h, content, 3);
    int maxRun = Math.Max(Math.Max(top, bottom), Math.Max(left, right));
    if (maxRun == 0) return content;
    Console.WriteLine("frame detected: top=" + top + " bottom=" + bottom + " left=" + left + " right=" + right);
    int inset = maxRun + 1;
    Rectangle r2 = Rectangle.FromLTRB(
      left > 0 ? content.Left + inset : content.Left,
      top > 0 ? content.Top + inset : content.Top,
      right > 0 ? content.Right - inset : content.Right,
      bottom > 0 ? content.Bottom - inset : content.Bottom);
    if (r2.Width < 20 || r2.Height < 20) return content;
    return r2;
  }

  // side: 0 top, 1 bottom, 2 left, 3 right. Counts consecutive mostly-ink lines.
  static int FrameRun(byte[] lum, int w, int h, Rectangle r, int side) {
    int run = 0;
    int limit = Math.Min(r.Width, r.Height) / 3;
    for (int i = 0; i < limit; i++) {
      double darkFrac;
      if (side == 0 || side == 1) {
        int y = side == 0 ? r.Top + i : r.Bottom - 1 - i;
        int dark = 0;
        for (int x = r.Left; x < r.Right; x++) if (lum[y * w + x] < 128) dark++;
        darkFrac = (double)dark / r.Width;
      } else {
        int x = side == 2 ? r.Left + i : r.Right - 1 - i;
        int dark = 0;
        for (int y = r.Top; y < r.Bottom; y++) if (lum[y * w + x] < 128) dark++;
        darkFrac = (double)dark / r.Height;
      }
      if (darkFrac < 0.85) break;
      run++;
    }
    return run;
  }

  // White connected to the rectangle border is page background; enclosed white (eyes)
  // stays part of the logo.
  static bool[] FloodBackground(byte[] lum, int w, int h, Rectangle r) {
    bool[] bg = new bool[w * h];
    Stack<int> stack = new Stack<int>();
    for (int x = r.Left; x < r.Right; x++) {
      PushIfWhite(stack, bg, lum, w, x, r.Top);
      PushIfWhite(stack, bg, lum, w, x, r.Bottom - 1);
    }
    for (int y = r.Top; y < r.Bottom; y++) {
      PushIfWhite(stack, bg, lum, w, r.Left, y);
      PushIfWhite(stack, bg, lum, w, r.Right - 1, y);
    }
    while (stack.Count > 0) {
      int idx = stack.Pop();
      int x = idx % w, y = idx / w;
      if (x > r.Left) PushIfWhite(stack, bg, lum, w, x - 1, y);
      if (x < r.Right - 1) PushIfWhite(stack, bg, lum, w, x + 1, y);
      if (y > r.Top) PushIfWhite(stack, bg, lum, w, x, y - 1);
      if (y < r.Bottom - 1) PushIfWhite(stack, bg, lum, w, x, y + 1);
    }
    return bg;
  }

  static void PushIfWhite(Stack<int> stack, bool[] bg, byte[] lum, int w, int x, int y) {
    int idx = y * w + x;
    if (!bg[idx] && lum[idx] >= 200) {
      bg[idx] = true;
      stack.Push(idx);
    }
  }

  // 32bpp BGRA bottom-up + empty AND mask, the classic ICO image format.
  static byte[] EncodeBmp(Bitmap bmp) {
    int w = bmp.Width, h = bmp.Height;
    int bits = w * h * 4;
    int maskRow = ((w + 31) / 32) * 4;
    int mask = maskRow * h;
    byte[] data = new byte[40 + bits + mask];
    data[0] = 40; // biSize
    WriteInt32(data, 4, w);
    WriteInt32(data, 8, h * 2); // biHeight covers XOR + AND planes
    WriteInt16(data, 12, 1);
    WriteInt16(data, 14, 32);
    WriteInt32(data, 20, bits + mask);
    BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    try {
      byte[] row = new byte[w * 4];
      for (int y = 0; y < h; y++) {
        IntPtr src = new IntPtr(bd.Scan0.ToInt64() + (long)(h - 1 - y) * bd.Stride);
        Marshal.Copy(src, row, 0, w * 4);
        Buffer.BlockCopy(row, 0, data, 40 + y * w * 4, w * 4);
      }
    } finally { bmp.UnlockBits(bd); }
    return data;
  }

  static void WriteInt32(byte[] b, int off, int v) {
    b[off] = (byte)v; b[off + 1] = (byte)(v >> 8); b[off + 2] = (byte)(v >> 16); b[off + 3] = (byte)(v >> 24);
  }
  static void WriteInt16(byte[] b, int off, int v) {
    b[off] = (byte)v; b[off + 1] = (byte)(v >> 8);
  }
}
