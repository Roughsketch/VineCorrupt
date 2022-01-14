using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Security.Cryptography;
using System.Net;
using System.IO;
using Newtonsoft.Json;
using System.Windows;

using System.Diagnostics;

namespace VineCorrupt
{
    class Updater
    {
        private struct VersionInfo
        {
            public string md5;
            public string url;
            public string version;
            public string witmd5;
            public string wit;
        }

        public static bool IsUpdateAvailable(string current)
        {
            VersionInfo json = JsonConvert.DeserializeObject<VersionInfo>(new WebClient().DownloadString("http://maiddog.com/projects/corrupter/version.json"));

            if (json.version == current)
            {
                return false;
            }
            return true;
        }

        public static void Update()
        {
            Process updater = new Process();
            updater.StartInfo.FileName = System.IO.Path.GetFullPath("bin/updater.exe");
            updater.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
            updater.Start();

            Application.Current.Shutdown();
        }

        public static void GetWIT()
        {
            Process updater = new Process();
            updater.StartInfo.FileName = System.IO.Path.GetFullPath("bin/updater.exe");
            updater.StartInfo.Arguments = "--wit";
            updater.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
            updater.Start();

            updater.WaitForExit();
        }

        private static string GetMD5()
        {
            using (var md5 = MD5.Create())
            {
                using (FileStream stream = File.OpenRead(Process.GetCurrentProcess().MainModule.FileName))
                {
                    return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToUpper();
                }
            }
        }
    }
}
