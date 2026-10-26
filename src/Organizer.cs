// 소니 카드 정리 로직 (화면과 분리: 테스트 프로그램도 이 파일을 그대로 씀)
//
//  넣은 폴더\261004_DATA\261004_Footage            원본 영상   M4ROOT\CLIP\C0561.MP4
//                       \261004_Proxy              프록시      M4ROOT\SUB\C0561S03.MP4 → C0561_Proxy.MP4
//                       \261004_XML                메타데이터  M4ROOT\CLIP\C0561M01.XML  (XML을 지우지 않는 옵션일 때만)
//                       \261004_Photo\jpg, arw, hif 사진      DCIM\100MSDCF\DSC09566.JPG
//
//  소니 카드의 규칙
//  - 영상 파일명(C0001)에는 날짜가 없음. 촬영일은 짝인 XML(C0001M01.XML)의 CreationDate
//    = 촬영 시작 시각, 카메라에 설정된 시간대 그대로. XML이 없으면 수정한 날짜(= 녹화가 끝난 시각)
//  - 프록시는 SUB 폴더의 원본이름+S03 → 원본과 같은 날, 이름은 원본이름_Proxy
//  - S&Q(슬로·퀵모션) 촬영은 카메라가 프록시를 만들지 않음 → XML의 RecordingMode로 알아봄
//  - 파일 번호가 카드·카메라마다 C0001부터 다시 시작 → 다른 카드를 같은 날짜 폴더에 넣으면 이름이 겹칠 수 있음
//  - M4ROOT는 기종에 따라 카드 맨 위 또는 PRIVATE 안
//  - M4ROOT\THMBNL의 JPG는 영상 썸네일 (사진 아님)
//  - XML은 원본을 옮겼을 때만 함께 처리 (지우거나 XML 폴더로). 원본이 남으면 XML도 남김
//
//  공통
//  - 파일은 넣은 폴더 안에서 옮기기만 함 → 되돌리기 가능. 같은 이름이 있으면 덮어쓰지 않고 건너뜀
//  - (옵션) 날짜 폴더 이름 끝에 용량 261004_DATA_26G · 같은 날짜 폴더가 이미 있으면 합치고 갱신
//  - 정리 후 옮긴 파일이 모두 제자리에 같은 크기로 있는지 확인
//  - 탐색기·Mac·NAS가 만드는 찌꺼기(Thumbs.db, desktop.ini, ._*, .DS_Store, @eaDir 등)는 정리 대상도 아니고
//    폴더를 남기는 이유도 아님 → 이것만 남은 카드 폴더(DCIM 등)는 함께 정리
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace SonySortTool
{
    enum Kind { Footage, Proxy, Xml, Jpg, Arw, Hif }

    class PlanItem
    {
        public string Source;
        public string Dest;      // 옮길 위치 (용량을 붙이기 전 폴더 이름 기준)
        public string Final;     // 정리가 끝난 뒤 실제 위치
        public string Day;       // yyMMdd
        public Kind Kind;
        public long Size;
        public bool Sq;          // 원본: S&Q 촬영
        public PlanItem Clip;    // XML: 짝인 원본
    }

    // 날짜 폴더 하나 (yymmdd_DATA 또는 이미 용량을 붙인 yymmdd_DATA_26G)
    class DayFolder
    {
        public string Day;
        public string Name;             // 지금 이름. 없으면 새로 만들 yymmdd_DATA
        public bool Existed;            // 정리 전부터 있던 폴더 (이번 파일을 합침)
        public long ExistingBytes;
        public long MediaBytes;         // 이번에 옮길 영상·프록시·사진 (건너뛸 파일 제외)
        public long XmlBytes;           // XML 폴더로 옮길 경우의 XML 용량
    }

    class ScanResult
    {
        public string Root;
        public string Error;
        public bool SonyCard;    // 넣은 폴더가 소니 카드(또는 그 PRIVATE 안쪽)를 복사한 것
        public List<PlanItem> Items = new List<PlanItem>();
        public Dictionary<string, DayFolder> Days = new Dictionary<string, DayFolder>();
        public List<string> NoProxy = new List<string>();     // 프록시 없는 원본 ("C0598(S&Q)")
        public List<string> Unhandled = new List<string>();   // 정리 규칙이 없는 영상·사진 (그대로 둠)
        public int Conflicts;                                 // 같은 이름이 이미 있어 건너뛸 영상·프록시·사진
        public int XmlConflicts;                              // XML 폴더에 같은 이름이 있음 (XML을 남길 때만 의미)
        public int Count(Kind k) { return Items.Count(x => x.Kind == k); }
    }

    class RunResult
    {
        public string Root;
        public List<PlanItem> Moved = new List<PlanItem>();
        public int Skipped, Failed;
        public int XmlTrashed, XmlTrashFailed;
        public List<string> Trashed = new List<string>();      // 휴지통으로 보낸 카드 관리 폴더·파일
        public List<string> Kept = new List<string>();         // 다른 파일이 섞여 있어 남겨둔 카드 관리 폴더
        public Dictionary<string, string> KeptBecause = new Dictionary<string, string>();  // 남겨둔 폴더 → 이유가 된 파일 (예: DCIM → 100MSDCF\IMG_0001.MOV)
        public List<string> TrashFailed = new List<string>();  // 휴지통으로 보내지 못한 카드 관리 폴더 (다른 프로그램이 사용 중 등)
        public Dictionary<string, string> FinalNames = new Dictionary<string, string>();
        public List<KeyValuePair<string, string>> Renamed = new List<KeyValuePair<string, string>>();
        public List<string> RenamedExisting = new List<string>();
        public int RenameFailed;
        public int Verified, VerifyFailed;
        public long VerifiedBytes;
    }

    class UndoResult
    {
        public int Restored, Failed;
    }

    static class Organizer
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly StringComparison Ic = StringComparison.OrdinalIgnoreCase;
        static readonly Regex DataDir = new Regex(@"^(\d{6})_DATA(_\d+(\.\d+)?[MGT])?$", RegexOptions.IgnoreCase);

        static readonly Dictionary<string, Kind> PhotoKinds = new Dictionary<string, Kind>(StringComparer.OrdinalIgnoreCase) {
            { ".jpg", Kind.Jpg }, { ".jpeg", Kind.Jpg }, { ".arw", Kind.Arw }, { ".hif", Kind.Hif }, { ".heif", Kind.Hif } };

        // 정리 대상은 아니지만 남아 있으면 알려 줄 영상 (옛 기종 AVCHD, 업무용 XDCAM 등)
        static readonly HashSet<string> OtherMedia = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            ".mp4", ".mov", ".mts", ".m2ts", ".mxf", ".wav" };

        // 카드 안에서 카메라가 쓰는 폴더 (사용자 파일이 아니므로 훑지 않음)
        static readonly HashSet<string> SkipDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "THMBNL", "GENERAL", "TAKE" };

        // 정리 후 휴지통으로 보낼 수 있는 카드 관리 폴더·파일 (넣은 폴더 바로 안에 있을 때만)
        static readonly string[] CardDirs  = { "M4ROOT", "PRIVATE", "AVF_INFO", "SONY", "DCIM", "THMBNL", "GENERAL", "TAKE", "CLIP", "SUB" };
        // PRIVATE 안쪽(M4ROOT·DATABASE)만 복사한 경우 맨 위에 오는 카드 폴더. 흔한 이름이라 소니 카드로 확인된 폴더에서만
        static readonly string[] SonyOnlyDirs = { "DATABASE" };
        static readonly string[] CardFiles = { "MEDIAPRO.XML", "STATUS.BIN" };
        // 카드 관리 폴더가 이 파일들로만 되어 있을 때만 휴지통으로 (썸네일 JPG는 THMBNL 안에서만 허용)
        static readonly HashSet<string> CardExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            ".xml", ".bin", ".ind", ".bnp", ".inp", ".int", ".dat", ".cube", "" };

        // 탐색기·Mac·NAS가 알아서 만드는 찌꺼기. 정리 대상이 아니고, 이것 때문에 빈 폴더가 남지 않게 함께 지움
        //  Thumbs.db·desktop.ini(탐색기, 특히 NAS), .DS_Store·._사진이름(Mac에 꽂았던 카드), @eaDir(시놀로지)·.@__thumb(QNAP) 썸네일,
        //  .dropbox.device(Dropbox 카메라 업로드가 꽂은 카드의 DCIM에 남기는 표시)
        static readonly HashSet<string> JunkFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "Thumbs.db", "ehthumbs.db", "ehthumbs_vista.db", "desktop.ini", ".DS_Store", ".localized", ".dropbox.device" };
        static readonly HashSet<string> JunkDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "@eaDir", ".@__thumb", ".Spotlight-V100", ".fseventsd", ".Trashes", ".TemporaryItems" };

        public static string SubFolder(Kind k)
        {
            switch (k) {
                case Kind.Footage: return "Footage";
                case Kind.Proxy:   return "Proxy";
                case Kind.Xml:     return "XML";
                case Kind.Jpg:     return @"Photo\jpg";
                case Kind.Arw:     return @"Photo\arw";
                default:           return @"Photo\hif";
            }
        }

        // ── 미리보기 ──────────────────────────────────────────────
        public static ScanResult Scan(string path)
        {
            var r = new ScanResult();
            DirectoryInfo dir;
            try { dir = new DirectoryInfo(path.Trim().Trim('"')); }
            catch (Exception) { r.Error = "폴더 경로를 읽을 수 없습니다."; return r; }
            if (!dir.Exists) { r.Error = "폴더를 찾을 수 없습니다."; return r; }
            r.Error = CheckLocation(dir);
            if (r.Error != null) return r;
            r.Root = dir.FullName.TrimEnd('\\');

            var files = Walk(dir).ToList();
            var names = new HashSet<string>(files.Select(f => f.FullName), StringComparer.OrdinalIgnoreCase);
            bool sonyCard = r.SonyCard = IsSonyCard(dir);

            // 1) 파일 나누기: 원본 · 프록시 · 사진 · 그 밖의 영상
            var clips = new List<FileInfo>(); var proxies = new List<FileInfo>(); var photos = new List<FileInfo>();
            foreach (var f in files) {
                string ext = f.Extension, b = Path.GetFileNameWithoutExtension(f.Name), parent = f.Directory.Name;
                if (ext.Equals(".mp4", Ic)) {
                    if (b.EndsWith("S03", Ic) && (parent.Equals("SUB", Ic) || names.Contains(Path.Combine(f.DirectoryName, b.Substring(0, b.Length - 3) + ".MP4"))))
                        proxies.Add(f);
                    else if (parent.Equals("CLIP", Ic) || names.Contains(XmlPath(f)))
                        clips.Add(f);                     // CLIP 폴더 안이거나 짝인 XML이 있어야 소니 영상
                    else r.Unhandled.Add(f.FullName);
                } else if (PhotoKinds.ContainsKey(ext)) {
                    // 소니 카드면 DCIM 안 사진, 파일만 모은 폴더면 소니 이름(DSC…)이나 소니 RAW(ARW)만
                    bool inDcim = HasAncestor(f, "DCIM", dir);
                    if ((sonyCard && inDcim) || ext.Equals(".arw", Ic) || f.Name.StartsWith("DSC", Ic)) photos.Add(f);
                    else r.Unhandled.Add(f.FullName);
                } else if (OtherMedia.Contains(ext)) r.Unhandled.Add(f.FullName);
            }

            if (clips.Count + proxies.Count + photos.Count == 0) {
                r.Error = files.Any(f => f.Name.StartsWith("DJI_", Ic))
                    ? "DJI 카메라 파일로 보입니다. SONY 타입 정리기는 소니 카메라 카드용입니다 (DJI 파일은 DJI 타입 정리기로)."
                    : "소니 카메라 영상·사진이 없습니다.\n소니 카드에서 복사한 폴더인지 확인해 주세요. 이미 정리한 폴더일 수도 있습니다.";
                return r;
            }

            // 2) 이미 있는 날짜 폴더 (전에 정리한 결과). 같은 날짜면 거기에 합침
            var existing = new Dictionary<string, string>();
            foreach (var d in dir.GetDirectories().OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)) {
                var m = DataDir.Match(d.Name);
                if (m.Success && !existing.ContainsKey(m.Groups[1].Value)) existing[m.Groups[1].Value] = d.Name;
            }
            var dests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Func<FileInfo, string, Kind, string, PlanItem> add = (f, day, kind, name) => {
                DayFolder df;
                if (!r.Days.TryGetValue(day, out df)) {
                    string name0;
                    df = existing.TryGetValue(day, out name0)
                        ? new DayFolder { Day = day, Name = name0, Existed = true, ExistingBytes = FolderBytes(Path.Combine(r.Root, name0)) }
                        : new DayFolder { Day = day, Name = day + "_DATA" };
                    r.Days[day] = df;
                }
                string dest = Path.Combine(r.Root, df.Name, day + "_" + SubFolder(kind), name);
                if (File.Exists(dest) || !dests.Add(dest)) { if (kind == Kind.Xml) r.XmlConflicts++; else r.Conflicts++; }
                else if (kind == Kind.Xml) df.XmlBytes += f.Length;
                else df.MediaBytes += f.Length;
                var it = new PlanItem { Source = f.FullName, Dest = dest, Day = day, Kind = kind, Size = f.Length };
                r.Items.Add(it);
                return it;
            };

            // 3) 원본: XML의 촬영 시작 시각으로 날짜. XML은 원본과 같은 날 XML 폴더 대상으로
            var clipByKey = new Dictionary<string, PlanItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in clips.OrderBy(x => x.FullName, StringComparer.OrdinalIgnoreCase)) {
                string xml = XmlPath(f);
                bool hasXml = names.Contains(xml);
                bool sq;
                DateTime when = ClipStart(f, hasXml ? xml : null, out sq);
                string day = when.ToString("yyMMdd", Inv);
                var clip = add(f, day, Kind.Footage, f.Name);
                clip.Sq = sq;
                clipByKey[PairKey(f, Path.GetFileNameWithoutExtension(f.Name))] = clip;
                if (hasXml) add(new FileInfo(xml), day, Kind.Xml, Path.GetFileName(xml)).Clip = clip;
            }

            // 4) 프록시: 짝인 원본과 같은 날
            var paired = new HashSet<PlanItem>();
            foreach (var f in proxies.OrderBy(x => x.FullName, StringComparer.OrdinalIgnoreCase)) {
                string b = Path.GetFileNameWithoutExtension(f.Name);
                string clipBase = b.Substring(0, b.Length - 3);
                PlanItem clip;
                string day = clipByKey.TryGetValue(PairKey(f, clipBase), out clip) ? clip.Day : f.LastWriteTime.ToString("yyMMdd", Inv);
                if (clip != null) paired.Add(clip);
                add(f, day, Kind.Proxy, clipBase + "_Proxy" + f.Extension);
            }
            if (proxies.Count > 0) {
                foreach (var c in clipByKey.Values.Where(c => !paired.Contains(c)).OrderBy(c => c.Source, StringComparer.OrdinalIgnoreCase)) {
                    string b = Path.GetFileNameWithoutExtension(c.Source);
                    r.NoProxy.Add(c.Sq ? b + "(S&Q)" : b);
                }
            }

            // 5) 사진: 수정한 날짜 = 촬영 시각 (복사해도 바뀌지 않음)
            foreach (var f in photos.OrderBy(x => x.FullName, StringComparer.OrdinalIgnoreCase))
                add(f, f.LastWriteTime.ToString("yyMMdd", Inv), PhotoKinds[f.Extension], f.Name);

            return r;
        }

        // SD카드에서 바로 정리하면 복사본 없이 카드 파일을 옮기게 되므로 막음:
        // 드라이브 맨 위, 또는 맨 위에 소니 카드 폴더(M4ROOT·PRIVATE·DCIM)가 있는 드라이브의 그 폴더 안
        static string CheckLocation(DirectoryInfo dir)
        {
            string root = dir.Root.FullName;
            if (dir.FullName.TrimEnd('\\').Equals(root.TrimEnd('\\'), Ic))
                return "드라이브(SD카드) 맨 위에서는 정리하지 않습니다.\n카드 내용을 PC 폴더로 복사한 뒤 그 폴더를 넣어 주세요.";
            foreach (var name in new[] { "M4ROOT", "PRIVATE", "DCIM" }) {
                string p = Path.Combine(root, name);
                if (Directory.Exists(p) && IsUnder(dir.FullName, p))
                    return "SD카드 안의 폴더로 보입니다.\n카드 내용을 PC 폴더로 복사한 뒤 그 폴더를 넣어 주세요.";
            }
            return null;
        }

        // 넣은 폴더가 소니 카드를 통째로 복사한 것인지 (또는 M4ROOT 자체인지)
        static bool IsSonyCard(DirectoryInfo dir)
        {
            Func<string, bool> has = rel => Directory.Exists(Path.Combine(dir.FullName, rel)) || File.Exists(Path.Combine(dir.FullName, rel));
            if (has("M4ROOT") || has(@"PRIVATE\M4ROOT") || has("AVF_INFO") || has(@"SONY\SONYCARD.IND")) return true;
            if (dir.Name.Equals("M4ROOT", Ic) || has("MEDIAPRO.XML")) return true;
            try { return Directory.Exists(Path.Combine(dir.FullName, "DCIM")) &&
                         Directory.GetDirectories(Path.Combine(dir.FullName, "DCIM"), "*MSDCF").Length > 0; }
            catch (Exception) { return false; }
        }

        // 하위 폴더까지 파일 나열. 이미 정리한 결과(yymmdd_DATA)·카메라 내부 폴더(THMBNL 등)·찌꺼기는 건너뜀
        // (Mac의 ._C0001.MP4, NAS의 @eaDir\DSC0001.JPG\썸네일.jpg가 영상·사진으로 섞이지 않게)
        static IEnumerable<FileInfo> Walk(DirectoryInfo root)
        {
            var stack = new Stack<DirectoryInfo>();
            stack.Push(root);
            while (stack.Count > 0) {
                var d = stack.Pop();
                FileInfo[] files; DirectoryInfo[] subs;
                try { files = d.GetFiles(); subs = d.GetDirectories(); }
                catch (Exception) { continue; }
                foreach (var f in files) if (!IsJunkFile(f.Name)) yield return f;
                foreach (var s in subs)
                    if (!DataDir.IsMatch(s.Name) && !SkipDirs.Contains(s.Name) && !JunkDirs.Contains(s.Name)) stack.Push(s);
            }
        }

        static bool IsJunkFile(string name) { return JunkFiles.Contains(name) || name.StartsWith("._", StringComparison.Ordinal); }

        // 찌꺼기 파일이거나 찌꺼기 폴더(@eaDir 등) 안의 파일인지 (root 아래 경로만 봄)
        static bool IsJunk(string file, string root)
        {
            if (IsJunkFile(Path.GetFileName(file))) return true;
            for (string d = Path.GetDirectoryName(file); d != null && d.Length > root.TrimEnd('\\').Length; d = Path.GetDirectoryName(d))
                if (JunkDirs.Contains(Path.GetFileName(d))) return true;
            return false;
        }

        // C0001.MP4 → C0001M01.XML (같은 폴더)
        static string XmlPath(FileInfo clip) { return Path.Combine(clip.DirectoryName, Path.GetFileNameWithoutExtension(clip.Name) + "M01.XML"); }

        // 원본과 프록시 짝 찾기: CLIP·SUB 안이면 그 위 M4ROOT 기준, 아니면 같은 폴더 기준
        // (번호가 카드마다 다시 시작하므로 다른 카드의 같은 번호와 섞이지 않게)
        static string PairKey(FileInfo f, string clipBase)
        {
            string parent = f.Directory.Name;
            string group = (parent.Equals("CLIP", Ic) || parent.Equals("SUB", Ic)) && f.Directory.Parent != null
                ? f.Directory.Parent.FullName : f.DirectoryName;
            return group + "|" + clipBase;
        }

        // 촬영 시작 시각: XML의 CreationDate(카메라 시간대 그대로). 없으면 수정한 날짜(녹화가 끝난 시각)
        static DateTime ClipStart(FileInfo clip, string xml, out bool sq)
        {
            sq = false;
            if (xml != null) {
                try {
                    string text = File.ReadAllText(xml);
                    sq = text.IndexOf("RecordingMode type=\"slowAndQuickMotion\"", Ic) >= 0;
                    var m = Regex.Match(text, "<CreationDate value=\"([^\"]+)\"");
                    DateTimeOffset d;
                    if (m.Success && DateTimeOffset.TryParse(m.Groups[1].Value, Inv, DateTimeStyles.None, out d)) return d.DateTime;
                } catch (Exception) { }
            }
            return clip.LastWriteTime;
        }

        static bool HasAncestor(FileInfo f, string name, DirectoryInfo stopAt)
        {
            for (var d = f.Directory; d != null && IsUnder(d.FullName, stopAt.FullName); d = d.Parent)
                if (d.Name.Equals(name, Ic)) return true;
            return false;
        }

        static bool IsUnder(string path, string parent)
        {
            return (path.TrimEnd('\\') + "\\").StartsWith(parent.TrimEnd('\\') + "\\", Ic);
        }

        // ── 정리 ──────────────────────────────────────────────────
        public static RunResult Run(ScanResult scan, bool deleteXml, bool trashCard, bool addSize, IProgress<int> progress)
        {
            var r = new RunResult { Root = scan.Root };
            var emptied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var moved = new HashSet<PlanItem>();
            int i = 0;

            // 1) 영상·프록시·사진
            foreach (var it in scan.Items.Where(x => x.Kind != Kind.Xml)) {
                if (progress != null) progress.Report(++i);
                if (Move(it, r, emptied)) moved.Add(it);
            }

            // 2) XML: 원본을 옮긴 것만. 지우기(휴지통) 또는 XML 폴더로
            var xmlDelete = new List<string>();
            foreach (var x in scan.Items.Where(x => x.Kind == Kind.Xml)) {
                if (progress != null) progress.Report(++i);
                if (x.Clip == null || !moved.Contains(x.Clip)) continue;
                if (deleteXml) xmlDelete.Add(x.Source);
                else Move(x, r, emptied);
            }

            // 파일을 옮겨서 비게 된 폴더부터 정리 (빈 폴더가 휴지통에 쌓이지 않게. 원래 비어 있던 폴더는 건드리지 않음)
            foreach (var d in emptied.OrderByDescending(x => x.Length)) RemoveEmptyUp(d, scan.Root);
            emptied.Clear();

            // 3) 카드 관리 폴더·파일 (넣은 폴더 바로 안)
            //    파일이 하나도 없는 카드 폴더(사진 없는 카드의 빈 DCIM\100MSDCF 등)는 옵션과 상관없이 지움
            //    나머지는 옵션이 켜져 있을 때 휴지통으로. 영상·사진 등 다른 파일이 섞여 있으면 남김
            var cardTrash = new List<string>();
            var dirs = scan.SonyCard ? CardDirs.Concat(SonyOnlyDirs) : CardDirs;
            foreach (var name in trashCard ? dirs.Concat(JunkDirs) : dirs) {
                string d = Path.Combine(scan.Root, name);
                if (!Directory.Exists(d) || RemoveHollow(d, scan.Root) || !trashCard) continue;
                string blocker = CardBlocker(d, scan.Root, xmlDelete, deleteXml);
                if (blocker == null) cardTrash.Add(d);
                else { r.Kept.Add(name); r.KeptBecause[name] = blocker.Substring(d.Length + 1); }
            }
            if (trashCard) {
                foreach (var name in CardFiles) {
                    string f = Path.Combine(scan.Root, name);
                    if (File.Exists(f)) cardTrash.Add(f);
                }
            }
            // 휴지통으로 갈 카드 폴더 안의 XML은 폴더째 함께 (휴지통 항목이 수백 개로 늘지 않게)
            var xmlAlone = xmlDelete.Where(x => !cardTrash.Any(t => IsUnder(x, t))).ToList();
            TrashAll(xmlAlone);
            TrashAll(cardTrash);
            foreach (var t in cardTrash) {
                if (!Directory.Exists(t) && !File.Exists(t)) r.Trashed.Add(Path.GetFileName(t));
                else r.TrashFailed.Add(Path.GetFileName(t));
            }
            // 카드 폴더를 못 보냈으면(탐색기가 그 안을 열고 있음 등) 그 안의 XML이라도 따로
            TrashAll(xmlDelete.Where(File.Exists).ToList());
            r.XmlTrashed = xmlDelete.Count(x => !File.Exists(x));
            r.XmlTrashFailed = xmlDelete.Count - r.XmlTrashed;
            foreach (var x in xmlDelete) emptied.Add(Path.GetDirectoryName(x));

            // 4) XML·카드 폴더를 보내고 비게 된 폴더 정리
            foreach (var d in emptied.OrderByDescending(x => x.Length)) RemoveEmptyUp(d, scan.Root);

            // 5) 날짜 폴더 이름 끝에 용량 (이번에 파일을 넣은 폴더만)
            foreach (var df in scan.Days.Values) {
                r.FinalNames[df.Day] = df.Name;
                if (!addSize || !r.Moved.Any(x => x.Day == df.Day)) continue;
                string from = Path.Combine(scan.Root, df.Name);
                string name = SizedName(df.Day, FolderBytes(from));
                if (name.Equals(df.Name, Ic)) continue;
                string to = Path.Combine(scan.Root, name);
                if (Directory.Exists(to) || !TryRenameDir(from, to)) { r.RenameFailed++; continue; }
                r.Renamed.Add(new KeyValuePair<string, string>(from, to));
                r.FinalNames[df.Day] = name;
                if (df.Existed) r.RenamedExisting.Add(df.Name + " → " + name);
            }

            // 6) 확인: 옮긴 파일이 모두 제자리에 같은 크기로 있는지
            foreach (var it in r.Moved) {
                string oldDir = Path.Combine(scan.Root, scan.Days[it.Day].Name);
                it.Final = Path.Combine(scan.Root, r.FinalNames[it.Day], it.Dest.Substring(oldDir.Length + 1));
                var fi = new FileInfo(it.Final);
                if (fi.Exists && fi.Length == it.Size) { r.Verified++; r.VerifiedBytes += it.Size; }
                else r.VerifyFailed++;
            }
            WriteLog(r);
            NotifyExplorer(scan.Root, r.Renamed);
            return r;
        }

        static bool Move(PlanItem it, RunResult r, HashSet<string> emptied)
        {
            if (File.Exists(it.Dest)) { r.Skipped++; return false; }
            Directory.CreateDirectory(Path.GetDirectoryName(it.Dest));
            if (!TryMove(it.Source, it.Dest)) {
                r.Failed++;
                RemoveEmptyUp(Path.GetDirectoryName(it.Dest), r.Root);   // 하나도 못 옮긴 날짜면 방금 만든 빈 yymmdd_DATA를 남기지 않음
                return false;
            }
            r.Moved.Add(it);
            emptied.Add(Path.GetDirectoryName(it.Source));
            return true;
        }

        // 탐색기 미리보기·동기화 프로그램이 잠깐 잡고 있는 경우가 있어 몇 번 재시도
        static bool TryMove(string from, string to)
        {
            for (int i = 0; i < 3; i++) {
                try { File.Move(from, to); return true; }
                catch (FileNotFoundException) { return false; }
                catch (DirectoryNotFoundException) { return false; }
                catch (Exception) { Thread.Sleep(500); }
            }
            return false;
        }

        // 카드 관리 파일로만 되어 있으면 null, 남겨야 할 파일이 있으면 그 경로 (결과 화면에 이유로 보여 줌)
        // 썸네일 JPG는 THMBNL 안에서만, 찌꺼기(Thumbs.db 등)는 어디서나 허용.
        // XML은 '지우기'로 고른 원본의 XML과, 원본이 없는 XML(카드 목록 등)만 허용 → 남겨야 할 XML이 있으면 폴더째 보내지 않음
        static string CardBlocker(string dir, string root, List<string> xmlDelete, bool deleteXml)
        {
            var deletable = new HashSet<string>(xmlDelete, StringComparer.OrdinalIgnoreCase);
            try {
                foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)) {
                    string ext = Path.GetExtension(f);
                    bool thumb = ext.Equals(".jpg", Ic) && f.IndexOf(@"\THMBNL\", Ic) >= 0;
                    if (thumb || IsJunk(f, root)) continue;
                    if (!CardExt.Contains(ext)) return f;
                    if (ext.Equals(".xml", Ic) && f.EndsWith("M01.XML", Ic) && !deletable.Contains(f)) {
                        // 원본이 남아 있는 XML (같은 이름 MP4가 곁에 있음)은 지키고, 원본 없는 XML은 카드 찌꺼기로 봄
                        string clip = f.Substring(0, f.Length - "M01.XML".Length) + ".MP4";
                        if (File.Exists(clip) || !deleteXml) return f;
                    }
                }
                return null;
            } catch (Exception) { return Path.Combine(dir, "?"); }
        }

        // 찌꺼기 말고는 파일이 하나도 없는 폴더(빈 하위 폴더만 있는 DCIM 등)면 찌꺼기를 지우고 폴더째 없앰. 없앴으면 true
        // 폴더는 비어 있을 때만 지워지는 방식으로 아래부터 하나씩 → 그 사이 생긴 파일이 있어도 지우지 않음
        static bool RemoveHollow(string dir, string root)
        {
            try {
                if (Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Any(f => !IsJunk(f, root))) return false;
                foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories)) DeleteJunk(f);
                foreach (var d in Directory.GetDirectories(dir, "*", SearchOption.AllDirectories).OrderByDescending(x => x.Length)) Directory.Delete(d);
                Directory.Delete(dir);
            } catch (Exception) { }
            return !Directory.Exists(dir);
        }

        static void DeleteJunk(string file)
        {
            File.SetAttributes(file, FileAttributes.Normal);   // Thumbs.db·desktop.ini는 숨김·시스템 파일
            File.Delete(file);
        }

        // 파일을 옮겨서 비게 된 폴더를 위로 올라가며 지움. 찌꺼기만 남은 폴더도 빈 폴더로 봄 (DCIM\100MSDCF의 Thumbs.db 등)
        static void RemoveEmptyUp(string dir, string root)
        {
            while (!string.IsNullOrEmpty(dir) && IsUnder(dir, root) && !dir.TrimEnd('\\').Equals(root.TrimEnd('\\'), Ic)) {
                try {
                    if (!Directory.Exists(dir)) { dir = Path.GetDirectoryName(dir); continue; }
                    var entries = new DirectoryInfo(dir).GetFileSystemInfos();
                    if (!entries.All(e => e is FileInfo ? IsJunkFile(e.Name) : JunkDirs.Contains(e.Name))) return;
                    foreach (var e in entries) {
                        if (e is FileInfo) DeleteJunk(e.FullName);
                        else if (!RemoveHollow(e.FullName, root)) return;
                    }
                    Directory.Delete(dir);
                } catch (Exception) { return; }
                dir = Path.GetDirectoryName(dir);
            }
        }

        // 폴더 안 모든 파일 용량 합계 (내용은 읽지 않음)
        static long FolderBytes(string dir)
        {
            try { return new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length); }
            catch (Exception) { return 0; }
        }

        public static string SizedName(string day, long bytes) { return day + "_DATA_" + FormatSize(bytes); }

        // 미리보기에서 보여줄 최종 폴더 이름 (XML을 지우면 XML 용량은 빠짐)
        public static string PreviewName(DayFolder df, bool addSize, bool deleteXml)
        {
            return addSize ? SizedName(df.Day, df.ExistingBytes + df.MediaBytes + (deleteXml ? 0 : df.XmlBytes)) : df.Name;
        }

        // 탐색기 속성 창과 같은 1024 기준, 소수점 1자리 내림:  2.19G → 2.1G, 1GB 미만은 850M, 1TB 이상은 T
        public static string FormatSize(long bytes)
        {
            const decimal MB = 1024m * 1024, GB = MB * 1024, TB = GB * 1024;
            if (bytes >= TB) return (Math.Floor(bytes * 10m / TB) / 10).ToString("0.#", Inv) + "T";
            if (bytes >= GB) return (Math.Floor(bytes * 10m / GB) / 10).ToString("0.#", Inv) + "G";
            return Math.Max(1m, Math.Floor(bytes / MB)).ToString("0", Inv) + "M";
        }

        static bool TryRenameDir(string from, string to)
        {
            for (int i = 0; i < 5; i++) {
                try { Directory.Move(from, to); return true; }
                catch (Exception) { Thread.Sleep(700); }
            }
            return false;
        }

        // ── 되돌리기 ──────────────────────────────────────────────
        // 휴지통으로 보낸 XML·카드 관리 파일은 되돌리지 않음 (화면에서 미리 알림)
        public static UndoResult Undo(RunResult run)
        {
            var u = new UndoResult();
            var emptied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = run.Renamed.Count - 1; i >= 0; i--) TryRenameDir(run.Renamed[i].Value, run.Renamed[i].Key);
            for (int i = run.Moved.Count - 1; i >= 0; i--) {
                var m = run.Moved[i];
                string at = File.Exists(m.Dest) ? m.Dest : (m.Final != null && File.Exists(m.Final) ? m.Final : null);
                try {
                    if (at != null && !File.Exists(m.Source)) {
                        Directory.CreateDirectory(Path.GetDirectoryName(m.Source));
                        File.Move(at, m.Source);
                        u.Restored++;
                        emptied.Add(Path.GetDirectoryName(at));
                    } else u.Failed++;
                } catch (Exception) { u.Failed++; }
            }
            foreach (var d in emptied.OrderByDescending(x => x.Length)) RemoveEmptyUp(d, run.Root);
            NotifyExplorer(run.Root, run.Renamed.Select(p => new KeyValuePair<string, string>(p.Value, p.Key)));
            return u;
        }

        // 옮긴 기록:  %LOCALAPPDATA%\SonySortTool\logs\yyyyMMdd_HHmmss.txt  "원래 위치<TAB>옮긴 위치"
        // 테스트에서는 임시 폴더로 바꿔 끼움 (실제 기록 폴더에 테스트 기록이 쌓이지 않게)
        internal static string LogDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SonySortTool", "logs");

        static void WriteLog(RunResult r)
        {
            if (r.Moved.Count == 0) return;
            try {
                Directory.CreateDirectory(LogDir);
                var sb = new StringBuilder();
                foreach (var m in r.Moved) sb.Append(m.Source).Append('\t').Append(m.Final ?? m.Dest).Append("\r\n");
                File.WriteAllText(Path.Combine(LogDir, DateTime.Now.ToString("yyyyMMdd_HHmmss", Inv) + ".txt"), sb.ToString(), new UTF8Encoding(true));
            } catch (Exception) { }
        }

        // ── 휴지통 ────────────────────────────────────────────────
        // 테스트에서는 실제 휴지통 대신 다른 동작으로 바꿔 끼움. 반환: 휴지통으로 간 개수
        internal static Func<IList<string>, int> Recycler = Recycle;

        // 한 번에 보내고, 남은 것(탐색기·백신이 잠깐 잡고 있던 것 등)은 잠시 뒤 하나씩 다시
        // (여러 개를 한 번에 보낼 때 하나가 실패하면 그 뒤 항목까지 남는 경우가 있음)
        static void TrashAll(List<string> paths)
        {
            if (paths.Count == 0) return;
            Recycler(paths);
            for (int i = 0; i < 3; i++) {
                var left = paths.Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
                if (left.Count == 0) return;
                Thread.Sleep(700);
                foreach (var p in left) Recycler(new[] { p });
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern void SHChangeNotify(int eventId, uint flags, string item1, string item2);

        // 탐색기 창이 이미 지운 폴더(DCIM 등)나 이름을 바꾸기 전 폴더(261004_DATA)를 계속 보여 주지 않게 새로 고침 알림
        static void NotifyExplorer(string root, IEnumerable<KeyValuePair<string, string>> renamed)
        {
            try {
                foreach (var p in renamed) SHChangeNotify(0x00020000, 0x0005, p.Key, p.Value);   // SHCNE_RENAMEFOLDER, SHCNF_PATHW
                SHChangeNotify(0x00001000, 0x3005, root, null);                                 // SHCNE_UPDATEDIR, SHCNF_PATHW | SHCNF_FLUSHNOWAIT
            } catch (Exception) { }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd; public uint wFunc; public string pFrom; public string pTo;
            public ushort fFlags; public bool fAnyOperationsAborted; public IntPtr hNameMappings; public string lpszProgressTitle;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHFileOperation(ref SHFILEOPSTRUCT op);

        // 여러 개를 한 번에. 휴지통이 없는 위치(NAS 등)라 영구 삭제가 되는 경우엔 Windows가 경고를 먼저 띄움
        static int Recycle(IList<string> paths)
        {
            var op = new SHFILEOPSTRUCT {
                wFunc  = 3,                                        // FO_DELETE
                pFrom  = string.Join("\0", paths) + "\0\0",
                fFlags = 0x40 | 0x10 | 0x4 | 0x400 | 0x4000        // 휴지통, 확인 생략, 진행창 없음, 오류창 없음, 영구삭제 경고
            };
            SHFileOperation(ref op);
            return paths.Count(p => !File.Exists(p) && !Directory.Exists(p));
        }
    }
}
