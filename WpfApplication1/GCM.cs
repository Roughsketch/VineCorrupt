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
    class GCM
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

            Process wit = new Process();
            wit.StartInfo.FileName = System.IO.Path.GetFullPath("bin/mdgcm.exe");
            wit.StartInfo.Arguments = "files \"" + file + "\"";

            wit.StartInfo.RedirectStandardOutput = true;
            wit.StartInfo.RedirectStandardError = true;
            wit.EnableRaisingEvents = true;
            wit.StartInfo.CreateNoWindow = true;

            wit.OutputDataReceived += Reciever;
            wit.ErrorDataReceived += Reciever;

            wit.StartInfo.UseShellExecute = false;
            wit.Start();

            wit.BeginErrorReadLine();
            wit.BeginOutputReadLine();

            wit.WaitForExit();

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
                    
                    for (int i = 0; i < 6; i++)
                    {
                        destination += Convert.ToChar(fs.ReadByte());
                    }
                }
                
                if (destination.Contains(Path.GetInvalidPathChars().ToString()))
                {
                    MessageBox.Show("GCM does not start with a valid identifier.");
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

            Process gcm = new Process();
            gcm.StartInfo.FileName = System.IO.Path.GetFullPath("bin/mdgcm.exe");
            gcm.StartInfo.Arguments = "extract \"" + file + "\" \"" + destination + "\"";

            gcm.StartInfo.RedirectStandardOutput = true;
            gcm.StartInfo.RedirectStandardError = true;
            gcm.EnableRaisingEvents = true;
            gcm.StartInfo.CreateNoWindow = true;

            gcm.OutputDataReceived += Reciever;
            gcm.ErrorDataReceived += Reciever;

            gcm.StartInfo.UseShellExecute = false;
            gcm.Start();

            gcm.BeginErrorReadLine();
            gcm.BeginOutputReadLine();

            gcm.WaitForExit();

            callback(destination);
        }

        public static void Create(string folder, string destination, Action<string, string> callback)
        {
            Process gcm = new Process();
            gcm.StartInfo.FileName = System.IO.Path.GetFullPath("bin/mdgcm.exe");
            gcm.StartInfo.Arguments = "build \"" + folder + "\" \"" + destination + "\"";

            gcm.StartInfo.RedirectStandardOutput = true;
            gcm.StartInfo.RedirectStandardError = true;
            gcm.EnableRaisingEvents = true;
            gcm.StartInfo.CreateNoWindow = true;

            gcm.OutputDataReceived += Reciever;
            gcm.ErrorDataReceived += Reciever;

            gcm.StartInfo.UseShellExecute = false;
            gcm.Start();

            gcm.BeginErrorReadLine();
            gcm.BeginOutputReadLine();

            gcm.WaitForExit();

            callback(folder, destination);
        }
    }
}
