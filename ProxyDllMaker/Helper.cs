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
        public static extern bool SymEnumerateSymbols64(IntPtr hProcess, ulong BaseOfDll, SymEnumerateSymbolsProc64 EnumSymbolsCallback, IntPtr UserContext);
        public delegate bool SymEnumerateSymbolsProc64(string SymbolName, ulong SymbolAddress, uint SymbolSize, IntPtr UserContext);


        [DllImport("dbghelp.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SymEnumSymbols(IntPtr hProcess, ulong BaseOfDll, string Mask, SymEnumSymbolsCallback EnumSymbolsCallback, IntPtr UserContext);
        public delegate bool SymEnumSymbolsCallback(ref SYMBOL_INFO pSymInfo, uint SymbolSize, IntPtr UserContext);

        public const int MAX_SYM_NAME = 2000;
        public enum SYMFLAG : uint
        {
            SYMFLAG_VALUEPRESENT = 0x00000001,
            SYMFLAG_REGISTER = 0x00000008,
            SYMFLAG_REGREL = 0x00000010,
            SYMFLAG_FRAMEREL = 0x00000020,
            SYMFLAG_PARAMETER = 0x00000040,
            SYMFLAG_LOCAL = 0x00000080,
            SYMFLAG_CONSTANT = 0x00000100,
            SYMFLAG_EXPORT = 0x00000200,
            SYMFLAG_FORWARDER = 0x00000400,
            SYMFLAG_FUNCTION = 0x00000800,
            SYMFLAG_VIRTUAL = 0x00001000,
            SYMFLAG_THUNK = 0x00002000,
            SYMFLAG_TLSREL = 0x00004000,
            SYMFLAG_SLOT = 0x00008000,
            SYMFLAG_ILREL = 0x00010000,
            SYMFLAG_METADATA = 0x00020000,
            SYMFLAG_CLR_TOKEN = 0x00040000,
            SYMFLAG_NULL = 0x00080000,
            SYMFLAG_FUNC_NO_RETURN = 0x00100000,
            SYMFLAG_SYNTHETIC_ZEROBASE = 0x00200000,
            SYMFLAG_PUBLIC_CODE = 0x00400000,
            SYMFLAG_REGREL_ALIASINDIR = 0x00800000,
            SYMFLAG_RESET = 0x80000000,
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SYMBOL_INFO
		{
			public uint SizeOfStruct;
			public uint TypeIndex;
			public ulong Reserved0;
			private ulong Reserved1;
			public uint Index;
			public uint Size;
			public ulong ModBase;
			public SYMFLAG Flags;
			public ulong Value;
			public ulong Address;
			public uint Register;
			public uint Scope;
			public uint Tag;
			public uint NameLen;
			public uint MaxNameLen;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_SYM_NAME)]
			public string Name;
		}

        public enum SymbolRetrieveMethod
        {
            SymEnumSymbols,
            SymEnumerateSymbols64,
            DirectHeaderReading
        }
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
        public static bool EnumSymbolsCallback(ref SYMBOL_INFO pSymInfo, uint SymbolSize, IntPtr UserContext)
        {
            ExportInfo ex = new ExportInfo();
            ex.Name = pSymInfo.Name;
            infolist.Add(ex);
            return true;
        }
        public static bool EnumerateSymbols64Callback(string name, ulong address, uint size, IntPtr context)
        {
            ExportInfo ex = new ExportInfo();
            ex.Name = name;
            infolist.Add(ex);
            return true;
        }

        public static List<ExportInfo> GetExports(string path, SymbolRetrieveMethod method)
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
            switch(method)
            {
                case SymbolRetrieveMethod.SymEnumerateSymbols64:
                    if (!SymEnumerateSymbols64(hCurrentProcess, baseOfDll, EnumerateSymbols64Callback, IntPtr.Zero))
                        throw new Exception("Failed to enum symbols.");
                    AutoAssignIndicies();
                    break;
                case SymbolRetrieveMethod.SymEnumSymbols:
                    if (!SymEnumSymbols(hCurrentProcess, baseOfDll, "*", EnumSymbolsCallback, IntPtr.Zero))
                        throw new Exception("Failed to enum symbols.");
                    AutoAssignIndicies();
                    break;
                case SymbolRetrieveMethod.DirectHeaderReading:
                    if(!DirectMethod(path))
                        throw new Exception("Failed to enum symbols.");
                    break;
            }
            SymCleanup(hCurrentProcess);
            return infolist;
        }

        public static void AutoAssignIndicies()
        {
            for (int i = 0; i < infolist.Count; i++)
            {
                ExportInfo e = infolist[i];
                e.Index = i + 1;
                infolist[i] = e;
            }
        }

        public static bool DirectMethod(string filePath)
        {
            PeHeaderReader reader = new PeHeaderReader(filePath);
            long exportDirAddr = 0;
            long offset = 0;
            foreach (PeHeaderReader.IMAGE_SECTION_HEADER ish in reader.ImageSectionHeaders)
            {
                string test = "";
                foreach (char c in ish.Name)
                    if (c != '\0')
                        test += c;
                if (test == ".rdata")
                {
                    ulong vAddr, vSize;
                    if(reader.Is32BitHeader)
                    {
                        vAddr = reader.OptionalHeader32.ExportTable.VirtualAddress;
                        vSize = reader.OptionalHeader32.ExportTable.Size;
                    }
                    else
                    {
                        vAddr = reader.OptionalHeader64.ExportTable.VirtualAddress;
                        vSize = reader.OptionalHeader64.ExportTable.Size;
                    }
                    if (vAddr == 0 || vSize == 0)
                        return false;
                    if (ish.VirtualAddress > vAddr || ish.VirtualAddress + ish.VirtualSize <= vAddr + vSize)
                        return false;
                    offset = (long)ish.PointerToRawData - (long)ish.VirtualAddress;
                    exportDirAddr = (long)vAddr + offset;
                    break;
                }
            }
            if (exportDirAddr == 0)
                return false;
            ReadExportTable(filePath, exportDirAddr, offset);
            return true;
        }

        public static void ReadExportTable(string fileName, long addr, long offset)
        {
            byte[] raw = File.ReadAllBytes(fileName);
            MemoryStream m = new MemoryStream(raw);
            m.Seek(addr + 16, 0);
            uint ordBase = ReadU32(m);
            uint countFunc = ReadU32(m);
            uint countNames = ReadU32(m);
            uint addrFuncTable = ReadU32(m);
            uint addrNamePointerTable = ReadU32(m);
            uint addrOrdTable = ReadU32(m);
            addrFuncTable = (uint)(addrFuncTable + offset);
            addrNamePointerTable = (uint)(addrNamePointerTable + offset);
            addrOrdTable = (uint)(addrOrdTable + offset);
            m.Seek(addrFuncTable, 0);
            uint[] exportAddresses = new uint[countFunc];
            for (int i = 0; i < countFunc; i++)
                exportAddresses[i] = ReadU32(m);
            m.Seek(addrNamePointerTable, 0);
            uint[] namePointer = new uint[countNames];
            for (int i = 0; i < countNames; i++)
                namePointer[i] = ReadU32(m);
            m.Seek(addrOrdTable, 0);
            ushort[] ordTable = new ushort[countNames];
            for (int i = 0; i < countNames; i++)
                ordTable[i] = ReadU16(m);
            string[] funcNames = new string[countFunc];
            for (int i = 0; i < countNames; i++)
            {
                m.Seek(namePointer[i] + offset, 0);
                string name = ReadCString(m);
                ushort ord = ordTable[i];
                funcNames[ord] = name;
            }
            for(int i = 0; i < countFunc; i++)
            {                
                ExportInfo ex = new ExportInfo();
                ex.isEmpty = funcNames[i] == null;
                if (!ex.isEmpty)
                    ex.Name = funcNames[i];
                else
                    ex.Name = Options.emptyFunc + (ordBase + i);
                ex.Index = (int)ordBase + i;
                infolist.Add(ex);
            }
        }

        public static string ReadCString(Stream s)
        {
            StringBuilder sb = new StringBuilder();
            int b;
            while(true)
            {
                b = s.ReadByte();
                if (b == -1 || b == 0)
                    break;
                sb.Append((char)b);
            }
            return sb.ToString();
        }

        public static ushort ReadU16(Stream s)
        {
            byte[] buff = new byte[2];
            s.Read(buff, 0, 2);
            return BitConverter.ToUInt16(buff, 0);
        }
        public static uint ReadU32(Stream s)
        {
            byte[] buff = new byte[4];
            s.Read(buff, 0, 4);
            return BitConverter.ToUInt32(buff, 0);
        }

        public struct ExportInfo
        {
            public string Name;
            public string Definition;
            public int WayOfExport; //0=none, 1=withasm, 2=withcalls, 3=withlink
            public int Index;
            public bool isEmpty;
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
