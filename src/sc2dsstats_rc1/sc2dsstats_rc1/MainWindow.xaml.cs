﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Deployment.Application;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.DataVisualization.Charting;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Xml.Serialization;
using Microsoft.Win32;
using System.Security.Cryptography;
using Newtonsoft.Json;

namespace sc2dsstats_rc1
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        public int DEBUG { get; set; } = 0;
        public bool scan_running { get; set; } = false;
        public bool INIT = false;
        //public List<dsreplay> replays = new List<dsreplay>();
        public List<dsreplay> replays { get; set; } = new List<dsreplay>();

        public Task tsscan { get; set; }
        public string player_name { get; set; }
        public List<string> player_list { get; set; }
        public string myReplay_Path { get; set; }
        public List<string> myReplay_list { get; set; }
        public ObservableCollection<KeyValuePair<string, double>> Items { get; set; } = new ObservableCollection<KeyValuePair<string, double>>();
        public ObservableCollection<KeyValuePair<string, double>> Items_sorted { get; set; } = new ObservableCollection<KeyValuePair<string, double>>();
        public List<KeyValuePair<string, double>> Cdata { get; set; }

        Chart dynChart = new Chart()
        {
            Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF041326"))
        };
        dsotfng OTF { get; set; }
        public System.Diagnostics.Process p = new System.Diagnostics.Process();

        public TextBox dynamicText = null;
        //private bool dps_handle = true;
        public string myScan_exe = null;
        public string myScan_log = null;
        public string myWorker_exe = null;
        public string myWorker_log = null;
        public string myStats_csv = null;
        public string myStats_json = null;
        public string myUnits_csv = null;
        public string mySkip_csv = null;
        public string myTemp_png = null;
        public string myWorker_png = null;
        public List<string> myTempfiles_col = new List<string>();
        public string myTemp_dir = null;
        public string myData_dir = null;
        public string myAppData_dir = null;
        public string mySample_json = null;
        public string myS2cli_exe = null;
        public string myDoc_pdf = null;
        public dsimage dsimg = null;

        public string[] s_races { get; } = new string[]
            {
                "Abathur",
                 "Alarak",
                 "Artanis",
                 "Dehaka",
                 "Fenix",
                 "Horner",
                 "Karax",
                 "Kerrigan",
                 "Nova",
                 "Raynor",
                 "Stukov",
                 "Swann",
                 "Tychus",
                 "Vorazun",
                 "Zagara",
                 "Protoss",
                 "Terran",
                 "Zerg"
            };

        public MainWindow()
        {
            InitializeComponent();
            dsimg = new dsimage();
            this.DataContext = dsimg;

            player_list = new List<string>();
            myReplay_list = new List<string>();

            Style = (Style)FindResource(typeof(Window));

            Version version = null;
            if (ApplicationDeployment.IsNetworkDeployed)
            {
                version = ApplicationDeployment.CurrentDeployment.CurrentVersion;
            }
            if (version != null) Console.WriteLine(version.ToString());

            // config
            string exedir = new FileInfo(Assembly.GetExecutingAssembly().Location).DirectoryName;
            myStats_csv = exedir + "\\stats.csv";
            myAppData_dir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\sc2dsstats";
            if (DEBUG > 1) myAppData_dir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\sc2dsstats2";
            mySkip_csv = myAppData_dir + "\\skip.json";
            myStats_csv = myAppData_dir + "\\stats.csv";
            myStats_json = myAppData_dir + "\\stats.json";
            myTemp_dir = myAppData_dir + "\\";
            mySample_json = exedir + "\\sample.json";
            myDoc_pdf = exedir + "\\doc.pdf";
            myScan_log = myAppData_dir + "\\log.txt";

            if (!System.IO.Directory.Exists(myTemp_dir))
            {
                try
                {
                    System.IO.Directory.CreateDirectory(myAppData_dir);
                }
                catch
                {
                    MessageBox.Show("Failed to create DataDir " + myAppData_dir + ". Please check your options.", "sc2dsstats");
                }
            }

            try
            {
                DEBUG = Properties.Settings.Default.DEBUG;
            }
            catch { }
            if (DEBUG > 2) Debug();

            if (Properties.Settings.Default.JSON_FILE != "0" && File.Exists(Properties.Settings.Default.JSON_FILE))
                myStats_json = Properties.Settings.Default.JSON_FILE;
            else
                Properties.Settings.Default.JSON_FILE = myStats_json;

            if (Properties.Settings.Default.SKIP_FILE != "0" && File.Exists(Properties.Settings.Default.SKIP_FILE))
                mySkip_csv = Properties.Settings.Default.SKIP_FILE;
            else
                Properties.Settings.Default.SKIP_FILE = mySkip_csv;

            if (!File.Exists(mySkip_csv))
            {
                try
                {
                    File.Create(mySkip_csv).Dispose();
                }
                catch (System.IO.IOException)
                {
                    MessageBox.Show("Failed to create DataDir " + mySkip_csv + ". Please check your options.", "sc2dsstats");
                }
            }

            int cpus = Environment.ProcessorCount;
            cpus /= 2;
            int usedCpus = 1;
            cb_doit_cpus.Items.Add(usedCpus.ToString());

            while (usedCpus <= cpus)
            {
                cb_doit_cpus.Items.Add(usedCpus.ToString());
                usedCpus += 1;
            }
            cb_doit_cpus.SelectedItem = cb_doit_cpus.Items[cb_doit_cpus.Items.Count - 1];

            player_name = Properties.Settings.Default.PLAYER;
            SetPlayerList(player_name);

            // xaml

            cb_mode.Items.Add("Winrate");
            cb_mode.Items.Add("Damage");
            cb_mode.Items.Add("MVP");
            cb_mode.Items.Add("Synergy");
            cb_mode.Items.Add("Timeline");
            cb_mode.Items.Add("Builds");
            cb_mode.SelectedItem = cb_mode.Items[0];

            foreach (string r in s_races)
            {
                cb_vs.Items.Add(r);
            }
            cb_vs.SelectedItem = cb_vs.Items[0];

            GenerateSynBtn();
            SetChartStyle("%", 100);
            SetGUIFilter(null, null);


            //dynChart.MouseMove += new MouseEventHandler(dyn_image_Move);
            dynChart.MouseDown += new MouseButtonEventHandler(dyn_Chart_Click);
            ContextMenu win_cm = new ContextMenu();
            MenuItem win_saveas = new MenuItem();
            win_saveas.Header = "Save as ...";
            win_saveas.Click += new RoutedEventHandler(win_SaveAs_Click);
            win_cm.Items.Add(win_saveas);
            dynChart.ContextMenu = win_cm;

            if (!File.Exists(myStats_json))
            {
                try
                {
                    File.Create(myStats_json).Dispose();
                }
                catch
                {
                    MessageBox.Show("Failed creating JSON_FILE: " + myStats_json + ". Please Check in File->Options.", "sc2dsstats");
                }
            }
            else
            {
                Win_configng.SetConfig(this);
                replays = LoadData(myStats_json);

                if (replays.Count > 10)
                {
                    if (Properties.Settings.Default.UPLOAD == null)
                    {
                        try
                        {
                            mnu_upload(null, null);
                        } catch
                        {
                            Properties.Settings.Default.UPLOAD = DateTime.Now;
                            Properties.Settings.Default.Save();
                        }
                    }
                    else
                    {
                        try
                        {
                            TimeSpan t = DateTime.Now - Properties.Settings.Default.UPLOAD;
                            if (t.Days > 30)
                            {
                                mnu_upload(null, null);
                            }
                        }
                        catch {
                            Properties.Settings.Default.UPLOAD = DateTime.Now;
                            Properties.Settings.Default.Save();
                        }
                    }
                }
            }

            if (Properties.Settings.Default.REPLAY_PATH == "0")
            {
                FirstRun();
                Properties.Settings.Default.FIRSTRUN = false;
                Properties.Settings.Default.Save();
            }
            else
            {
                myReplay_Path = Properties.Settings.Default.REPLAY_PATH;
                SetReplayList(myReplay_Path);
            }

            if (Properties.Settings.Default.V8 == false)
            {
                var appSettings = ConfigurationManager.AppSettings;
                if (appSettings["STATS_FILE"] != null && File.Exists(appSettings["STATS_FILE"]))
                    myStats_csv = appSettings["STATS_FILE"];
                try
                {
                    FirstRun_Json();
                } catch { }
                Properties.Settings.Default.V8 = true;
                Properties.Settings.Default.FIRSTRUN = false;
                Properties.Settings.Default.Save();
            }

            if (Properties.Settings.Default.FIRSTRUN == true)
            {
                try
                {
                    FirstRunNewVersion();
                } catch { }
                Properties.Settings.Default.FIRSTRUN = false;
                Properties.Settings.Default.Save();

            }

            myStats_csv = myStats_json;

            if (Properties.Settings.Default.UPDATE == true) DoUpdate();
            if (DEBUG > 0) Console.WriteLine("MW init finished.");
            INIT = true;
        }

        public void Debug()
        {

            Properties.Settings.Default.PLAYER = "0";
            Properties.Settings.Default.REPLAY_PATH = "0";
            Properties.Settings.Default.CORES = 1;
            Properties.Settings.Default.V8 = false;
            Properties.Settings.Default.JSON_FILE = "0";
            Properties.Settings.Default.Save();

            //SetPlayerList(Properties.Settings.Default.PLAYER);
            //SetReplayList(Properties.Settings.Default.REPLAY_PATH);
        }

        public void FirstRunNewVersion()
        {
            if (File.Exists(mySkip_csv) && new FileInfo(mySkip_csv).Length > 0)
            {

            }
        }

        public void FirstRun_Json()
        {
            gr_filter1.Visibility = System.Windows.Visibility.Hidden;
            gr_mode.Visibility = Visibility.Hidden;
            bt_show.IsEnabled = false;
            cb_sample.IsEnabled = false;
            bt_filter2.IsEnabled = false;
            dp_menu.IsEnabled = false;
            TextBox mybox = new TextBox();
            mybox.Width = 300;
            mybox.Height = 150;
            mybox.VerticalAlignment = VerticalAlignment.Center;
            mybox.HorizontalAlignment = HorizontalAlignment.Center;
            mybox.Text = "Please wait a few seconds ..";

            Regex rx_path = new Regex(@"(.*)_(\d+)$", RegexOptions.Singleline);


            if (File.Exists(myStats_csv) && new FileInfo(myStats_csv).Length > 0)
            {
                if (!File.Exists(myStats_json) || new FileInfo(myStats_json).Length == 0)
                {
                    dsunits myunit = new dsunits(this);
                    myunit.GetData(myUnits_csv);
                    List<dsreplay> replaysold = new List<dsreplay>(LoadData_deprecated(myStats_csv));
                    dsdecode dsdec = new dsdecode(Environment.ProcessorCount, this);
                    Task.Factory.StartNew(() =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            gr_chart.Children.Add(mybox);
                        });
                        int i = 0;
                        foreach (dsreplay replay in replaysold)
                        {
                            i++;
                            List<dsplayer> playersng = new List<dsplayer>();
                            foreach (dsplayer pl in replay.PLAYERS)
                            {
                                dsplayer plng = new dsplayer();
                                plng.POS = pl.POS;
                                plng.REALPOS = pl.POS;
                                plng.INCOME = pl.INCOME;
                                plng.KILLSUM = pl.KILLSUM;
                                plng.ARMY = pl.ARMY;
                                plng.NAME = pl.NAME;
                                plng.PDURATION = pl.PDURATION;
                                plng.RACE = pl.RACE;
                                plng.TEAM = pl.TEAM;
                                plng.RESULT = pl.RESULT;

                                if (myunit.UNITLIST.ContainsKey(replay.REPLAY))
                                {
                                    foreach (dsunits_player upl in myunit.UNITLIST[replay.REPLAY])
                                    {
                                        if (int.Parse(upl.PLAYERID) == pl.POS)
                                        {
                                            if (upl.UNITS.Count > 0)
                                            {
                                                foreach (string bp in upl.UNITS.Keys)
                                                {
                                                    if (bp == "5min")
                                                    {
                                                        if (upl.UNITS[bp].Count > 0)
                                                        {
                                                            if (!plng.UNITS.ContainsKey("MIN5")) plng.UNITS.Add("MIN5", new Dictionary<string, int>());
                                                            foreach (KeyValuePair<string, int> unit in upl.UNITS[bp])
                                                            {
                                                                plng.UNITS["MIN5"].Add(unit.Key, unit.Value);
                                                            }
                                                        }
                                                    }
                                                    else if (bp == "10min")
                                                    {
                                                        if (upl.UNITS[bp].Count > 0)
                                                        {
                                                            if (!plng.UNITS.ContainsKey("MIN10")) plng.UNITS.Add("MIN10", new Dictionary<string, int>());
                                                            foreach (KeyValuePair<string, int> unit in upl.UNITS[bp])
                                                            {
                                                                plng.UNITS["MIN10"].Add(unit.Key, unit.Value);
                                                            }
                                                        }
                                                    }
                                                    else if (bp == "15min")
                                                    {
                                                        if (upl.UNITS[bp].Count > 0)
                                                        {
                                                            if (!plng.UNITS.ContainsKey("MIN15")) plng.UNITS.Add("MIN15", new Dictionary<string, int>());
                                                            foreach (KeyValuePair<string, int> unit in upl.UNITS[bp])
                                                            {
                                                                plng.UNITS["MIN15"].Add(unit.Key, unit.Value);
                                                            }
                                                        }
                                                    }
                                                    else if (bp == "fin")
                                                    {
                                                        if (upl.UNITS[bp].Count > 0)
                                                        {
                                                            if (!plng.UNITS.ContainsKey("ALL")) plng.UNITS.Add("ALL", new Dictionary<string, int>());
                                                            foreach (KeyValuePair<string, int> unit in upl.UNITS[bp])
                                                            {
                                                                plng.UNITS["ALL"].Add(unit.Key, unit.Value);
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                playersng.Add(plng);
                            }
                            dsdec.FixPos(replay);
                            dsreplay replayng = new dsreplay();
                            replayng.WINNER = replay.WINNER;
                            replayng.DURATION = replay.DURATION;
                            replayng.GAMETIME = replay.GAMETIME;
                            replayng.PLAYERCOUNT = replay.PLAYERCOUNT;

                            int j = 0;
                            string repid = replay.REPLAY;

                            foreach (string reppath in myReplay_list)
                            {

                                var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(reppath);
                                MD5 md5 = new MD5CryptoServiceProvider();
                                string reppath_md5 = System.BitConverter.ToString(md5.ComputeHash(plainTextBytes));
                                string id = replay.REPLAY;
                                repid = reppath_md5 + "/" + id;
                                Match m = rx_path.Match(id);
                                if (m.Success)
                                {
                                    if (int.Parse(m.Groups[2].Value) == j)
                                    {
                                        id = m.Groups[1].Value.ToString();
                                        repid = reppath_md5 + "/" + id;
                                        break;
                                    }
                                }
                                else
                                {
                                    break;
                                }
                                j++;
                            }
                            replayng.REPLAY = repid;
                            replayng.ID = i;
                            replayng.PLAYERS = playersng;

                            dsdec.Save(myStats_json, replayng);
                        }
                        Dispatcher.Invoke(() =>
                        {
                            gr_filter1.Visibility = System.Windows.Visibility.Visible;
                            gr_mode.Visibility = Visibility.Visible;
                            bt_show.IsEnabled = true;
                            cb_sample.IsEnabled = true;
                            bt_filter2.IsEnabled = true;
                            dp_menu.IsEnabled = true;
                            gr_chart.Children.Remove(mybox);
                            MessageBox.Show("Finished.", "sc2dsstats");
                            replays = LoadData(myStats_json);
                        });
                    }, TaskCreationOptions.AttachedToParent);
                } else EnableLight();
            } else EnableLight();
            MessageBox.Show("Version 0.8.0.0: We improved the scan process. Please wait till your data is converted to the new format. This should take a maximum of a few minutes.", "sc2dsstats");
            Properties.Settings.Default.V8 = true;
            Properties.Settings.Default.Save();
        }

        public void EnableLight()
        {
            gr_filter1.Visibility = System.Windows.Visibility.Visible;
            gr_mode.Visibility = Visibility.Visible;
            bt_show.IsEnabled = true;
            cb_sample.IsEnabled = true;
            bt_filter2.IsEnabled = true;
            dp_menu.IsEnabled = true;
        }

        public void FirstRun_Version()
        {
            var appSettings = ConfigurationManager.AppSettings;
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

            string backup = myStats_csv + "_bak";
            // Backup
            if (File.Exists(myStats_csv))
            {

                try
                {
                    System.IO.File.Copy(myStats_csv, backup, true);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message.ToString());
                }
            }

            // MessageBox.Show("Version 0.6.0.7: It is now possile to add multiple player names and multiple replay directories.", "sc2dsstats");
            string info = "Version 0.7.0.5: It is now possile to generate some Units statistics." + Environment.NewLine + Environment.NewLine;
            if (MessageBox.Show(info + "Do you want to enable the Unit statistics now? (You can enable/disable it at any time at File->Options", "sc2dsstats", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.No)
            {
                //do no stuff
                config.AppSettings.Settings.Remove("UNITS");
                config.AppSettings.Settings.Add("UNITS", "0");
            }
            else
            {
                //do yes stuff
                config.AppSettings.Settings.Remove("UNITS");
                config.AppSettings.Settings.Add("UNITS", "1");

                string info2 = "To have the Units statistic available for your old replays we have to rescan all replays - this may take a long time." + Environment.NewLine + Environment.NewLine;
                if (MessageBox.Show(info2 + "Do you want to rescan all your replays, now? (You can do this later by renaming the STATS_FILE)", "sc2dsstats", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.No)
                {
                    //do no stuff
                }
                else
                {
                    //do yes stuff
                    string mbackup = myStats_csv + "_moved";

                    try
                    {
                        System.IO.File.Move(myStats_csv, mbackup);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message.ToString());
                    }
                    mnu_Scanpre(null, null);
                }
            }

            //Properties.Settings.Default.V7 = true;
            Properties.Settings.Default.Save();
            ConfigurationManager.RefreshSection("appSettings");
            config.Save();

        }

        public void SetPlayerList(string pl)
        {
            if (pl.Contains(";"))
            {
                pl = string.Concat(pl.Where(c => !char.IsWhiteSpace(c)));
                if (pl.EndsWith(";")) pl = pl.Remove(pl.Length - 1);
                player_list = new List<string>(pl.Split(';').ToList());
                player_list.RemoveAll(RemoveEmpty);
            }
            else
            {

                player_list.Add(pl);
            }
        }

        public void SetReplayList(string rep)
        {
            if (rep.Contains(";"))
            {
                if (rep.EndsWith(";")) rep = rep.Remove(myReplay_Path.Length - 1);
                myReplay_list = new List<string>(rep.Split(';').ToList());
                myReplay_list.RemoveAll(RemoveEmpty);
            }
            else
            {
                myReplay_list.Add(rep);
            }

            foreach (string rep_path in myReplay_list)
            {
                if (!Directory.Exists(rep_path))
                {
                    MessageBox.Show("We can't find all directorys in your replay_path - please check in File->Options");
                    FirstRun();
                    break;
                }
            }
        }

        private static bool RemoveEmpty(String s)
        {
            return s == "";
        }

        public void FirstRun()
        {
            gr_filter1.Visibility = System.Windows.Visibility.Hidden;
            gr_mode.Visibility = Visibility.Hidden;
            bt_show.IsEnabled = false;
            cb_sample.IsEnabled = false;
            bt_filter2.IsEnabled = false;
            dp_menu.IsEnabled = false;

            if (Properties.Settings.Default.PLAYER != "0")
            {
                fr_InputTextBox.Text = Properties.Settings.Default.PLAYER;
            }
            if (Properties.Settings.Default.REPLAY_PATH != "0")
            {
                fr_InputTextBox2.Text = Properties.Settings.Default.REPLAY_PATH + ";";
            }

            gr_firstrun.Visibility = Visibility.Visible;

        }
        private void SetGUIFilter(object sender, EventArgs e) {
            Win_configng.SetConfigDefault(this);
        }
        private void SetGUIFilter_deprecated(object sender, EventArgs e)
        {
            var appSettings = ConfigurationManager.AppSettings;

            if (appSettings["SKIP_STD"] != null && appSettings["SKIP_STD"] == "1")
            {
                cb_std.IsChecked = false;
            }
            else
            {
                cb_std.IsChecked = true;
            }

            if (appSettings["BETA"] != null && appSettings["BETA"] == "1")
            {
                cb_beta.IsChecked = true;
            }
            else
            {
                cb_beta.IsChecked = true;
            }

            if (appSettings["HOTS"] != null && appSettings["HOTS"] == "1")
            {
                cb_hots.IsChecked = true;
            }
            else
            {
                cb_hots.IsChecked = true;
            }

            cb_duration.IsChecked = false;
            tb_duration.Text = "0";
            tb_duration.IsEnabled = false;
            if (appSettings["DURATION"] != null)
            {
                if (appSettings["DURATION"] != "0")
                {
                    cb_duration.IsChecked = true;
                    tb_duration.Text = appSettings["DURATION"];
                    tb_duration.IsEnabled = true;
                }
            }

            cb_leaver.IsChecked = false;
            tb_leaver.Text = "0";
            tb_leaver.IsEnabled = false;
            if (appSettings["LEAVER"] != null)
            {
                if (appSettings["LEAVER"] != "0")
                {
                    cb_leaver.IsChecked = true;
                    tb_leaver.Text = appSettings["LEAVER"];
                    tb_leaver.IsEnabled = true;
                }
            }

            cb_killsum.IsChecked = false;
            tb_killsum.Text = "0";
            tb_killsum.IsEnabled = false;
            if (appSettings["KILLSUM"] != null)
            {
                if (appSettings["KILLSUM"] != "0")
                {
                    cb_killsum.IsChecked = true;
                    tb_killsum.Text = appSettings["KILLSUM"];
                    tb_killsum.IsEnabled = true;
                }
            }

            cb_income.IsChecked = false;
            tb_income.Text = "0";
            tb_income.IsEnabled = false;
            if (appSettings["INCOME"] != null)
            {
                if (appSettings["INCOME"] != "0")
                {
                    cb_income.IsChecked = true;
                    tb_income.Text = appSettings["INCOME"];
                    tb_income.IsEnabled = true;
                }
            }

            cb_army.IsChecked = false;
            tb_army.Text = "0";
            tb_army.IsEnabled = false;
            if (appSettings["ARMY"] != null)
            {
                if (appSettings["ARMY"] != "0")
                {
                    cb_army.IsChecked = true;
                    tb_army.Text = appSettings["ARMY"];
                    tb_army.IsEnabled = true;
                }
            }
        }

        public void DeepCopy<T>(ref T object2Copy, ref T objectCopy)
        {
            using (var stream = new MemoryStream())
            {
                var serializer = new XmlSerializer(typeof(T));

                serializer.Serialize(stream, object2Copy);
                stream.Position = 0;
                objectCopy = (T)serializer.Deserialize(stream);
            }
        }

        private dsselect GetSelection(dsstats sum, dsstats sum_pl, dsmvp sum_mvp, dsmvp sum_mvp_pl, dsdps sum_dps, dsdps sum_dps_pl)
        {

            List<dsstats_race> data = new List<dsstats_race>();
            dsselect selection = new dsselect();
            int ggames = 0;
            double gwr = 0;
            double gdr = 0;
            string interest = cb_vs.SelectedItem.ToString();
            string y_axis = "%";
            List<KeyValuePair<string, double>> tdata = new List<KeyValuePair<string, double>>();

            if (cb_mode.SelectedItem.ToString() == "Winrate")
            {
                lb_sb_info1.Content = "Winrate";
                if (cb_player.IsChecked == true)
                {
                    lb_sb_info1.Content += " player";

                    if (chb_vs.IsChecked == true)
                    {
                        lb_sb_info1.Content += " - " + interest + " vs ...";

                        dsstats_vs vs = new dsstats_vs();
                        dsstats_race cmdr = new dsstats_race();
                        cmdr = sum_pl.objRace(interest);
                        vs = cmdr.OPP;

                        data = vs.VS;

                        ggames = vs.GAMES;
                        gwr = vs.GetWR();
                        gdr = vs.GetDURATION(interest);

                    }
                    else if (chb_cmdr_vs.IsChecked == true)
                    {
                        lb_sb_info1.Content += " - ... vs " + interest;

                        dsstats_vs vs = new dsstats_vs();
                        dsstats_race cmdr = new dsstats_race();
                        dsstats_race data_vs = new dsstats_race();
                        foreach (var intcmdr in s_races)
                        {
                            cmdr = sum_pl.objRace(intcmdr);
                            vs = cmdr.OPP;
                            dsstats_race cmdr_vs = new dsstats_race();
                            cmdr_vs = vs.VS.Find(x => x.RACE == interest);

                            if (cmdr_vs != null)
                            {
                                dsstats_race cmdr_data = new dsstats_race();
                                DeepCopy(ref cmdr_vs, ref cmdr_data);
                                cmdr_data.RACE = intcmdr;
                                data.Add(cmdr_data);
                                data_vs.RGAMES += cmdr_data.RGAMES;
                                data_vs.RDURATION += cmdr_data.RDURATION;
                                data_vs.RWINS += cmdr_data.RWINS;
                            }
                        }
                        ggames = data_vs.RGAMES;
                        gwr = data_vs.GetWR();
                        gdr = data_vs.GetDURATION();
                    }
                    else
                    {

                        data = sum_pl.LRACE;
                        ggames = sum_pl.GAMES;
                        gwr = sum_pl.GetWR();
                        gdr = sum_pl.GetDURATION();

                    }
                }
                else
                {
                    lb_sb_info1.Content += " world";
                    if (chb_vs.IsChecked == true)
                    {
                        lb_sb_info1.Content += " - " + interest + " vs ... ";

                        dsstats_vs vs = new dsstats_vs();
                        dsstats_race cmdr = new dsstats_race();
                        cmdr = sum.objRace(interest);
                        vs = cmdr.OPP;

                        data = vs.VS;

                        ggames = vs.GAMES;
                        gwr = vs.GetWR();
                        gdr = vs.GetDURATION(interest);

                    }
                    else if (chb_cmdr_vs.IsChecked == true)
                    {
                        lb_sb_info1.Content += " - ... vs " + interest;

                        dsstats_vs vs = new dsstats_vs();
                        dsstats_race cmdr = new dsstats_race();
                        dsstats_race data_vs = new dsstats_race();
                        foreach (var intcmdr in s_races)
                        {
                            cmdr = sum.objRace(intcmdr);
                            vs = cmdr.OPP;
                            dsstats_race cmdr_vs = new dsstats_race();
                            cmdr_vs = vs.VS.Find(x => x.RACE == interest);

                            if (cmdr_vs != null)
                            {
                                dsstats_race cmdr_data = new dsstats_race();
                                DeepCopy(ref cmdr_vs, ref cmdr_data);
                                cmdr_data.RACE = intcmdr;
                                data.Add(cmdr_data);
                                data_vs.RGAMES += cmdr_data.RGAMES;
                                data_vs.RDURATION += cmdr_data.RDURATION;
                                data_vs.RWINS += cmdr_data.RWINS;
                            }
                        }
                        ggames = data_vs.RGAMES;
                        gwr = data_vs.GetWR();
                        gdr = data_vs.GetDURATION();
                    }
                    else
                    {

                        data = sum.LRACE;
                        ggames = sum.GAMES;
                        gwr = sum.GetWR();
                        sum.GAMES /= 6; // dirty quick
                        gdr = sum.GetDURATION();
                    }

                }

            }
            else if (cb_mode.SelectedItem.ToString() == "MVP")
            {

                lb_sb_info1.Content = "MVP";
                if (cb_player.IsChecked == true)
                {
                    lb_sb_info1.Content += " player";

                    if (chb_vs.IsChecked == true)
                    {
                        lb_sb_info1.Content += " - " + interest + " vs ...";
                        dsstats_vs vs = new dsstats_vs();
                        dsstats_race cmdr = new dsstats_race();
                        cmdr = sum_mvp_pl.objRace(interest);
                        vs = cmdr.OPP;

                        data = vs.VS;

                        ggames = vs.GAMES;
                        gwr = vs.GetWR();
                        gdr = vs.GetDURATION(interest);
                    }
                    else if (chb_cmdr_vs.IsChecked == true)
                    {
                        lb_sb_info1.Content += " - ... vs " + interest;

                        dsstats_vs vs = new dsstats_vs();
                        dsstats_race cmdr = new dsstats_race();
                        dsstats_race data_vs = new dsstats_race();
                        foreach (var intcmdr in s_races)
                        {
                            cmdr = sum_mvp_pl.objRace(intcmdr);
                            vs = cmdr.OPP;
                            dsstats_race cmdr_vs = new dsstats_race();
                            cmdr_vs = vs.VS.Find(x => x.RACE == interest);

                            if (cmdr_vs != null)
                            {
                                dsstats_race cmdr_data = new dsstats_race();
                                DeepCopy(ref cmdr_vs, ref cmdr_data);
                                cmdr_data.RACE = intcmdr;
                                data.Add(cmdr_data);
                                data_vs.RGAMES += cmdr_data.RGAMES;
                                data_vs.RDURATION += cmdr_data.RDURATION;
                                data_vs.RWINS += cmdr_data.RWINS;
                            }
                        }
                        ggames = data_vs.RGAMES;
                        gwr = data_vs.GetWR();
                        gdr = data_vs.GetDURATION();
                    }
                    else
                    {

                        data = sum_mvp_pl.LRACE;
                        ggames = sum_mvp_pl.GAMES;
                        gwr = sum_mvp_pl.GetWR();
                        gdr = sum_mvp_pl.GetDURATION();
                    }

                }
                else
                {
                    lb_sb_info1.Content += " world";
                    if (chb_vs.IsChecked == true)
                    {
                        lb_sb_info1.Content += " - " + interest + " vs ...";
                        dsstats_vs vs = new dsstats_vs();
                        dsstats_race cmdr = new dsstats_race();
                        cmdr = sum_mvp.objRace(interest);
                        vs = cmdr.OPP;

                        data = vs.VS;

                        ggames = vs.GAMES;
                        gwr = vs.GetWR();
                        gdr = vs.GetDURATION(interest);

                    }
                    else if (chb_cmdr_vs.IsChecked == true)
                    {
                        lb_sb_info1.Content += " - ... vs " + interest;

                        dsstats_vs vs = new dsstats_vs();
                        dsstats_race cmdr = new dsstats_race();
                        dsstats_race data_vs = new dsstats_race();
                        foreach (var intcmdr in s_races)
                        {
                            cmdr = sum_mvp.objRace(intcmdr);
                            vs = cmdr.OPP;
                            dsstats_race cmdr_vs = new dsstats_race();
                            cmdr_vs = vs.VS.Find(x => x.RACE == interest);

                            if (cmdr_vs != null)
                            {
                                dsstats_race cmdr_data = new dsstats_race();
                                DeepCopy(ref cmdr_vs, ref cmdr_data);
                                cmdr_data.RACE = intcmdr;
                                data.Add(cmdr_data);
                                data_vs.RGAMES += cmdr_data.RGAMES;
                                data_vs.RDURATION += cmdr_data.RDURATION;
                                data_vs.RWINS += cmdr_data.RWINS;
                            }
                        }
                        ggames = data_vs.RGAMES;
                        gwr = data_vs.GetWR();
                        gdr = data_vs.GetDURATION();
                    }
                    else
                    {

                        data = sum_mvp.LRACE;

                        ggames = sum_mvp.GAMES;
                        gwr = sum_mvp.GetWR();
                        gdr = sum_mvp.GetDURATION();
                    }

                }


            }
            else if (cb_mode.SelectedItem.ToString() == "Damage")
            {
                lb_sb_info1.Content = "Damage";
                if (cb_player.IsChecked == true)
                {
                    lb_sb_info1.Content += " player";

                    if (chb_vs.IsChecked == true)
                    {
                        lb_sb_info1.Content += " - " + interest + " vs ...";

                        dsstats_vs vs = new dsstats_vs();
                        dsstats_race cmdr = new dsstats_race();
                        cmdr = sum_dps_pl.objRace(interest);
                        vs = cmdr.OPP;
                        data = vs.VS;

                    }
                    else if (chb_cmdr_vs.IsChecked == true)
                    {
                        lb_sb_info1.Content += " - ... vs " + interest;

                        dsstats_vs vs = new dsstats_vs();
                        dsstats_race cmdr = new dsstats_race();
                        dsstats_race data_vs = new dsstats_race();
                        foreach (var intcmdr in s_races)
                        {
                            cmdr = sum_dps_pl.objRace(intcmdr);
                            vs = cmdr.OPP;
                            dsstats_race cmdr_vs = new dsstats_race();
                            cmdr_vs = vs.VS.Find(x => x.RACE == interest);

                            if (cmdr_vs != null)
                            {
                                dsstats_race cmdr_data = new dsstats_race();
                                DeepCopy(ref cmdr_vs, ref cmdr_data);
                                cmdr_data.RACE = intcmdr;
                                data.Add(cmdr_data);
                                data_vs.RGAMES += cmdr_data.RGAMES;
                                data_vs.RDURATION += cmdr_data.RDURATION;
                                data_vs.RWINS += cmdr_data.RWINS;
                            }
                        }
                        ggames = data_vs.RGAMES;
                        gwr = data_vs.GetWR();
                        gdr = data_vs.GetDURATION();
                    }
                    else
                    {
                        data = sum_dps_pl.LRACE;
                    }
                }
                else
                {
                    lb_sb_info1.Content += " world";
                    if (chb_vs.IsChecked == true)
                    {
                        lb_sb_info1.Content += " - " + interest + " vs ...";

                        dsstats_vs vs = new dsstats_vs();
                        dsstats_race cmdr = new dsstats_race();
                        cmdr = sum_dps.objRace(interest);
                        vs = cmdr.OPP;
                        data = vs.VS;
                    }
                    else if (chb_cmdr_vs.IsChecked == true)
                    {
                        lb_sb_info1.Content += " - ... vs " + interest;

                        dsstats_vs vs = new dsstats_vs();
                        dsstats_race cmdr = new dsstats_race();
                        dsstats_race data_vs = new dsstats_race();
                        foreach (var intcmdr in s_races)
                        {
                            cmdr = sum_dps.objRace(intcmdr);
                            vs = cmdr.OPP;
                            dsstats_race cmdr_vs = new dsstats_race();
                            cmdr_vs = vs.VS.Find(x => x.RACE == interest);

                            if (cmdr_vs != null)
                            {
                                dsstats_race cmdr_data = new dsstats_race();
                                DeepCopy(ref cmdr_vs, ref cmdr_data);
                                cmdr_data.RACE = intcmdr;
                                data.Add(cmdr_data);
                                data_vs.RGAMES += cmdr_data.RGAMES;
                                data_vs.RDURATION += cmdr_data.RDURATION;
                                data_vs.RWINS += cmdr_data.RWINS;
                            }
                        }
                        ggames = data_vs.RGAMES;
                        gwr = data_vs.GetWR();
                        gdr = data_vs.GetDURATION();
                    }
                    else
                    {
                        data = sum_dps.LRACE;

                    }

                }

            }
            else if (cb_mode.SelectedItem.ToString() == "Timeline")
            {
                lb_sb_info1.Content = "Timeline " + interest;
                //interest = "Nova";
                cb_vs.Visibility = Visibility.Visible;


                DSfilter dsfil = new DSfilter(this);
                List<dsreplay> filtered_replays = new List<dsreplay>();
                filtered_replays = dsfil.Filter(replays);

                List<dsreplay> replay_sorted = new List<dsreplay>();
                replay_sorted = filtered_replays.Where(x => x.RACES.Contains(interest)).OrderBy(o => o.GAMETIME).ToList();

                int total = 0;
                int games = 0;
                int wins = 0;
                int gwins = 0;

                foreach (dsreplay rep in replay_sorted)
                {
                    foreach (dsplayer pl in rep.PLAYERS)
                    {
                        if (pl.RACE == interest)
                        {
                            if (cb_player.IsChecked == true && !player_list.Contains(pl.NAME))
                            {
                                continue;
                            }
                            total++;
                            ggames++;
                        }
                    }
                }

                int sep = 0;
                //if (total > 100)
                //{
                sep = total / 10;
                //}

                int i = 0;
                foreach (dsreplay rep in replay_sorted)
                {
                    foreach (dsplayer pl in rep.PLAYERS)
                    {
                        if (pl.RACE == interest)
                        {
                            if (cb_player.IsChecked == true && !player_list.Contains(pl.NAME))
                            {
                                continue;
                            }
                            i++;
                            games++;
                            if (pl.TEAM == rep.WINNER)
                            {
                                wins++;
                                gwins++;
                            }

                            if (i > sep)
                            {
                                if (games > 0)
                                {
                                    double wr = 0;
                                    if (games == 0)
                                    {
                                        wr = 0;
                                    }
                                    else
                                    {
                                        double dwins = wins;
                                        double dgames = games;
                                        wr = dwins * 100 / dgames;
                                        wr = Math.Round(wr, 2);
                                        i = 0;
                                        string mtime = rep.GAMETIME.ToString().Substring(0, 8);

                                        KeyValuePair<string, double> ent = new KeyValuePair<string, double>(mtime + " (" + games + ") ", wr);
                                        tdata.Add(ent);
                                        games = 0;
                                        wins = 0;
                                    }
                                }
                            }
                        }
                    }
                }

                if (games > 0)
                {
                    double wr = 0;
                    if (games == 0)
                    {
                        wr = 0;
                    }
                    else
                    {
                        double dwins = wins;
                        double dgames = games;
                        wr = dwins * 100 / dgames;
                        wr = Math.Round(wr, 2);
                        i = 0;
                        string mtime = otf_enddate.SelectedDate.Value.ToString("yyyyMMdd");

                        KeyValuePair<string, double> ent = new KeyValuePair<string, double>(mtime + " (" + games + ") ", wr);
                        tdata.Add(ent);
                    }
                }

                gwr = (double)gwins * 100 / (double)ggames;
                gwr = Math.Round(gwr, 2);
            }

            List<KeyValuePair<string, double>> cdata = new List<KeyValuePair<string, double>>();
            string add = "";

            double max = 0;

            foreach (dsstats_race cmdr in data)
            {
                double wr = 0;
                if (cb_mode.SelectedItem.ToString() == "Damage")
                {
                    if (rb_dps.IsChecked == true)
                    {
                        add = " (DPS)";
                        y_axis = "MineralValueKilled / gameduration";
                        wr = cmdr.GetDPS();
                    }
                    else if (rb_dpm.IsChecked == true)
                    {
                        add = " (DPM)";
                        y_axis = "income / MineralValueKilled";
                        wr = cmdr.GetDPM();
                    }
                    else if (rb_dpv.IsChecked == true)
                    {
                        add = " (DPV)";
                        y_axis = "ArmyValue / MineralValueKilled";
                        wr = cmdr.GetDPV();
                    }
                }
                else
                {
                    wr = cmdr.GetWR();
                }

                if (cmdr.RGAMES > 0)
                {
                    if (!Double.IsInfinity(wr))
                    { // strange things happen ..
                        if (wr > max)
                        {
                            max = wr;
                        }
                        KeyValuePair<string, double> ent = new KeyValuePair<string, double>(cmdr.RACE + " (" + cmdr.RGAMES + ") ", wr);
                        cdata.Add(ent);
                    }
                }
            }
            lb_sb_info1.Content += add;
            char average = '\u2300';
            if (gdr > 0)
            {
                TimeSpan t = TimeSpan.FromSeconds(gdr);
                lb_sb_info1.Content += " (" + average.ToString() + " duration: " + t.Minutes + ":" + t.Seconds.ToString("D2") + " min)";
            }
            cdata.Sort(delegate (KeyValuePair<string, double> x, KeyValuePair<string, double> y)
            {
                if (x.Value == 0 && y.Value == 0) return 0;
                else if (x.Value == 0) return -1;
                else if (y.Value == 0) return 1;
                else return x.Value.CompareTo(y.Value);
            });

            if (cb_mode.SelectedItem.ToString() == "Timeline")
            {
                cdata = tdata;
            }

            string grace = average.ToString();
            if (gwr > 0) cdata.Insert(0, new KeyValuePair<string, double>(grace + " (" + ggames.ToString() + ") ", gwr));

            selection.LIST = data;
            selection.CLIST = cdata;
            selection.GAMES = ggames;
            selection.WINS = gwr;
            selection.TITLE = lb_sb_info1.Content.ToString();
            selection.YAXIS = y_axis;
            selection.YMAX = (int)max;

            return selection;

        }

        public void GetWinrate()
        {

            string sd = otf_startdate.SelectedDate.Value.ToString("yyyyMMdd");
            sd += "000000";
            double sd_int = double.Parse(sd);
            string ed = otf_enddate.SelectedDate.Value.ToString("yyyyMMdd");
            ed += "999999";
            double ed_int = double.Parse(ed);

            int duration = 0;
            int leaver = 0;
            int killsum = 0;
            int army = 0;
            int income = 0;

            List<string> races = new List<string>();
            dsfilter fil = new dsfilter();
            Dictionary<string, int> cmdrs_sum = new Dictionary<string, int>();

            try
            {
                duration = int.Parse(tb_duration.Text);
                leaver = int.Parse(tb_leaver.Text);
                killsum = int.Parse(tb_killsum.Text);
                army = int.Parse(tb_army.Text);
                income = int.Parse(tb_income.Text);
            }
            catch (FormatException)
            {

            }

            dsstats sum = new dsstats();
            sum.Init();
            dsstats sum_pl = new dsstats();
            sum_pl.Init();
            dsmvp sum_mvp = new dsmvp();
            sum_mvp.Init();
            dsmvp sum_mvp_pl = new dsmvp();
            sum_mvp_pl.Init();
            dsdps sum_dps = new dsdps();
            sum_dps.Init();
            dsdps sum_dps_pl = new dsdps();
            sum_dps_pl.Init();

            DSfilter dsfil = new DSfilter(this);
            List<dsreplay> filtered_replays = new List<dsreplay>();
            filtered_replays = dsfil.Filter(replays);

            //foreach (dsreplay dsrep in replays)
            foreach (dsreplay dsrep in filtered_replays)
            {
                if (dsrep.PLAYERCOUNT != 6) continue;
                //if (RepFilter(dsrep, fil, sd_int, ed_int, duration, leaver, killsum, army, income)) continue;

                dsplayer mvp = new dsplayer();

                double gdur = dsrep.DURATION;
                sum.AddGame(gdur);
                sum_pl.AddGame(gdur);
                sum_mvp.AddGame(gdur);
                sum_mvp_pl.AddGame(gdur);
                sum_dps.AddGame(gdur);
                sum_dps_pl.AddGame(gdur);

                foreach (dsplayer pl in dsrep.PLAYERS)
                {
                    if (cmdrs_sum.ContainsKey(pl.RACE))
                    {
                        cmdrs_sum[pl.RACE]++;
                    }
                    else
                    {
                        cmdrs_sum.Add(pl.RACE, 1);
                    }

                    if (pl.KILLSUM == dsrep.MAXKILLSUM)
                    {
                        mvp = pl;
                    }
                    sum.AddGame(pl, dsrep.GetOpp(pl.REALPOS));
                    sum_mvp.AddGame(pl, dsrep.GetOpp(pl.REALPOS));
                    sum_dps.AddGame(pl, dsrep.GetOpp(pl.REALPOS));

                    if (pl.TEAM == dsrep.WINNER)
                    {
                        sum.AddWin(pl, dsrep.GetOpp(pl.REALPOS));
                        sum_dps.AddWin(pl, dsrep.GetOpp(pl.REALPOS));
                    }

                    //if (pl.NAME == player_name)
                    if (player_list.Contains(pl.NAME))
                    {
                        sum_pl.AddGame(pl, dsrep.GetOpp(pl.REALPOS));
                        sum_mvp_pl.AddGame(pl, dsrep.GetOpp(pl.REALPOS));
                        sum_dps_pl.AddGame(pl, dsrep.GetOpp(pl.REALPOS));

                        if (pl.TEAM == dsrep.WINNER)
                        {
                            sum_pl.AddWin(pl, dsrep.GetOpp(pl.REALPOS));
                            sum_dps_pl.AddWin(pl, dsrep.GetOpp(pl.REALPOS));
                        }

                    }
                }

                //if (mvp.NAME == player_name)
                if (player_list.Contains(mvp.NAME))
                {
                    sum_mvp_pl.AddWin(mvp, dsrep.GetOpp(mvp.REALPOS));
                }
                else
                {
                    sum_mvp.AddWin(mvp, dsrep.GetOpp(mvp.REALPOS));
                }
            }

            List<dsstats_race> data = new List<dsstats_race>();
            List<KeyValuePair<string, double>> cdata = new List<KeyValuePair<string, double>>();
            dsselect sel = new dsselect();

            int ggames = 0;
            double gwr = 0;

            sel = GetSelection(sum, sum_pl, sum_mvp, sum_mvp_pl, sum_dps, sum_dps_pl);
            cdata = sel.CLIST;
            ggames = sel.GAMES;
            gwr = sel.WINS;
            //Title = sel.TITLE;
            string yaxis = sel.YAXIS;
            int max_y = sel.YMAX;

            //lb_info.Text = fil.Info();
            lb_info.Text = dsfil.FIL.Info();
            lb_info.Text += Environment.NewLine;
            string cmdr_info = "";
            var ordered = cmdrs_sum.OrderBy(x => x.Value);
            foreach (var bab in ordered)
            {
                double per = 0;
                per = (double)bab.Value * 100 / (filtered_replays.Count * 6);
                per = Math.Round(per, 2);
                cmdr_info += bab.Key + " => " + bab.Value.ToString() + " (" + per.ToString() + "%); ";
            }
            lb_info.Text += cmdr_info;

            if (cb_add.IsChecked == false)
            {
                Items = new ObservableCollection<KeyValuePair<string, double>>(cdata);
            }
            else
            {
                foreach (var bab in cdata)
                {
                    Items.Add(bab);
                }
            }

            if (cb_all.IsChecked == false)
            {
                yaxis = "(" + otf_startdate.SelectedDate.Value.ToString("yyyy-MM-dd") + " to " + otf_enddate.SelectedDate.Value.ToString("yyyy-MM-dd") + ")     " + yaxis;
            }
            else
            {
                yaxis = "(" + otf_enddate.SelectedDate.Value.ToString("yyyy-MM-dd") + ")          " + yaxis;
            }

            SetChartStyle(yaxis, max_y);

            //dynChart.Title = Title;
            dynChart.Title = new TextBlock
            {
                Text = lb_sb_info1.Content.ToString(),
                FontFamily = new System.Windows.Media.FontFamily("/sc2dsstats_rc1;component/#Titillium Web"),
                FontWeight = FontWeights.Bold,
                //Foreground = System.Windows.Media.Brushes.DarkBlue;
                Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFffffff"))
            };





        }

        private bool RepFilter(dsreplay rep, dsfilter fil, double sd, double ed, int duration, int leaver, int killsum, int army, int income)
        {
            bool skip = false;
            fil.GAMES++;

            // Beta
            if (cb_beta.IsChecked == false && skip == false)
            {
                if (rep.REPLAY.Contains("Beta")) skip = true;
                if (skip) fil.Beta++;
            }

            //HotS
            if (cb_hots.IsChecked == false && skip == false)
            {
                if (rep.REPLAY.Contains("HotS")) skip = true;
                if (skip) fil.Hots++;
            }

            //gametime
            if (cb_all.IsChecked == false && skip == false)
            {
                if (rep.GAMETIME < sd) skip = true;
                if (rep.GAMETIME > ed) skip = true;
                if (skip) fil.Gametime++;
            }

            //duration
            if (cb_duration.IsChecked == true && skip == false)
            {
                if (rep.DURATION < duration) skip = true;
                if (skip) fil.Duration++;
            }

            //leaver
            if (cb_leaver.IsChecked == true && skip == false)
            {
                if (rep.MAXLEAVER > leaver) skip = true;
                if (skip) fil.Leaver++;
            }

            //killsum
            if (cb_killsum.IsChecked == true && skip == false)
            {
                if (rep.MINKILLSUM < killsum) skip = true;
                if (skip) fil.Killsum++;
            }

            //army
            if (cb_army.IsChecked == true && skip == false)
            {
                if (rep.MINARMY < army) skip = true;
                if (skip) fil.Army++;
            }

            //income
            if (cb_income.IsChecked == true && skip == false)
            {
                if (rep.MININCOME < income) skip = true;
                if (skip) fil.Income++;
            }

            //std
            if (cb_std.IsChecked == false && skip == false)
            {
                if (rep.RACES.Contains("Terran")) skip = true;
                if (rep.RACES.Contains("Protoss")) skip = true;
                if (rep.RACES.Contains("Zerg")) skip = true;
                if (skip) fil.Std++;
            }

            if (skip == true)
            {
                fil.FILTERED++;
            }

            return skip;

        }

        private void OFTScan()
        {



        }

        public void dg_build_MouseUp(object sender, EventArgs e)
        {
            dsunits myunits = new dsunits(this);
            this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(myunits.ProcessRows_units));
        }



        // chart

        public void UpdateGraph(object sender)
        {
            if (gr_doit.Visibility == Visibility.Visible)
            {
                gr_doit.Visibility = Visibility.Collapsed;
            }

            if (gr_chart.Visibility == Visibility.Collapsed)
            {
                gr_chart.Visibility = Visibility.Visible;
                gr_images.Visibility = Visibility.Visible;
                gr_syn.Visibility = Visibility.Collapsed;
            }
            if (rb_horizontal.IsChecked == true) gr_images.Visibility = Visibility.Visible;
            bool doit = true;

            if (cb_mode.SelectedItem.ToString() == "Synergy")
            {
                doit = false;
                gr_chart.Visibility = Visibility.Collapsed;
                gr_images.Visibility = Visibility.Collapsed;
                gr_syn.Visibility = Visibility.Visible;
                cb_antisyn.Visibility = Visibility.Visible;
                lb_sb_info1.Content = "Synergy";
                GetSynergy();
            }

            if (cb_mode.SelectedItem.ToString() == "Builds")
            {
                doit = false;
                gr_chart.Visibility = Visibility.Collapsed;
                gr_images.Visibility = Visibility.Collapsed;
                gr_syn.Visibility = Visibility.Visible;
                cb_vs.Visibility = Visibility.Visible;
                wb_chart.Visibility = Visibility.Collapsed;
                cb_antisyn.Visibility = Visibility.Hidden;
                lb_sb_info1.Content = "Builds";
                GetBuilds();
            }


            if (cb_add.IsChecked == true && sender != null) doit = false;

            if (doit)
            {
                if (Items != null)
                {
                    if (cb_add.IsChecked == false) Items.Clear();
                    ///dynChart = null;
                    ///dynChart = new Chart() { Background = System.Windows.Media.Brushes.FloralWhite };
                    ///dynChart.Series.Clear();
                    GetWinrate();


                    if (rb_horizontal.IsChecked == true)
                    {
                        tb_fl2_rb_horizontal_Click(null, null);
                    }
                    else if (rb_vertical.IsChecked == true)
                    {
                        tb_fl2_rb_vertical_Click(null, null);
                    }

                    if (gr_chart.Children.Contains(dynChart))
                    {

                    }
                    else
                    {
                        gr_chart.Children.Add(dynChart);
                    }
                }
            }

        }

        private void SetChartStyle_on(string y_Title)
        { }

        private void SetChartStyle(string y_Title, int y_max)
        {

            if (cb_add.IsChecked == false)
            {
                dynChart.Series.Clear();
                dynChart.Axes.Clear();
            }

            Style style = new Style { TargetType = typeof(Grid) };
            //style.Setters.Add(new Setter(Grid.BackgroundProperty, System.Windows.Media.Brushes.LightBlue));
            style.Setters.Add(new Setter(Grid.BackgroundProperty, new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF030A16"))));

            dynChart.PlotAreaStyle = style;

            Style styleLegand = new Style { TargetType = typeof(Control) };
            styleLegand.Setters.Add(new Setter(Control.WidthProperty, 0d));
            styleLegand.Setters.Add(new Setter(Control.HeightProperty, 0d));

            dynChart.LegendStyle = styleLegand;

            CategoryAxis axisX = new CategoryAxis()
            {
                Orientation = AxisOrientation.X,
                //HorizontalAlignment = HorizontalAlignment.Right,
                FontFamily = new System.Windows.Media.FontFamily("/sc2dsstats_rc1;component/#Titillium Web"),
                Title = "Commanders (generated by https://github.com/ipax77/sc2dsstats)",
                Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF59bbe0"))
            };


            style = new Style { TargetType = typeof(AxisLabel) };
            style.Setters.Add(new Setter(AxisLabel.LayoutTransformProperty, new RotateTransform() { Angle = -90 }));
            style.Setters.Add(new Setter(AxisLabel.FontFamilyProperty, new System.Windows.Media.FontFamily("/sc2dsstats_rc1;component/#Titillium Web")));
            style.Setters.Add(new Setter(AxisLabel.FontSizeProperty, 15.0));
            //style.Setters.Add(new Setter(AxisLabel.ForegroundProperty, System.Windows.Media.Brushes.Black));
            style.Setters.Add(new Setter(AxisLabel.ForegroundProperty, new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF59bbe0"))));
            style.Setters.Add(new Setter(Grid.BackgroundProperty, new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF041427"))));
            axisX.AxisLabelStyle = style;


            LinearAxis axisY = new LinearAxis()
            {
                Orientation = AxisOrientation.Y,
                FontFamily = new System.Windows.Media.FontFamily("/sc2dsstats_rc1;component/#Titillium Web"),
                Title = y_Title,
                //VerticalContentAlignment = VerticalAlignment.Bottom,
                Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF59bbe0")),

            };
            if (cb_yscale.IsChecked == true)
            {
                axisY.Minimum = 0;
                axisY.Maximum = (double)y_max + (((double)y_max / 100) * 20);
                if (axisY.Maximum < 1)
                {
                    axisY.Maximum = 1;
                }
                //axisY.Maximum = 120;
            }
            style = new Style { TargetType = typeof(AxisLabel) };
            style.Setters.Add(new Setter(AxisLabel.FontFamilyProperty, new System.Windows.Media.FontFamily("/sc2dsstats_rc1;component/#Titillium Web")));
            style.Setters.Add(new Setter(AxisLabel.FontSizeProperty, 15.0));
            style.Setters.Add(new Setter(AxisLabel.ForegroundProperty, new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF59bbe0"))));
            style.Setters.Add(new Setter(Grid.BackgroundProperty, new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF041427"))));
            axisY.AxisLabelStyle = style;
            axisY.ShowGridLines = true;

            dynChart.Axes.Add(axisX);
            dynChart.Axes.Add(axisY);



        }

        private void tb_fl2_rb_horizontal_Click(object sender, RoutedEventArgs e)
        {
            /**
            System.Windows.Media.Brush arColor = JADEXCODEColor.JADEColor.HCBPAtoARGB(((double)i / a), 1.0, 0.5, 0.5, 0.0);
            // Setup ToolTip XamlString insert.

            string BarColorsAre = "Red = " + ((SolidColorBrush)BarColor).Color.R.ToString() + ", " +
                        "Green = " + ((SolidColorBrush)BarColor).Color.G.ToString() + ", " +
                        "Blue = " + ((SolidColorBrush)BarColor).Color.B.ToString() + ", " +
                        "Alpha = " + ((SolidColorBrush)BarColor).Color.A.ToString();
            **/
            string ctXamlString =

//"<Style x:Key=\"CmdrImage_Style\" TargetType=\"{x:Type Image}\">" +
//"</Style>" +

"<ControlTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:src=\"clr-namespace:sc2dsstats_rc1\" xmlns:DVC=\"clr-namespace:System.Windows.Controls.DataVisualization.Charting;assembly=System.Windows.Controls.DataVisualization.Toolkit\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" TargetType=\"DVC:ColumnDataPoint\">" +
"<Border x:Name=\"Root\" Opacity=\"0\" BorderBrush=\"{TemplateBinding BorderBrush}\" BorderThickness=\"{TemplateBinding BorderThickness}\">" +
"<VisualStateManager.VisualStateGroups>" +
"<VisualStateGroup x:Name=\"CommonStates\">" +
"<VisualStateGroup.Transitions>" +
"<VisualTransition GeneratedDuration=\"0:0:0.1\"/>" +
"</VisualStateGroup.Transitions>" +
"<VisualState x:Name=\"Normal\"/>" +
"<VisualState x:Name=\"MouseOver\">" +
"<Storyboard>" +
"<DoubleAnimation Duration=\"0\" Storyboard.TargetName=\"MouseOverHighlight\" Storyboard.TargetProperty=\"Opacity\" To=\"0.6\"/>" +
"</Storyboard>" +
"</VisualState>" +
"</VisualStateGroup>" +
"<VisualStateGroup x:Name=\"SelectionStates\">" +
"<VisualStateGroup.Transitions>" +
"<VisualTransition GeneratedDuration=\"0:0:0.1\"/>" +
"</VisualStateGroup.Transitions>" +
"<VisualState x:Name=\"Unselected\"/>" +
"<VisualState x:Name=\"Selected\">" +
"<Storyboard>" +
"<DoubleAnimation Duration=\"0\" Storyboard.TargetName=\"SelectionHighlight\" Storyboard.TargetProperty=\"Opacity\" To=\"0.6\"/>" +
"</Storyboard>" +
"</VisualState>" +
"</VisualStateGroup>" +
"<VisualStateGroup x:Name=\"RevealStates\">" +
"<VisualStateGroup.Transitions>" +
"<VisualTransition GeneratedDuration=\"0:0:0.5\"/>" +
"</VisualStateGroup.Transitions>" +
"<VisualState x:Name=\"Shown\">" +
"<Storyboard>" +
"<DoubleAnimation Duration=\"0\" Storyboard.TargetName=\"Root\" Storyboard.TargetProperty=\"Opacity\" To=\"1\"/>" +
"</Storyboard>" +
"</VisualState>" +
"<VisualState x:Name=\"Hidden\">" +
"<Storyboard>" +
"<DoubleAnimation Duration=\"0\" Storyboard.TargetName=\"Root\" Storyboard.TargetProperty=\"Opacity\" To=\"0\"/>" +
"</Storyboard>" +
"</VisualState>" +
"</VisualStateGroup>" +
"</VisualStateManager.VisualStateGroups>" +
"<Grid>" +
"<Rectangle Fill=\"{TemplateBinding Background}\" Stroke=\"DarkBlue\" />" +
"<Grid Margin=\"0 -20 0 0\" HorizontalAlignment=\"Center\" VerticalAlignment=\"Top\"> " +
"<Border CornerRadius=\"2\" BorderBrush=\"#88888888\" BorderThickness=\"0.5\">" +
"<Border CornerRadius=\"2\" BorderBrush=\"#44888888\" BorderThickness=\"0.5\"/>" +
"</Border>" +
"<TextBlock Margin=\"2\">" +
//"<TextBlock Text=\"{TemplateBinding FormattedDependentValue}\" Margin=\"2\"/>" +
"<Run FontWeight=\"Bold\" Background=\"BlanchedAlmond\" Foreground=\"DarkRed\" FontFamily=\"Courier New\" FontSize=\"13\" Text=\"{TemplateBinding FormattedDependentValue}\"/>" +
"</TextBlock>" +
"</Grid>" +
"</Grid>" +
"</Border>" +
"</ControlTemplate>";

            string ctXamlString2 =
"<ControlTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:src=\"clr-namespace:sc2dsstats_rc1\" xmlns:DVC=\"clr-namespace:System.Windows.Controls.DataVisualization.Charting;assembly=System.Windows.Controls.DataVisualization.Toolkit\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" TargetType=\"DVC:ColumnDataPoint\">" +
"<Border x:Name=\"Root\" Opacity=\"0\" BorderBrush=\"{TemplateBinding BorderBrush}\" BorderThickness=\"{TemplateBinding BorderThickness}\">" +
"<VisualStateManager.VisualStateGroups>" +
"<VisualStateGroup x:Name=\"CommonStates\">" +
"<VisualStateGroup.Transitions>" +
"<VisualTransition GeneratedDuration=\"0:0:0.1\"/>" +
"</VisualStateGroup.Transitions>" +
"<VisualState x:Name=\"Normal\"/>" +
"<VisualState x:Name=\"MouseOver\">" +
"<Storyboard>" +
"<DoubleAnimation Duration=\"0\" Storyboard.TargetName=\"MouseOverHighlight\" Storyboard.TargetProperty=\"Opacity\" To=\"0.6\"/>" +
"</Storyboard>" +
"</VisualState>" +
"</VisualStateGroup>" +
"<VisualStateGroup x:Name=\"SelectionStates\">" +
"<VisualStateGroup.Transitions>" +
"<VisualTransition GeneratedDuration=\"0:0:0.1\"/>" +
"</VisualStateGroup.Transitions>" +
"<VisualState x:Name=\"Unselected\"/>" +
"<VisualState x:Name=\"Selected\">" +
"<Storyboard>" +
"<DoubleAnimation Duration=\"0\" Storyboard.TargetName=\"SelectionHighlight\" Storyboard.TargetProperty=\"Opacity\" To=\"0.6\"/>" +
"</Storyboard>" +
"</VisualState>" +
"</VisualStateGroup>" +
"<VisualStateGroup x:Name=\"RevealStates\">" +
"<VisualStateGroup.Transitions>" +
"<VisualTransition GeneratedDuration=\"0:0:0.5\"/>" +
"</VisualStateGroup.Transitions>" +
"<VisualState x:Name=\"Shown\">" +
"<Storyboard>" +
"<DoubleAnimation Duration=\"0\" Storyboard.TargetName=\"Root\" Storyboard.TargetProperty=\"Opacity\" To=\"1\"/>" +
"</Storyboard>" +
"</VisualState>" +
"<VisualState x:Name=\"Hidden\">" +
"<Storyboard>" +
"<DoubleAnimation Duration=\"0\" Storyboard.TargetName=\"Root\" Storyboard.TargetProperty=\"Opacity\" To=\"0\"/>" +
"</Storyboard>" +
"</VisualState>" +
"</VisualStateGroup>" +
"</VisualStateManager.VisualStateGroups>" +
"<Grid>" +
"<Rectangle Fill=\"{TemplateBinding Background}\" Stroke=\"DarkBlue\" />" +
"<Grid Margin=\"0 -20 0 0\" HorizontalAlignment=\"Center\" VerticalAlignment=\"Top\"> " +
"<Border CornerRadius=\"2\" BorderBrush=\"#88888888\" BorderThickness=\"0.5\">" +
"<Border CornerRadius=\"2\" BorderBrush=\"#44888888\" BorderThickness=\"0.5\"/>" +
"</Border>" +
"<TextBlock Margin=\"2\">" +
//"<TextBlock Text=\"{TemplateBinding FormattedDependentValue}\" Margin=\"2\"/>" +
"<Run FontWeight=\"Bold\" Background=\"BlanchedAlmond\" Foreground=\"DarkRed\" FontFamily=\"Courier New\" FontSize=\"13\" Text=\"{TemplateBinding FormattedDependentValue}\"/>" +
"</TextBlock>" +
"<Image Source=\"{Binding Path=FormattedIndependentValue, RelativeSource={RelativeSource TemplatedParent}}\" Margin =\"0, 0, 0, 0\" />" +
"</Grid>" +
"</Grid>" +
"</Border>" +
"</ControlTemplate>";

            dynChart.Series.Clear();

            //Horizontal        
            ColumnSeries columnseries = new ColumnSeries();
            columnseries.ItemsSource = Items;
            columnseries.DependentValuePath = "Value";
            columnseries.IndependentValuePath = "Key";



            Style style = new Style { TargetType = typeof(ColumnDataPoint) };
            //style.Setters.Add(new Setter(ColumnDataPoint.IsTabStopProperty, false));

            LinearGradientBrush myLinearGradientBrush = new LinearGradientBrush();
            myLinearGradientBrush.StartPoint = new System.Windows.Point(0, 0);
            myLinearGradientBrush.EndPoint = new System.Windows.Point(1, 1);
            myLinearGradientBrush.GradientStops.Add(new GradientStop(Colors.DarkBlue, 0.0));
            myLinearGradientBrush.GradientStops.Add(new GradientStop(Colors.DarkRed, 0.5));
            myLinearGradientBrush.GradientStops.Add(new GradientStop(Colors.DarkBlue, 1.0));
            //myLinearGradientBrush.GradientStops.Add(new GradientStop(Colors.DarkRed, 0.75));
            //myLinearGradientBrush.GradientStops.Add(new GradientStop(Colors.DarkBlue, 1.0));

            ControlTemplate ct;
            ct = (ControlTemplate)XamlReader.Parse(ctXamlString);
            style.Setters.Add(new Setter(ColumnDataPoint.TemplateProperty, ct));
            style.Setters.Add(new Setter(ColumnDataPoint.BorderBrushProperty, System.Windows.Media.Brushes.Red));
            //style.Setters.Add(new Setter(ColumnDataPoint.BackgroundProperty, System.Windows.Media.Brushes.DarkSlateBlue));
            //style.Setters.Add(new Setter(ColumnDataPoint.BackgroundProperty, new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFb3131d"))));
            //style.Setters.Add(new Setter(ColumnDataPoint.BackgroundProperty, new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF59bbe0"))));
            style.Setters.Add(new Setter(ColumnDataPoint.BackgroundProperty, myLinearGradientBrush));
            columnseries.DataPointStyle = style;
            dynChart.Series.Add(columnseries);


            // Some cmdr pics

            dsimg.ShowImages(this, Items.ToList());
            List<KeyValuePair<string, double>> icoList = new List<KeyValuePair<string, double>>();

            ColumnSeries icoSeries = new ColumnSeries();

            foreach (var bab in Items)
            {
                string skey = "image\\dummy.png";

                double sval = bab.Value;
                skey = dsimg.GetImage(bab.Key.ToString());
                icoList.Add(new KeyValuePair<string, double>(skey, sval));
            }
            icoSeries.ItemsSource = icoList;
            icoSeries.DependentValuePath = "Value";
            icoSeries.IndependentValuePath = "Key";

            style = new Style { TargetType = typeof(ColumnDataPoint) };
            ct = (ControlTemplate)XamlReader.Parse(ctXamlString2);
            style.Setters.Add(new Setter(ColumnDataPoint.TemplateProperty, ct));
            style.Setters.Add(new Setter(ColumnDataPoint.BorderBrushProperty, System.Windows.Media.Brushes.Red));
            style.Setters.Add(new Setter(ColumnDataPoint.BackgroundProperty, myLinearGradientBrush));
            icoSeries.DataPointStyle = style;
            //dynChart.Series.Add(icoSeries);
        }

        private void tb_fl2_rb_vertical_Click(object sender, RoutedEventArgs e)
        {

            gr_images.Visibility = Visibility.Hidden;
            dynChart.Series.Clear();


            string ctXamlString =

//"<Style x:Key=\"CmdrImage_Style\" TargetType=\"{x:Type Image}\">" +
//"</Style>" +

"<ControlTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:src=\"clr-namespace:sc2dsstats_rc1\" xmlns:DVC=\"clr-namespace:System.Windows.Controls.DataVisualization.Charting;assembly=System.Windows.Controls.DataVisualization.Toolkit\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" TargetType=\"DVC:BarDataPoint\">" +
"<Border x:Name=\"Root\" Opacity=\"0\" BorderBrush=\"{TemplateBinding BorderBrush}\" BorderThickness=\"{TemplateBinding BorderThickness}\">" +
"<VisualStateManager.VisualStateGroups>" +
"<VisualStateGroup x:Name=\"CommonStates\">" +
"<VisualStateGroup.Transitions>" +
"<VisualTransition GeneratedDuration=\"0:0:0.1\"/>" +
"</VisualStateGroup.Transitions>" +
"<VisualState x:Name=\"Normal\"/>" +
"<VisualState x:Name=\"MouseOver\">" +
"<Storyboard>" +
"<DoubleAnimation Duration=\"0\" Storyboard.TargetName=\"MouseOverHighlight\" Storyboard.TargetProperty=\"Opacity\" To=\"0.6\"/>" +
"</Storyboard>" +
"</VisualState>" +
"</VisualStateGroup>" +
"<VisualStateGroup x:Name=\"SelectionStates\">" +
"<VisualStateGroup.Transitions>" +
"<VisualTransition GeneratedDuration=\"0:0:0.1\"/>" +
"</VisualStateGroup.Transitions>" +
"<VisualState x:Name=\"Unselected\"/>" +
"<VisualState x:Name=\"Selected\">" +
"<Storyboard>" +
"<DoubleAnimation Duration=\"0\" Storyboard.TargetName=\"SelectionHighlight\" Storyboard.TargetProperty=\"Opacity\" To=\"0.6\"/>" +
"</Storyboard>" +
"</VisualState>" +
"</VisualStateGroup>" +
"<VisualStateGroup x:Name=\"RevealStates\">" +
"<VisualStateGroup.Transitions>" +
"<VisualTransition GeneratedDuration=\"0:0:0.5\"/>" +
"</VisualStateGroup.Transitions>" +
"<VisualState x:Name=\"Shown\">" +
"<Storyboard>" +
"<DoubleAnimation Duration=\"0\" Storyboard.TargetName=\"Root\" Storyboard.TargetProperty=\"Opacity\" To=\"1\"/>" +
"</Storyboard>" +
"</VisualState>" +
"<VisualState x:Name=\"Hidden\">" +
"<Storyboard>" +
"<DoubleAnimation Duration=\"0\" Storyboard.TargetName=\"Root\" Storyboard.TargetProperty=\"Opacity\" To=\"0\"/>" +
"</Storyboard>" +
"</VisualState>" +
"</VisualStateGroup>" +
"</VisualStateManager.VisualStateGroups>" +
"<Grid>" +
"<Rectangle Fill=\"{TemplateBinding Background}\" Stroke=\"DarkBlue\" />" +
"<Grid Margin=\"0, 0, -45, 0\" HorizontalAlignment=\"Right\" VerticalAlignment=\"Center\"> " +
"<Border CornerRadius=\"2\" BorderBrush=\"#88888888\" BorderThickness=\"0.5\">" +
"<Border CornerRadius=\"2\" BorderBrush=\"#44888888\" BorderThickness=\"0.5\"/>" +
"</Border>" +
"<TextBlock Margin=\"0, 0, 0, 0\">" +
"<Run FontWeight=\"Bold\" Background=\"BlanchedAlmond\" Foreground=\"DarkRed\" FontFamily=\"Courier New\" FontSize=\"13\" Text=\"{TemplateBinding FormattedDependentValue}\"/>" +
"</TextBlock>" +
"</Grid>" +
"</Grid>" +
"</Border>" +
"</ControlTemplate>";

            //Vertical
            BarSeries barseries = new BarSeries();

            barseries.ItemsSource = Items;
            barseries.DependentValuePath = "Value";
            barseries.IndependentValuePath = "Key";

            LinearGradientBrush myLinearGradientBrush = new LinearGradientBrush();
            myLinearGradientBrush.StartPoint = new System.Windows.Point(0, 0);
            myLinearGradientBrush.EndPoint = new System.Windows.Point(1, 1);
            myLinearGradientBrush.GradientStops.Add(new GradientStop(Colors.DarkBlue, 0.0));
            myLinearGradientBrush.GradientStops.Add(new GradientStop(Colors.DarkRed, 0.5));
            myLinearGradientBrush.GradientStops.Add(new GradientStop(Colors.DarkBlue, 1.0));

            Style style = new Style { TargetType = typeof(BarDataPoint) };
            ControlTemplate ct;
            ct = (ControlTemplate)XamlReader.Parse(ctXamlString);
            style.Setters.Add(new Setter(BarDataPoint.TemplateProperty, ct));
            style.Setters.Add(new Setter(BarDataPoint.BorderBrushProperty, System.Windows.Media.Brushes.Red));
            style.Setters.Add(new Setter(BarDataPoint.BackgroundProperty, myLinearGradientBrush));
            barseries.DataPointStyle = style;
            dynChart.Series.Add(barseries);





        }

        public void GenerateSynBtn()
        {
            gr_syn_btn.Children.Clear();
            gr_syn_btn.RowDefinitions.Clear();
            gr_syn_btn.ColumnDefinitions.Clear();
            dsimage myimg = new dsimage();

            ColumnDefinition gridCol0 = new ColumnDefinition();
            gridCol0.Width = new GridLength(100);
            gr_syn_btn.ColumnDefinitions.Add(gridCol0);
            ColumnDefinition gridCol1 = new ColumnDefinition();
            gridCol1.Width = new GridLength(300);
            gr_syn_btn.ColumnDefinitions.Add(gridCol1);
            int i = 0;
            foreach (string r in s_races)
            {
                RowDefinition gridRow1 = new RowDefinition();
                gridRow1.Height = new GridLength(30);
                gr_syn_btn.RowDefinitions.Add(gridRow1);

                CheckBox cb = new CheckBox();
                cb.Style = (Style)Application.Current.Resources["cb_Style"];
                //cb.Foreground = System.Windows.Media.Brushes.White;
                //cb.Background = System.Windows.Media.Brushes.LightYellow;
                cb.VerticalAlignment = VerticalAlignment.Center;
                cb.HorizontalAlignment = HorizontalAlignment.Left;
                cb.Content = r;
                cb.Name = "cb_syn_" + r;
                cb.Click += new RoutedEventHandler(tb_fl2_Click);
                if (r == "Abathur")
                {
                    cb.IsChecked = true;
                }

                Grid.SetRow(cb, i);
                Grid.SetColumn(cb, 1);
                gr_syn_btn.Children.Add(cb);

                System.Windows.Controls.Image img_syn = new System.Windows.Controls.Image();
                BitmapImage bit_syn = new BitmapImage();
                bit_syn.BeginInit();
                bit_syn.UriSource = new Uri(myimg.GetImage(r), UriKind.Relative);
                bit_syn.EndInit();
                img_syn.Source = bit_syn;
                img_syn.VerticalAlignment = VerticalAlignment.Center;
                img_syn.HorizontalAlignment = HorizontalAlignment.Right;

                Grid.SetRow(img_syn, i);
                Grid.SetColumn(img_syn, 0);
                gr_syn_btn.Children.Add(img_syn);

                i++;
            }
        }

        public void GetSynergy()
        {
            List<string> synlist = new List<string>();
            foreach (var bab in gr_syn_btn.Children)
            {
                try
                {
                    CheckBox cb = (CheckBox)bab;
                    if (cb.IsChecked == true)
                    {
                        synlist.Add(cb.Content.ToString());
                    }
                }
                catch
                {


                }
            }

            DSfilter dsfil = new DSfilter(this);
            List<dsreplay> filtered_replays = new List<dsreplay>();
            filtered_replays = dsfil.Filter(this.replays);

            if (synlist.Count > 0)
            {
                dsradar myradar = new dsradar(this);
                string myhtml = myradar.GetHTML(synlist, filtered_replays);
                //Console.WriteLine(myhtml);
                wb_chart.NavigateToString(myhtml);
            }
            wb_chart.Visibility = Visibility.Visible;

        }

        public void GetBuilds()
        {
            dsunits myunits = new dsunits(this);


            if (cb_build_sum.IsChecked == true)
            {
                myunits.SumSum();
                return;
            }

            List<string> synlist = new List<string>();
            foreach (var cbcmdr in gr_syn_btn.Children)
            {
                try
                {
                    CheckBox cb = (CheckBox)cbcmdr;
                    if (cb.IsChecked == true)
                    {
                        synlist.Add(cb.Content.ToString());
                    }
                }
                catch
                {


                }
            }
            string gametime = "MIN10";
            if (rb_10min.IsChecked == true)
            {
                gametime = "MIN10";
            }
            else if (rb_fin.IsChecked == true)
            {
                gametime = "ALL";
            }
            else if (rb_5min.IsChecked == true)
            {
                gametime = "MIN5";
            }
            else if (rb_15min.IsChecked == true)
            {
                gametime = "MIN15";
            }

            if (synlist.Count > 0)
            {
                myunits.Sum(cb_vs.SelectedItem.ToString(), synlist.ElementAt(0), gametime);

            }
            else
            {
                myunits.Sum(cb_vs.SelectedItem.ToString(), null, gametime);
            }



        }

        /// read in csv
        /// 

        public List<dsreplay> LoadData(string json)
        {
            replays.Clear();

            string filePath = json;
            TextReader reader = null;
            dsreplay rep = null;
            int maxid = 0;
            try
            {
                reader = new StreamReader(filePath, Encoding.UTF8);
                string fileContents;
                while ((fileContents = reader.ReadLine()) != null)
                {
                    rep = JsonConvert.DeserializeObject<dsreplay>(fileContents);
                    if (rep != null)
                    {
                        rep.Init();
                        replays.Add(rep);
                        if (rep.ID > maxid) maxid = rep.ID;
                    }
                }
            }
            finally
            {
                if (reader != null)
                    reader.Close();
            }
            return replays;
        }

        public List<dsreplay> LoadData_deprecated(string csv)
        {
            //replays.Clear();
            List<dsreplay> replays_deprecated = new List<dsreplay>();
            string line;
            ///string pattern = @"^(\d+); ([^;]+); ([^;]+); ([^;]+); ([^;]+); ([^;]+); ([^;]+); ([^;]+); ([^;]+); ([^;]+); ([^;]+); ([^;]+); ([^;]+);";
            string[] myline = new string[12];

            string id = null;
            char[] cTrim = { ' ' };
            List<dscsv> single_replays = new List<dscsv>();

            bool doit = false;
            if (!File.Exists(csv))
            {
                MessageBox.Show("No data found :( - Have you tried File->Scan?", "sc2dsstats");
            }
            else
            {
                doit = true;
            }

            if (doit)
            {
                System.IO.StreamReader file_c = new System.IO.StreamReader(csv);
                int i = 0;
                while (file_c.ReadLine() != null) { i++; ; }
                int j = 0;
                file_c.Close();
                System.IO.StreamReader file = new System.IO.StreamReader(csv);

                while ((line = file.ReadLine()) != null)
                {
                    j++;
                    myline = line.Split(';');

                    for (int k = 0; k <= 12; k++)
                    {
                        string result = myline[k].Trim(cTrim);
                        myline[k] = result;
                    }

                    if (myline[2].Contains("\\"))
                    {
                        sc2hex myhex = new sc2hex();
                        myline[2] = myhex.UTF8Convert(myline[2]);
                    }

                    dscsv rep = new dscsv()
                    {
                        ID = int.Parse(myline[0]),
                        REPLAY = myline[1],
                        NAME = myline[2],
                        RACE = myline[4],
                        TEAM = int.Parse(myline[5]),
                        RESULT = int.Parse(myline[6]),
                        KILLSUM = int.Parse(myline[7]),
                        DURATION = int.Parse(myline[8]),
                        GAMETIME = double.Parse(myline[9]),
                        PLAYERID = int.Parse(myline[10]),
                        INCOME = double.Parse(myline[11], CultureInfo.InvariantCulture),
                        ARMY = int.Parse(myline[12])
                    };

                    if (id == null)
                    {
                        id = rep.REPLAY;
                    }

                    if (String.Equals(id, rep.REPLAY))
                    {
                        single_replays.Add(rep);
                    }
                    else
                    {
                        replays_deprecated = CollectData(single_replays, replays_deprecated);
                        id = rep.REPLAY;
                        single_replays.Clear();
                        single_replays.Add(rep);
                    }

                    if (j == i)
                    {
                        replays_deprecated = CollectData(single_replays, replays_deprecated);
                    }
                }

                file.Close();
            }

            return replays_deprecated;
        }

        private List<dsreplay> CollectData(List<dscsv> single_replays, List<dsreplay> replays_deprecated)
        {
            dsreplay game = new dsreplay();
            dsplayer player = new dsplayer();
            List<dsplayer> gameplayer = new List<dsplayer>();

            foreach (dscsv srep in single_replays)
            {
                if (game.ID == 0) game.ID = srep.ID;
                if (game.REPLAY == null) game.REPLAY = srep.REPLAY;
                if (game.GAMETIME == 0) game.GAMETIME = srep.GAMETIME;

                //if (String.Equals(srep.NAME, player_name))
                if (player_list.Contains(srep.NAME))
                {

                    game.GAMETIME = srep.GAMETIME;
                    player.POS = srep.PLAYERID;
                    player.RACE = srep.RACE;
                    player.NAME = srep.NAME;
                    player.KILLSUM = srep.KILLSUM;
                    player.PDURATION = srep.DURATION;
                    player.INCOME = srep.INCOME;
                    player.ARMY = srep.ARMY;
                    player.RESULT = 2;
                    //player.REPLAY = srep.REPLAY;
                    //player.ID = srep.ID;
                    player.TEAM = srep.TEAM;

                    game.DURATION = srep.DURATION;
                    int result = srep.RESULT;
                    //if (srep.PLAYERID <= 3)
                    //{
                    //   player.TEAM = 0;
                    if (srep.TEAM == 0)
                    {
                        if (srep.RESULT == 1)
                        {
                            player.RESULT = 1;
                            game.WINNER = 0;
                        }
                        else
                        {
                            game.WINNER = 1;
                        }
                    }
                    //else if (srep.PLAYERID > 3)
                    else if (srep.TEAM == 1)
                    {
                        //player.TEAM = 1;
                        if (srep.RESULT == 1)
                        {
                            player.RESULT = 1;
                            game.WINNER = 1;
                        }
                        else
                        {
                            game.WINNER = 0;
                        }
                    }
                }
                else
                {

                }


            }

            int minkillsum = -1;
            int maxkillsum = 0;
            int minarmy = -1;
            double minincome = -1;
            int maxleaver = 0;
            List<string> races = new List<string>();
            dsplayer MVP = new dsplayer();

            game.PLAYERCOUNT = single_replays.Count;
            foreach (dscsv srep in single_replays)
            {
                if (minkillsum == -1)
                {
                    minkillsum = srep.KILLSUM;
                }
                else
                {
                    if (srep.KILLSUM < minkillsum) minkillsum = srep.KILLSUM;

                }
                if (maxkillsum == 0)
                {
                    maxkillsum = srep.KILLSUM;
                }
                else
                {
                    if (srep.KILLSUM > maxkillsum) maxkillsum = srep.KILLSUM;
                }

                if (minincome == -1)
                {
                    minincome = srep.INCOME;
                }
                else
                {
                    if (srep.INCOME < minincome) minincome = srep.INCOME;

                }
                if (minarmy == -1)
                {
                    minarmy = srep.ARMY;
                }
                else
                {
                    if (srep.ARMY < minarmy) minarmy = srep.ARMY;

                }
                int leaver = game.DURATION - srep.DURATION;

                if (maxleaver == 0)
                {
                    maxleaver = leaver;
                }
                else
                {
                    if (leaver > maxleaver) maxleaver = leaver;

                }
                races.Add(srep.RACE);

                //if (String.Equals(srep.NAME, player_name))
                if (player_list.Contains(srep.NAME))
                {
                    gameplayer.Add(player);
                }
                else
                {
                    dsplayer mplayer = new dsplayer();
                    mplayer.POS = srep.PLAYERID;
                    mplayer.RACE = srep.RACE;
                    mplayer.NAME = srep.NAME;
                    mplayer.KILLSUM = srep.KILLSUM;
                    mplayer.PDURATION = srep.DURATION;
                    mplayer.INCOME = srep.INCOME;
                    mplayer.ARMY = srep.ARMY;
                    //mplayer.REPLAY = srep.REPLAY;
                    //mplayer.ID = srep.ID;
                    mplayer.RESULT = 2;
                    if (srep.PLAYERID <= game.PLAYERCOUNT / 2)
                    {
                        mplayer.TEAM = 0;
                        if (game.WINNER == 0)
                        {
                            mplayer.RESULT = 1;
                        }
                    }
                    else if (srep.PLAYERID > game.PLAYERCOUNT / 2)
                    {
                        mplayer.TEAM = 1;
                        if (game.WINNER == 1)
                        {
                            mplayer.RESULT = 1;
                        }
                    }

                    gameplayer.Add(mplayer);
                }
            }

            game.MAXLEAVER = maxleaver;
            game.MINARMY = minarmy;
            game.MININCOME = minincome;
            game.MINKILLSUM = minkillsum;
            game.MAXKILLSUM = maxkillsum;
            game.RACES = new List<string>(races);
            game.PLAYERS = new List<dsplayer>(gameplayer);
            replays_deprecated.Add(game);


            gameplayer.Clear();

            return replays_deprecated;
        }

        public string GetTempPNG()
        {
            string rng = myTemp_dir + Guid.NewGuid().ToString() + ".png";
            myTempfiles_col.Add(rng);
            return rng;
        }


        /// xaml
        /// 


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (cb_sample.IsArrangeValid == false)
            {
                replays.Clear();
                replays = LoadData(myStats_json);
            }
            UpdateGraph(null);

            if (gr_chart.Children.Contains(dynChart))
            {

            }
            else
            {
                gr_chart.Children.Add(dynChart);
            }

            if (scan_running == false)
            {
                if (gr_sb_grid.Children.Count > 0)
                {
                    try
                    {
                        Dispatcher.Invoke(() =>
                        {
                            foreach (UIElement ent in gr_sb_grid.Children)
                            {
                                gr_sb_grid.Children.Remove(ent);
                            }
                        });
                    } catch { }
                }
                lb_sb_info2.Content = "";
            }

        }

        private void mnu_Options(object sender, RoutedEventArgs e)
        {

            ///ClearImage();
            //Win_config win3 = new Win_config(this);
            //win3.Show();

            Win_configng winng = new Win_configng();
            winng.Show();

        }

        public void mnu_Scanpre(object sender, RoutedEventArgs e)
        {
            if (sender != null)
            {
                gr_chart.Visibility = Visibility.Collapsed;
                gr_syn.Visibility = Visibility.Collapsed;
                if (gr_filter1.Visibility == Visibility.Visible)
                {
                    //gr_doit.Margin = new Thickness(10,160,15,0);

                }
                if (gr_filter2.Visibility == Visibility.Visible)
                {
                    //gr_doit.Margin = new Thickness(10, 240, 15, 0);
                }
            }
            gr_images.Visibility = Visibility.Hidden;
            gr_doit.Visibility = Visibility.Visible;

            int total = 0;
            List<string> todo_replays = dsscan.Scan(this, out total);
            int todo = todo_replays.Count;
            lb_sb_info1.Content = "Scan (" + todo.ToString() + " new replays available. Total replays: " + total.ToString() + ")";

            doit_TextBox1.Document.Blocks.Clear();

            TextRange rangeOfText1 = new TextRange(doit_TextBox1.Document.ContentEnd, doit_TextBox1.Document.ContentEnd);
            rangeOfText1.Text = "We found ";
            rangeOfText1.ApplyPropertyValue(TextElement.ForegroundProperty, System.Windows.Media.Brushes.White);
            rangeOfText1.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
            rangeOfText1.ApplyPropertyValue(TextElement.FontFamilyProperty, new System.Windows.Media.FontFamily("/sc2dsstats_rc1;component/#Titillium Web"));

            TextRange rangeOfWord = new TextRange(doit_TextBox1.Document.ContentEnd, doit_TextBox1.Document.ContentEnd);
            rangeOfWord.Text = todo.ToString();
            rangeOfWord.ApplyPropertyValue(TextElement.ForegroundProperty, System.Windows.Media.Brushes.Red);
            rangeOfWord.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
            rangeOfWord.ApplyPropertyValue(TextElement.FontFamilyProperty, new System.Windows.Media.FontFamily("/sc2dsstats_rc1;component/#Titillium Web"));

            TextRange rangeOfText2 = new TextRange(doit_TextBox1.Document.ContentEnd, doit_TextBox1.Document.ContentEnd);
            rangeOfText2.Text = " new Replays (total:  " + total.ToString() + ")";
            rangeOfText2.ApplyPropertyValue(TextElement.ForegroundProperty, System.Windows.Media.Brushes.White);
            rangeOfText2.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
            rangeOfText2.ApplyPropertyValue(TextElement.FontFamilyProperty, new System.Windows.Media.FontFamily("/sc2dsstats_rc1;component/#Titillium Web"));

            doit_TextBox1.AppendText(Environment.NewLine);

            //Paragraph para1 = new Paragraph();
            //para1.Inlines.Add(new Run("We found " + scan.NEWREP + " new Replays (total: " + scan.TOTAL + ")"));
            //para1.Foreground = System.Windows.Media.Brushes.DarkRed;
            //doit_TextBox1.Document.Blocks.Add(para1);

            //doit_TextBox1.AppendText("We found " + scan.NEWREP + " new Replays (total: " + scan.TOTAL + ")" + Environment.NewLine);
            //doit_TextBox1.AppendText(Environment.NewLine);
            //doit_TextBox1.AppendText(Environment.NewLine);

            double time = todo * 7.2;
            time /= 60;
            time /= 60;
            string st_time = string.Format("{0:0.00}", time);
            doit_TextBox1.AppendText("Expected time needed: " + st_time + " h" + Environment.NewLine);
            doit_TextBox1.AppendText("(can be decresed by setting more CPUs at the cost of the computers workload)" + Environment.NewLine);
            //doit_TextBox1.AppendText(Environment.NewLine);

            /**
            if (String.Equals(appSettings["KEEP"], "1"))
            {
                doit_TextBox1.AppendText("Expected disk space needed: " + "0" + " GB" + Environment.NewLine);
                doit_TextBox1.AppendText("(Your current free disk space is " + "0" + " GB)" + Environment.NewLine);


                if (double.Parse("0") > double.Parse("0"))
                {
                    doit_TextBox1.AppendText("WARNING: There might be not enough Diskspace available!!!" + Environment.NewLine);
                }
            }
    **/
            //doit_TextBox1.AppendText(Environment.NewLine);

            doit_TextBox1.AppendText("You can always quit the process, next time it will continue at the last position." + Environment.NewLine);
            //doit_TextBox1.AppendText(Environment.NewLine);
            doit_TextBox1.AppendText("You can reach this info at 'File->Scan preview' at any time.");

            if (scan_running)
            {
                Paragraph para = new Paragraph();
                para.Inlines.Add(new Run("Decoding replays. Please wait."));
                para.Foreground = System.Windows.Media.Brushes.DarkRed;
                para.FontFamily = new System.Windows.Media.FontFamily("/sc2dsstats_rc1;component/#Titillium Web");
                doit_TextBox1.Document.Blocks.Add(para);

                //doit_TextBox1.AppendText("Decoding replays. Please wait." + Environment.NewLine);

            }
            else if (sender == null)
            {
                Paragraph para = new Paragraph();
                para.Inlines.Add(new Run("Decoding finished. Have fun."));
                para.Foreground = System.Windows.Media.Brushes.DarkRed;
                para.FontFamily = new System.Windows.Media.FontFamily("/sc2dsstats_rc1;component/#Titillium Web");
                doit_TextBox1.Document.Blocks.Add(para);
            }

            //gr_doit.Visibility = Visibility.Hidden;
        }

        public void mnu_Scan(object sender, RoutedEventArgs e)
        {
            if (scan_running == false)
            {
                scan_running = true;
                int cores = Environment.ProcessorCount / 2;
                if (Properties.Settings.Default.CORES > 0) cores = Properties.Settings.Default.CORES;

                if (gr_doit.Visibility == Visibility.Visible)
                {
                    cores = int.Parse(cb_doit_cpus.SelectedItem.ToString());
                }
                dsdecode dsdec = new dsdecode(cores, this);
                dsdec.Scan();
                mnu_Scanpre(null, null);
                lb_sb_info1.Content = "Scanning with " + cores.ToString() + " threads.";
            } else
            {
                MessageBox.Show("Scan already running. Please wait. (You can do 'File->Reload data' to see the processed data)", "sc2dsstats");
            }
        }

        public void mnu_Scan_deprecated(object sender, RoutedEventArgs e)
        {
            var appSettings = ConfigurationManager.AppSettings;
            int cores = 2;
            if (appSettings["CORES"] != null && appSettings["CORES"] != "0")
            {
                cores = int.Parse(appSettings["CORES"]);
            }

            if (gr_doit.Visibility == Visibility.Visible)
            {
                cores = int.Parse(cb_doit_cpus.SelectedItem.ToString());
            }

            if (scan_running == false)
            {
                scan_running = true;
                mnu_Scanpre(null, null);
                string ExecutableFilePath = myScan_exe;
                string Arguments = @"--priority=" + "NORMAL" + " "
                                    + "--cores=" + cores.ToString() + " "
                                    + "--player=\"" + Properties.Settings.Default.PLAYER + "\" "
                                    + "--stats_file=\"" + myStats_csv + "\" "
                                    + "--replay_path=\"" + Properties.Settings.Default.REPLAY_PATH + "\" "
                                    + "--DEBUG=" + appSettings["DEBUG"] + " "
                                    + "--keep=" + appSettings["KEEP"] + " "
                                    + "--store_path=\"" + appSettings["STORE_PATH"] + "\" "
                                    + "--skip_file=\"" + appSettings["SKIP_FILE"] + "\" "
                                    + "--log_file=\"" + myScan_log + "\" "
                                    + "--s2_cli=\"" + myS2cli_exe + "\" "
                                    + "--num_file=\"" + myAppData_dir + "\\num.txt" + "\" "
                                   //+ "--ladder=\"" + Properties.Settings.Default.MM_CREDENTIAL + "\" "
                                   ;
                //MessageBox.Show(Arguments);

                if (appSettings["UNITS"] == "1") Arguments += "--units_file=\"" + appSettings["UNITS_FILE"] + "\" ";

                tsscan = Task.Factory.StartNew(() =>
                {
                    Process doit = new Process();

                    if (File.Exists(ExecutableFilePath))
                    {
                        doit.StartInfo.FileName = ExecutableFilePath;
                        doit.StartInfo.Arguments = Arguments;


                        doit.StartInfo.UseShellExecute = false;
                        doit.StartInfo.RedirectStandardOutput = false;
                        doit.StartInfo.RedirectStandardError = false;

                        //StringBuilder output = new StringBuilder();
                        //StringBuilder error = new StringBuilder();

                        //using (AutoResetEvent outputWaitHandle = new AutoResetEvent(false))
                        //using (AutoResetEvent errorWaitHandle = new AutoResetEvent(false))
                        //{
                        //}




                        //doit = System.Diagnostics.Process.Start(ExecutableFilePath, Arguments);
                        doit.Start();
                        doit.WaitForExit();
                        scan_running = false;


                        replays.Clear();
                        replays = LoadData(myStats_json);

                        //UpdateGraph(null);


                    }

                    //MessageBox.Show("Scanning complete.", "sc2dsstats");

                    Dispatcher.Invoke(() =>
                    {
                        if (File.Exists(myScan_log))
                        {
                            string log = "";
                            StreamReader reader = new StreamReader(myScan_log, Encoding.UTF8, true);
                            log = "Log:" + Environment.NewLine;
                            byte[] bytes = Encoding.UTF8.GetBytes(reader.ReadToEnd());

                            log += Encoding.Default.GetString(bytes);
                            reader.Close();
                            lb_info.Text = log;

                        }
                        List<Block> myList = doit_TextBox1.Document.Blocks.ToList();
                        if (myList.Count > 0)
                        {
                            myList.RemoveAt(myList.Count - 1);
                        }
                        mnu_Scanpre(null, null);
                        scan_running = false;
                    });
                }, TaskCreationOptions.AttachedToParent);
            }
            else
            {
                MessageBox.Show("Scan already running. Please wait. (You can do 'File->Reload data' to see the processed data)", "sc2dsstats");
            }
        }

        private void mnu_LoadData_Click(object sender, RoutedEventArgs e)
        {
            replays.Clear();
            if (File.Exists(myStats_json))
            {
                replays = LoadData(myStats_json);
                UpdateGraph(null);
            }
            else
            {
                MessageBox.Show("No data found :(", "sc2dsstats");
            }
        }

        private void mnu_log_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(myScan_log))
            {
                string log = "";
                try
                {
                    StreamReader reader = new StreamReader(myScan_log, Encoding.UTF8, true);

                    log = "Log:" + Environment.NewLine;
                    byte[] bytes = Encoding.UTF8.GetBytes(reader.ReadToEnd());

                    log += Encoding.Default.GetString(bytes);
                    reader.Close();
                }
                catch { }

                Win_log lw = new Win_log();
                lw.win_Log_Textbox_Log.Text = log;
                lw.Show();
            }
        }

        private void mnu_Log_scan(object sender, RoutedEventArgs e)
        { }

        private void mnu_Exit(object sender, RoutedEventArgs e)
        {
            main_Closing(null, null);
        }

        private void mnu_doc(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(myDoc_pdf);
        }

        private void mnu_info(object sender, RoutedEventArgs e)
        {
            Win_log wlog = new Win_log();
            wlog.Title = "Info";
            wlog.win_Log_Textbox_Log.Visibility = Visibility.Collapsed;
            wlog.rtb_info.Visibility = Visibility.Visible;
            wlog.rtb_info.AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(wlog.Hyperlink_RequestNavigate));


            wlog.Show();
        }
        private void mnu_doupdate(object sender, RoutedEventArgs e)
        {
            DoUpdate();
        }

        private void mnu_update(object sender, RoutedEventArgs e)
        {
            bool available = false;
            lb_sb_info1.Content = "Checking for new update ...";
            Task tCheck = Task.Factory.StartNew(() =>
            {
                available = isVersionAvailable();
                if (available == false)
                {
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("No update available.", "sc2dsstats");
                    });
                }
                else
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (MessageBox.Show("New update available. Do you want to update?", "sc2dsstats", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.No)
                        {
                        }
                        else
                        {
                            //do yes stuff
                            DoUpdate();
                        }
                    });
                }
            });
        }

        private void DoUpdate()
        {
            Task tUpdate = Task.Factory.StartNew(() =>
            {
                isVersionOK();
            });
        }

        private void mnu_Database(object sender, RoutedEventArgs e)
        {

            ///ClearImage();
            Win_regex win5 = new Win_regex();
            win5.Show();

        }

        public void mnu_export(object sender, RoutedEventArgs e)
        {

            List<string> ano_stats = new List<string>(dsupload.GenExport(myStats_csv, this));

            // Create OpenFileDialog 
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();

            string filename = "unknown";
            // Set filter for file extension and default file extension 
            dlg.DefaultExt = ".csv";

            // Display OpenFileDialog by calling ShowDialog method 
            Nullable<bool> result = dlg.ShowDialog();

            // Get the selected file name and display in a TextBox 
            if (result == true)
            {
                // Open document 
                filename = dlg.FileName;
            }

            // Write the string array to a new file named "WriteLines.txt".
            using (StreamWriter outputFile = new StreamWriter(filename))
            {
                foreach (string line in ano_stats)
                    outputFile.WriteLine(line);
            }
        }

        public void mnu_upload(object sender, RoutedEventArgs e)
        {
            string exp_csv = myAppData_dir + "\\export.csv";

            string hash = "";


            string credential = "";
            if (sender == null)
            {
                credential = "To improve this application it would be very nice if the statistics were uploaded from time to time." + Environment.NewLine + Environment.NewLine;
            }
            credential += "All player names (including yours) will be anonymized before sending. By clicking \"Yes\" you agree that your DS-replay data will be used at https://www.pax77.org to generate global charts." + Environment.NewLine + Environment.NewLine;


            if (MessageBox.Show(credential + "Upload anonymized data to https://www.pax77.org?", "sc2dsstats", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.No)
            {
                //do no stuff
            }
            else
            {
                //do yes stuff

                using (SHA256 sha256Hash = SHA256.Create())
                {
                    hash = GetHash(sha256Hash, player_name);

                }

                List<string> ano_stats = new List<string>(dsupload.GenExport(myStats_json, this));
                using (StreamWriter outputFile = new StreamWriter(exp_csv))
                {
                    foreach (string line in ano_stats)
                        outputFile.WriteLine(line);
                }

                dsclient.StartClient(hash, exp_csv);
                //if (File.Exists(myUnits_csv))
                //{
                //    dsclient.StartClient(hash + "_units", myUnits_csv);
                //}
            }
            Properties.Settings.Default.UPLOAD = DateTime.Now;
            Properties.Settings.Default.Save();
        }

        public void mnu_mm(object sender, RoutedEventArgs e)
        {
            Win_mm Wmm = new Win_mm(this);
            Wmm.Show();
        }

        public void mnu_ladder(object sender, RoutedEventArgs e)
        {
            string credential = "This will open a Website with a DS-ladder" + Environment.NewLine + Environment.NewLine;

            if (MessageBox.Show(credential + "Do you want to open the external link https://www.pax77.org/sc2dsmm ?", "sc2dsmm", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.No)
            {
                //do no stuff
            }
            else
            {
                //do yes stuff
                string targetURL = @"https://www.pax77.org/sc2dsmm";
                System.Diagnostics.Process.Start(targetURL);
            }
        }

        public void mnu_manual(object sender, RoutedEventArgs e)
        {
            Win_ladder wld = new Win_ladder(this);
            wld.Show();
        }

        public void mnu_delete(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to delete all external mm-data for all your users?", "sc2dsmm", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.No)
            {
                //do no stuff
            }
            else
            {
                //do yes stuff
                Win_mm Wmm = new Win_mm(this);
                Wmm.Delete();
            }
        }


        public void btn_show_world_Click(object sender, RoutedEventArgs e)
        {

            string credential = "This will open a Website with combined charts of over 3k games (You can upload your data too with the menu Export->Upload to make it even more meaningful)" + Environment.NewLine + Environment.NewLine;

            if (MessageBox.Show(credential + "Do you want to open the external link https://www.pax77.org/sc2dsstats?", "sc2dsstats", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.No)
            {
                //do no stuff
            }
            else
            {
                //do yes stuff
                string targetURL = @"https://www.pax77.org/sc2dsstats";
                System.Diagnostics.Process.Start(targetURL);
            }
        }

        public static string GetHash(HashAlgorithm hashAlgorithm, string input)
        {

            // Convert the input string to a byte array and compute the hash.
            byte[] data = hashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(input));

            // Create a new Stringbuilder to collect the bytes
            // and create a string.
            var sBuilder = new StringBuilder();

            // Loop through each byte of the hashed data 
            // and format each one as a hexadecimal string.
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            // Return the hexadecimal string.
            return sBuilder.ToString();
        }


        private void doit_Button_Click(object sender, RoutedEventArgs e)
        {
            mnu_Scan(null, null);
        }

        private void cb_all_Click(object sender, RoutedEventArgs e)
        {

            if (cb_all.IsChecked == true)
            {
                gr_date.IsEnabled = false;
                Properties.Settings.Default.GUI_START_DATE = new DateTime(2018, 01, 01);
                UpdateGraph(sender);
            }
            else if (cb_all.IsChecked == false)
            {
                gr_date.IsEnabled = true;
            }

        }

        private void cb_std_Click(object sender, RoutedEventArgs e)
        {
            UpdateGraph(sender);
        }

        private void cb_player_Click(object sender, RoutedEventArgs e)
        {
            UpdateGraph(sender);
        }

        private void bt_filter2_Click(object sender, RoutedEventArgs e)
        {
            if (gr_filter2.Visibility == Visibility.Collapsed)
            {
                gr_filter2.Visibility = Visibility.Visible;
                /**
                gr_chart.Margin = new Thickness(0, 140, 0, 0);
                gr_doit.Margin = new Thickness(10, 140, 15, 0);
                gr_syn.Margin = new Thickness(0, 140, 0, 0);
    **/
            }
            else if (gr_filter2.Visibility == Visibility.Visible)
            {
                if (gr_info.Visibility == Visibility.Visible)
                {
                    bt_filter3_Click(null, null);
                }
                
                gr_filter2.Visibility = Visibility.Collapsed;
                /**
                gr_chart.Margin = new Thickness(0, 80, 0, 0);
                gr_doit.Margin = new Thickness(10, 80, 15, 0);
                gr_syn.Margin = new Thickness(0, 80, 0, 0);
    **/
            }
        }

        private void bt_filter3_Click(object sender, RoutedEventArgs e)
        {
            if (gr_info.Visibility == Visibility.Collapsed)
            {
                gr_info.Visibility = Visibility.Visible;
                /**
                gr_chart.Margin = new Thickness(0, 240, 0, 0);
                gr_doit.Margin = new Thickness(10, 240, 15, 0);
                gr_syn.Margin = new Thickness(0, 240, 0, 0);
    **/
            }
            else if (gr_info.Visibility == Visibility.Visible)
            {
                gr_info.Visibility = Visibility.Collapsed;
                /**
                gr_chart.Margin = new Thickness(0, 140, 0, 0);
                gr_doit.Margin = new Thickness(10, 140, 15, 0);
                gr_syn.Margin = new Thickness(0, 140, 0, 0);
    **/
            }
        }

        private void cb_otf_Click(object sender, RoutedEventArgs e)
        {
            if (OTF == null)
            {
                OTF = new dsotfng(this);
            }

            if (cb_otf.IsChecked == true)
            {
                OTF.Start();
            }
            else
            {
                OTF.Stop();
            }
        }

        public class ChartItem
        {
            public string Title { get; set; } // coil456

            public double Value { get; set; } // 334

            public string TooltipLabel
            {
                get { return string.Format("{0}({1})", this.Title, this.Value); } // coil456(334)
            }
        }



        private void tb_fl2_EnterClick(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                UpdateGraph(sender);
            }
        }

        private void chb_vs_Click(object sender, RoutedEventArgs e)
        {
            if (chb_vs.IsChecked == true)
            {
                if (chb_cmdr_vs.IsChecked == true) chb_cmdr_vs.IsChecked = false;
                cb_vs.Visibility = Visibility.Visible;
            }
            else if (chb_vs.IsChecked == false)
            {
                cb_vs.Visibility = Visibility.Hidden;
            }
            UpdateGraph(sender);
        }

        private void chb_cmdr_vs_Click(object sender, RoutedEventArgs e)
        {
            if (chb_cmdr_vs.IsChecked == true)
            {
                if (chb_vs.IsChecked == true) chb_vs.IsChecked = false;
                cb_vs.Visibility = Visibility.Visible;
            }
            else if (chb_cmdr_vs.IsChecked == false)
            {
                cb_vs.Visibility = Visibility.Hidden;
            }
            UpdateGraph(sender);
        }

        private void add_Click(object sender, RoutedEventArgs e)
        {
            if (cb_add.IsChecked == true)
            {
                bt_show_textblock1.Text = "Add";
                bt_show_textblock2.Text = "Add";
                //bt_show.Content = "Add";
            }
            else if (cb_add.IsChecked == false)
            {
                bt_show_textblock1.Text = "Show";
                bt_show_textblock2.Text = "Show";

                //bt_show.Content = "Show";
            }
        }

        private void tb_fl2_Click(object sender, RoutedEventArgs e)
        {
            if (INIT == false) return;

            if (sender is CheckBox)
            {
                CheckBox cb = sender as CheckBox;
                if (cb != null)
                {
                    if (cb.Name == "cb_duration")
                    {
                        if (cb.IsChecked == true)
                        {
                            tb_duration.IsEnabled = true;
                            try { Properties.Settings.Default.GUI_DURATION = int.Parse(tb_duration.Text); }
                            catch { }
                        }
                        else if (cb.IsChecked == false)
                        {
                            tb_duration.IsEnabled = false;
                            Properties.Settings.Default.GUI_DURATION = 0;
                        }
                    }
                    else if (cb.Name == "cb_leaver")
                    {
                        if (cb.IsChecked == true)
                        {
                            tb_leaver.IsEnabled = true;
                            try { Properties.Settings.Default.GUI_LEAVER = int.Parse(tb_leaver.Text); }
                            catch { }

                        }
                        else if (cb.IsChecked == false)
                        {
                            tb_leaver.IsEnabled = false;
                            Properties.Settings.Default.GUI_LEAVER = 0;
                        }
                    }
                    else if (cb.Name == "cb_killsum")
                    {
                        if (cb.IsChecked == true)
                        {
                            tb_killsum.IsEnabled = true;
                            try { Properties.Settings.Default.GUI_KILLSUM = int.Parse(tb_killsum.Text); }
                            catch { }

                        }
                        else if (cb.IsChecked == false)
                        {
                            tb_killsum.IsEnabled = false;
                            Properties.Settings.Default.GUI_KILLSUM = 0;
                        }
                    }
                    else if (cb.Name == "cb_income")
                    {
                        if (cb.IsChecked == true)
                        {
                            tb_income.IsEnabled = true;
                            try { Properties.Settings.Default.GUI_INCOME = int.Parse(tb_income.Text); }
                            catch { }

                        }
                        else if (cb.IsChecked == false)
                        {
                            tb_income.IsEnabled = false;
                            Properties.Settings.Default.GUI_INCOME = 0;
                        }
                    }
                    else if (cb.Name == "cb_army")
                    {
                        if (cb.IsChecked == true)
                        {
                            tb_army.IsEnabled = true;
                            try { Properties.Settings.Default.GUI_ARMY = int.Parse(tb_army.Text); }
                            catch { }

                        }
                        else if (cb.IsChecked == false)
                        {
                            tb_army.IsEnabled = false;
                            Properties.Settings.Default.GUI_ARMY = 0;
                        }
                    }
                }
            }


            UpdateGraph(sender);
        }

        private void tb_fl2_date_EnterClick(object sender, SelectionChangedEventArgs e)
        {
            if (INIT == true)
            {
                tb_fl2_Click(sender, null);
                Properties.Settings.Default.GUI_START_DATE = otf_startdate.SelectedDate.Value;
            }
        }

        private void ib_OkButton_Click(object sender, RoutedEventArgs e)
        {
            // YesButton Clicked! Let's hide our InputBox and handle the input text.


            // Do something with the Input
            String input = fr_InputTextBox.Text;
            player_name = input;
            SetPlayerList(input);
            Properties.Settings.Default.PLAYER = input;

            // Clear InputBox.
            fr_InputTextBox.Text = String.Empty;

            string filename = fr_InputTextBox2.Text;

            myReplay_Path = filename;
            SetReplayList(filename);
            Properties.Settings.Default.REPLAY_PATH = filename;

            Properties.Settings.Default.Save();

            MessageBox.Show("Now we are good to go - have fun :) (There are more options available at File->Options)", "sc2dsstats");

            gr_filter1.Visibility = System.Windows.Visibility.Visible;
            gr_mode.Visibility = Visibility.Visible;
            bt_show.IsEnabled = true;
            cb_sample.IsEnabled = true;
            bt_filter2.IsEnabled = true;
            dp_menu.IsEnabled = true;

            gr_firstrun.Visibility = Visibility.Collapsed;
            mnu_Scanpre(null, null);

        }

        private void sample_Click(object sender, RoutedEventArgs e)
        {
            replays.Clear();
            Items.Clear();
            if (cb_sample.IsChecked == true)
            {
                //player_name = "player";
                player_list.Add("player");
                if (File.Exists(mySample_json)) replays = LoadData(mySample_json);
                GetWinrate();
                UpdateGraph(null);
            }
            else if (cb_sample.IsChecked == false)
            {
                player_list.Remove("player");
                if (File.Exists(myStats_csv)) replays = LoadData(myStats_json);
                GetWinrate();
                UpdateGraph(null);
            }

        }

        private void bt_chart_Click(object sender, RoutedEventArgs e)
        {
            Win_chart chartwin = new Win_chart(this);
            chartwin.Show();
        }

        private void ib_BrowseButton_Click(object sender, RoutedEventArgs e)
        {

            // Create OpenFileDialog 
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

            /// MessageBox.Show("Thank you. Now we need to know where the SC2Replays are - please select one Replay in your folder. Usually it is something like C:\\Users\\<username>\\Documents\\StarCraft II\\Accounts\\107095918\\2-S2-1-226401\\Replays\\Multiplayer");
            string filename = "";
            // Set filter for file extension and default file extension 
            dlg.DefaultExt = ".SC2Replay";

            // Display OpenFileDialog by calling ShowDialog method 
            Nullable<bool> result = dlg.ShowDialog();



            // Get the selected file name and display in a TextBox 
            if (result == true)
            {
                // Open document 
                filename = dlg.FileName;
                filename = Path.GetDirectoryName(filename);
            }

            fr_InputTextBox2.Text += filename + ";";

        }

        private void dyn_image_Move(object sender, MouseEventArgs e)
        {

            if (dynChart != null && e.LeftButton == MouseButtonState.Pressed)
            {

                try
                {
                    BitmapImage dropBitmap = new BitmapImage();
                    System.Windows.Controls.Image myImage = new System.Windows.Controls.Image();

                    RenderTargetBitmap renderBitmap = new RenderTargetBitmap(
                        (int)gr_chart.ActualWidth,
                        (int)gr_chart.ActualHeight,
                        96d,
                        96d,
                        PixelFormats.Pbgra32
                        );

                    double cwidth = gr_chart.ActualWidth;
                    double cheight = gr_chart.ActualHeight;

                    gr_chart.Measure(new System.Windows.Size(cwidth, cheight));

                    gr_chart.Arrange(new Rect(new System.Windows.Size(cwidth, cheight)));

                    gr_chart.UpdateLayout();

                    renderBitmap.Render(gr_chart);

                    var png = new PngBitmapEncoder();

                    png.Frames.Add(BitmapFrame.Create(renderBitmap));

                    // Save the bitmap into a file.

                    string drop = GetTempPNG();


                    using (FileStream stream =
                        new FileStream(drop, FileMode.Create))
                    {
                        PngBitmapEncoder encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(renderBitmap));
                        encoder.Save(stream);
                    }

                    Dispatcher.Invoke(() =>
                    {
                        dropBitmap.BeginInit();
                        dropBitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                        dropBitmap.CacheOption = BitmapCacheOption.OnLoad;
                        dropBitmap.UriSource = new Uri(drop);
                        dropBitmap.EndInit();

                        myImage.Source = dropBitmap;
                        myImage.AllowDrop = true;
                    });

                    string[] files = new string[1];
                    BitmapImage[] dBitmaps = new BitmapImage[1];
                    files[0] = drop;
                    dBitmaps[0] = dropBitmap;



                    DataObject dropObj = new DataObject(DataFormats.FileDrop, files);
                    ///dropObj.SetData(DataFormats.Text, files[0]);
                    dropObj.SetData(DataFormats.Bitmap, dBitmaps[0]);

                    ///DragDrop.DoDragDrop(myImage, dps_png, DragDropEffects.Copy);
                    DragDrop.DoDragDrop(gr_chart, dropObj, DragDropEffects.Copy);
                }
                catch { }

            }
        }

        public BitmapImage GraphToBitmap()
        {
            BitmapImage crBitmap = new BitmapImage();
            BitmapImage dropBitmap = new BitmapImage();

            RenderTargetBitmap renderBitmap = new RenderTargetBitmap(
             (int)gr_chart.ActualWidth,
             (int)gr_chart.ActualHeight,
             96d,
             96d,
             PixelFormats.Pbgra32
             );

            double cwidth = gr_chart.ActualWidth;
            double cheight = gr_chart.ActualHeight;

            gr_chart.Measure(new System.Windows.Size(cwidth, cheight));
            gr_chart.Arrange(new Rect(new System.Windows.Size(cwidth, cheight)));
            gr_chart.UpdateLayout();
            renderBitmap.Render(gr_chart);

            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(renderBitmap));

            // Save the bitmap into a file.

            string drop = GetTempPNG();
            using (FileStream stream =
                new FileStream(drop, FileMode.Create))
            {
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderBitmap));
                encoder.Save(stream);
            }

            dropBitmap.BeginInit();
            dropBitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            dropBitmap.CacheOption = BitmapCacheOption.OnLoad;
            dropBitmap.UriSource = new Uri(drop);
            dropBitmap.EndInit();

            if (File.Exists(drop))
            {
                //Graphics g = Graphics.FromImage(dropBitmap);

                int crop = 80;
                if (gr_filter2.Visibility == Visibility.Visible)
                {
                    crop = 140;
                }
                if (gr_info.Visibility == Visibility.Visible)
                {
                    crop = 240;
                }

                Bitmap bitmap = new Bitmap(drop);
                Rectangle rect = new Rectangle(0, crop, (int)gr_chart.ActualWidth, (int)gr_chart.ActualHeight);
                Bitmap cropped = bitmap;
                if (crop < (int)gr_chart.ActualHeight)
                {
                    try
                    {
                        cropped = bitmap.Clone(rect, bitmap.PixelFormat);
                    }
                    catch (OutOfMemoryException)
                    {

                    }
                }

                string crdrop = GetTempPNG();
                cropped.Save(crdrop);

                crBitmap.BeginInit();
                crBitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                crBitmap.CacheOption = BitmapCacheOption.OnLoad;
                crBitmap.UriSource = new Uri(crdrop);
                crBitmap.EndInit();

                bitmap.Dispose();
                cropped.Dispose();
                dropBitmap = null;
            }


            return crBitmap;

        }


        public void dyn_Chart_Click(object sender, MouseEventArgs e)
        {
            /// MessageBox.Show("Und es war SOmmer");

            System.Windows.Controls.Image objImage = sender as System.Windows.Controls.Image;
            BitmapImage crBitmap = new BitmapImage();

            if (e is MouseEventArgs)
            {

                if (e.RightButton == MouseButtonState.Released)
                {
                    Win_pupup win1 = new Win_pupup();
                    System.Windows.Controls.Image myImage = new System.Windows.Controls.Image();

                    crBitmap = GraphToBitmap();

                    win1.Height = crBitmap.Height;
                    win1.Width = crBitmap.Width;
                    // Set Image.Source  
                    win1.win_dps_img1.Source = crBitmap;
                    win1.win_dps_grid.Visibility = Visibility.Visible;


                    win1.win_dps_grid.Visibility = Visibility.Visible;
                    win1.Show();


                    this.Height = this.Height - 1;
                    this.Height = this.Height + 1;



                }

                else if (e.LeftButton == MouseButtonState.Pressed)
                {

                }
            }
        }


        private void win_SaveAs_Click(object sender, RoutedEventArgs e)
        {

            BitmapImage crBitmap = new BitmapImage();
            crBitmap = GraphToBitmap();

            SaveFileDialog saveFileDialog1 = new SaveFileDialog();
            saveFileDialog1.Filter = "PNG Image|*.png";
            saveFileDialog1.Title = "Save PNG Image File";
            saveFileDialog1.ShowDialog();

            if (saveFileDialog1.FileName != "")
            {
                // Save the bitmap into a file.
                using (FileStream stream =
                    new FileStream(saveFileDialog1.FileName, FileMode.Create))
                {
                    PngBitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(crBitmap));
                    encoder.Save(stream);
                }
            }
        }

        private Boolean isVersionAvailable()
        {
            UpdateCheckInfo info = null;

            if (ApplicationDeployment.IsNetworkDeployed)
            {
                ApplicationDeployment ad = ApplicationDeployment.CurrentDeployment;

                try
                {
                    info = ad.CheckForDetailedUpdate();
                }
                catch (DeploymentDownloadException)
                {
                    // No network connection
                    Dispatcher.Invoke(() =>
                    {
                        lb_sb_info1.Content = "No network connection.";
                    });
                    return false;
                }
                catch (InvalidDeploymentException)
                {
                    return false;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }

                if (info.UpdateAvailable)
                {
                    return true;
                }
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    lb_sb_info1.Content = "No network version.";
                });
                return false;
            }
            return false;
        }

        private Boolean isVersionOK()
        {
            UpdateCheckInfo info = null;

            if (ApplicationDeployment.IsNetworkDeployed)
            {
                ApplicationDeployment ad = ApplicationDeployment.CurrentDeployment;

                try
                {
                    info = ad.CheckForDetailedUpdate();
                }
                catch (DeploymentDownloadException)
                {
                    // No network connection
                    Dispatcher.Invoke(() =>
                    {
                        lb_sb_info1.Content = "No network connection.";
                    });
                    return false;
                }
                catch (InvalidDeploymentException)
                {
                    return false;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }

                if (info.UpdateAvailable)
                {
                    Dispatcher.Invoke(() =>
                    {
                        lb_sb_info1.Content = "New version available. Downloading ...";
                        lb_sb_info2.Content = "The applications will restart after the download.";
                    });
                    try
                    {
                        ad.Update();

                        //Application.Restart();
                        System.Windows.Forms.Application.Restart();
                        Environment.Exit(0);
                    }
                    catch (DeploymentDownloadException)
                    {
                        // No network connection
                    }

                    return false;
                } else
                {
                    Dispatcher.Invoke(() =>
                    {
                        lb_sb_info1.Content = "No new version available.";
                    });

                }
                return true;
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    lb_sb_info1.Content = "No network version.";
                });
                return false;
            }
        }

        public void main_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Properties.Settings.Default.Save();
            foreach (string img in myTempfiles_col)
            {
                if (File.Exists(img))
                {
                    try
                    {
                        File.Delete(img);
                    }
                    catch (System.IO.IOException)
                    {

                    }
                }
            }
            Application.Current.Shutdown();
        }

        private void Chb_cmdrvs_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            //UNITS.GenJson();
        }

        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            Win_configng cfg = new Win_configng();
            cfg.Show();
        }


    }


}


