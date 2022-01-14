using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Security.Cryptography;
namespace MD5
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            System.Windows.Forms.Clipboard.SetText(GetMD5(args[0]));
        }

        private static string GetMD5(string file)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToUpper();
                }
            }
        }
    }
}
