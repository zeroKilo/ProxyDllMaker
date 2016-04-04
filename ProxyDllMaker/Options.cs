using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProxyDllMaker
{
    public static class Options
    {
        public static string suffix = "_org";
        public static string prefix = "Proxy_";

        public static void SaveToFile(string path)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("suffix=" + suffix);
            sb.AppendLine("prefix=" + prefix);
            File.WriteAllText(path, sb.ToString());
        }
        public static void LoadFromFile(string path)
        {            
            string[] lines = File.ReadAllLines(path);
            foreach (string line in lines)
            {
                try
                {
                    string[] parts = line.Split('=');
                    switch (parts[0].Trim().ToLower())
                    {
                        case "suffix":
                            suffix = parts[1].Trim();
                            break;
                        case "prefix":
                            prefix = parts[1].Trim();
                            break;
                    }
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
