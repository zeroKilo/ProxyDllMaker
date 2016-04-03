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
        public string lastfilename;
        public Form1()
        {
            InitializeComponent();
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
                withCallsToolStripMenuItem.Enabled = true;
                rtb1.Text = Helper.DumpObject(header);
                listBox1.Items.Clear();
                try
                {
                    exportlist = Helper.GetExports(d.FileName);
                    for (int i = 0; i < exportlist.Count(); i++)
                        listBox1.Items.Add(i + " : \"" + exportlist[i].Name + "\"");
                    GenerateDefintions();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error:\n" + ex.Message);
                }
            }
        }

        private void GenerateWithJumps()
        {
            string temp = Properties.Resources.template_cpp;
            temp = temp.Replace("##ph1##", exportlist.Count.ToString());
            temp = temp.Replace("##ph2##", lastfilename);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < exportlist.Count; i++)
                sb.AppendFormat("		p[{0}] = GetProcAddress(hL,\"{1}\");\n", i, exportlist[i].Name);
            temp = temp.Replace("##ph3##", sb.ToString());
            sb = new StringBuilder();
            for (int i = 0; i < exportlist.Count; i++)
                sb.AppendLine(MakeAsmJump(exportlist[i].Name, i));
            temp = temp.Replace("##ph4##", sb.ToString());
            rtb2.Text = temp;
        }

        private string MakeAsmJump(string name, int index)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("extern \"C\" __declspec(naked) void Proxy_" + name + "()");
            sb.AppendLine("{");
            sb.AppendLine(" __asm");
            sb.AppendLine(" {");
            sb.AppendLine("     jmp p[" + index + "*4];");
            sb.AppendLine(" }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private string MakeCall(string name, int index)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                Helper.ExportInfo info = exportlist[index];
                string def = info.Definition.Replace(";", "").Replace(name, "Proxy_" + name);
                int namepos = def.IndexOf("Proxy_" + name);
                def = def.Substring(namepos, def.Length - namepos);
                string[] args = def.Split('(')[1].Split(')')[0].Split(',');
                string argcall = "";
                for (int i = 0; i < args.Length; i++)
                {
                    string[] argparts = args[i].Split(' ');
                    argcall += argparts[argparts.Length - 1].Replace("*","").Trim() + ", ";
                }
                if (argcall.Length > 1)
                    argcall = argcall.Substring(0, argcall.Length - 2);
                string[] parts = info.Definition.Split(' ');
                if (header.Is32BitHeader)
                {
                    sb.AppendLine("extern \"C\" int " + def);
                    sb.AppendLine("{");
                    sb.AppendLine(" typedef int (__stdcall *pS)(" + def.Split('(')[1].Split(')')[0] + ");");
                }
                else
                {
                    sb.AppendLine("extern \"C\" long " + def);
                    sb.AppendLine("{");
                    sb.AppendLine(" typedef long (__stdcall *pS)(" + def.Split('(')[1].Split(')')[0] + ");");
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
            }
            return sb.ToString();
        }

        private void GenerateWithCalls()
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.c|*.c";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string[] input = File.ReadAllLines(d.FileName);
                for (int i = 0; i < exportlist.Count; i++)
                {
                    bool found = false;
                    foreach(string line in input)
                        if (line.Contains(exportlist[i].Name))
                        {
                            Helper.ExportInfo info = exportlist[i];
                            info.Definition = line;
                            exportlist[i] = info;
                            found = true;
                            break;
                        }
                    if (!found)
                    {
                        MessageBox.Show("Could not find definition for function \"" + exportlist[i].Name + "\"");
                        return;
                    }
                }
                string temp = Properties.Resources.template_cpp;
                temp = temp.Replace("##ph1##", exportlist.Count.ToString());
                temp = temp.Replace("##ph2##", lastfilename);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < exportlist.Count; i++)
                    sb.AppendFormat("		p[{0}] = GetProcAddress(hL,\"{1}\");\n", i, exportlist[i].Name);
                temp = temp.Replace("##ph3##", sb.ToString());
                sb = new StringBuilder();
                for (int i = 0; i < exportlist.Count; i++)
                    sb.AppendLine(MakeCall(exportlist[i].Name, i));
                temp = temp.Replace("##ph4##", sb.ToString());
                rtb2.Text = temp;
            }
        }

        private void GenerateDefintions()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("EXPORTS");
            for (int i = 0; i < exportlist.Count; i++)
                sb.AppendFormat("{0}=Proxy_{0} @{1}\n", exportlist[i].Name, i + 1);
            rtb3.Text = sb.ToString();
        }

        private void withasmJumpsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GenerateWithJumps();
        }

        private void withCallsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GenerateWithCalls();
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

    }
}
