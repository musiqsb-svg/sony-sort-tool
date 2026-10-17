using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SonySortTool
{
    // 폴더를 넣으면 미리보기 → [정리 시작] → 결과 · [되돌리기]
    class MainForm : Form
    {
        static readonly Color Accent    = Color.FromArgb(234, 88, 12);
        static readonly Color Warn      = Color.FromArgb(180, 83, 9);
        static readonly Color Ok        = Color.FromArgb(21, 128, 61);
        static readonly Color Muted     = Color.FromArgb(107, 114, 128);
        static readonly Color Ink       = Color.FromArgb(31, 41, 55);
        static readonly Color DropBack  = Color.FromArgb(243, 244, 246);
        static readonly Color DropHover = Color.FromArgb(255, 237, 213);
        static readonly Color DropLine  = Color.FromArgb(156, 163, 175);
        static readonly Color Disabled  = Color.FromArgb(209, 213, 219);

        readonly float k;                         // 화면 배율 (100% = 1)
        readonly Settings settings = Settings.Load();
        readonly string startPath;
        readonly Panel drop;
        readonly Label dropTitle, dropHint, pathLabel, notes, status;
        readonly Button pickButton, runButton, undoButton, openButton;
        readonly ListView list;
        readonly CheckBox xmlBox, sizeBox, cardBox;
        readonly ProgressBar progress;
        bool hover, busy;
        string currentPath;
        ScanResult scan;
        RunResult lastRun;

        public MainForm(string startPath)
        {
            this.startPath = startPath;
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) k = g.DpiX / 96f;

            Text = Program.Title;
            Font = new Font("Malgun Gothic", 9.75f);
            BackColor = Color.White;
            AutoScaleMode = AutoScaleMode.None;   // 크기는 S()로 직접 배율 적용
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(S(780), S(690));
            MinimumSize = new Size(S(660), S(600));
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }

            // ── 머리말 ──
            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, Margin = Padding.Empty };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.Controls.Add(new Label {
                Text = "소니 카메라 영상·사진을 촬영일별 폴더로 정리합니다", AutoSize = true, ForeColor = Ink,
                Font = new Font(Font.FontFamily, 11f, FontStyle.Bold), Margin = new Padding(0, 0, 0, S(2)) }, 0, 0);
            var about = new LinkLabel { Text = "정보", AutoSize = true, LinkColor = Muted, ActiveLinkColor = Accent, Margin = new Padding(0, S(4), 0, 0) };
            about.LinkClicked += (s, e) => ShowAbout();
            header.Controls.Add(about, 1, 0);

            // ── 끌어다 놓는 곳 ──
            drop = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(0, S(10), 0, S(8)) };
            dropTitle = new Label { Text = "폴더를 여기에 끌어다 놓으세요", AutoSize = true, ForeColor = Ink, BackColor = Color.Transparent,
                                    Font = new Font(Font.FontFamily, 13f, FontStyle.Bold) };
            dropHint  = new Label { Text = "카드를 통째로 복사한 폴더 · M4ROOT 폴더 · 영상과 XML만 모은 폴더 모두 됩니다", AutoSize = true,
                                    ForeColor = Muted, BackColor = Color.Transparent };
            pickButton = new Button { Text = "폴더 선택…", AutoSize = true, MinimumSize = new Size(S(110), S(32)), BackColor = Color.White, UseVisualStyleBackColor = true };
            pickButton.Click += (s, e) => { var p = FolderPicker.Pick(this, "정리할 폴더 선택"); if (p != null) LoadFolder(p); };
            drop.Controls.AddRange(new Control[] { dropTitle, dropHint, pickButton });
            drop.Paint  += PaintDrop;
            drop.Layout += (s, e) => {
                int w = drop.ClientSize.Width;
                int top = (drop.ClientSize.Height - (dropTitle.Height + S(4) + dropHint.Height + S(12) + pickButton.Height)) / 2;
                dropTitle.Location  = new Point((w - dropTitle.Width) / 2, top);
                dropHint.Location   = new Point((w - dropHint.Width) / 2, dropTitle.Bottom + S(4));
                pickButton.Location = new Point((w - pickButton.Width) / 2, dropHint.Bottom + S(12));
            };

            pathLabel = new PathLabel { Dock = DockStyle.Fill, ForeColor = Muted, Margin = Padding.Empty };

            // ── 미리보기 표 ──
            list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HeaderStyle = ColumnHeaderStyle.Nonclickable,
                                  MultiSelect = false, Margin = new Padding(0, S(4), 0, S(8)) };
            list.Columns.Add("촬영일");
            list.Columns.Add("만들 폴더");
            foreach (var c in new[] { "영상", "프록시", "XML", "사진", "용량" }) list.Columns.Add(c, 60, HorizontalAlignment.Center);
            list.Resize += (s, e) => FitColumns();

            // UseMnemonic = false: "S&Q"의 &를 단축키 표시로 바꾸지 않게
            notes = new Label { AutoSize = true, ForeColor = Muted, UseMnemonic = false, Margin = new Padding(0, 0, 0, S(8)) };

            // ── 옵션 ──
            xmlBox = Option("XML 파일(카메라 메타데이터)은 휴지통으로 보내기 — 끄면 날짜별 XML 폴더에 따로 모음", settings.DeleteXml, S(4));
            xmlBox.CheckedChanged += (s, e) => { settings.DeleteXml = xmlBox.Checked; settings.Save(); RefreshPreview(); };
            sizeBox = Option("정리한 날짜 폴더 이름 끝에 용량 붙이기 (예: 261004_DATA_26G)", settings.AddSize, S(4));
            sizeBox.CheckedChanged += (s, e) => { settings.AddSize = sizeBox.Checked; settings.Save(); RefreshPreview(); };
            new ToolTip().SetToolTip(sizeBox, "폴더 이름이 바뀌므로 Premiere로 가져오기 전에 정리하세요.\n카드를 포맷한 뒤에도 백업본과 용량을 비교할 수 있습니다.");
            cardBox = Option("정리 후 남은 카드 관리 파일(썸네일·카드 정보)도 휴지통으로 보내기", settings.TrashCard, S(10));
            cardBox.CheckedChanged += (s, e) => { settings.TrashCard = cardBox.Checked; settings.Save(); };

            // ── 버튼 ──
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            runButton = new Button { Text = "정리 시작", AutoSize = true, MinimumSize = new Size(S(130), S(38)), FlatStyle = FlatStyle.Flat,
                                     ForeColor = Color.White, BackColor = Disabled, Enabled = false, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(S(8), 0, 0, 0) };
            runButton.FlatAppearance.BorderSize = 0;
            runButton.EnabledChanged += (s, e) => runButton.BackColor = runButton.Enabled ? Accent : Disabled;
            runButton.Click += RunClick;
            undoButton = new Button { Text = "되돌리기", AutoSize = true, MinimumSize = new Size(S(100), S(38)), Visible = false, Margin = new Padding(S(8), 0, 0, 0) };
            undoButton.Click += UndoClick;
            openButton = new Button { Text = "폴더 열기", AutoSize = true, MinimumSize = new Size(S(100), S(38)), Visible = false, Margin = new Padding(S(8), 0, 0, 0) };
            openButton.Click += (s, e) => { if (lastRun != null) Process.Start("explorer.exe", "\"" + lastRun.Root + "\""); };
            buttons.Controls.AddRange(new Control[] { runButton, undoButton, openButton });

            // ── 상태 줄 ──
            var statusRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, Margin = new Padding(0, S(10), 0, 0) };
            statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(170)));
            status = new Label { Dock = DockStyle.Fill, AutoEllipsis = true, ForeColor = Muted, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false,
                                 Text = "폴더를 넣으면 무엇이 어디로 옮겨질지 먼저 보여 줍니다." };
            progress = new ProgressBar { Dock = DockStyle.Fill, Height = S(16), Visible = false, Margin = new Padding(S(8), S(4), 0, S(4)) };
            statusRow.Controls.Add(status, 0, 0);
            statusRow.Controls.Add(progress, 1, 0);

            // ── 전체 배치 ──
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(S(18), S(14), S(18), S(12)) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var rows = new Control[] { header, drop, pathLabel, list, notes, xmlBox, sizeBox, cardBox, buttons, statusRow };
            for (int i = 0; i < rows.Length; i++) {
                RowStyle style = rows[i] == drop ? new RowStyle(SizeType.Absolute, S(150))
                               : rows[i] == pathLabel ? new RowStyle(SizeType.Absolute, S(22))
                               : rows[i] == list ? new RowStyle(SizeType.Percent, 100)
                               : new RowStyle(SizeType.AutoSize);
                layout.RowStyles.Add(style);
                layout.Controls.Add(rows[i], 0, i);
            }
            layout.Resize += (s, e) => notes.MaximumSize = new Size(Math.Max(S(200), layout.ClientSize.Width - layout.Padding.Horizontal), 0);
            Controls.Add(layout);

            // 창 어디에 놓아도 받음
            foreach (var c in new Control[] { this, layout, drop, dropTitle, dropHint, list, pathLabel, notes }) HookDrop(c);

            Shown += (s, e) => { if (!string.IsNullOrWhiteSpace(this.startPath)) LoadFolder(this.startPath); };
            FormClosing += (s, e) => {
                if (busy) { e.Cancel = true; MessageBox.Show(this, "파일을 옮기는 중입니다. 끝난 뒤에 닫아 주세요.", Program.Title, MessageBoxButtons.OK, MessageBoxIcon.Information); }
            };
        }

        int S(int v) { return (int)Math.Round(v * k); }

        CheckBox Option(string text, bool on, int bottom)
        {
            return new CheckBox { Text = text, AutoSize = true, Checked = on, Margin = new Padding(0, 0, 0, bottom) };
        }

        // ── 끌어다 놓기 ──────────────────────────────────────────
        void HookDrop(Control c)
        {
            c.AllowDrop = true;
            c.DragEnter += (s, e) => {
                bool ok = !busy && e.Data.GetDataPresent(DataFormats.FileDrop);
                e.Effect = ok ? DragDropEffects.Copy : DragDropEffects.None;
                SetHover(ok);
            };
            c.DragLeave += (s, e) => SetHover(false);
            c.DragDrop  += (s, e) => {
                SetHover(false);
                var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (busy || paths == null || paths.Length == 0) return;
                string p = paths[0];
                if (File.Exists(p)) p = Path.GetDirectoryName(p);    // 파일을 넣으면 그 파일이 있는 폴더
                BeginInvoke(new Action(() => LoadFolder(p)));         // 탐색기를 바로 놓아 주고 처리
            };
        }

        void SetHover(bool on)
        {
            if (hover == on) return;
            hover = on;
            drop.Invalidate(true);
        }

        void PaintDrop(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = drop.ClientRectangle;
            r.Inflate(-S(2), -S(2));
            using (var path = RoundRect(r, S(10)))
            using (var fill = new SolidBrush(hover ? DropHover : DropBack))
            using (var pen = new Pen(hover ? Accent : DropLine, Math.Max(1f, 1.5f * k)) { DashStyle = DashStyle.Dash }) {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }
        }

        static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // ── 미리보기 ──────────────────────────────────────────────
        async void LoadFolder(string path)
        {
            if (busy) return;
            currentPath = path;
            lastRun = null;
            SetBusy(true, "폴더를 살펴보는 중…", 0);
            scan = await Task.Run(() => Organizer.Scan(path));
            SetBusy(false, null, 0);
            ShowPreview();
        }

        // 옵션을 바꾸면 미리보기의 폴더 이름·용량·안내를 다시 계산
        void RefreshPreview()
        {
            if (lastRun == null && scan != null && scan.Error == null && !busy) ShowPreview();
        }

        void ShowPreview()
        {
            openButton.Visible = undoButton.Visible = false;
            pathLabel.Text = scan.Root ?? currentPath;
            if (scan.Error != null) {
                FillList(new PlanItem[0]);
                SetNotes(Warn, scan.Error);
                runButton.Text = "정리 시작";
                runButton.Enabled = false;
                status.Text = "다른 폴더를 넣어 주세요.";
                return;
            }
            bool delXml = xmlBox.Checked;
            FillList(scan.Items);

            var warn = new List<string>();
            var info = new List<string>();
            if (scan.NoProxy.Count > 0)
                warn.Add(string.Format("프록시가 없는 원본 {0}개: {1}  (S&Q 슬로·퀵모션 촬영은 카메라가 프록시를 만들지 않음)",
                                       scan.NoProxy.Count, ShortList(scan.NoProxy)));
            int conflicts = scan.Conflicts + (delXml ? 0 : scan.XmlConflicts);
            if (conflicts > 0)
                warn.Add(string.Format("같은 이름의 파일이 이미 있어 {0}개는 건너뜁니다. 소니는 카드마다 번호가 C0001부터 다시 시작하니, 다른 카드는 다른 폴더에 정리하세요.", conflicts));
            if (scan.Unhandled.Count > 0)
                warn.Add(string.Format("정리 규칙이 없는 파일 {0}개는 그대로 둡니다 ({1}).", scan.Unhandled.Count, ExtSummary(scan.Unhandled)));
            int xml = scan.Count(Kind.Xml);
            if (xml > 0) info.Add(delXml ? string.Format("XML {0}개는 정리 후 휴지통으로 보냅니다.", xml)
                                         : string.Format("XML {0}개는 날짜별 XML 폴더(예: {1}_XML)에 따로 모읍니다.", xml, scan.Items.First(x => x.Kind == Kind.Xml).Day));
            if (scan.Count(Kind.Proxy) > 0) info.Add("프록시는 이름을 바꿔(C0001_Proxy.MP4) 넣습니다. Premiere에서 바로 프록시로 연결됩니다.");
            SetNotes(warn.Count > 0 ? Warn : Muted, string.Join("\n", warn.Concat(info)));

            runButton.Text = "정리 시작";
            runButton.Enabled = true;
            long bytes = scan.Items.Where(x => !(delXml && x.Kind == Kind.Xml)).Sum(x => x.Size);
            status.Text = string.Format("파일 {0}개({1})를 날짜 폴더 {2}개로 정리합니다. 아직 아무것도 바뀌지 않았습니다.",
                                        scan.Items.Count(x => !(delXml && x.Kind == Kind.Xml)), Bytes(bytes), scan.Days.Count);
        }

        // 미리보기: 만들어질 폴더 이름(옵션 반영) · 결과: 실제 폴더 이름
        string FolderName(string day)
        {
            string name;
            if (lastRun != null && lastRun.FinalNames.TryGetValue(day, out name)) return name;
            DayFolder df;
            if (scan != null && scan.Days.TryGetValue(day, out df)) return Organizer.PreviewName(df, sizeBox.Checked, xmlBox.Checked);
            return day + "_DATA";
        }

        static string Bytes(long b) { return Organizer.FormatSize(b) + "B"; }

        void FillList(IEnumerable<PlanItem> items)
        {
            bool delXml = lastRun == null && xmlBox.Checked;   // 미리보기에서 XML을 지우면 용량에서 뺌
            list.BeginUpdate();
            list.Items.Clear();
            var groups = items.GroupBy(x => x.Day).OrderBy(g => g.Key).ToList();
            foreach (var g in groups) list.Items.Add(Row(FormatDay(g.Key), FolderName(g.Key), g, delXml));
            if (groups.Count > 1) {
                var total = Row("합계", "", groups.SelectMany(g => g), delXml);
                total.Font = new Font(list.Font, FontStyle.Bold);
                total.BackColor = DropBack;
                list.Items.Add(total);
            }
            list.EndUpdate();
            FitColumns();
        }

        static ListViewItem Row(string day, string folder, IEnumerable<PlanItem> items, bool delXml)
        {
            var a = items.ToList();
            Func<Func<PlanItem, bool>, string> n = f => { int c = a.Count(f); return c > 0 ? c.ToString() : "–"; };
            return new ListViewItem(new[] {
                day, folder,
                n(x => x.Kind == Kind.Footage), n(x => x.Kind == Kind.Proxy), n(x => x.Kind == Kind.Xml),
                n(x => x.Kind == Kind.Jpg || x.Kind == Kind.Arw || x.Kind == Kind.Hif),
                Bytes(a.Where(x => !(delXml && x.Kind == Kind.Xml)).Sum(x => x.Size)) });
        }

        static string FormatDay(string yymmdd) { return "20" + yymmdd.Substring(0, 2) + "-" + yymmdd.Substring(2, 2) + "-" + yymmdd.Substring(4, 2); }

        static string ShortList(List<string> items)
        {
            string s = string.Join(", ", items.Take(6));
            return items.Count > 6 ? s + string.Format(" 외 {0}개", items.Count - 6) : s;
        }

        static string ExtSummary(IEnumerable<string> files)
        {
            return string.Join(" · ", files.GroupBy(f => Path.GetExtension(f).TrimStart('.').ToUpperInvariant())
                                           .OrderByDescending(g => g.Count())
                                           .Select(g => (g.Key == "" ? "확장자 없음" : g.Key) + " " + g.Count()));
        }

        void FitColumns()
        {
            if (list.Columns.Count < 7) return;
            int w = list.ClientSize.Width, a = S(100), b = S(180);
            int rest = Math.Max(S(50), (w - a - b) / 5);
            list.Columns[0].Width = a;
            list.Columns[1].Width = b;
            for (int i = 2; i < 7; i++) list.Columns[i].Width = rest;
        }

        void SetNotes(Color color, string text) { notes.ForeColor = color; notes.Text = text; }

        // ── 정리 ──────────────────────────────────────────────────
        async void RunClick(object sender, EventArgs e)
        {
            if (busy || scan == null || scan.Error != null) return;
            var sc = scan;
            bool delXml = xmlBox.Checked, addSize = sizeBox.Checked, trashCard = cardBox.Checked;
            SetBusy(true, "정리하는 중…", sc.Items.Count);
            var report = new Progress<int>(v => progress.Value = Math.Min(v, progress.Maximum));
            lastRun = await Task.Run(() => Organizer.Run(sc, delXml, trashCard, addSize, report));
            SetBusy(false, null, 0);
            ShowResult(sc, delXml);
        }

        void ShowResult(ScanResult sc, bool delXml)
        {
            var r = lastRun;
            FillList(r.Moved);
            var lines = new List<string>();
            bool verified = r.VerifyFailed == 0 && r.Verified == r.Moved.Count;
            bool problem = !verified || r.Skipped > 0 || r.Failed > 0 || r.XmlTrashFailed > 0 || r.RenameFailed > 0 || r.RenamedExisting.Count > 0 || r.TrashFailed.Count > 0;
            if (!verified) lines.Add(string.Format("확인 결과 {0}개 파일이 제자리에 없거나 크기가 다릅니다. [폴더 열기]로 확인해 주세요.", r.VerifyFailed));
            if (r.Skipped > 0) lines.Add(string.Format("같은 이름의 파일이 이미 있어 {0}개는 그대로 뒀습니다.", r.Skipped));
            if (r.Failed > 0)  lines.Add(string.Format("{0}개는 옮기지 못했습니다. 다른 프로그램(Premiere, 동기화 등)이 쓰고 있을 수 있습니다.", r.Failed));
            foreach (var renamed in r.RenamedExisting)
                lines.Add("기존 폴더에 합치면서 용량이 바뀌었습니다: " + renamed + "  (이미 Premiere로 가져왔다면 링크를 다시 연결해야 합니다)");
            if (r.RenameFailed > 0) lines.Add(string.Format("폴더 {0}개는 이름에 용량을 붙이지 못했습니다 (탐색기·동기화 프로그램이 사용 중이거나 같은 이름이 있음).", r.RenameFailed));
            if (delXml && r.XmlTrashed > 0) lines.Add(string.Format("XML {0}개는 휴지통으로 보냈습니다.", r.XmlTrashed));
            if (r.XmlTrashFailed > 0) lines.Add(string.Format("XML {0}개는 휴지통으로 보내지 못했습니다.", r.XmlTrashFailed));
            if (!delXml) { int moved = r.Moved.Count(x => x.Kind == Kind.Xml); if (moved > 0) lines.Add(string.Format("XML {0}개는 날짜별 XML 폴더에 모았습니다.", moved)); }
            if (r.Trashed.Count > 0) lines.Add("남은 카드 관리 파일은 휴지통으로 보냈습니다: " + string.Join(", ", r.Trashed));
            if (r.Kept.Count > 0) lines.Add("남은 파일이 있어 휴지통으로 보내지 않은 폴더: " + string.Join(", ", r.Kept.Select(k => {
                string why;
                return r.KeptBecause.TryGetValue(k, out why) ? k + " (" + why + " 등)" : k;
            })));
            if (r.TrashFailed.Count > 0)
                lines.Add("휴지통으로 보내지 못한 폴더: " + string.Join(", ", r.TrashFailed) + "  (탐색기 창 등 다른 프로그램이 열고 있었을 수 있습니다. 창을 닫고 직접 지워도 됩니다)");
            if (sc.Unhandled.Count > 0) lines.Add(string.Format("정리 규칙이 없는 파일 {0}개는 그대로 뒀습니다 ({1}).", sc.Unhandled.Count, ExtSummary(sc.Unhandled)));
            if (sc.NoProxy.Count > 0) lines.Add(string.Format("프록시가 없는 원본 {0}개: {1}", sc.NoProxy.Count, ShortList(sc.NoProxy)));
            SetNotes(problem ? Warn : Muted, string.Join("\n", lines));

            runButton.Text = "정리 완료";
            runButton.Enabled = false;
            openButton.Visible = true;
            undoButton.Visible = r.Moved.Count > 0;
            status.ForeColor = verified ? Ok : Warn;
            status.Text = verified
                ? string.Format("✔ 완료 — 파일 {0}개({1})를 옮겼고, 정리 전과 개수·용량이 같은 것을 확인했습니다.", r.Verified, Bytes(r.VerifiedBytes))
                : string.Format("완료 — 파일 {0}개를 옮겼지만 {1}개는 확인되지 않았습니다.", r.Moved.Count, r.VerifyFailed);
        }

        async void UndoClick(object sender, EventArgs e)
        {
            var run = lastRun;
            if (busy || run == null) return;
            string msg = "정리하기 전 위치로 파일을 되돌릴까요?";
            if (run.XmlTrashed > 0 || run.Trashed.Count > 0)
                msg += "\n\n휴지통으로 보낸 XML·카드 관리 파일은 자동으로 돌아오지 않습니다. 필요하면 휴지통에서 직접 복원하세요.";
            if (MessageBox.Show(this, msg, Program.Title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            SetBusy(true, "되돌리는 중…", 0);
            var u = await Task.Run(() => Organizer.Undo(run));
            scan = await Task.Run(() => Organizer.Scan(run.Root));
            lastRun = null;
            SetBusy(false, null, 0);
            ShowPreview();
            status.Text = u.Failed == 0
                ? string.Format("되돌렸습니다 — 파일 {0}개가 원래 위치로 돌아갔습니다.", u.Restored)
                : string.Format("파일 {0}개를 되돌렸고, {1}개는 되돌리지 못했습니다 (이미 다른 곳으로 옮겼거나 사용 중).", u.Restored, u.Failed);
        }

        void SetBusy(bool on, string text, int max)
        {
            busy = on;
            UseWaitCursor = on;
            pickButton.Enabled = !on;
            xmlBox.Enabled = sizeBox.Enabled = cardBox.Enabled = !on;
            status.ForeColor = Muted;
            if (on) { runButton.Enabled = false; undoButton.Enabled = false; openButton.Enabled = false; }
            else { undoButton.Enabled = true; openButton.Enabled = true; }
            progress.Visible = on && max > 0;
            if (max > 0) { progress.Maximum = max; progress.Value = 0; }
            if (text != null) status.Text = text;
        }

        void ShowAbout()
        {
            MessageBox.Show(this,
                Program.Title + " (베타)\n\n" +
                "소니 카메라로 찍은 영상(MP4·프록시·XML)과 사진(JPG·ARW·HIF)을 촬영일별 폴더로 정리하는 무료 프로그램입니다.\n\n" +
                "· 촬영일은 영상마다 붙어 있는 XML의 촬영 시작 시각으로 정합니다\n" +
                "· 파일을 넣은 폴더 안에서 옮기기만 합니다 (되돌리기 가능)\n" +
                "· 인터넷에 연결하지 않으며, 어떤 정보도 수집하지 않습니다\n\n" +
                "만든 사람: 이중필름 (비영리)\n" +
                "Sony와는 무관한 개인 제작 프로그램입니다.",
                "정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    // 긴 경로는 가운데를 줄여 폴더 이름이 보이게
    class PathLabel : Label
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor,
                TextFormatFlags.PathEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
    }
}
