using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.Serialization.Json;

namespace ProxyDllMaker
{
    public partial class Form1 : Form
    {
        PeHeaderReader header;
        List<Helper.ExportInfo> exportlist;
        OptionForm optf;
        public string lastfilename;
        public Form1()
        {
            InitializeComponent();
            if (File.Exists("settings.txt"))
                Options.LoadFromFile("settings.txt");
        }

        private void openDLLToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.dll|*.dll";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                lastfilename = Path.GetFileNameWithoutExtension(d.FileName);
                header = new PeHeaderReader(d.FileName);
                if (!header.Is32BitHeader)
                {
                    status.Text = "64 BIT";
                    statusStrip1.BackColor = Color.LightBlue;
                    withasmJumpsToolStripMenuItem.Enabled = false;
                }
                else
                {
                    status.Text = "32 BIT";
                    statusStrip1.BackColor = Color.LightGreen;
                    withasmJumpsToolStripMenuItem.Enabled = true;
                }
                rtb1.Text = Helper.DumpObject(header);
                try
                {
                    exportlist = Helper.GetExports(d.FileName);
                    RefreshExportList();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error:\n" + ex.Message);
                }
            }
        }

        private void RefreshExportList()
        {
            listBox1.Items.Clear();
            listBox1.Visible = false;
            foreach (Helper.ExportInfo e in exportlist)
                listBox1.Items.Add(Helper.ExportInfoToString(e));
            listBox1.Visible = true;
        }

        private void withasmJumpsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndices.Count == 0)
                for (int i = 0; i < listBox1.Items.Count; i++)
                    listBox1.SetSelected(i, true);
            for (int i = 0; i < listBox1.SelectedIndices.Count; i++)
            {
                Helper.ExportInfo ex = exportlist[listBox1.SelectedIndices[i]];
                ex.WayOfExport = 1;
                exportlist[listBox1.SelectedIndices[i]] = ex;
            }
            RefreshExportList();
        }

        private void withCallsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndices.Count == 0)
                for (int i = 0; i < listBox1.Items.Count; i++)
                    listBox1.SetSelected(i, true);
            for (int i = 0; i < listBox1.SelectedIndices.Count; i++)
            {
                Helper.ExportInfo ex = exportlist[listBox1.SelectedIndices[i]];
                ex.WayOfExport = 2;
                exportlist[listBox1.SelectedIndices[i]] = ex;
            }
            RefreshExportList();
        }

        private void saveCFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = lastfilename + ".c|" + lastfilename + ".c";
            d.FileName = lastfilename + ".c";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                File.WriteAllText(d.FileName, rtb2.Text);
                MessageBox.Show("Done.");
            }
        }

        private void saveDEFFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = lastfilename + ".def|" + lastfilename + ".def";
            d.FileName = lastfilename + ".def";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                File.WriteAllText(d.FileName, rtb3.Text);
                MessageBox.Show("Done.");
            }
        }

        private void optionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (optf != null)
                optf.Close();
            optf = new OptionForm();
            optf.Show();
        }

        private void withLinksToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndices.Count == 0)
                for (int i = 0; i < listBox1.Items.Count; i++)
                    listBox1.SetSelected(i, true);
            for (int i = 0; i < listBox1.SelectedIndices.Count; i++)
            {
                Helper.ExportInfo ex = exportlist[listBox1.SelectedIndices[i]];
                ex.WayOfExport = 3;
                exportlist[listBox1.SelectedIndices[i]] = ex;
            }
            RefreshExportList();
        }

        private void loadDefinitionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.c;*.h;*.txt|*.c;*.h;*.txt";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string[] input = File.ReadAllLines(d.FileName);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < exportlist.Count; i++)
                    if(exportlist[i].WayOfExport == 2)
                {
                    
                    bool found = false;
                    foreach (string line in input)
                        if (line.Contains(exportlist[i].Name + "("))
                        {
                            Helper.ExportInfo info = exportlist[i];
                            info.Definition = line.Trim();
                            exportlist[i] = info;
                            found = true;
                            break;
                        }
                    if (!found)
                    {
                        sb.AppendLine(exportlist[i].Name);
                        Helper.ExportInfo ex = exportlist[i];
                        ex.WayOfExport = 0;
                        exportlist[i] = ex;
                    }
                }
                RefreshExportList();
                if (sb.Length != 0)
                    MessageBox.Show("Error: no definition(s) found for:\n" + sb.ToString()+ "\n please use \"asm jmp\" or \"link\" as method for those exports");
            }
        }

        private void generateToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            GenerateDefintions();
            GenerateCode();
        }

        private void GenerateDefintions()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("EXPORTS");
            int count = 0;
            foreach (Helper.ExportInfo e in exportlist)
                if (e.WayOfExport != 0 && e.WayOfExport != 3)
                    sb.AppendFormat("{0}={1}{0} @{2}\n", e.Name, Options.prefix, count++ + 1);
                else if (e.WayOfExport == 3)
                    count++;
            rtb3.Text = sb.ToString();
        }

        private void GenerateCode()
        {
            string temp = Properties.Resources.template_cpp;
            int count = 0;
            foreach (Helper.ExportInfo e in exportlist)
                if (e.WayOfExport == 1 | e.WayOfExport == 2)
                    count++;
            if (count != 0)
                temp = temp.Replace("##ph1##", "FARPROC p[" + count + "] = {0};");
            else
                temp = temp.Replace("##ph1##", "");
            temp = temp.Replace("##ph2##", lastfilename + Options.suffix);
            StringBuilder sb = new StringBuilder();
            count = 0;
            foreach (Helper.ExportInfo e in exportlist)
                if (e.WayOfExport == 1 | e.WayOfExport == 2)
                    sb.AppendFormat("		p[{0}] = GetProcAddress(hL,\"{1}\");\n", count++, e.Name);
            temp = temp.Replace("##ph3##", sb.ToString());
            sb = new StringBuilder();
            count = 0;
            foreach (Helper.ExportInfo e in exportlist)
                switch (e.WayOfExport)
                {
                    case 1:
                        sb.AppendLine(MakeAsmJump(e, count++));
                        break;
                    case 2:
                        
                        sb.AppendLine(MakeCall(e, count++));
                        break;
                    case 3:
                        sb.AppendLine(MakeLink(e));
                        break;
                }
            temp = temp.Replace("##ph4##", sb.ToString());
            rtb2.Text = temp;
        }

        private string MakeAsmJump(Helper.ExportInfo e, int index)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("extern \"C\" __declspec(naked) void " + Options.prefix + e.Name + "()");
            sb.AppendLine("{");
            sb.AppendLine(" __asm");
            sb.AppendLine(" {");
            sb.AppendLine("     jmp p[" + index + "*4];");
            sb.AppendLine(" }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private string MakeCall(Helper.ExportInfo e, int index)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                string def = e.Definition.Replace(";", "").Replace(e.Name, Options.prefix + e.Name);
                int namepos = def.IndexOf(Options.prefix + e.Name);
                def = def.Substring(namepos, def.Length - namepos);
                string argstr =def.Split('(')[1].Split(')')[0];
                string[] args = argstr.Split(',');
                string argcall = "";
                string argdef = "";
                int acount = 1;
                if (argstr.Trim() == "")
                    args = new string[0];
                for (int i = 0; i < args.Length; i++)                                
                {
                    if (args[i].ToLower().Replace(" ", "") == "void*")
                    {
                        if (header.Is32BitHeader)
                            args[i] = "int ";
                        else
                            args[i] = "long ";
                        args[i] += "a" + (acount++);
                    }
                    string[] argparts = args[i].Trim().Split(' ');
                    if (argparts.Length == 1)
                        argparts = new List<string>() { argparts[0], "a" + (acount++) }.ToArray();
                    if (argparts.Length > 2)
                    {
                        StringBuilder sb2 = new StringBuilder();
                        for (int j = 0; j < argparts.Length - 1; j++)
                            sb2.Append(argparts[j] + " ");
                        argparts = new List<string>() {sb2.ToString(), argparts[argparts.Length -1] }.ToArray();
                    }
                    argcall += argparts[argparts.Length - 1].Replace("*", "").Trim() + ", ";
                    argdef += argparts[0] + " " + argparts[1] + ", ";

                }
                if (args.Length > 0)
                {
                    argcall = argcall.Substring(0, argcall.Length - 2);
                    argdef = argdef.Substring(0, argdef.Length - 2);
                }
                string[] parts = e.Definition.Split(' ');
                if (header.Is32BitHeader)
                {
                    sb.AppendLine("extern \"C\" int " + def.Split('(')[0] + "(" + argdef + ")");
                    sb.AppendLine("{");
                    sb.AppendLine(" typedef int (__stdcall *pS)(" + argdef + ");");
                }
                else
                {
                    sb.AppendLine("extern \"C\" long " + def.Split('(')[0] + "(" + argdef + ")");
                    sb.AppendLine("{");
                    sb.AppendLine(" typedef long (__stdcall *pS)(" +argdef + ");");
                }
                sb.AppendLine(" pS pps = (pS)p[" + index + "*4];");
                if (header.Is32BitHeader)
                    sb.AppendLine(" int rv = pps(" + argcall + ");");
                else
                    sb.AppendLine(" long rv = pps(" + argcall + ");");
                sb.AppendLine(" return rv;");
                sb.AppendLine("}");
            }
            catch (Exception)
            {
                sb = new StringBuilder();
                MessageBox.Show("Error exporting \"" + e.Name + "\" with Definition:\n\"" + e.Definition + "\"");
            }
            return sb.ToString();
        }

        private string MakeLink(Helper.ExportInfo e)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("#pragma comment(linker, \"/export:");
            sb.AppendFormat("{0}={1}.{2}", e.Name, lastfilename + Options.suffix, e.Name);
            sb.Append("\")");
            sb.AppendLine();
            return sb.ToString();
        }

        private void undecoratorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new UnDecorator().Show();
        }

        private void listBox1_DoubleClick_1(object sender, EventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            FunctionDialog d = new FunctionDialog();
            d.info = exportlist[n];
            d.header = header;
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                exportlist[n] = d.info;
                RefreshExportList();
            }
        }

        private void listBox1_Click(object sender, EventArgs e)
        {
            RefreshPreview();
        }

        private void undecorateAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < exportlist.Count; i++)
            {

                Helper.ExportInfo info = exportlist[i];
                StringBuilder builder = new StringBuilder(255);
                DbgHelper.UnDecorateSymbolName(info.Name, builder, builder.Capacity, DbgHelper.UnDecorateFlags.UNDNAME_COMPLETE);
                info.Definition = builder.ToString();
                exportlist[i] = info;
            }
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            Helper.ExportInfo info = exportlist[n];
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("Index\t\t: {0}\n", info.Index);
            sb.Append("Export\t:");
            switch (info.WayOfExport)
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
            sb.AppendLine();
            sb.AppendFormat("Name\t\t: {0}\n", info.Name);
            sb.AppendFormat("Definition\t: {0}", info.Definition);
            rtb4.Text = sb.ToString();
        }
    }
}
