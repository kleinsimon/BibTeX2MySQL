﻿using System;
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
        bool AutoMode = false;
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
            "isbn",
        };

        public MainForm()
        {
            if (Environment.GetCommandLineArgs().Contains("-auto"))
            {
                AutoMode = true;
            }
            InitializeComponent();
            if (AutoMode)
            {
                this.Hide();
                this.SuspendLayout();
            }

            foreach (string tag in tags)
            {
                EntryTable.Columns.Add(tag);
            }

            if (AutoMode)
            {
                if (File.Exists(Properties.Settings.Default.watchFilePath))
                {
                    if (parseFile(Properties.Settings.Default.watchFilePath))
                    {
                        updateTable();
                        if (Properties.Settings.Default.deleteFileAfterExport)
                        {
                            File.Delete(Properties.Settings.Default.watchFilePath);
                        }
                    }
                }
                this.exit();
            }
        }

        private void exit()
        {
            this.Dispose();
            this.Close();
            Environment.Exit(0);
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

        bool parseFile(string path)
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
                        try
                        {
                            valid = parseEntry(buffer + curChar);
                        }
                        catch (Exception ex)
                        {
                            debugOutput("Exception: " + ex.Message + "\r\n Fehler im Tag:\r\n" + buffer + curChar);
                            return false;
                        }
                        buffer = "";
                    }
                }
                if (entry)
                {
                    buffer += curChar;
                }
                lastchar = curChar;
            }
            if (!valid)
            {
                debugOutput("Fehler Aufgetreten");
                return false;
            }

            dataGridView1.DataSource = EntryTable;
            RF.Close();

            label1.Text = EntryTable.Rows.Count.ToString() + " Entries read";
            return true;
        }

        void debugOutput(string Msg, int level = 0)
        {
            if (!AutoMode) MessageBox.Show(Msg);
            else if (level == 0) EventLog.WriteEntry("BibTex2SQL", Msg);
        }

        bool parseEntry(string entry)
        {
            if (!entry.StartsWith("@"))
                return false;

            if (!entry.Contains(@"Lehrstuhl-Veröffentlichungen"))
                return true;

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

                if (open == 0 && n == 0)
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
                            tagBody = Regex.Unescape(tagBody);

                            if (tags.Contains(tagName))
                            {
                                MakeCol(tagName, EntryTable);
                                curEntry[tagName] = tagBody;
                            }
                            if (tagName == "location")
                            {
                                if (tagBody.Contains(Properties.Settings.Default.locationFilter))
                                {
                                    ownPub = true;
                                    try
                                    {

                                        int first = tagBody.IndexOf(Properties.Settings.Default.locationFilter) + Properties.Settings.Default.locationFilter.Length + 2;
                                        ownPubID = tagBody.Substring(first, tagBody.IndexOf(')', first) - first);
                                    }
                                    catch (Exception ex)
                                    {
                                        //debugOutput(ex.Message);
                                    }
                                    curEntry["signature"] = ownPubID;
                                }
                            }
                            lastTag = "";
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

                if (curEntry["author"].GetType() == typeof(System.DBNull))
                {
                    debugOutput("Einträge ohne Author werden nicht unterstützt");
                }

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
            updateTable();
        }

        public void updateTable(bool noChange = false)
        {
            Process plink = null;

            if (Properties.Settings.Default.SSHtunnel)
            {
                try
                {
                    sqlconn.Server = @"127.0.0.1";
                    sqlconn.Port = uint.Parse(Properties.Settings.Default.SSHPort);

                    ProcessStartInfo psi = new ProcessStartInfo(Properties.Settings.Default.plinkPath);

                    psi.Arguments = "-ssh -l " + Properties.Settings.Default.SSHUser + " -L " + Properties.Settings.Default.port + ":localhost:" +
                                    Properties.Settings.Default.SSHPort + " -pw " + Properties.Settings.Default.SSHPass + " " + Properties.Settings.Default.server;


                    psi.RedirectStandardInput = true;
                    psi.RedirectStandardOutput = true;
                    psi.WindowStyle = ProcessWindowStyle.Hidden;
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;

                    plink = Process.Start(psi);
                }
                catch
                {
                    debugOutput("SSH-Tunnel konnte nicht aufgebaut werden");
                    return;
                }
            }
            else
            {
                sqlconn.Server = Properties.Settings.Default.server;
                sqlconn.Port = Properties.Settings.Default.port;
            }

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
                    if (!connection.Ping())
                    {
                        throw new Exception("No Connection to Server. \r\n Connection: " + sqlconn.ConnectionString);
                    }

                    if (!noChange)
                    {
                        MySqlCommand del = new MySqlCommand(@"TRUNCATE TABLE " + sqlconn.Database + "." + table, connection);
                        del.ExecuteNonQuery();

                        MySqlDataAdapter adapter = new MySqlDataAdapter(@"SELECT * FROM " + sqlconn.Database + "." + table, connection);
                        MySqlCommandBuilder builder = new MySqlCommandBuilder(adapter);
                        adapter.ContinueUpdateOnError = true;

                        adapter.Update(EntryTable);
                    }
                    debugOutput("Successful", 1);
                }
                catch (Exception ex)
                {
                    debugOutput(ex.Message.ToString());
                    //string tmp = "";
                    //if (plink != null)
                    //    tmp = plink.StandardOutput.ReadToEnd();
                    //debugOutput(ex.Message.ToString() + tmp);
                }
            }

            try
            {
                plink.StandardInput.WriteLine("exit");

                plink.Kill();
                plink.Dispose();
            }
            catch { }

            if (!noChange)
                EntryTable.Clear();
        }

        private void buttonSettings_Click(object sender, EventArgs e)
        {
            settings setWin = new settings(this);
            setWin.Show();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Properties.Settings.Default.Save();
        }

    }

}
