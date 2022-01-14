using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VineCorrupt
{
    class Logger
    {
        public static void Error(string msg)
        {
            System.IO.File.AppendAllText("log/error.log", string.Format("[{0}]\t-\t{1}\n", DateTime.Now, msg));
        }

        public static void Warning(string msg)
        {
            System.IO.File.AppendAllText("log/warn.log", string.Format("[{0}]\t-\t{1}\n", DateTime.Now, msg));
        }
    }
}
