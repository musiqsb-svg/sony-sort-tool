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
    //  머리말 / 끌어다 놓는 곳 (폴더를 넣으면 작아짐) / 날짜별 표 / 알림 / 옵션 / 상태 · 버튼
    class MainForm : Form
    {
        readonly Settings settings = Settings.Load();
        readonly string startPath;
        readonly TableLayoutPanel layout;
        readonly DropZone drop;
        readonly Card tableCard, noteCard;
        readonly DayTable list;
        readonly Label status;
        readonly PillButton runButton, undoButton, openButton;
        readonly Toggle xmlBox, sizeBox, cardBox;
        readonly SlimProgress progress;
        bool busy;
        string currentPath;
        ScanResult scan;
        RunResult lastRun;

        const int DropRow = 1, NoteRow = 3;

        static int S(float v) { return Theme.S(v); }

        public MainForm(string startPath)
        {
            this.startPath = startPath;
            Text = Program.Title;
            Font = Theme.Font(9.75f);
            BackColor = Theme.Window;
            AutoScaleMode = AutoScaleMode.None;   // 크기는 S()로 직접 배율 적용
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(S(800), S(720));
            MinimumSize = new Size(S(680), S(600));
            FitToScreen();
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }

            // ── 머리말 ──
            var header = new Header { Dock = DockStyle.Fill, Margin = Padding.Empty };
            header.About.LinkClicked += (s, e) => { using (var f = new AboutForm()) f.ShowDialog(this); };

            // ── 끌어다 놓는 곳 ──
            drop = new DropZone { Dock = DockStyle.Fill, Margin = new Padding(0, S(14), 0, S(12)) };
            drop.Pick.Click += (s, e) => { var p = FolderPicker.Pick(this, "정리할 폴더 선택"); if (p != null) LoadFolder(p); };

            // ── 날짜별 표 ──
            list = new DayTable { Dock = DockStyle.Fill, Visible = false };
            list.Columns.Add("촬영일");
            list.Columns.Add("만들 폴더");
            foreach (var c in new[] { "영상", "프록시", "XML", "사진", "용량" }) list.Columns.Add(c, 60, HorizontalAlignment.Center);
            list.Resize += (s, e) => FitColumns();
            tableCard = new Card { Dock = DockStyle.Fill, Padding = new Padding(S(10), S(8), S(10), S(8)), Margin = new Padding(0, 0, 0, S(12)),
                                   Placeholder = "폴더를 넣으면 어떤 파일이 어느 날짜 폴더로 갈지 여기에 먼저 보여 드립니다.\n[정리 시작]을 누르기 전에는 아무것도 옮기지 않습니다." };
            tableCard.Controls.Add(list);

            // ── 알림 (경고·안내·결과) ──
            noteCard = new Card { Dock = DockStyle.Fill, Padding = new Padding(S(18), S(10), S(14), S(10)), Margin = new Padding(0, 0, 0, S(12)),
                                  Radius = S(10), Visible = false };

            // ── 옵션 ──
            xmlBox = new Toggle { Text = "XML(카메라 메타데이터)은 휴지통으로", Note = "끄면 날짜별 XML 폴더에 따로 모읍니다", Checked = settings.DeleteXml };
            xmlBox.CheckedChanged += (s, e) => { settings.DeleteXml = xmlBox.Checked; settings.Save(); RefreshPreview(); };
            sizeBox = new Toggle { Text = "날짜 폴더 이름에 용량 붙이기", Note = "예: 261004_DATA_26G", Checked = settings.AddSize };
            sizeBox.CheckedChanged += (s, e) => { settings.AddSize = sizeBox.Checked; settings.Save(); RefreshPreview(); };
            new ToolTip().SetToolTip(sizeBox, "폴더 이름이 바뀌므로 Premiere로 가져오기 전에 정리하세요.\n카드를 포맷한 뒤에도 백업본과 용량을 비교할 수 있습니다.");
            cardBox = new Toggle { Text = "남은 카드 관리 파일도 휴지통으로", Note = "썸네일·카드 정보 (영상·사진이 섞인 폴더는 남김)", Checked = settings.TrashCard };
            cardBox.CheckedChanged += (s, e) => { settings.TrashCard = cardBox.Checked; settings.Save(); };
            var options = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true,
                                                Margin = new Padding(0, 0, 0, S(8)), BackColor = Theme.Window };
            foreach (var t in new[] { xmlBox, sizeBox, cardBox }) { t.Margin = new Padding(0, 0, 0, S(2)); options.Controls.Add(t); }

            // ── 상태 · 버튼 ──
            runButton = new PillButton { Text = "정리 시작", Primary = true, Enabled = false, Font = Theme.Font(10f, FontStyle.Bold),
                                         MinimumSize = new Size(S(140), S(42)), Margin = new Padding(S(8), 0, 0, 0) };
            runButton.Click += RunClick;
            undoButton = new PillButton { Text = "되돌리기", Visible = false, MinimumSize = new Size(S(104), S(42)), Margin = new Padding(S(8), 0, 0, 0) };
            undoButton.Click += UndoClick;
            openButton = new PillButton { Text = "폴더 열기", Visible = false, MinimumSize = new Size(S(104), S(42)), Margin = new Padding(S(8), 0, 0, 0) };
            openButton.Click += (s, e) => { if (lastRun != null) Process.Start("explorer.exe", "\"" + lastRun.Root + "\""); };
            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, WrapContents = false, Anchor = AnchorStyles.Right,
                                                Margin = Padding.Empty, BackColor = Theme.Window };
            buttons.Controls.AddRange(new Control[] { runButton, undoButton, openButton });

            status = new Label { Dock = DockStyle.Fill, AutoEllipsis = true, ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false,
                                 Text = "폴더를 넣으면 무엇이 어디로 옮겨질지 먼저 보여 줍니다." };
            progress = new SlimProgress { Dock = DockStyle.Bottom, Height = S(4), Visible = false };
            var statusBox = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, S(6), S(12), S(6)), BackColor = Theme.Window };
            statusBox.Controls.Add(status);      // Fill을 먼저 넣어야 Bottom 막대 자리가 남음
            statusBox.Controls.Add(progress);

            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, BackColor = Theme.Window };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            footer.Controls.Add(statusBox, 0, 0);
            footer.Controls.Add(buttons, 1, 0);

            // ── 전체 배치 ──
            layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(S(22), S(16), S(22), S(14)), BackColor = Theme.Window };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(48)));    // 머리말
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(202)));   // 끌어다 놓는 곳 (여백 포함)
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));       // 표
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));        // 알림 (글자에 맞춰 바꿈)
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));           // 옵션
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(54)));    // 상태 · 버튼
            var rows = new Control[] { header, drop, tableCard, noteCard, options, footer };
            for (int i = 0; i < rows.Length; i++) layout.Controls.Add(rows[i], 0, i);
            layout.Resize += (s, e) => FitNotes();
            Controls.Add(layout);

            // 창 어디에 놓아도 받음
            foreach (var c in new Control[] { this, layout, header, drop, tableCard, list, noteCard, options, footer, statusBox, status }) HookDrop(c);

            Shown += (s, e) => { if (!string.IsNullOrWhiteSpace(this.startPath)) LoadFolder(this.startPath); };
            FormClosing += (s, e) => {
                if (busy) { e.Cancel = true; MessageBox.Show(this, "파일을 옮기는 중입니다. 끝난 뒤에 닫아 주세요.", Program.Title, MessageBoxButtons.OK, MessageBoxIcon.Information); }
            };
        }

        // 작은 노트북 화면(150% 배율 등)에서도 창이 화면 밖으로 나가지 않게
        void FitToScreen()
        {
            var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            MinimumSize = new Size(Math.Min(MinimumSize.Width, wa.Width), Math.Min(MinimumSize.Height, wa.Height));
            Size = new Size(Math.Min(Width, wa.Width), Math.Min(Height, wa.Height));
        }

        // ── 끌어다 놓기 ──────────────────────────────────────────
        void HookDrop(Control c)
        {
            c.AllowDrop = true;
            c.DragEnter += (s, e) => {
                bool ok = !busy && e.Data.GetDataPresent(DataFormats.FileDrop);
                e.Effect = ok ? DragDropEffects.Copy : DragDropEffects.None;
                drop.Hover = ok;
            };
            c.DragLeave += (s, e) => drop.Hover = false;
            c.DragDrop  += (s, e) => {
                drop.Hover = false;
                var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (busy || paths == null || paths.Length == 0) return;
                string p = paths[0];
                if (File.Exists(p)) p = Path.GetDirectoryName(p);    // 파일을 넣으면 그 파일이 있는 폴더
                BeginInvoke(new Action(() => LoadFolder(p)));         // 탐색기를 바로 놓아 주고 처리
            };
        }

        // ── 미리보기 ──────────────────────────────────────────────
        async void LoadFolder(string path)
        {
            if (busy) return;
            currentPath = path;
            lastRun = null;
            ShowFolder(path);
            SetBusy(true, "폴더를 살펴보는 중…", 0);
            scan = await Task.Run(() => Organizer.Scan(path));
            SetBusy(false, null, 0);
            ShowPreview();
        }

        // 폴더를 넣으면 끌어다 놓는 곳을 작게 줄이고 표를 넓힘
        void ShowFolder(string path)
        {
            drop.ShowFolder(path);
            layout.RowStyles[DropRow].Height = path == null ? S(202) : S(100);
        }

        // 옵션을 바꾸면 미리보기의 폴더 이름·용량·안내를 다시 계산
        void RefreshPreview()
        {
            if (lastRun == null && scan != null && scan.Error == null && !busy) ShowPreview();
        }

        void ShowPreview()
        {
            openButton.Visible = undoButton.Visible = false;
            ShowFolder(scan.Root ?? currentPath);
            status.ForeColor = Theme.Muted;
            if (scan.Error != null) {
                FillList(new PlanItem[0]);
                SetNotes(Note.Warn, scan.Error);
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
            SetNotes(warn.Count > 0 ? Note.Warn : Note.Info, string.Join("\n", warn.Concat(info)));

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
                total.Tag = DayTable.Total;
                list.Items.Add(total);
            }
            list.EndUpdate();
            list.Visible = groups.Count > 0;
            tableCard.Invalidate();
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

        // 칸 너비를 표 너비에 맞춤. 줄이 넘쳐 세로 막대가 생길 자리는 미리 빼서 가로 막대가 생기지 않게
        void FitColumns()
        {
            if (list.Columns.Count < 7) return;
            int rowH = list.SmallImageList.ImageSize.Height;    // 줄 높이 (표를 처음 그리는 중에는 줄 위치를 물을 수 없음)
            bool scroll = (list.Items.Count + 1) * rowH + S(4) > list.Height;
            int w = list.Width - (scroll ? SystemInformation.VerticalScrollBarWidth : 0), a = S(108), b = S(200);
            int rest = Math.Max(S(50), (w - a - b) / 5);
            list.Columns[0].Width = a;
            list.Columns[1].Width = w - a - rest * 5 > b ? w - a - rest * 5 : b;
            for (int i = 2; i < 7; i++) list.Columns[i].Width = rest;
        }

        // ── 알림 ──────────────────────────────────────────────────
        enum Note { Info, Warn, Ok }

        void SetNotes(Note kind, string text)
        {
            noteCard.Message = text;
            noteCard.Fill = kind == Note.Warn ? Theme.WarnSoft : kind == Note.Ok ? Theme.OkSoft : Theme.Surface;
            noteCard.Border = kind == Note.Warn ? Color.FromArgb(253, 230, 138) : kind == Note.Ok ? Color.FromArgb(187, 247, 208) : Theme.Line;
            noteCard.Bar = kind == Note.Warn ? Theme.WarnLine : kind == Note.Ok ? Theme.OkLine : Theme.LineStrong;
            noteCard.MessageColor = kind == Note.Warn ? Theme.Warn : Theme.Body;
            noteCard.Visible = !string.IsNullOrEmpty(text);
            FitNotes();
            noteCard.Invalidate();
        }

        void FitNotes()
        {
            int width = layout.ClientSize.Width - layout.Padding.Horizontal;
            int h = noteCard.Visible ? noteCard.HeightFor(width) + noteCard.Margin.Vertical : 0;
            if ((int)layout.RowStyles[NoteRow].Height != h) layout.RowStyles[NoteRow].Height = h;
        }

        // ── 정리 ──────────────────────────────────────────────────
        async void RunClick(object sender, EventArgs e)
        {
            if (busy || scan == null || scan.Error != null) return;
            var sc = scan;
            bool delXml = xmlBox.Checked, addSize = sizeBox.Checked, trashCard = cardBox.Checked;
            SetBusy(true, "정리하는 중…", sc.Items.Count);
            var report = new Progress<int>(v => progress.Value = v);
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
            // 결과 요약은 알림 첫 줄에 (상태 줄은 짧게)
            if (verified && r.Moved.Count > 0)
                lines.Insert(0, string.Format("파일 {0}개({1})를 옮겼고, 정리 전과 개수·용량이 같은 것을 확인했습니다.", r.Verified, Bytes(r.VerifiedBytes)));
            SetNotes(problem ? Note.Warn : Note.Ok, string.Join("\n", lines));

            runButton.Text = "정리 완료";
            runButton.Enabled = false;
            openButton.Visible = true;
            undoButton.Visible = r.Moved.Count > 0;
            status.ForeColor = verified && r.Moved.Count > 0 ? Theme.Ok : Theme.Warn;
            status.Text = r.Moved.Count == 0 ? "옮긴 파일이 없습니다."
                        : verified ? string.Format("✔ 정리를 마쳤습니다 — 날짜 폴더 {0}개", r.Moved.Select(x => x.Day).Distinct().Count())
                        : string.Format("정리를 마쳤지만 {0}개는 확인되지 않았습니다.", r.VerifyFailed);
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

        // max > 0: 정리 중 (진행 막대에 개수), 0: 끝을 모르는 작업 (흐르는 막대)
        void SetBusy(bool on, string text, int max)
        {
            busy = on;
            UseWaitCursor = on;
            drop.Pick.Enabled = !on;
            xmlBox.Enabled = sizeBox.Enabled = cardBox.Enabled = !on;
            status.ForeColor = Theme.Muted;
            if (on) { runButton.Enabled = false; undoButton.Enabled = false; openButton.Enabled = false; }
            else { undoButton.Enabled = true; openButton.Enabled = true; }
            progress.Visible = on;
            progress.Marquee = on && max == 0;
            if (max > 0) { progress.Maximum = max; progress.Value = 0; }
            if (text != null) status.Text = text;
        }
    }

    // 창 맨 위: 아이콘 · 이름 · 버전 · 한 줄 설명 · [정보]
    class Header : Control
    {
        readonly Icon icon = Theme.AppIcon(Theme.S(40));
        public readonly LinkLabel About;

        public Header()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            About = new LinkLabel { Text = "정보", AutoSize = true, LinkColor = Theme.Muted, ActiveLinkColor = Product.Accent,
                                    LinkBehavior = LinkBehavior.HoverUnderline, BackColor = Theme.Window };
            Controls.Add(About);
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            About.Location = new Point(Width - About.Width, (Height - About.Height) / 2);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Window);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int ic = Theme.S(40);
            if (icon != null) g.DrawIcon(icon, new Rectangle(0, (Height - ic) / 2, ic, ic));
            int x = ic + Theme.S(12);
            using (var title = Theme.Font(13f, FontStyle.Bold))
            using (var chip = Theme.Font(8f, FontStyle.Bold))
            using (var sub = Theme.Font(9f)) {
                string line = Product.Summary + "  ·  " + Product.Unofficial;   // 한 줄 설명 + 정식 프로그램이 아니라는 안내
                var t = Theme.Measure(Product.Name, title);
                var s = Theme.Measure(line, sub);
                int top = (Height - t.Height - s.Height + Theme.S(1)) / 2;
                Theme.Text(g, Product.Name, title, new Rectangle(x, top, t.Width + Theme.S(2), t.Height), Theme.Ink, TextFormatFlags.SingleLine);
                // 버전 표시 (예: 1.5 베타)
                string v = Product.Version + (Product.Stage.Length > 0 ? " " + Product.Stage : "");
                var c = Theme.Measure(v, chip);
                var cr = new Rectangle(x + t.Width + Theme.S(6), top + (t.Height - c.Height - Theme.S(4)) / 2 + Theme.S(1), c.Width + Theme.S(10), c.Height + Theme.S(4));
                using (var path = Theme.Round(cr, cr.Height / 2f)) using (var b = new SolidBrush(Product.AccentSoft)) g.FillPath(b, path);
                Theme.Text(g, v, chip, cr, Product.AccentDeep, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                Theme.Text(g, line, sub, new Rectangle(x + Theme.S(1), top + t.Height - Theme.S(1), Width - x - About.Width - Theme.S(8), s.Height),
                           Theme.Muted, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            }
        }
    }

    // 폴더를 끌어다 놓는 곳. 처음엔 크게 (그림 + 안내 + [폴더 선택…]), 폴더를 넣은 뒤엔 작게 (폴더 이름·경로 + [다른 폴더…])
    class DropZone : Panel
    {
        public readonly PillButton Pick;
        readonly Image art = Theme.Art("drop.png");
        Bitmap big, small;
        bool hover;
        string folder;

        const string Title = "복사해 둔 카드 폴더를 여기에 끌어다 놓으세요";
        const string Hint  = "카드를 통째로 복사한 폴더 · M4ROOT 폴더 · 영상과 XML만 모은 폴더 모두 됩니다";

        public DropZone()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;
            Pick = new PillButton { Text = "폴더 선택…", AutoSize = true, MinimumSize = new Size(Theme.S(112), Theme.S(36)) };
            Controls.Add(Pick);
            if (art != null) {
                big = Theme.Resize(art, Theme.S(124), Theme.S(124));
                small = Theme.Resize(art, Theme.S(44), Theme.S(44));
            }
        }

        bool Compact { get { return folder != null; } }

        public bool Hover
        {
            set { if (hover == value) return; hover = value; Invalidate(); Pick.Invalidate(); }
        }

        public void ShowFolder(string path)
        {
            folder = path;
            Pick.Text = Compact ? "다른 폴더…" : "폴더 선택…";
            PerformLayout();
            Invalidate();
        }

        Rectangle ArtRect, TitleRect, HintRect;

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (Pick == null) return;
            int pad = Theme.S(18);
            using (var tf = Theme.Font(Compact ? 11f : 13f, FontStyle.Bold)) {
                if (!Compact) {
                    var t = Theme.Measure(Title, tf);
                    var h = Theme.Measure(Hint, Font);
                    int a = big != null ? big.Width : 0, gap = big != null ? Theme.S(26) : 0;
                    int textW = Math.Max(t.Width, Math.Max(h.Width, Pick.Width));
                    int x0 = Math.Max(pad, (Width - a - gap - textW) / 2);
                    ArtRect = new Rectangle(x0, (Height - a) / 2, a, a);
                    int tx = x0 + a + gap;
                    int blockH = t.Height + Theme.S(4) + h.Height + Theme.S(14) + Pick.Height;
                    int top = (Height - blockH) / 2;
                    TitleRect = new Rectangle(tx, top, Math.Min(t.Width + Theme.S(4), Width - tx - pad), t.Height);
                    HintRect = new Rectangle(tx, TitleRect.Bottom + Theme.S(4), Math.Min(h.Width + Theme.S(4), Width - tx - pad), h.Height);
                    Pick.Location = new Point(tx, HintRect.Bottom + Theme.S(14));
                } else {
                    int a = small != null ? small.Width : 0;
                    ArtRect = new Rectangle(pad, (Height - a) / 2, a, a);
                    Pick.Location = new Point(Width - pad - Pick.Width, (Height - Pick.Height) / 2);
                    int tx = ArtRect.Right + (a > 0 ? Theme.S(14) : 0);
                    int tw = Math.Max(0, Pick.Left - Theme.S(16) - tx);
                    var t = Theme.Measure("가", tf);
                    var h = Theme.Measure("가", Font);
                    int top = (Height - t.Height - h.Height) / 2;
                    TitleRect = new Rectangle(tx, top, tw, t.Height);
                    HintRect = new Rectangle(tx, TitleRect.Bottom, tw, h.Height);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Window);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(1, 1, Width - 3, Height - 3);
            using (var path = Theme.Round(r, Theme.S(14))) {
                using (var b = new SolidBrush(hover ? Product.AccentSoft : Theme.Surface)) g.FillPath(b, path);
                Color line = hover ? Product.Accent : Compact ? Theme.Line : Theme.LineStrong;
                using (var p = new Pen(line, Math.Max(1f, 1.5f * Theme.Scale))) {
                    if (hover || !Compact) p.DashStyle = DashStyle.Dash;
                    g.DrawPath(p, path);
                }
            }
            var img = Compact ? small : big;
            if (img != null) g.DrawImage(img, ArtRect);
            using (var tf = Theme.Font(Compact ? 11f : 13f, FontStyle.Bold)) {
                if (!Compact) {
                    Theme.Text(g, Title, tf, TitleRect, Theme.Ink, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                    Theme.Text(g, Hint, Font, HintRect, Theme.Muted, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                } else {
                    string name = Path.GetFileName(folder.TrimEnd('\\'));
                    if (name.Length == 0) name = folder;
                    Theme.Text(g, name, tf, TitleRect, Theme.Ink, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                    Theme.Text(g, folder, Font, HintRect, Theme.Muted, TextFormatFlags.SingleLine | TextFormatFlags.PathEllipsis);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { if (big != null) big.Dispose(); if (small != null) small.Dispose(); if (art != null) art.Dispose(); }
            base.Dispose(disposing);
        }
    }

    // 날짜별 표: 줄을 넉넉히, 머리줄은 흐리게, 합계 줄은 굵게 (ListView 직접 그리기)
    class DayTable : ListView
    {
        public static readonly object Total = new object();

        public DayTable()
        {
            OwnerDraw = true;
            DoubleBuffered = true;
            View = View.Details;
            FullRowSelect = true;
            MultiSelect = false;
            HeaderStyle = ColumnHeaderStyle.Nonclickable;
            BorderStyle = BorderStyle.None;
            BackColor = Theme.Surface;
            SmallImageList = new ImageList { ImageSize = new Size(1, Theme.S(34)) };   // 줄 높이
        }

        TextFormatFlags Align(int column)
        {
            return (column < 2 ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter)
                   | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
        }

        protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e)
        {
            var g = e.Graphics;
            g.FillRectangle(Brushes.White, e.Bounds);
            var r = e.Bounds;
            if (e.ColumnIndex < 2) { r.X += Theme.S(8); r.Width -= Theme.S(8); }
            using (var f = Theme.Font(8.75f)) Theme.Text(g, e.Header.Text, f, r, Theme.Muted, Align(e.ColumnIndex));
            using (var p = new Pen(Theme.Line)) g.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        }

        protected override void OnDrawItem(DrawListViewItemEventArgs e) { }   // 칸마다 그림 (OnDrawSubItem)

        protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e)
        {
            var g = e.Graphics;
            bool total = e.Item.Tag == Total;
            Color back = total ? Theme.Subtle : e.Item.Selected ? Theme.Mix(Theme.Surface, Product.AccentSoft, 0.7f) : Theme.Surface;
            using (var b = new SolidBrush(back)) g.FillRectangle(b, e.Bounds);
            var r = e.Bounds;
            if (e.ColumnIndex < 2) { r.X += Theme.S(8); r.Width -= Theme.S(8); }
            string text = e.SubItem.Text;
            Color color = text == "–" ? Theme.Faint : e.ColumnIndex < 2 || total ? Theme.Ink : Theme.Body;
            if (total) using (var f = new Font(Font, FontStyle.Bold)) Theme.Text(g, text, f, r, color, Align(e.ColumnIndex));
            else Theme.Text(g, text, Font, r, color, Align(e.ColumnIndex));
            using (var p = new Pen(Theme.Line)) g.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        }
    }

    // [정보] 창: 만든 사람 · 문의 · 소스 코드
    class AboutForm : Form
    {
        public AboutForm()
        {
            Text = "정보";
            Font = Theme.Font(9.75f);
            BackColor = Theme.Surface;
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            int w = Theme.S(440), pad = Theme.S(26);

            var icon = Theme.AppIcon(Theme.S(48));
            var head = new Label { BackColor = Theme.Surface };
            head.Paint += (s, e) => {
                if (icon != null) e.Graphics.DrawIcon(icon, new Rectangle(0, 0, Theme.S(48), Theme.S(48)));
                using (var f = Theme.Font(14f, FontStyle.Bold)) Theme.Text(e.Graphics, Product.Name, f, new Rectangle(Theme.S(62), Theme.S(2), w, Theme.S(28)), Theme.Ink, TextFormatFlags.SingleLine);
                string v = "버전 " + Product.Version + (Product.Stage.Length > 0 ? " (" + Product.Stage + ")" : "");
                Theme.Text(e.Graphics, v, Font, new Rectangle(Theme.S(62), Theme.S(28), w, Theme.S(22)), Theme.Muted, TextFormatFlags.SingleLine);
            };
            var stack = new Stack(this, pad, w - pad * 2, pad);
            stack.Add(head, Theme.S(52), Theme.S(16));
            stack.Text("소니 카메라로 찍은 영상(MP4·프록시·XML)과 사진(JPG·ARW·HIF)을 촬영일별 폴더로 정리하는 무료 프로그램입니다.", Font, Theme.Body, Theme.S(10));
            stack.Text("· 촬영일은 영상마다 붙어 있는 XML의 촬영 시작 시각으로 정합니다\n· 넣은 폴더 안에서 옮기기만 합니다 (되돌리기 가능)\n· 인터넷에 연결하지 않으며, 어떤 정보도 수집하지 않습니다",
                       Font, Theme.Muted, Theme.S(16));
            Link(stack, "만든 사람", "이중필름 (유튜브)", Product.Channel);
            Link(stack, "문의·오류 제보", Product.Contact, "mailto:" + Product.Contact);
            Link(stack, "소스 코드", "GitHub", Product.Home);
            stack.Y += Theme.S(8);
            stack.Text("비영리 무료 배포 · " + Product.Notice, Theme.Font(8.75f), Theme.Faint, Theme.S(18));
            var ok = new PillButton { Text = "확인", Primary = true, DialogResult = DialogResult.OK };
            stack.Add(ok, w - pad - Theme.S(100), Theme.S(100), Theme.S(38), pad);
            AcceptButton = CancelButton = ok;
            ClientSize = new Size(w, stack.Y);
        }

        void Link(Stack stack, string label, string text, string url)
        {
            var row = new Panel { BackColor = Theme.Surface };
            row.Controls.Add(new Label { Text = label, AutoSize = false, Width = Theme.S(110), Height = Theme.S(22), ForeColor = Theme.Muted, BackColor = Theme.Surface });
            var link = new LinkLabel { Text = text, AutoSize = true, Left = Theme.S(110), LinkColor = Product.AccentDeep, ActiveLinkColor = Product.Accent,
                                       LinkBehavior = LinkBehavior.HoverUnderline, BackColor = Theme.Surface };
            link.LinkClicked += (s, e) => { try { Process.Start(url); } catch (Exception) { } };
            row.Controls.Add(link);
            stack.Add(row, Theme.S(24), Theme.S(2));
        }
    }
}
