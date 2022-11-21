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
    public partial class OptionForm : Form
    {
        public OptionForm()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Options.suffix = textBox1.Text;
            Options.prefix = textBox2.Text;
            Options.emptyFunc = textBox3.Text;
            Options.symMethod = (Helper.SymbolRetrieveMethod)comboBox1.SelectedIndex;
            Options.SaveToFile("settings.txt");
            this.Close();
        }

        private void OptionForm_Load(object sender, EventArgs e)
        {
            textBox1.Text = Options.suffix;
            textBox2.Text = Options.prefix;
            textBox3.Text = Options.emptyFunc;
            comboBox1.SelectedIndex = (int)Options.symMethod;
        }
    }
}
