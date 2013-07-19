using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using MySql.Data.MySqlClient;

namespace BibTex2SQL
{
    public partial class MainForm : Form
    {
        DataTable EntryTable = new DataTable();

        MySqlConnectionStringBuilder sqlconn = new MySqlConnectionStringBuilder();


        public string[] entries = 
        {
            "article",
            "book",
            "booklet",
            "inbook",
            "incollection",
            "inproceedings",
            "manual",
            "mastersthesis",
            "misc",
            "phdthesis",
            "proceedings",
            "techreport",
            "unpublished",
        };

        public string[] tags =
        {
            "address",
            "annote",
            "author",
            "booktitle",
            "chapter",
            "crossref",
            "edition",
            "editor",
            "howpublished",
            "institution",
            "journal",
            "key",
            "month",
            "note",
            "number",
            "organization",
            "pages",
            "publisher",
            "school",
            "series",
            "title",
            "type",
            "volume",
            "year",
            "signature",
            "keywords",
            "doi",
        };

        public MainForm()
        {
            InitializeComponent();


            foreach (string tag in tags)
            {
                EntryTable.Columns.Add(tag);
            }
        }

        private void Form1_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                foreach (string file in (string[])e.Data.GetData(DataFormats.FileDrop))
                {
                    parseFile(file);
                }
            }
        }

        private void Form1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Move;
        }

        void parseFile(string path)
        {
            StreamReader RF = new StreamReader(path);
            string buffer = "";
            char curChar = '.';
            char lastchar = '.';
            bool entry = false;
            bool valid = false;
            int open = 0;

            while (!RF.EndOfStream)
            {
                curChar = (char)RF.Read();
                if ((0 == open) && ('@' == curChar))
                {
                    entry = true;
                }
                else if (entry && ('{' == curChar) && ('\\' != lastchar))
                {
                    open++;
                }
                else if (entry && ('}' == curChar) && ('\\' != lastchar))
                {
                    open--;
                    if (open < 0)
                    {
                        valid = false;
                    }
                    if (0 == open)
                    {
                        entry = false;
                        valid = parseEntry(buffer);
                        //entrydata = this->_parseEntry(buffer);
                        //if(!entrydata) {
                        //    valid = false;
                        //} else {
                        //    this->data[] = entrydata;
                        //}
                        buffer = "";
                    }
                }
                if (entry)
                {
                    buffer += curChar;
                }
                lastchar = curChar;
            }
            if (!valid) MessageBox.Show("Fehler Aufgetreten");

            //MessageBox.Show(EntryList.Count.ToString() + " Einträge eingelesen");
            //fillTable();

            dataGridView1.DataSource = EntryTable;
            RF.Close();
        }

        bool parseEntry(string entry)
        {
            //bibEntry curEntry = new bibEntry();
            //Dictionary<string, string> curEntry = new Dictionary<string, string>();
            DataRow curEntry = EntryTable.NewRow();

            StringReader s = new StringReader(entry);
            string curline = null;
            bool ownPub = false;
            string ownPubID = "";

            while ((curline = s.ReadLine()) != null)
            {
                curline = curline.Trim();
                if (curline.StartsWith("@"))
                {
                    MakeCol("BibType", EntryTable);
                    MakeCol("BibKey", EntryTable);

                    curEntry["BibType"] = curline.Substring(1, curline.IndexOf('{') - 1);
                    if (!entries.Contains(curEntry["BibType"]))
                        return false;

                    curEntry["BibKey"] = curline.Substring(curline.IndexOf('{') + 1);
                    if (((string)curEntry["BibKey"]).EndsWith(","))
                    {
                        curEntry["BibKey"] = ((string)curEntry["BibKey"]).Substring(0, ((string)curEntry["BibKey"]).Length - 1);
                    }
                }
                else
                {
                    string tag, content;

                    int open = 0;
                    int close = 0;
                    open = countCharNum(curline, '{');
                    close = countCharNum(curline, '}');

                    while (open > close)
                    {
                        string nextline = s.ReadLine();
                        if (nextline == null)
                        {
                            return false;
                        }
                        curline += Environment.NewLine + nextline;
                        open = countCharNum(curline, '{');
                        close = countCharNum(curline, '}');
                    }

                    try
                    {
                        int delim = curline.IndexOf('=');

                        tag = curline.Substring(0, delim).Trim();
                        content = curline.Substring(delim + 1).Trim();
                        if (content.StartsWith("{") && content.EndsWith("},"))
                        {
                            content = content.Substring(1, content.Length - 3);
                        }
                        if (content.StartsWith("{") && content.EndsWith("}"))
                        {
                            content = content.Substring(1, content.Length - 2);
                        }


                        if (tags.Contains(tag))
                        {
                            MakeCol(tag, EntryTable);
                            curEntry[tag] = content;
                        }

                        if (tag == "location")
                        {
                            if (content.Contains(@"Lehrstuhl-Veröffentlichungen"))
                            {
                                ownPub = true;
                                try
                                {
                                    int first = content.IndexOf(@"Lehrstuhl-Veröffentlichungen") + 30;
                                    ownPubID = content.Substring(first, content.IndexOf(')') - first);
                                }
                                catch { }
                                curEntry["signature"] = ownPubID;
                            }
                        }
                    }
                    catch
                    {
                        return false;
                    }
                }
            }

            if (ownPub)
            {
                curEntry["author"] = ((string)curEntry["author"]).Replace(@"{", "");
                curEntry["author"] = ((string)curEntry["author"]).Replace(@"}", "");

                if (((string)curEntry["author"]).Contains(','))
                {
                    string[] authors = ((string)curEntry["author"]).Split(';');
                    List<string> newAuthors = new List<string>();
                    foreach (string a in authors)
                    {
                        string vorname, nachname;
                        vorname = a.Substring(a.IndexOf(',') +1);
                        nachname = a.Substring(0, a.IndexOf(',')+1);

                        vorname = Regex.Replace(vorname, @"(\w)\w+[^.]", @"$1.");
                        newAuthors.Add(nachname + "" + vorname);
                    }

                    curEntry["author"] = string.Join("; ", newAuthors);
                }

                EntryTable.Rows.Add(curEntry);

            }

            //EntryList.Add(curEntry);
            return true;
        }

        int countCharNum(string source, char needle)
        {
            int count = 0;
            foreach (char c in source)
                if (c == needle) count++;
            return count;
        }

        void MakeCol(string col, DataTable table)
        {
            if (!table.Columns.Contains(col))
                table.Columns.Add(col);
        }

        void dumpSQL()
        {

        }

        private void buttonConnect_Click(object sender, EventArgs e)
        {
            sqlconn.Server = Properties.Settings.Default.server;
            sqlconn.Port = Properties.Settings.Default.port;
            sqlconn.UserID = Properties.Settings.Default.user;
            sqlconn.Password = Properties.Settings.Default.password;
            sqlconn.Database = Properties.Settings.Default.database;
            string table = Properties.Settings.Default.table;

            EntryTable.TableName = table;

            using (MySqlConnection connection = new MySqlConnection(sqlconn.ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand del = new MySqlCommand(@"TRUNCATE TABLE " + sqlconn.Database + "." + table, connection);
                    del.ExecuteNonQuery();

                    MySqlDataAdapter adapter = new MySqlDataAdapter(@"SELECT * FROM " + sqlconn.Database + "." + table, connection);
                    MySqlCommandBuilder builder = new MySqlCommandBuilder(adapter);
                    adapter.ContinueUpdateOnError = true;

                    adapter.Update(EntryTable);
                    MessageBox.Show("Successful");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message.ToString());
                }
            }


        }

        private void buttonSettings_Click(object sender, EventArgs e)
        {
            settings setWin = new settings();
            setWin.Show();
        }
    }

}
