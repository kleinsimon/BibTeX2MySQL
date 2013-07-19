using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace BibTex2SQL
{
    public partial class settings : Form
    {
        public settings()
        {
            InitializeComponent();

            textBoxServer.Text = Properties.Settings.Default.server;
            textBoxPort.Text = Properties.Settings.Default.port.ToString();
            textBoxDB.Text = Properties.Settings.Default.database;
            textBoxTable.Text = Properties.Settings.Default.table;
            textBoxUser.Text = Properties.Settings.Default.user;
            textBoxPassw.Text = Properties.Settings.Default.password;
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.server = textBoxServer.Text;
            uint tmp;
            if (uint.TryParse(textBoxPort.Text, out tmp))
                Properties.Settings.Default.port = tmp;
            Properties.Settings.Default.database = textBoxDB.Text;
            Properties.Settings.Default.user = textBoxUser.Text;
            Properties.Settings.Default.password = textBoxPassw.Text;
            Properties.Settings.Default.table = textBoxTable.Text;

            Properties.Settings.Default.Save();

            this.Dispose();
        }
    }
}
