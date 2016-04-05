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
    public partial class UnDecorator : Form
    {
        public UnDecorator()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            StringBuilder builder = new StringBuilder(255);
            DbgHelper.UnDecorateSymbolName(textBox1.Text, builder, builder.Capacity, DbgHelper.UnDecorateFlags.UNDNAME_COMPLETE);
            textBox2.Text = builder.ToString();
        }
    }
}
