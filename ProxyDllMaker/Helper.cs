using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;
using System.ComponentModel;

namespace ProxyDllMaker
{
    public static class Helper
    {
        [DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SymInitialize(IntPtr hProcess, string UserSearchPath, [MarshalAs(UnmanagedType.Bool)]bool fInvadeProcess);

        [DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SymCleanup(IntPtr hProcess);

        [DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern ulong SymLoadModuleEx(IntPtr hProcess, IntPtr hFile,
             string ImageName, string ModuleName, long BaseOfDll, int DllSize, IntPtr Data, int Flags);

        [DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SymEnumerateSymbols64(IntPtr hProcess,
           ulong BaseOfDll, SymEnumerateSymbolsProc64 EnumSymbolsCallback, IntPtr UserContext);

        public delegate bool SymEnumerateSymbolsProc64(string SymbolName,
              ulong SymbolAddress, uint SymbolSize, IntPtr UserContext);

        public static string DumpObject(object obj)
        {
            System.Runtime.Serialization.Json.DataContractJsonSerializer serializer = new System.Runtime.Serialization.Json.DataContractJsonSerializer(obj.GetType());
            MemoryStream ms = new MemoryStream();
            serializer.WriteObject(ms, obj);
            string midstep = Encoding.Default.GetString(ms.ToArray());
            return FormatJson(midstep);
        }
        public static string FormatJson(string json)
        {
            string INDENT_STRING = "  ";
            int indentation = 0;
            int quoteCount = 0;
            var result =
                from ch in json
                let quotes = ch == '"' ? quoteCount++ : quoteCount
                let lineBreak = ch == ',' && quotes % 2 == 0 ? ch + Environment.NewLine + String.Concat(Enumerable.Repeat(INDENT_STRING, indentation)) : null
                let openChar = ch == '{' || ch == '[' ? ch + Environment.NewLine + String.Concat(Enumerable.Repeat(INDENT_STRING, ++indentation)) : ch.ToString()
                let closeChar = ch == '}' || ch == ']' ? Environment.NewLine + String.Concat(Enumerable.Repeat(INDENT_STRING, --indentation)) + ch : ch.ToString()
                select lineBreak == null
                            ? openChar.Length > 1
                                ? openChar
                                : closeChar
                            : lineBreak;
            return String.Concat(result);
        }

        private static List<ExportInfo> infolist;

        public static bool EnumSyms(string name, ulong address, uint size, IntPtr context)
        {
            ExportInfo ex = new ExportInfo();
            ex.Name = name;
            infolist.Add(ex);
            return true;
        }

        public static List<ExportInfo> GetExports(string path)
        {
            infolist = new List<ExportInfo>();
            IntPtr hCurrentProcess = Process.GetCurrentProcess().Handle;
            ulong baseOfDll;
            bool status;
            status = SymInitialize(hCurrentProcess, null, false);
            if (status == false)
                throw new Exception("Failed to initialize sym.");
            baseOfDll = SymLoadModuleEx(hCurrentProcess, IntPtr.Zero, path, null, 0, 0, IntPtr.Zero, 0);
            if (baseOfDll == 0)
            {
                SymCleanup(hCurrentProcess);
                throw new Exception("Failed to load module.");
            }
            if (SymEnumerateSymbols64(hCurrentProcess, baseOfDll, EnumSyms, IntPtr.Zero) == false)
            {
                throw new Exception("Failed to enum symbols.");
            }
            SymCleanup(hCurrentProcess);
            for (int i = 0; i < infolist.Count; i++)
            {
                ExportInfo e = infolist[i];
                e.Index = i;
                infolist[i] = e;
            }
            return infolist;
        }

        public struct ExportInfo
        {
            public string Name;
            public string Definition;
            public int WayOfExport; //0=none, 1=withasm, 2=withcalls, 3=withlink
            public int Index;
        }

        public static string ExportInfoToString(ExportInfo e)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(e.Index + "\t: \"" + e.Name + "\"");
            for (int i = 0; i < 10; i++)
                if (e.Name.Length + 12 < i * 8)
                    sb.AppendLine("\t");
            sb.AppendLine("Export:");
            switch (e.WayOfExport)
            {
                case 0:
                    sb.Append(" not exported");
                    break;
                case 1:
                    sb.Append(" with asm jmp");
                    break;
                case 2:
                    sb.Append(" with call");
                    break;
                case 3:
                    sb.Append(" with link");
                    break;

            }
            return sb.ToString();
        }
    }
}
