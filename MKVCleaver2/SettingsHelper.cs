using System.Collections.Generic;
using System.IO;
using System.Text;
using IniParser;
using IniParser.Model;
using System.Runtime.InteropServices;

namespace MKVCleaver2
{
    static class SettingsHelper
    {
        private const string settingsFileName = "settings.ini";
        private static FileIniDataParser parser = new FileIniDataParser();
        private static IniData iniData = new IniData();

        public static void Init()
        {
            if (File.Exists(settingsFileName))
            {
                iniData = parser.ReadFile(settingsFileName);
            }
            else
            {
                File.Create(settingsFileName);
                iniData = new IniData();
            }
        }

        public static void SetToolnixPath(string path)
        {
            iniData["General"]["ToolnixPath"] = path;
            parser.WriteFile(settingsFileName, iniData);
        }

        public static string GetToolnixPath()
        {
            var path = iniData["General"]["ToolnixPath"];
            if (string.IsNullOrEmpty(path))
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    if (File.Exists("/usr/bin/mkvinfo"))
                        return "/usr/bin";
                    if (File.Exists("/usr/local/bin/mkvinfo"))
                        return "/usr/local/bin";
                }
            }
            return path ?? "";
        }

        public static string GetMkvInfoPath()
        {
            var toolnixPath = GetToolnixPath();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return Path.Combine(toolnixPath, "mkvinfo.exe");
            else
                return Path.Combine(toolnixPath, "mkvinfo");
        }

        public static string GetMkvExtractPath()
        {
            var toolnixPath = GetToolnixPath();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return Path.Combine(toolnixPath, "mkvextract.exe");
            else
                return Path.Combine(toolnixPath, "mkvextract");
        }

        public static string GetCodecContainerExtension(string codecId)
        {
            switch (codecId)
            {
                case "V_MPEG4/ISO/AVC":
                    return ".h264";
                case "A_AAC":
                    return ".aac";
                case "A_OPUS":
                    return ".opus";
                case "A_AC3":
                    return ".ac3";
                case "A_DTS":
                    return ".dts";
                case "A_MP3":
                    return ".mp3";
                case "S_TEXT/ASS":
                    return ".ass";
                case "S_TEXT/SSA":
                    return ".ssa";
                case "S_TEXT/SRT":
                    return ".srt";
                default:
                    return ".unknown";
            }
        }
    }
}