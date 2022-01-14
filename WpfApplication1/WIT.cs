using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace VineCorrupt
{
    class WIT
    {
        private static string m_output;

        public static void WIT_Reciever(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                m_output += e.Data + "\n";
            }
        }

        public static List<string> Files(string file)
        {
            m_output = "";
            List<string> ret = new List<string>();

            Process wit = new Process();
            wit.StartInfo.FileName = System.IO.Path.GetFullPath("wit/wit.exe");
            wit.StartInfo.Arguments = "FILES \"" + file + "\"";

            wit.StartInfo.RedirectStandardOutput = true;
            wit.StartInfo.RedirectStandardError = true;
            wit.EnableRaisingEvents = true;
            wit.StartInfo.CreateNoWindow = true;

            wit.OutputDataReceived += WIT_Reciever;
            wit.ErrorDataReceived += WIT_Reciever;

            wit.StartInfo.UseShellExecute = false;
            wit.Start();

            wit.BeginErrorReadLine();
            wit.BeginOutputReadLine();

            wit.WaitForExit();

            foreach(var line in m_output.Split('\n'))
            {
                string start = line.Split('/').First();
                if (line.StartsWith(start + "/files") && line.EndsWith("/") == false)
                {
                    ret.Add("." + line.Substring(start.Length));
                }
            }

            return ret;
        }

        public static void Extract(string file, string destination, Action<string> callback)
        {
            try
            {
                if (!File.Exists(file))
                {
                    System.Windows.MessageBox.Show("Cannot extract data from file because it does not exist:\n\n" + file);
                    return;
                }

                FileInfo fi = new FileInfo(file);

                if (file.EndsWith(".wbfs"))
                {
                    using (FileStream fs = new FileStream(fi.FullName, FileMode.Open, FileAccess.Read))
                    {
                        fs.Position = 0x200;
                        destination += "/";

                        for (int i = 0; i < 6; i++)
                        {
                            destination += Convert.ToChar(fs.ReadByte());
                        }
                    }
                }
                else if (file.EndsWith(".iso"))
                {
                    using (FileStream fs = new FileStream(fi.FullName, FileMode.Open, FileAccess.Read))
                    {
                        destination += "/";

                        for (int i = 0; i < 6; i++)
                        {
                            destination += Convert.ToChar(fs.ReadByte());
                        }
                    }
                }
                else
                {
                    destination += "/" + Path.GetFileName(fi.FullName).Split('.')[0];
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText("log/error.log", "User does not have access to file: " + file);
                return;
            }

            if (Directory.Exists(destination))
            {
                callback(destination);
                return;
            }

            Process wit = new Process();
            wit.StartInfo.FileName = System.IO.Path.GetFullPath("wit/wit.exe");
            wit.StartInfo.Arguments = "EXTRACT \"" + file + "\" --dest=\"" + destination + "\"";

            wit.StartInfo.RedirectStandardOutput = true;
            wit.StartInfo.RedirectStandardError = true;
            wit.EnableRaisingEvents = true;
            wit.StartInfo.CreateNoWindow = true;

            wit.OutputDataReceived += WIT_Reciever;
            wit.ErrorDataReceived += WIT_Reciever;

            wit.StartInfo.UseShellExecute = false;
            wit.Start();

            wit.BeginErrorReadLine();
            wit.BeginOutputReadLine();

            wit.WaitForExit();

            callback(destination);
        }

        public static void Create(string folder, string destination, Action<string, string> callback)
        {
            Process wit = new Process();
            wit.StartInfo.FileName = System.IO.Path.GetFullPath("wit/wit.exe");
            wit.StartInfo.Arguments = "COPY \"" + folder + "\" --dest=\"" + destination + "\"";

            wit.StartInfo.RedirectStandardOutput = true;
            wit.StartInfo.RedirectStandardError = true;
            wit.EnableRaisingEvents = true;
            wit.StartInfo.CreateNoWindow = true;

            wit.OutputDataReceived += WIT_Reciever;
            wit.ErrorDataReceived += WIT_Reciever;

            wit.StartInfo.UseShellExecute = false;
            wit.Start();

            wit.BeginErrorReadLine();
            wit.BeginOutputReadLine();

            wit.WaitForExit();

            callback(folder, destination);
        }

        /*
         * Debug user rights for file access denied
        private static bool IsAdmin()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        */
    }
}
