using System;
using System.IO;

namespace SonySortTool
{
    // %LOCALAPPDATA%\SonySortTool\settings.ini
    class Settings
    {
        public bool DeleteXml = true;   // XML(카메라 메타데이터)을 휴지통으로. 끄면 날짜별 XML 폴더로
        public bool AddSize   = true;   // 날짜 폴더 이름 끝에 용량 (261004_DATA_26G)
        public bool TrashCard = true;   // 정리 후 카드 관리 파일(썸네일·카드 정보)을 휴지통으로

        static string FilePath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SonySortTool", "settings.ini"); }
        }

        public static Settings Load()
        {
            var s = new Settings();
            try {
                foreach (var line in File.ReadAllLines(FilePath)) {
                    var p = line.Split(new[] { '=' }, 2);
                    if (p.Length != 2) continue;
                    bool on = p[1].Trim() == "1";
                    switch (p[0].Trim()) {
                        case "DeleteXml": s.DeleteXml = on; break;
                        case "AddSize":   s.AddSize   = on; break;
                        case "TrashCard": s.TrashCard = on; break;
                    }
                }
            } catch (Exception) { }
            return s;
        }

        public void Save()
        {
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath,
                    "DeleteXml=" + (DeleteXml ? "1" : "0") + "\r\n" +
                    "AddSize="   + (AddSize   ? "1" : "0") + "\r\n" +
                    "TrashCard=" + (TrashCard ? "1" : "0") + "\r\n");
            } catch (Exception) { }
        }
    }
}
