// 화면 공통: 색·글꼴·화면 배율, 프로그램 안에 넣은 그림, 직접 그리는 부품 (둥근 버튼 · 토글 · 얇은 진행 막대 · 카드 · 머리 그림)
// 프로그램과 설치·제거 프로그램이 같은 모양을 쓰도록 함께 컴파일함 (build.ps1)
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

namespace SonySortTool
{
    static class Theme
    {
        public static readonly Color Window     = Color.FromArgb(246, 247, 249);   // 창 바탕
        public static readonly Color Surface    = Color.White;                      // 카드
        public static readonly Color Subtle     = Color.FromArgb(249, 250, 251);   // 합계 줄 · 버튼 위에 올렸을 때
        public static readonly Color Line       = Color.FromArgb(229, 231, 235);
        public static readonly Color LineStrong = Color.FromArgb(209, 213, 219);
        public static readonly Color Ink        = Color.FromArgb(17, 24, 39);
        public static readonly Color Body       = Color.FromArgb(55, 65, 81);
        public static readonly Color Muted      = Color.FromArgb(107, 114, 128);
        public static readonly Color Faint      = Color.FromArgb(156, 163, 175);
        public static readonly Color Ok         = Color.FromArgb(21, 128, 61);
        public static readonly Color OkSoft     = Color.FromArgb(236, 253, 243);
        public static readonly Color OkLine     = Color.FromArgb(34, 197, 94);
        public static readonly Color Warn       = Color.FromArgb(180, 83, 9);
        public static readonly Color WarnSoft   = Color.FromArgb(255, 248, 235);
        public static readonly Color WarnLine   = Color.FromArgb(245, 158, 11);
        public static readonly Color Danger     = Color.FromArgb(185, 28, 28);
        public static readonly Color DangerSoft = Color.FromArgb(254, 242, 242);

        // 화면 배율 (100% = 1). 크기는 S()로 직접 배율 적용 (AutoScaleMode.None)
        public static readonly float Scale;
        static Theme() { using (var g = Graphics.FromHwnd(IntPtr.Zero)) Scale = g.DpiX / 96f; }
        public static int S(float v) { return (int)Math.Round(v * Scale); }

        public static Font Font(float pt, FontStyle style = FontStyle.Regular) { return new Font("Malgun Gothic", pt, style); }

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            var p = new GraphicsPath();
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static Color Mix(Color a, Color b, float t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb((int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        // 선명한 글자 (GDI). & 를 단축키 표시로 바꾸지 않음 ("S&Q")
        public static void Text(Graphics g, string text, Font font, Rectangle r, Color color, TextFormatFlags flags)
        {
            TextRenderer.DrawText(g, text, font, r, color, flags | TextFormatFlags.NoPrefix);
        }

        public static Size Measure(string text, Font font, int width = 0)
        {
            var flags = TextFormatFlags.NoPrefix | (width > 0 ? TextFormatFlags.WordBreak : TextFormatFlags.SingleLine);
            return TextRenderer.MeasureText(text, font, new Size(width > 0 ? width : int.MaxValue, int.MaxValue), flags);
        }

        // ── 프로그램 안에 넣은 그림 (build.ps1이 assets에서 넣음). 없으면 null → 그림 없이 그림 ──
        public static Image Art(string name)
        {
            try {
                using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)) {
                    if (s == null) return null;
                    using (var img = Image.FromStream(s)) return new Bitmap(img);   // 스트림을 닫아도 쓸 수 있게 복사
                }
            } catch (Exception) { return null; }
        }

        public static Icon AppIcon(int size)
        {
            try {
                using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("app.ico"))
                    return s == null ? null : new Icon(s, size, size);
            } catch (Exception) { return null; }
        }

        // 큰 그림을 표시할 크기로 미리 줄여 둠 (그릴 때마다 줄이면 느림)
        public static Bitmap Resize(Image src, int w, int h)
        {
            var bmp = new Bitmap(Math.Max(1, w), Math.Max(1, h), PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            using (var wrap = new ImageAttributes()) {
                wrap.SetWrapMode(WrapMode.TileFlipXY);                 // 가장자리가 흐려지지 않게
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(src, new Rectangle(0, 0, bmp.Width, bmp.Height), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, wrap);
            }
            return bmp;
        }

        // 확인 ✓ · 안내 i · 주의 ! 동그라미
        public static void Badge(Graphics g, Rectangle r, char kind, Color fill)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(fill)) g.FillEllipse(b, r);
            float w = r.Width;
            using (var pen = new Pen(Color.White, Math.Max(1.5f, w * 0.1f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }) {
                if (kind == '✓') {
                    g.DrawLines(pen, new[] { new PointF(r.X + w * 0.29f, r.Y + w * 0.52f), new PointF(r.X + w * 0.44f, r.Y + w * 0.66f), new PointF(r.X + w * 0.72f, r.Y + w * 0.36f) });
                } else {
                    bool info = kind == 'i';
                    float x = r.X + w / 2;
                    g.DrawLine(pen, x, r.Y + w * (info ? 0.46f : 0.27f), x, r.Y + w * (info ? 0.72f : 0.56f));
                    float dot = w * 0.11f;
                    using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, x - dot / 2, r.Y + w * (info ? 0.25f : 0.67f), dot, dot);
                }
            }
        }
    }

    // 둥근 버튼. Primary = 브랜드 색으로 채움, 아니면 흰 바탕 + 테두리. Fill로 채움 색을 바꿀 수 있음 (제거 버튼)
    class PillButton : Button
    {
        public bool Primary;
        public Color? Fill = null;
        bool hover, down;

        public PillButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            AccessibleRole = AccessibleRole.PushButton;   // 직접 그려도 화면 낭독기에는 버튼으로
            Cursor = Cursors.Hand;
            Padding = new Padding(Theme.S(18), 0, Theme.S(18), 0);
            MinimumSize = new Size(Theme.S(96), Theme.S(38));
        }

        public override Size GetPreferredSize(Size proposed)
        {
            var t = Theme.Measure(Text, Font);
            return new Size(Math.Max(MinimumSize.Width, t.Width + Padding.Horizontal), Math.Max(MinimumSize.Height, t.Height + Theme.S(14)));
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { down = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { hover = down = false; Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Window);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color brand = Fill ?? Product.Accent, fill, ink, line;
            if (!Enabled) {
                fill = Primary ? Theme.Line : Theme.Surface; ink = Theme.Faint; line = Theme.Line;
            } else if (Primary) {
                fill = down ? Theme.Mix(brand, Color.Black, 0.22f) : hover ? Theme.Mix(brand, Color.Black, 0.1f) : brand;
                ink = Color.White; line = fill;
            } else {
                fill = down ? Theme.Line : hover ? Theme.Subtle : Theme.Surface;
                ink = Theme.Ink; line = hover ? Theme.LineStrong : Theme.Line;
            }
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = Theme.Round(r, Theme.S(8)))
            using (var b = new SolidBrush(fill))
            using (var p = new Pen(line)) {
                g.FillPath(b, path);
                g.DrawPath(p, path);
            }
            if (Focused && ShowFocusCues && Enabled) {
                var f = RectangleF.Inflate(r, -Theme.S(3), -Theme.S(3));
                using (var path = Theme.Round(f, Theme.S(6)))
                using (var p = new Pen(Primary ? Color.FromArgb(170, Color.White) : Color.FromArgb(150, brand), Math.Max(1f, Theme.Scale * 1.5f)))
                    g.DrawPath(p, path);
            }
            Theme.Text(g, Text, Font, ClientRectangle, ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    // 켜고 끄는 스위치. CheckBox라 키보드(Space)·접근성은 그대로. Note = 이름 옆의 흐린 설명
    class Toggle : CheckBox
    {
        public string Note = null;
        float pos = -1;       // 0 = 꺼짐, 1 = 켜짐 (움직이는 동안 중간값)
        bool hover;
        readonly Timer anim = new Timer { Interval = 15 };

        public Toggle()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AutoSize = true;
            AccessibleRole = AccessibleRole.CheckButton;
            Cursor = Cursors.Hand;
            anim.Tick += (s, e) => {
                float target = Checked ? 1 : 0;
                pos += Math.Sign(target - pos) * 0.25f;
                if (Math.Abs(target - pos) < 0.25f) { pos = target; anim.Stop(); }
                Invalidate();
            };
        }

        protected override void Dispose(bool disposing) { if (disposing) anim.Dispose(); base.Dispose(disposing); }

        int TrackW { get { return Theme.S(34); } }
        int Gap    { get { return Theme.S(10); } }

        public override Size GetPreferredSize(Size proposed)
        {
            var t = Theme.Measure(Text, Font);
            int w = TrackW + Gap + t.Width;
            if (!string.IsNullOrEmpty(Note)) w += Theme.S(8) + Theme.Measure(Note, Font).Width;
            return new Size(w + Theme.S(4), Math.Max(Theme.S(28), t.Height + Theme.S(6)));
        }

        protected override void OnCheckedChanged(EventArgs e)
        {
            base.OnCheckedChanged(e);
            if (IsHandleCreated && Visible && pos >= 0) anim.Start(); else pos = Checked ? 1 : 0;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (pos < 0) pos = Checked ? 1 : 0;
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Window);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int th = Theme.S(20), tw = TrackW;
            var track = new RectangleF(0.5f, (Height - th) / 2f, tw, th);
            Color off = hover && Enabled ? Color.FromArgb(196, 201, 209) : Theme.LineStrong;
            Color color = Theme.Mix(off, Product.Accent, pos);
            if (!Enabled) color = Theme.Mix(color, Theme.Surface, 0.55f);
            using (var path = Theme.Round(track, th / 2f))
            using (var b = new SolidBrush(color)) g.FillPath(b, path);
            float d = th - Theme.S(4);
            float x = track.X + Theme.S(2) + (tw - d - Theme.S(4)) * pos, y = track.Y + Theme.S(2);
            using (var shadow = new SolidBrush(Color.FromArgb(40, 0, 0, 0))) g.FillEllipse(shadow, x, y + Theme.Scale, d, d);
            g.FillEllipse(Brushes.White, x, y, d, d);
            if (Focused && ShowFocusCues) {
                var f = RectangleF.Inflate(track, Theme.S(2), Theme.S(2));
                using (var path = Theme.Round(f, f.Height / 2))
                using (var p = new Pen(Color.FromArgb(140, Product.Accent), Math.Max(1f, Theme.Scale * 1.5f))) g.DrawPath(p, path);
            }
            var text = Theme.Measure(Text, Font);
            var tr = new Rectangle(tw + Gap, 0, text.Width + Theme.S(2), Height);
            Theme.Text(g, Text, Font, tr, Enabled ? Theme.Body : Theme.Faint, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            if (!string.IsNullOrEmpty(Note)) {
                var nr = new Rectangle(tr.Right + Theme.S(6), 0, Math.Max(0, Width - tr.Right - Theme.S(6)), Height);
                Theme.Text(g, Note, Font, nr, Theme.Faint, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            }
        }
    }

    // 얇은 진행 막대. Marquee면 끝을 모를 때 흐르는 막대
    class SlimProgress : Control
    {
        int value, maximum = 100;
        bool marquee;
        float phase;
        readonly Timer timer = new Timer { Interval = 16 };

        public SlimProgress()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            timer.Tick += (s, e) => { phase = (phase + 0.012f) % 1.4f; Invalidate(); };
        }

        protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }

        public int Maximum { get { return maximum; } set { maximum = Math.Max(1, value); Invalidate(); } }
        public int Value   { get { return value; } set { this.value = Math.Max(0, Math.Min(maximum, value)); Invalidate(); } }
        public bool Marquee
        {
            get { return marquee; }
            set { marquee = value; phase = 0; timer.Enabled = value; Invalidate(); }
        }

        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); timer.Enabled = marquee && Visible; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Window);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0, 0, Width - 1, Height - 1);
            float rad = r.Height / 2;
            using (var path = Theme.Round(r, rad)) using (var b = new SolidBrush(Theme.Line)) g.FillPath(b, path);
            RectangleF bar;
            if (marquee) {
                float w = r.Width * 0.3f, x = (phase - 0.3f) * r.Width;
                bar = RectangleF.Intersect(r, new RectangleF(x, 0, w, r.Height));
            } else bar = new RectangleF(0, 0, r.Width * value / maximum, r.Height);
            if (bar.Width < 1) return;
            using (var path = Theme.Round(bar, rad)) using (var b = new SolidBrush(Product.Accent)) g.FillPath(b, path);
        }
    }

    // 둥근 카드 (흰 바탕 + 테두리). Bar를 주면 왼쪽에 색 띠 (알림 상자), Message를 주면 안에 여러 줄 글자
    class Card : Panel
    {
        public Color Fill = Theme.Surface, Border = Theme.Line;
        public Color? Bar;
        public int Radius = Theme.S(12);
        public string Placeholder = null;          // 안의 부품이 모두 숨었을 때 가운데 흐린 글자
        public string Message;
        public Color MessageColor = Theme.Body;

        public Card()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;
        }

        // Message가 다 들어가는 높이 (너비 width일 때)
        public int HeightFor(int width)
        {
            var t = Theme.Measure(Message ?? "", Font, Math.Max(10, width - Padding.Horizontal));
            return t.Height + Padding.Vertical;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Window);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = Theme.Round(r, Radius)) {
                using (var b = new SolidBrush(Fill)) g.FillPath(b, path);
                if (Bar != null) {
                    var clip = g.Clip;
                    g.SetClip(path);
                    using (var b = new SolidBrush(Bar.Value)) g.FillRectangle(b, 0, 0, Theme.S(4), Height);
                    g.Clip = clip;
                }
                using (var p = new Pen(Border)) g.DrawPath(p, path);
            }
            if (!string.IsNullOrEmpty(Message)) {
                var tr = new Rectangle(Padding.Left, Padding.Top, Width - Padding.Horizontal, Height - Padding.Vertical);
                Theme.Text(g, Message, Font, tr, MessageColor, TextFormatFlags.WordBreak);
            }
            if (!string.IsNullOrEmpty(Placeholder) && !HasVisibleChild())
                Theme.Text(g, Placeholder, Font, ClientRectangle, Theme.Faint,
                           TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        }

        bool HasVisibleChild()
        {
            foreach (Control c in Controls) if (c.Visible) return true;
            return false;
        }
    }

    // 고정 크기 창에서 위에서 아래로 차례로 쌓는 배치 (설치·제거 프로그램, 정보 창)
    class Stack
    {
        readonly Control host;
        readonly int left, width;
        public int Y;

        public Stack(Control host, int left, int width, int top) { this.host = host; this.left = left; this.width = width; Y = top; }

        public Label Text(string text, Font font, Color color, int gapAfter)
        {
            var size = Theme.Measure(text, font, width);
            var l = new Label { Text = text, Font = font, ForeColor = color, BackColor = host.BackColor, AutoSize = false, UseMnemonic = false,
                                Bounds = new Rectangle(left, Y, width, size.Height + Theme.S(2)) };
            host.Controls.Add(l);
            Y += l.Height + gapAfter;
            return l;
        }

        public T Add<T>(T c, int height, int gapAfter) where T : Control { return Add(c, left, width, height, gapAfter); }

        public T Add<T>(T c, int x, int w, int height, int gapAfter) where T : Control
        {
            c.Bounds = new Rectangle(x, Y, w, height);
            host.Controls.Add(c);
            Y += height + gapAfter;
            return c;
        }
    }

    // 설치·제거 프로그램 위쪽의 그림 + 이름. 그림은 높이에 맞춰 오른쪽에 두고, 왼쪽은 그림의 왼쪽 끝 색을 늘여 이음
    class Hero : Control
    {
        readonly Image art;
        readonly Icon icon;
        Bitmap cache;
        public string Title = null, Caption = null;

        public Hero(Image art, Icon icon)
        {
            this.art = art;
            this.icon = icon;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void Dispose(bool disposing) { if (disposing && cache != null) cache.Dispose(); base.Dispose(disposing); }
        protected override void OnResize(EventArgs e) { base.OnResize(e); if (cache != null) { cache.Dispose(); cache = null; } }

        Bitmap Compose()
        {
            var bmp = new Bitmap(Math.Max(1, Width), Math.Max(1, Height), PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp)) {
                if (art == null) {
                    using (var b = new LinearGradientBrush(new Rectangle(0, 0, bmp.Width, bmp.Height), Product.Accent, Product.AccentDeep, 20f))
                        g.FillRectangle(b, 0, 0, bmp.Width, bmp.Height);
                    return bmp;
                }
                // 그림을 높이보다 조금 크게 (위아래 여백을 줄임), 오른쪽 끝에 맞춤
                float scale = Height * 1.18f / art.Height;
                int dw = (int)(art.Width * scale), dh = (int)(art.Height * scale);
                int dx = Width - dw + (int)(dw * 0.04f), dy = (Height - dh) / 2;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                using (var wrap = new ImageAttributes()) {
                    wrap.SetWrapMode(WrapMode.TileFlipXY);
                    if (dx > 0)   // 왼쪽: 그림 왼쪽 끝 2px를 가로로 늘여 같은 색으로 이음
                        g.DrawImage(art, new Rectangle(0, dy, dx + 2, dh), 0, 0, 2, art.Height, GraphicsUnit.Pixel, wrap);
                    g.DrawImage(art, new Rectangle(dx, dy, dw, dh), 0, 0, art.Width, art.Height, GraphicsUnit.Pixel, wrap);
                }
                // 글자가 잘 보이게 왼쪽을 살짝 어둡게
                using (var b = new LinearGradientBrush(new Rectangle(0, 0, bmp.Width, bmp.Height), Color.FromArgb(90, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), 0f)) {
                    var blend = new ColorBlend { Positions = new[] { 0f, 0.55f, 1f },
                                                 Colors = new[] { Color.FromArgb(90, 0, 0, 0), Color.FromArgb(20, 0, 0, 0), Color.FromArgb(0, 0, 0, 0) } };
                    b.InterpolationColors = blend;
                    g.FillRectangle(b, 0, 0, bmp.Width, bmp.Height);
                }
            }
            return bmp;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (cache == null) cache = Compose();
            var g = e.Graphics;
            g.DrawImageUnscaled(cache, 0, 0);
            int pad = Theme.S(28), ic = Theme.S(48);
            int y = Height - pad - ic;
            if (icon != null) g.DrawIcon(icon, new Rectangle(pad, y, ic, ic));
            int x = pad + (icon != null ? ic + Theme.S(14) : 0);
            using (var big = Theme.Font(17f, FontStyle.Bold))
            using (var small = Theme.Font(9.5f)) {
                var t = Theme.Measure(Title ?? "", big);
                var c = Theme.Measure(Caption ?? "", small);
                int top = y + (ic - t.Height - c.Height + Theme.S(2)) / 2;
                Theme.Text(g, Title ?? "", big, new Rectangle(x, top, Width - x - pad, t.Height), Color.White, TextFormatFlags.SingleLine);
                Theme.Text(g, Caption ?? "", small, new Rectangle(x + Theme.S(2), top + t.Height - Theme.S(2), Width - x - pad, c.Height),
                           Color.FromArgb(235, 255, 255, 255), TextFormatFlags.SingleLine);
            }
        }
    }
}
