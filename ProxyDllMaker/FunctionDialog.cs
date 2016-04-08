using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ProxyDllMaker
{
    public partial class FunctionDialog : Form
    {
        public FunctionDialog()
        {
            InitializeComponent();
        }

        public Helper.ExportInfo info;
        public PeHeaderReader header;

        private void FunctionDialog_Load(object sender, EventArgs e)
        {
            textBox1.Text = info.Index.ToString();
            textBox2.Text = info.Name;
            textBox3.Text = info.Definition;
            comboBox1.Items.Add("Not exported");
            if (header.Is32BitHeader)
                comboBox1.Items.Add("with asm jmp");
            comboBox1.Items.Add("with call");
            comboBox1.Items.Add("with link");
            if (header.Is32BitHeader)
                comboBox1.SelectedIndex = info.WayOfExport;
            else
            {
                if (info.WayOfExport == 0)
                    comboBox1.SelectedIndex = 0;
                else
                    comboBox1.SelectedIndex = info.WayOfExport - 1;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            info.Name = textBox2.Text;
            info.Definition = textBox3.Text;
            if (header.Is32BitHeader)
                info.WayOfExport = comboBox1.SelectedIndex;
            else
            {
                if (comboBox1.SelectedIndex == 0)
                    info.WayOfExport = 0;
                else
                    info.WayOfExport = comboBox1.SelectedIndex + 1;
            }
        }
    }
}
