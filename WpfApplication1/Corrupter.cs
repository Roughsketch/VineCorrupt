using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Diagnostics;
using System.Windows;

namespace VineCorrupt
{
    class Corrupter
    {
        private string m_output;


        public Corrupter()
        {

        }

        public string Output()
        {
            return m_output;
        }

        private void Corrupt_Reciever(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                m_output += e.Data + "\n";
            }
        }

        public string Corrupt(string args)
        {
            m_output = "";

            Process corrupter = new Process();
            corrupter.StartInfo.FileName = System.IO.Path.GetFullPath("bin/mdcorrupt.exe");
            corrupter.StartInfo.Arguments = args;

            corrupter.StartInfo.RedirectStandardOutput = true;
            corrupter.StartInfo.RedirectStandardError = true;
            corrupter.EnableRaisingEvents = true;
            corrupter.StartInfo.CreateNoWindow = true;

            corrupter.OutputDataReceived += Corrupt_Reciever;
            corrupter.ErrorDataReceived += Corrupt_Reciever;

            corrupter.StartInfo.UseShellExecute = false;
            corrupter.Start();

            corrupter.BeginErrorReadLine();
            corrupter.BeginOutputReadLine();

            corrupter.WaitForExit();

            return m_output;
        }
    }
}
