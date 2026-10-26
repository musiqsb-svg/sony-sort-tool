// SONY 타입 정리기 정리 로직 테스트 (화면 없이 Organizer만)
//   OrganizerTest.exe                    실제 소니 카드(ILCE-7M5) 구조를 본뜬 가짜 카드로 여러 상황 확인
//   OrganizerTest.exe --replica <폴더>   실제 폴더의 이름·날짜·XML을 본뜬 복제본으로 확인 (원본은 건드리지 않음)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SonySortTool
{
    static class OrganizerTest
    {
        static int failures;
        static string temp, trash;
        static int recycled;          // 휴지통으로 보낸 항목 수 (폴더 하나 = 1)

        static int Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            temp  = Path.Combine(Path.GetTempPath(), "SonySortToolTest_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            trash = Path.Combine(temp, "_trash");
            Organizer.LogDir = Path.Combine(temp, "_logs");   // 실제 기록 폴더(%LOCALAPPDATA%)에 테스트 기록을 남기지 않음
            Directory.CreateDirectory(trash);
            // 실제 휴지통 대신 테스트 폴더로 옮김. 실제 휴지통처럼 못 보내면(사용 중) 조용히 남김
            Organizer.Recycler = paths => {
                int n = 0;
                foreach (var p in paths) {
                    string to = Path.Combine(trash, Guid.NewGuid().ToString("N"));
                    try {
                        if (Directory.Exists(p)) { Directory.Move(p, to); n++; }
                        else if (File.Exists(p)) { File.Move(p, to); n++; }
                    } catch (IOException) { }
                }
                recycled += n;
                return n;
            };
            try {
                if (args.Length == 2 && args[0] == "--replica") Replica(args[1]);
                else {
                    DeleteXml(); KeepXml(); XmlOnlyNoCardCleanup(); OldLayoutNoProxy(); Midnight();
                    SecondCardSameNames(); LooseFiles(); M4rootDropped(); AvchdLeftover(); CustomTitle(); SizeFormat(); Guards();
                    JunkLeftovers(); EmptyDcimOptionOff(); LockedFolder(); PrivateContents();
                }
            } finally {
                try { Directory.Delete(temp, true); } catch (Exception) { }
            }
            Console.WriteLine(failures == 0 ? "\n모든 테스트 통과" : "\n실패 " + failures + "개");
            return failures;
        }

        // ── 가짜 소니 카드 ─────────────────────────────────────────
        // 실제 카드(ILCE-7M5)와 같은 구조: M4ROOT(CLIP·SUB·THMBNL·GENERAL·TAKE·MEDIAPRO.XML), PRIVATE, AVF_INFO, SONY, DCIM\100MSDCF
        //  261003: C0561~C0563 · 261004: C0564~C0566 (C0565는 S&Q라 프록시 없음, C0566은 XML 없음 → 수정한 날짜)
        static string SonyCard(string name, string m4 = "M4ROOT")
        {
            string root = Dir(name), clip = Path.Combine(root, m4, "CLIP"), sub = Path.Combine(root, m4, "SUB");
            for (int n = 561; n <= 566; n++) {
                var start = (n <= 563 ? new DateTime(2026, 10, 3, 10, 0, 0) : new DateTime(2026, 10, 4, 10, 0, 0)).AddMinutes(n - 560);
                string b = "C0" + n;
                Touch(clip, b + ".MP4", start.AddMinutes(1));
                if (n != 566) Xml(clip, b, start, n == 565);
                if (n != 565) Touch(sub, b + "S03.MP4", start.AddMinutes(1));
                Touch(Path.Combine(root, m4, "THMBNL"), b + "T01.JPG", start);
            }
            Touch(Path.Combine(root, m4), "MEDIAPRO.XML");
            Directory.CreateDirectory(Path.Combine(root, m4, @"GENERAL\SONY\PXTMP"));
            Directory.CreateDirectory(Path.Combine(root, m4, @"GENERAL\LUT"));
            Directory.CreateDirectory(Path.Combine(root, m4, "TAKE"));
            Touch(Path.Combine(root, @"PRIVATE\DATABASE"), "DATABASE.BIN");
            Touch(Path.Combine(root, "AVF_INFO"), "AVIN0001.BNP");
            Touch(Path.Combine(root, "AVF_INFO"), "AVIN0001.INP");
            Touch(Path.Combine(root, "SONY"), "SONYCARD.IND");
            Directory.CreateDirectory(Path.Combine(root, @"SONY\PRO\LUT"));
            var shot = new DateTime(2026, 10, 4, 13, 50, 34);
            Touch(Path.Combine(root, @"DCIM\100MSDCF"), "DSC09566.JPG", shot);
            Touch(Path.Combine(root, @"DCIM\100MSDCF"), "DSC09566.ARW", shot);
            Touch(Path.Combine(root, @"DCIM\100MSDCF"), "DSC09567.HIF", shot);
            return root;
        }

        // ── 상황별 테스트 ─────────────────────────────────────────

        static void DeleteXml()
        {
            Console.WriteLine("[카드 통째 · XML 지우기(기본)]");
            string root = SonyCard("del");
            Directory.CreateDirectory(Path.Combine(root, "내가 만든 빈 폴더"));
            var before = Snapshot(root);
            var s = Organizer.Scan(root);
            Check(s.Error == null, "미리보기 오류 없음 " + (s.Error ?? ""));
            Check(s.Count(Kind.Footage) == 6 && s.Count(Kind.Proxy) == 5 && s.Count(Kind.Xml) == 5, "영상 6 · 프록시 5 · XML 5");
            Check(s.Count(Kind.Jpg) == 1 && s.Count(Kind.Arw) == 1 && s.Count(Kind.Hif) == 1, "사진 JPG·ARW·HIF 1개씩 (THMBNL 썸네일 제외)");
            Check(s.NoProxy.SequenceEqual(new[] { "C0565(S&Q)" }), "프록시 없는 원본 = C0565(S&Q): " + string.Join(",", s.NoProxy));
            Check(s.Items.Where(x => x.Source.Contains("C0561") || x.Source.Contains("C0563")).All(x => x.Day == "261003"), "XML 날짜 261003 (영상·XML·프록시)");
            Check(s.Items.Where(x => x.Source.Contains("C0566")).All(x => x.Day == "261004"), "XML 없는 C0566은 수정한 날짜 261004");
            Check(s.Unhandled.Count == 0 && s.Conflicts == 0, "그대로 둘 파일·같은 이름 없음");

            recycled = 0;
            var r = Organizer.Run(s, true, true, true, null);
            Check(r.Moved.Count == 14 && r.Failed == 0 && r.Skipped == 0, "14개 옮김 (영상 6 · 프록시 5 · 사진 3)");
            Check(r.XmlTrashed == 5 && r.XmlTrashFailed == 0, "XML 5개 휴지통");
            Check(r.Verified == 14 && r.VerifyFailed == 0, "정리 후 확인 14개");
            Check(Exists(root, @"261003_DATA_1M\261003_Footage\C0561.MP4"), "원본 → Footage");
            Check(Exists(root, @"261003_DATA_1M\261003_Proxy\C0561_Proxy.MP4"), "C0561S03 → Proxy\\C0561_Proxy.MP4");
            Check(Exists(root, @"261004_DATA_1M\261004_Photo\hif\DSC09567.HIF") && Exists(root, @"261004_DATA_1M\261004_Photo\arw\DSC09566.ARW"), "사진 → Photo\\jpg·arw·hif");
            Check(!Directory.EnumerateFiles(root, "*.XML", SearchOption.AllDirectories).Any(), "XML이 어디에도 남지 않음");
            Check(r.Trashed.OrderBy(x => x).SequenceEqual(new[] { "AVF_INFO", "M4ROOT", "PRIVATE", "SONY" }), "카드 관리 폴더 휴지통: " + string.Join(",", r.Trashed));
            Check(recycled == 4, "휴지통 항목 4개 (XML은 M4ROOT와 함께): " + recycled);
            Check(!Directory.Exists(Path.Combine(root, "DCIM")) && Directory.Exists(Path.Combine(root, "내가 만든 빈 폴더")), "빈 DCIM 정리 · 원래 빈 폴더는 그대로");
            Check(Organizer.Scan(root).Error != null, "정리한 폴더를 다시 넣으면 할 일 없음");

            var u = Organizer.Undo(r);
            var after = Snapshot(root);
            Check(u.Restored == 14 && u.Failed == 0, "되돌리기 14개");
            Check(before.Where(p => !p.EndsWith(".XML") && IsMedia(p)).All(after.Contains), "영상·프록시·사진은 원래 자리로 (휴지통으로 보낸 XML·카드 파일은 제외)");
        }

        static void KeepXml()
        {
            Console.WriteLine("[XML 남기기 → 날짜별 XML 폴더]");
            string root = SonyCard("keep");
            var before = Snapshot(root);
            var s = Organizer.Scan(root);
            var r = Organizer.Run(s, false, true, true, null);
            Check(r.Moved.Count == 19 && r.XmlTrashed == 0, "19개 옮김 (XML 5개 포함)");
            Check(Exists(root, @"261003_DATA_1M\261003_XML\C0561M01.XML") && Exists(root, @"261004_DATA_1M\261004_XML\C0565M01.XML"), "XML → 날짜별 XML 폴더");
            Check(!Directory.EnumerateFiles(Path.Combine(root, "261003_DATA_1M", "261003_Footage"), "*.XML").Any(), "Footage에는 XML 없음");
            Check(r.Verified == 19, "정리 후 확인 19개");
            Check(r.Trashed.Contains("M4ROOT"), "XML을 옮긴 뒤 M4ROOT(썸네일 등) 휴지통");
            var u = Organizer.Undo(r);
            var after = Snapshot(root);
            Check(u.Restored == 19 && before.Where(p => p.StartsWith(@"M4ROOT\CLIP\")).All(after.Contains), "되돌리면 XML도 CLIP으로 돌아옴");
        }

        static void XmlOnlyNoCardCleanup()
        {
            Console.WriteLine("[XML 지우기 · 카드 관리 파일은 그대로]");
            string root = SonyCard("xmlonly");
            recycled = 0;
            var r = Organizer.Run(Organizer.Scan(root), true, false, true, null);
            Check(r.XmlTrashed == 5 && recycled == 5 && r.Trashed.Count == 0, "XML 5개만 휴지통");
            Check(Directory.Exists(Path.Combine(root, @"M4ROOT\THMBNL")) && Directory.Exists(Path.Combine(root, "PRIVATE")), "카드 관리 폴더는 그대로");
            Check(!Directory.Exists(Path.Combine(root, @"M4ROOT\CLIP")) && !Directory.Exists(Path.Combine(root, @"M4ROOT\SUB")), "비게 된 CLIP·SUB 정리");
        }

        // 옛 기종: M4ROOT가 PRIVATE 안, 프록시 녹화 끔
        static void OldLayoutNoProxy()
        {
            Console.WriteLine("[옛 기종 PRIVATE\\M4ROOT · 프록시 없음]");
            string root = Dir("old");
            string clip = Path.Combine(root, @"PRIVATE\M4ROOT\CLIP");
            Touch(clip, "C0100.MP4", new DateTime(2026, 9, 30, 11, 0, 0));
            Xml(clip, "C0100", new DateTime(2026, 9, 30, 10, 59, 0), false);
            Touch(Path.Combine(root, @"PRIVATE\M4ROOT"), "MEDIAPRO.XML");
            Touch(Path.Combine(root, @"PRIVATE\DATABASE"), "DATABASE.BIN");
            Touch(Path.Combine(root, @"DCIM\100MSDCF"), "DSC00100.ARW", new DateTime(2026, 9, 30, 12, 0, 0));
            var s = Organizer.Scan(root);
            Check(s.Count(Kind.Footage) == 1 && s.NoProxy.Count == 0, "프록시를 안 쓴 카드는 '프록시 없음' 알림 안 함");
            var r = Organizer.Run(s, true, true, true, null);
            Check(Exists(root, @"260930_DATA_1M\260930_Footage\C0100.MP4") && Exists(root, @"260930_DATA_1M\260930_Photo\arw\DSC00100.ARW"), "PRIVATE\\M4ROOT에서도 정리");
            Check(r.Trashed.SequenceEqual(new[] { "PRIVATE" }), "PRIVATE 통째로 휴지통");
        }

        // 자정 무렵: 시작 10/3 23:58, 끝(수정한 날짜) 10/4 00:03 → 시작한 날
        static void Midnight()
        {
            Console.WriteLine("[자정 무렵 촬영]");
            string root = Dir("midnight");
            string clip = Path.Combine(root, @"M4ROOT\CLIP"), sub = Path.Combine(root, @"M4ROOT\SUB");
            Touch(clip, "C0001.MP4", new DateTime(2026, 10, 4, 0, 3, 0));
            Xml(clip, "C0001", new DateTime(2026, 10, 3, 23, 58, 0), false);
            Touch(sub, "C0001S03.MP4", new DateTime(2026, 10, 4, 0, 3, 0));
            var s = Organizer.Scan(root);
            Check(s.Items.All(x => x.Day == "261003"), "영상·XML·프록시 모두 261003");
        }

        // 같은 폴더에 두 번째 카드: 번호가 같아 이름이 겹침 → 건너뛰고, 그 XML과 M4ROOT는 남김
        static void SecondCardSameNames()
        {
            Console.WriteLine("[두 번째 카드 · 같은 번호]");
            string root = Dir("twocards");
            Action<string> card = model => {
                string clip = Path.Combine(root, @"M4ROOT\CLIP");
                for (int n = 1; n <= 3; n++) {
                    var start = new DateTime(2026, 10, 10, 9, n, 0);
                    Touch(clip, "C000" + n + ".MP4", start);
                    Xml(clip, "C000" + n, start, false);
                }
                Touch(Path.Combine(root, @"M4ROOT\THMBNL"), "C0001T01.JPG");
            };
            card("A");
            Organizer.Run(Organizer.Scan(root), true, true, true, null);
            card("B");
            var s = Organizer.Scan(root);
            Check(s.Conflicts == 3 && s.Days["261010"].Existed, "같은 이름 3개 미리 알림 (기존 261010 폴더)");
            var r = Organizer.Run(s, true, true, true, null);
            Check(r.Skipped == 3 && r.Moved.Count == 0 && r.XmlTrashed == 0, "3개 건너뜀 · XML은 지우지 않음");
            Check(r.Kept.Contains("M4ROOT") && Exists(root, @"M4ROOT\CLIP\C0001M01.XML"), "정리 못 한 영상이 남은 M4ROOT와 XML은 그대로");
        }

        // DCIM·M4ROOT 없이 영상·XML·사진만 모은 폴더 + 소니가 아닌 파일
        static void LooseFiles()
        {
            Console.WriteLine("[파일만 모은 폴더]");
            string root = Dir("loose");
            var t = new DateTime(2026, 10, 12, 8, 0, 0);
            Touch(root, "C0100.MP4", t); Xml(root, "C0100", t, false); Touch(root, "C0100S03.MP4", t);
            Touch(root, "DSC00100.ARW", t);
            Touch(root, "phone_video.mp4", t);       // XML이 없는 MP4 → 소니 영상으로 보지 않음
            Touch(root, "IMG_0001.JPG", t);          // 소니 이름이 아닌 사진
            var s = Organizer.Scan(root);
            Check(s.Count(Kind.Footage) == 1 && s.Count(Kind.Proxy) == 1 && s.Count(Kind.Xml) == 1 && s.Count(Kind.Arw) == 1, "소니 영상·프록시·XML·ARW만");
            Check(s.Unhandled.Count == 2, "소니가 아닌 MP4·JPG는 그대로: " + s.Unhandled.Count);
            var r = Organizer.Run(s, true, true, true, null);
            Check(Exists(root, @"261012_DATA_1M\261012_Proxy\C0100_Proxy.MP4") && Exists(root, "phone_video.mp4"), "넣은 폴더 안에 날짜 폴더 · 다른 파일은 제자리");
        }

        // M4ROOT 폴더 자체를 넣음
        static void M4rootDropped()
        {
            Console.WriteLine("[M4ROOT 폴더를 넣음]");
            string root = Path.Combine(SonyCard("m4"), "M4ROOT");
            var r = Organizer.Run(Organizer.Scan(root), true, true, true, null);
            Check(r.Moved.Count == 11 && r.XmlTrashed == 5, "영상 6 · 프록시 5 옮기고 XML 5개 휴지통");
            var left = Directory.GetFileSystemEntries(root).Select(Path.GetFileName).OrderBy(x => x).ToList();
            Check(left.SequenceEqual(new[] { "261003_DATA_1M", "261004_DATA_1M" }), "M4ROOT 안에는 날짜 폴더만: " + string.Join(", ", left));
        }

        // 옛 AVCHD 영상(MTS)이 같이 있는 카드: 정리하지 않고 PRIVATE는 남김
        static void AvchdLeftover()
        {
            Console.WriteLine("[AVCHD 영상이 섞인 카드]");
            string root = Dir("avchd");
            string clip = Path.Combine(root, @"M4ROOT\CLIP");
            var t = new DateTime(2026, 10, 15, 9, 0, 0);
            Touch(clip, "C0001.MP4", t); Xml(clip, "C0001", t, false);
            Touch(Path.Combine(root, @"PRIVATE\AVCHD\BDMV\STREAM"), "00000.MTS", t);
            var s = Organizer.Scan(root);
            Check(s.Unhandled.Count == 1 && s.Unhandled[0].EndsWith("00000.MTS"), "MTS는 정리 규칙 없는 파일로 알림");
            var r = Organizer.Run(s, true, true, true, null);
            Check(r.Kept.Contains("PRIVATE") && Exists(root, @"PRIVATE\AVCHD\BDMV\STREAM\00000.MTS"), "MTS가 든 PRIVATE는 남김");
        }

        // 파일 이름 앞부분을 바꾼 카메라 (예: A7V_0001)도 XML·프록시 짝이 맞음
        static void CustomTitle()
        {
            Console.WriteLine("[파일 이름을 바꾼 카메라]");
            string root = Dir("title");
            var t = new DateTime(2026, 10, 20, 9, 0, 0);
            Touch(Path.Combine(root, @"M4ROOT\CLIP"), "A7V_0001.MP4", t);
            Xml(Path.Combine(root, @"M4ROOT\CLIP"), "A7V_0001", t, false);
            Touch(Path.Combine(root, @"M4ROOT\SUB"), "A7V_0001S03.MP4", t);
            var s = Organizer.Scan(root);
            Check(s.NoProxy.Count == 0 && s.Items.Any(x => x.Dest.EndsWith(@"261020_Proxy\A7V_0001_Proxy.MP4")), "A7V_0001S03 → A7V_0001_Proxy");
        }

        static void SizeFormat()
        {
            Console.WriteLine("[용량 표기]");
            const long MB = 1024L * 1024, GB = MB * 1024, TB = GB * 1024;
            Check(Organizer.FormatSize(1) == "1M" && Organizer.FormatSize(850 * MB) == "850M", "1바이트 → 1M · 850MB → 850M");
            Check(Organizer.FormatSize(GB * 26) == "26G" && Organizer.FormatSize(GB * 245 / 4) == "61.2G", "26GB → 26G · 61.25GB → 61.2G (내림)");
            Check(Organizer.FormatSize(TB * 3 / 2) == "1.5T", "1.5TB → 1.5T");
        }

        static void Guards()
        {
            Console.WriteLine("[막아야 하는 경우]");
            Check(Organizer.Scan(@"C:\").Error.Contains("맨 위"), "드라이브 맨 위는 막음");
            Check(Organizer.Scan(Path.Combine(temp, "없는 폴더")).Error != null, "없는 폴더");
            string dji = Dir("dji");
            Touch(Path.Combine(dji, @"DCIM\DJI_001"), "DJI_20260823135545_0039_D.MP4");
            Touch(Path.Combine(dji, @"DCIM\DJI_001"), "DJI_20260823135545_0039_D.LRF");
            var e = Organizer.Scan(dji).Error;
            Check(e != null && e.Contains("DJI"), "DJI 카드는 할 일 없음 (DJI 카드라고 알려 줌)");
        }

        // 탐색기·Mac·NAS 찌꺼기가 있는 카드: 영상·사진으로 섞지 않고, DCIM 등이 남지 않음
        static void JunkLeftovers()
        {
            Console.WriteLine("[탐색기·Mac·NAS 찌꺼기]");
            string root = SonyCard("junk");
            Touch(Path.Combine(root, @"DCIM\100MSDCF"), "Thumbs.db");
            Touch(Path.Combine(root, "DCIM"), "desktop.ini");
            Touch(Path.Combine(root, "DCIM"), ".DS_Store");
            Touch(Path.Combine(root, @"DCIM\100MSDCF"), "._DSC09566.JPG");
            Touch(Path.Combine(root, @"M4ROOT\CLIP"), "._C0561.MP4");
            Touch(Path.Combine(root, @"DCIM\100MSDCF\@eaDir\DSC09566.JPG"), "SYNOPHOTO_THUMB_XL.jpg");
            Touch(Path.Combine(root, ".Spotlight-V100"), "store.db");
            Touch(Path.Combine(root, @"DCIM\MISC"), ".dropbox.device");       // 실제로 남았던 경우 (261017)
            var s = Organizer.Scan(root);
            Check(s.Count(Kind.Footage) == 6 && s.Count(Kind.Jpg) == 1 && s.Unhandled.Count == 0, "._C0561.MP4·@eaDir 썸네일은 영상·사진으로 세지 않음");
            var r = Organizer.Run(s, true, true, true, null);
            var left = Directory.GetFileSystemEntries(root).Select(Path.GetFileName).OrderBy(x => x).ToList();
            Check(left.SequenceEqual(new[] { "261003_DATA_1M", "261004_DATA_1M" }), "날짜 폴더만 남음: " + string.Join(", ", left));
            Check(r.Kept.Count == 0 && r.TrashFailed.Count == 0, "남긴 폴더 없음");
        }

        // 사진을 안 찍은 카드(빈 DCIM\100MSDCF) · 카드 관리 파일 옵션 끔 → 빈 DCIM은 지우고 M4ROOT 등은 그대로
        static void EmptyDcimOptionOff()
        {
            Console.WriteLine("[사진 없는 카드 · 카드 관리 파일은 그대로]");
            string root = SonyCard("nophoto");
            Directory.Delete(Path.Combine(root, "DCIM"), true);
            Directory.CreateDirectory(Path.Combine(root, @"DCIM\100MSDCF"));
            Directory.CreateDirectory(Path.Combine(root, @"DCIM\101MSDCF"));
            recycled = 0;
            var r = Organizer.Run(Organizer.Scan(root), true, false, true, null);
            Check(!Directory.Exists(Path.Combine(root, "DCIM")), "파일 없는 DCIM은 지움");
            Check(Directory.Exists(Path.Combine(root, "M4ROOT")) && Directory.Exists(Path.Combine(root, "SONY")) && r.Trashed.Count == 0, "M4ROOT·SONY는 그대로");
        }

        // 탐색기 창이 M4ROOT\THMBNL을 열고 있어 M4ROOT를 휴지통으로 못 보냄 → '남은 파일' 대신 '보내지 못함'으로 알리고 XML은 따로 보냄
        static void LockedFolder()
        {
            Console.WriteLine("[탐색기가 카드 폴더를 열고 있음]");
            string root = SonyCard("locked");
            using (var h = CreateFile(Path.Combine(root, @"M4ROOT\THMBNL"), 0x1, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero)) {
                var r = Organizer.Run(Organizer.Scan(root), true, true, true, null);
                Check(r.TrashFailed.SequenceEqual(new[] { "M4ROOT" }) && !r.Kept.Contains("M4ROOT"), "M4ROOT는 '휴지통으로 보내지 못함': " + string.Join(",", r.TrashFailed));
                Check(r.XmlTrashed == 5 && r.XmlTrashFailed == 0, "M4ROOT 안의 XML 5개는 따로 휴지통");
                Check(!Directory.Exists(Path.Combine(root, @"M4ROOT\CLIP")), "비게 된 CLIP은 정리");
            }

            // 모르는 파일이 든 폴더는 남기고, 결과에 그 파일을 이유로 보여 줌
            string other = SonyCard("unknown");
            Touch(Path.Combine(other, @"PRIVATE\DATABASE"), "DATABASE.DB");
            var k = Organizer.Run(Organizer.Scan(other), true, true, true, null);
            string why;
            Check(k.Kept.Contains("PRIVATE") && k.KeptBecause.TryGetValue("PRIVATE", out why) && why == @"DATABASE\DATABASE.DB", "PRIVATE는 남기고 이유 = DATABASE\\DATABASE.DB");
        }

        // 카드의 PRIVATE 안쪽(M4ROOT·DATABASE)만 복사한 폴더 → DATABASE도 남지 않음 (실제로 남았던 경우, 261017)
        // 소니 카드가 아닌 폴더의 DATABASE는 건드리지 않음
        static void PrivateContents()
        {
            Console.WriteLine("[PRIVATE 안쪽만 복사한 폴더]");
            string root = Dir("privonly");
            string clip = Path.Combine(root, @"M4ROOT\CLIP");
            var t = new DateTime(2026, 10, 17, 9, 0, 0);
            Touch(clip, "C0001.MP4", t); Xml(clip, "C0001", t, false);
            Touch(Path.Combine(root, @"M4ROOT\SUB"), "C0001S03.MP4", t);
            Touch(Path.Combine(root, "M4ROOT"), "MEDIAPRO.XML");
            Touch(Path.Combine(root, "DATABASE"), "DATABASE.BIN");
            var r = Organizer.Run(Organizer.Scan(root), true, true, true, null);
            var left = Directory.GetFileSystemEntries(root).Select(Path.GetFileName).ToList();
            Check(left.SequenceEqual(new[] { "261017_DATA_1M" }) && r.Trashed.Contains("DATABASE"), "날짜 폴더만 남음 (DATABASE 휴지통): " + string.Join(", ", left));

            string loose = Dir("loosedb");
            Touch(loose, "C0002.MP4", t); Xml(loose, "C0002", t, false);
            Touch(Path.Combine(loose, "DATABASE"), "DATABASE.BIN");
            Organizer.Run(Organizer.Scan(loose), true, true, true, null);
            Check(Exists(loose, @"DATABASE\DATABASE.BIN"), "소니 카드가 아닌 폴더의 DATABASE는 그대로");
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr sec, uint disp, uint flags, IntPtr tmpl);

        // ── 실제 폴더 복제본 (XML은 내용까지, 나머지는 1바이트) ────────
        static void Replica(string src)
        {
            src = Path.GetFullPath(src).TrimEnd('\\');
            string root = Dir("replica");
            int n = 0;
            foreach (var f in new DirectoryInfo(src).EnumerateFiles("*", SearchOption.AllDirectories)) {
                string dst = Path.Combine(root, f.FullName.Substring(src.Length + 1));
                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                if (f.Extension.Equals(".xml", StringComparison.OrdinalIgnoreCase)) File.Copy(f.FullName, dst);
                else File.WriteAllBytes(dst, new byte[] { 0 });
                File.SetLastWriteTime(dst, f.LastWriteTime);
                n++;
            }
            Console.WriteLine("[복제본] " + src + "  파일 " + n + "개");
            var s = Organizer.Scan(root);
            Check(s.Error == null, "미리보기 오류 없음 " + (s.Error ?? ""));
            foreach (var g in s.Items.GroupBy(x => x.Day).OrderBy(g => g.Key))
                Console.WriteLine(string.Format("    {0}_DATA  영상 {1} · 프록시 {2} · XML {3} · 사진 {4}", g.Key,
                    g.Count(x => x.Kind == Kind.Footage), g.Count(x => x.Kind == Kind.Proxy), g.Count(x => x.Kind == Kind.Xml),
                    g.Count(x => x.Kind == Kind.Jpg || x.Kind == Kind.Arw || x.Kind == Kind.Hif)));
            Console.WriteLine("    프록시 없는 원본: " + string.Join(", ", s.NoProxy) + "   그대로 둘 파일: " + s.Unhandled.Count + "   같은 이름: " + s.Conflicts);
            var r = Organizer.Run(s, true, true, true, null);
            Console.WriteLine(string.Format("    옮김 {0} · XML 휴지통 {1} · 카드 폴더 휴지통 {2} · 확인 {3}/{4}",
                r.Moved.Count, r.XmlTrashed, string.Join(",", r.Trashed), r.Verified, r.Moved.Count));
            Console.WriteLine("    정리 후 맨 위: " + string.Join(", ", Directory.GetFileSystemEntries(root).Select(Path.GetFileName)));
            Check(r.Failed == 0 && r.VerifyFailed == 0, "전부 옮기고 확인 통과");
        }

        // ── 도우미 ────────────────────────────────────────────────
        static void Check(bool ok, string name)
        {
            Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + name);
            if (!ok) failures++;
        }

        static string Dir(string name) { string d = Path.Combine(temp, name); Directory.CreateDirectory(d); return d; }

        static void Touch(string dir, string name, DateTime? time = null)
        {
            Directory.CreateDirectory(dir);
            string p = Path.Combine(dir, name);
            File.WriteAllBytes(p, new byte[] { 0 });
            File.SetLastWriteTime(p, time ?? new DateTime(2026, 1, 1));
        }

        // 실제 카드의 XML과 같은 형식 (CreationDate·RecordingMode)
        static void Xml(string dir, string clipBase, DateTime start, bool sq)
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, clipBase + "M01.XML"),
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n" +
                "<NonRealTimeMeta xmlns=\"urn:schemas-professionalDisc:nonRealTimeMeta:ver.2.20\">\r\n" +
                "\t<Duration value=\"624\"/>\r\n" +
                "\t<CreationDate value=\"" + start.ToString("yyyy-MM-ddTHH:mm:ss") + "+09:00\"/>\r\n" +
                "\t<VideoFormat><VideoFrame videoCodec=\"HEVC_3840_2160_M42210P@L5HT\" captureFps=\"" + (sq ? "119.88p" : "23.98p") + "\" formatFps=\"23.98p\"/></VideoFormat>\r\n" +
                "\t<Device manufacturer=\"Sony\" modelName=\"ILCE-7M5\" serialNo=\"4294967295\"/>\r\n" +
                "\t<RecordingMode type=\"" + (sq ? "slowAndQuickMotion" : "normal") + "\" cacheRec=\"false\"/>\r\n" +
                "</NonRealTimeMeta>\r\n");
            File.SetLastWriteTime(Path.Combine(dir, clipBase + "M01.XML"), start);
        }

        static bool Exists(string root, string rel) { return File.Exists(Path.Combine(root, rel)); }

        static bool IsMedia(string rel)
        {
            string e = Path.GetExtension(rel).ToUpperInvariant();
            return (e == ".MP4" || e == ".JPG" || e == ".ARW" || e == ".HIF") && rel.IndexOf("THMBNL", StringComparison.OrdinalIgnoreCase) < 0;
        }

        static HashSet<string> Snapshot(string root)
        {
            return new HashSet<string>(Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
                                                .Select(p => p.Substring(root.Length + 1)), StringComparer.OrdinalIgnoreCase);
        }
    }
}
