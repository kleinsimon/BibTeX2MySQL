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
                        valid = parseEntry(buffer + curChar);
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

            dataGridView1.DataSource = EntryTable;
            RF.Close();
        }

        bool parseEntry(string entry)
        {
            if (!entry.StartsWith("@"))
                return false;

            DataRow curEntry = EntryTable.NewRow();
            char lastChar = '.';
            string bibType = "";
            string bibKey = "";
            string lastTag = "";
            bool ownPub = false;
            string ownPubID = "";
            int i = 0;
            int open = 0;
            int n = 0;

            foreach (char c in entry)
            {
                if (i == 0)
                {
                    i++;
                    continue;
                }
                if (c == '{' && lastChar != '\\')
                {
                    open++;
                }
                else if (c == '}' && lastChar != '\\')
                {
                    open--;
                }

                if (open == 0 && n==0)
                {
                    //before body
                    bibType += c;
                }
                else
                {
                    //in Body or after body
                    if ((open == 1 && c == ',') || open == 0)
                    {
                        if (n != 0)
                        {
                            //Tag ended
                            string tagName = lastTag.Substring(0, lastTag.IndexOf('=')).Trim();
                            string tagBody = lastTag.Substring(lastTag.IndexOf('=') + 1);
                            tagBody = tagBody.Replace("{", "");
                            tagBody = tagBody.Replace("}", "");
                            tagBody = tagBody.Trim();

                            if (tags.Contains(tagName))
                            {
                                MakeCol(tagName, EntryTable);
                                curEntry[tagName] = tagBody;
                            }
                            if (tagName == "location")
                            {
                                if (tagBody.Contains(@"Lehrstuhl-Veröffentlichungen"))
                                {
                                    ownPub = true;
                                    try
                                    {
                                        int first = tagBody.IndexOf(@"Lehrstuhl-Veröffentlichungen") + 30;
                                        ownPubID = tagBody.Substring(first, tagBody.IndexOf(')') - first);
                                    }
                                    catch { }
                                    curEntry["signature"] = ownPubID;
                                }
                            }
                            lastTag="";
                        }

                        i++;
                        n++;
                        continue;
                    }
                    else if (n == 0)
                    {
                        //must be in bibkey
                        if (c != '{' && c != '}')
                            bibKey += c;
                    }
                    else
                    {
                        //must be in Tag
                        lastTag += c;
                    }
                }

                lastChar = c;
                i++;
            }
            if (open > 0)
                return false;

            if (ownPub)
            {
                MakeCol("BibType", EntryTable);
                MakeCol("BibKey", EntryTable);

                curEntry["BibType"] = bibType;
                curEntry["BibKey"] = bibKey;

                curEntry["author"] = ((string)curEntry["author"]).Replace(@"{", "");
                curEntry["author"] = ((string)curEntry["author"]).Replace(@"}", "");

                if (((string)curEntry["author"]).Contains(','))
                {
                    string[] authors = ((string)curEntry["author"]).Split(';');
                    List<string> newAuthors = new List<string>();
                    foreach (string a in authors)
                    {
                        string vorname, nachname;
                        vorname = a.Substring(a.IndexOf(',') + 1);
                        nachname = a.Substring(0, a.IndexOf(',') + 1);

                        vorname = Regex.Replace(vorname, @"(\w)\w+[^.]", @"$1.");
                        newAuthors.Add(nachname + "" + vorname);
                    }

                    curEntry["author"] = string.Join("; ", newAuthors);
                }

                EntryTable.Rows.Add(curEntry);

            }

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
