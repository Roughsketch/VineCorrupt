using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VineCorrupt
{
    class NDS
    {
        private static string m_output;

        public static void Reciever(object sender, DataReceivedEventArgs e)
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

            Process nds = new Process();
            nds.StartInfo.FileName = System.IO.Path.GetFullPath("bin/mdnds.exe");
            nds.StartInfo.Arguments = "files \"" + file + "\"";

            nds.StartInfo.RedirectStandardOutput = true;
            nds.StartInfo.RedirectStandardError = true;
            nds.EnableRaisingEvents = true;
            nds.StartInfo.CreateNoWindow = true;

            nds.OutputDataReceived += Reciever;
            nds.ErrorDataReceived += Reciever;

            nds.StartInfo.UseShellExecute = false;
            nds.Start();

            nds.BeginErrorReadLine();
            nds.BeginOutputReadLine();

            nds.WaitForExit();

            foreach (var line in m_output.Split('\n'))
            {
                if (line.StartsWith("./"))
                {
                    ret.Add(line);
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


                using (FileStream fs = new FileStream(fi.FullName, FileMode.Open, FileAccess.Read))
                {
                    destination += "/";

                    fs.Seek(12, SeekOrigin.Begin);

                    for (int i = 0; i < 6; i++)
                    {
                        destination += Convert.ToChar(fs.ReadByte());
                    }
                }

                if (destination.Contains(Path.GetInvalidPathChars().ToString()))
                {
                    MessageBox.Show("NDS does not start with a valid identifier.");
                    File.AppendAllText("log/error.log", "Invalid directory string taken from " + file + "\n\tDestination: " + destination);
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: Could not open file for reading.");
                File.AppendAllText("log/error.log", "User does not have access to file: " + file + "\n\n" + ex.Message);
                return;
            }

            //  If it was already extracted then don't do it again
            if (Directory.Exists(destination))
            {
                callback(destination);
                return;
            }

            Process nds = new Process();
            nds.StartInfo.FileName = System.IO.Path.GetFullPath("bin/mdnds.exe");
            nds.StartInfo.Arguments = "extract \"" + file + "\" \"" + destination + "\"";

            nds.StartInfo.RedirectStandardOutput = true;
            nds.StartInfo.RedirectStandardError = true;
            nds.EnableRaisingEvents = true;
            nds.StartInfo.CreateNoWindow = true;

            nds.OutputDataReceived += Reciever;
            nds.ErrorDataReceived += Reciever;

            nds.StartInfo.UseShellExecute = false;
            nds.Start();

            nds.BeginErrorReadLine();
            nds.BeginOutputReadLine();

            nds.WaitForExit();

            callback(destination);
        }

        public static void Create(string folder, string destination, Action<string, string> callback)
        {
            Process nds = new Process();
            nds.StartInfo.FileName = System.IO.Path.GetFullPath("bin/mdnds.exe");
            nds.StartInfo.Arguments = "build \"" + folder + "\" \"" + destination + "\"";

            nds.StartInfo.RedirectStandardOutput = true;
            nds.StartInfo.RedirectStandardError = true;
            nds.EnableRaisingEvents = true;
            nds.StartInfo.CreateNoWindow = true;

            nds.OutputDataReceived += Reciever;
            nds.ErrorDataReceived += Reciever;

            nds.StartInfo.UseShellExecute = false;
            nds.Start();

            nds.BeginErrorReadLine();
            nds.BeginOutputReadLine();

            nds.WaitForExit();

            callback(folder, destination);
        }
    }
}
