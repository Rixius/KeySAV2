using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Security.Cryptography;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Timers;
using CheckComboBox;

namespace KeySAV3
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            // Set up our automatic file loading
            FileSystemWatcher fsw = new FileSystemWatcher();
            fsw.SynchronizingObject = this; // Timer Threading Related fix to cross-access control.
            myTimer.Elapsed += new ElapsedEventHandler(DisplayTimeEvent);
            myTimer.Interval = 400; // milliseconds per trigger interval (0.4s)
            myTimer.Start();

            // Handle drag-n-drop files
            this.tab_Main.AllowDrop = true;
            this.DragEnter += new DragEventHandler(tabMain_DragEnter);
            this.DragDrop += new DragEventHandler(tabMain_DragDrop);
            tab_Main.DragEnter += new DragEventHandler(tabMain_DragEnter);
            tab_Main.DragDrop += new DragEventHandler(tabMain_DragDrop);

            // Initialize some UI stuff
            CB_Game.SelectedIndex = 0;
            CB_MainLanguage.SelectedIndex = 0;
            CB_BoxStart.SelectedIndex = 0;
            changeboxsetting(null, null);
            CB_BoxEnd.SelectedIndex = 0;
            CB_BoxEnd.Enabled = false;
            CB_Team.SelectedIndex = 0;
            CB_ExportStyle.SelectedIndex = 0;
            CB_BoxColor.SelectedIndex = 0;
            CB_No_IVs.SelectedIndex = 0;
            toggleFilter(null, null);
            updatePreview();

            // Load configuration, initialize strings
            loadINI();
            this.FormClosing += onFormClose;
            InitializeStrings();
            
            // Create some data arrays for our getLevel function
            // This data doesn't change, ever
            object txt = Properties.Resources.ResourceManager.GetObject("text_expTable_all");
            List<string> rawList = ((string)txt).Split(new char[] { '\n' }).ToList();
            expTable = new int[rawList.Count][];
            for (int i = 0; i < rawList.Count; i++)
                expTable[i] = Array.ConvertAll(Regex.Split(rawList[i].Trim(), ","), int.Parse);
        }

        #region Global Variables

        // Finding the 3DS SD Files
        private System.Timers.Timer myTimer = new System.Timers.Timer();
        private static string path_exe = System.Windows.Forms.Application.StartupPath;
        private static string datapath = path_exe + Path.DirectorySeparatorChar + "data";
        private static string dbpath = path_exe + Path.DirectorySeparatorChar + "db";
        private static string bakpath = path_exe + Path.DirectorySeparatorChar + "backup";
        private string path_3DS = "";
        private string lastOpenedFilename = "";

        // Static data
        private static string[] expGrowth;
        private static int[][] expTable;

        // Language
        private string[] natures;
        private string[] types;
        private string[] abilitylist;
        private string[] movelist;
        private string[] itemlist;
        private string[] specieslist;
        private string[] balls;
        private string[] formlist;
        private string[] countryList;
        private string[] regionList;
        private string[] gameList;
        private string[] vivlist;
        private string[] unownlist = { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z", "!", "?" };
        // Blank File Egg Names
        private string[] eggnames = { "タマゴ", "Egg", "Œuf", "Uovo", "Ei", "", "Huevo", "알" };
        private string[] languageList = { "???", "JPN", "ENG", "FRE", "ITA", "GER", "???", "ESP", "KOR" };

        // Inputs
        private byte[] savefile = new Byte[0x10009C];
        private byte[] savkey = new Byte[0xB4AD4];
        private byte[] batvideo = new Byte[0x100000]; // whatever
        private byte[] zerobox = new Byte[232 * 30];

        // Dumping Usage
        private string binType = "sav";
        private string vidpath = "";
        private string savpath = "";
        private string savkeypath = "";
        private string vidkeypath = "";
        private string custom1 = "";
        private string custom2 = "";
        private string custom3 = "";
        private string customcsv = "";
        private bool custom1b = false;
        private bool custom2b = false;
        private bool custom3b = false;
        private string[] boxcolors = new string[] { "", "###", "####", "#####", "######" };
        private string csvdata = "";
        private int dumpedcounter = 0;
        private int slots = 0;
        private bool ghost = false;
        private ushort[] selectedTSVs = new ushort[0];
        private string defaultCSVcustom = "{59},{42},{0},{1},{2},{3},{4},{5},{68},{6},{7},{8},{9},{10},{11},{54},{47},{48},{49},{50},{51},{52},{53},{12},{60},{61},{35},{34},{13},{14},{15},{16},{18},{19},{17},{20},{21},{22},{23},{24},{25},{55},{56},{57},{63},{26},{27},{28},{29},{30},{31},{32},{33},{58},{36},{44},{37},{38},{39},{40},{41},{43},{45},{46},{62},{64},{66},{65}";

        // Breaking Usage
        private byte[] break1 = new Byte[0x10009C];
        private byte[] break2 = new Byte[0x10009C];
        private byte[] break3 = new Byte[0x10009C];
        private byte[] video1 = new Byte[28256];
        private byte[] video2 = new Byte[28256];

        // UI Usage
        private bool updateIVCheckboxes = true;
        private volatile int game;

        #endregion
        
        // Drag & Drop Events
        private void tabMain_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        private void tabMain_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            string path = files[0]; // open first D&D
            long len = new FileInfo(files[0]).Length;

            // Handle battle video size check, SAV size check is in openSAV
            if (len == 28256)
            {
                tab_Main.SelectedIndex = 0;
                openVID(path);
            }
            else
            {
                tab_Main.SelectedIndex = 1;
                openSAV(path);
            }
        }

        public void DisplayTimeEvent(object source, ElapsedEventArgs e)
        {
            find3DS();
        }

        // Utility
        private void onFormClose(object sender, FormClosingEventArgs e)
        {
            // Save the ini file
            saveINI();
        }

        private void loadINI()
        {
            try
            {
                // Detect startup path and data path.
                if (!Directory.Exists(datapath)) // Create data path if it doesn't exist.
                    Directory.CreateDirectory(datapath);
                if (!Directory.Exists(dbpath)) // Create db path if it doesn't exist.
                    Directory.CreateDirectory(dbpath);
                if (!Directory.Exists(bakpath)) // Create backup path if it doesn't exist.
                    Directory.CreateDirectory(bakpath);
            
                // Load .ini data.
                if (!File.Exists(Path.Combine(datapath, "config.ini")))
                    File.Create(Path.Combine(datapath, "config.ini"));
                else
                {
                    TextReader tr = new StreamReader(Path.Combine(datapath, "config.ini"));
                    try
                    {
                        // Load the data
                        tab_Main.SelectedIndex = Convert.ToInt16(tr.ReadLine());
                        custom1 = tr.ReadLine();
                        custom2 = tr.ReadLine();
                        custom3 = tr.ReadLine();
                        customcsv = tr.ReadLine();
                        custom1b = Convert.ToBoolean(Convert.ToInt16(tr.ReadLine()));
                        custom2b = Convert.ToBoolean(Convert.ToInt16(tr.ReadLine()));
                        custom3b = Convert.ToBoolean(Convert.ToInt16(tr.ReadLine()));
                        CB_ExportStyle.SelectedIndex = Convert.ToInt16(tr.ReadLine());
                        CB_MainLanguage.SelectedIndex = Convert.ToInt16(tr.ReadLine());
                        CB_Game.SelectedIndex = Convert.ToInt16(tr.ReadLine());
                        CHK_MarkFirst.Checked = Convert.ToBoolean(Convert.ToInt16(tr.ReadLine()));
                        CHK_Split.Checked = Convert.ToBoolean(Convert.ToInt16(tr.ReadLine()));
                        CHK_BoldIVs.Checked = Convert.ToBoolean(Convert.ToInt16(tr.ReadLine()));
                        CHK_ShowESV.Checked = Convert.ToBoolean(Convert.ToInt16(tr.ReadLine()));
                        CHK_NameQuotes.Checked = Convert.ToBoolean(Convert.ToInt16(tr.ReadLine()));
                        CB_BoxColor.SelectedIndex = Convert.ToInt16(tr.ReadLine());
                        CHK_ColorBox.Checked = Convert.ToBoolean(Convert.ToInt16(tr.ReadLine()));
                        CHK_HideFirst.Checked = Convert.ToBoolean(Convert.ToInt16(tr.ReadLine()));
                        this.Height = Convert.ToInt16(tr.ReadLine());
                        this.Width = Convert.ToInt16(tr.ReadLine());
                        CHK_Unicode.Checked = Convert.ToBoolean(Convert.ToInt16(tr.ReadLine()));
                        tr.Close();
                    }
                    catch
                    {
                        tr.Close();
                    }
                }
            }
            catch (Exception e) { MessageBox.Show("Ini config file loading failed.\n\n" + e, "Error"); }
        }

        private void saveINI()
        {
            try
            {
                // Detect startup path and data path.
                if (!Directory.Exists(datapath)) // Create data path if it doesn't exist.
                    Directory.CreateDirectory(datapath);
            
                // Save .ini data.
                if (!File.Exists(Path.Combine(datapath, "config.ini")))
                    File.Create(Path.Combine(datapath, "config.ini"));
                else
                {
                    TextWriter tr = new StreamWriter(Path.Combine(datapath, "config.ini"));
                    try
                    {
                        // Save the data
                        tr.WriteLine(tab_Main.SelectedIndex.ToString());
                        tr.WriteLine(custom1.ToString());
                        tr.WriteLine(custom2.ToString());
                        tr.WriteLine(custom3.ToString());
                        tr.WriteLine(customcsv.ToString());
                        tr.WriteLine(Convert.ToInt16(custom1b).ToString());
                        tr.WriteLine(Convert.ToInt16(custom2b).ToString());
                        tr.WriteLine(Convert.ToInt16(custom3b).ToString());
                        tr.WriteLine(CB_ExportStyle.SelectedIndex.ToString());
                        tr.WriteLine(CB_MainLanguage.SelectedIndex.ToString());
                        tr.WriteLine(CB_Game.SelectedIndex.ToString());
                        tr.WriteLine(Convert.ToInt16(CHK_MarkFirst.Checked).ToString());
                        tr.WriteLine(Convert.ToInt16(CHK_Split.Checked).ToString());
                        tr.WriteLine(Convert.ToInt16(CHK_BoldIVs.Checked).ToString());
                        tr.WriteLine(Convert.ToInt16(CHK_ShowESV.Checked).ToString());
                        tr.WriteLine(Convert.ToInt16(CHK_NameQuotes.Checked).ToString());
                        tr.WriteLine(CB_BoxColor.SelectedIndex.ToString());
                        tr.WriteLine(Convert.ToInt16(CHK_ColorBox.Checked).ToString());
                        tr.WriteLine(Convert.ToInt16(CHK_HideFirst.Checked).ToString());
                        tr.WriteLine(this.Height.ToString());
                        tr.WriteLine(this.Width.ToString());
                        tr.WriteLine(Convert.ToInt16(CHK_Unicode.Checked).ToString());
                        tr.Close();
                    }
                    catch
                    {
                        tr.Close();
                    }
                }
            }
            catch (Exception e) { MessageBox.Show("Ini config file saving failed.\n\n" + e, "Error"); }
        }

        // RNG
        private static uint LCRNG(uint seed)
        {
            return (seed * 0x41C64E6D + 0x00006073) & 0xFFFFFFFF;
        }
        private static Random rand = new Random();
        private static uint rnd32()
        {
            return (uint)(rand.Next(1 << 30)) << 2 | (uint)(rand.Next(1 << 2));
        }

        // PKX Struct Manipulation
        private byte[] shuffleArray(byte[] pkx, uint sv)
        {
            byte[] ekx = new Byte[260]; Array.Copy(pkx, ekx, 8);

            // Now to shuffle the blocks

            // Define Shuffle Order Structure
            var aloc = new byte[] { 0, 0, 0, 0, 0, 0, 1, 1, 2, 3, 2, 3, 1, 1, 2, 3, 2, 3, 1, 1, 2, 3, 2, 3 };
            var bloc = new byte[] { 1, 1, 2, 3, 2, 3, 0, 0, 0, 0, 0, 0, 2, 3, 1, 1, 3, 2, 2, 3, 1, 1, 3, 2 };
            var cloc = new byte[] { 2, 3, 1, 1, 3, 2, 2, 3, 1, 1, 3, 2, 0, 0, 0, 0, 0, 0, 3, 2, 3, 2, 1, 1 };
            var dloc = new byte[] { 3, 2, 3, 2, 1, 1, 3, 2, 3, 2, 1, 1, 3, 2, 3, 2, 1, 1, 0, 0, 0, 0, 0, 0 };

            // Get Shuffle Order
            var shlog = new byte[] { aloc[sv], bloc[sv], cloc[sv], dloc[sv] };

            // UnShuffle Away!
            for (int b = 0; b < 4; b++)
                Array.Copy(pkx, 8 + 56 * shlog[b], ekx, 8 + 56 * b, 56);

            // Fill the Battle Stats back
            if (pkx.Length > 232)
                Array.Copy(pkx, 232, ekx, 232, 28);
            return ekx;
        }

        private byte[] decryptArray(byte[] ekx)
        {
            byte[] pkx = new Byte[0xE8]; Array.Copy(ekx, pkx, 0xE8);
            uint pv = BitConverter.ToUInt32(pkx, 0);
            uint sv = (((pv & 0x3E000) >> 0xD) % 24);

            uint seed = pv;

            // Decrypt Blocks with RNG Seed
            for (int i = 8; i < 232; i += 2)
            {
                int pre = pkx[i] + ((pkx[i + 1]) << 8);
                seed = LCRNG(seed);
                int seedxor = (int)((seed) >> 16);
                int post = (pre ^ seedxor);
                pkx[i] = (byte)((post) & 0xFF);
                pkx[i + 1] = (byte)(((post) >> 8) & 0xFF);
            }

            // Deshuffle
            pkx = shuffleArray(pkx, sv);
            return pkx;
        }

        private byte[] encryptArray(byte[] pkx)
        {
            // Shuffle
            uint pv = BitConverter.ToUInt32(pkx, 0);
            uint sv = (((pv & 0x3E000) >> 0xD) % 24);

            byte[] ekxdata = new Byte[pkx.Length]; Array.Copy(pkx, ekxdata, pkx.Length);

            // If I unshuffle 11 times, the 12th (decryption) will always decrypt to ABCD.
            // 2 x 3 x 4 = 12 (possible unshuffle loops -> total iterations)
            for (int i = 0; i < 11; i++)
                ekxdata = shuffleArray(ekxdata, sv);

            uint seed = pv;
            // Encrypt Blocks with RNG Seed
            for (int i = 8; i < 232; i += 2)
            {
                int pre = ekxdata[i] + ((ekxdata[i + 1]) << 8);
                seed = LCRNG(seed);
                int seedxor = (int)((seed) >> 16);
                int post = (pre ^ seedxor);
                ekxdata[i] = (byte)((post) & 0xFF);
                ekxdata[i + 1] = (byte)(((post) >> 8) & 0xFF);
            }

            // Encrypt the Party Stats
            seed = pv;
            for (int i = 232; i < 260; i += 2)
            {
                int pre = ekxdata[i] + ((ekxdata[i + 1]) << 8);
                seed = LCRNG(seed);
                int seedxor = (int)((seed) >> 16);
                int post = (pre ^ seedxor);
                ekxdata[i] = (byte)((post) & 0xFF);
                ekxdata[i + 1] = (byte)(((post) >> 8) & 0xFF);
            }

            // Done
            return ekxdata;
        }

        private int getDloc(uint ec)
        {
            // Define Shuffle Order Structure
            var dloc = new byte[] { 3, 2, 3, 2, 1, 1, 3, 2, 3, 2, 1, 1, 3, 2, 3, 2, 1, 1, 0, 0, 0, 0, 0, 0 };
            uint sv = (((ec & 0x3E000) >> 0xD) % 24);

            return dloc[sv];
        }

        private bool verifyCHK(byte[] pkx)
        {
            ushort chk = 0;
            for (int i = 8; i < 232; i += 2) // Loop through the entire PKX
                chk += BitConverter.ToUInt16(pkx, i);

            ushort actualsum = BitConverter.ToUInt16(pkx, 0x6);
            if ((BitConverter.ToUInt16(pkx, 0x8) > 750) || (BitConverter.ToUInt16(pkx, 0x90) != 0)) 
                return false;
            return (chk == actualsum);
        }

        // File Type Loading
        private void B_OpenSAV_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.InitialDirectory = savpath;
            ofd.RestoreDirectory = true;
            ofd.Filter = "Save|*.sav;*.bin";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                lastOpenedFilename = ofd.SafeFileName;
                openSAV(ofd.FileName);
            }
        }

        private void B_OpenVid_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.InitialDirectory = vidpath;
            ofd.RestoreDirectory = true;
            ofd.Filter = "Battle Video|*.*";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                lastOpenedFilename = ofd.SafeFileName;
                openVID(ofd.FileName);
            }
        }

        private void openSAV(string path)
        {
            long len = new FileInfo(path).Length;
            if (len == 232*30*31)
            {
                // PCEdit pcdata.bin
                binType = "raw";
                openBIN(path);
            }
            else if (len == 232*30*32)
            {
                // Cu3PO42's boxes.bin
                binType = "yabd";
                openBIN(path);
            }
            else if (len == 0x70000)
            {
                // RAMSAV for XY
                binType = "xy";
                openBIN(path);
            }
            else if (len == 0x80000)
            {
                // RAMSAV for ORAS
                binType = "oras";
                openBIN(path);
            }
            else
            {
                binType = "sav";
                openSAV_(path, ref savefile, ref savkeypath, true);
            }
        }

        private int openSAV_(string path, ref byte[] savefile, ref string savkeypath, bool showUI)
        {
            // check to see if good input file
            long len = new FileInfo(path).Length;
            if (len != 0x100000 && len != 0x10009C && len != 0x10019A)
            { 
                if (showUI)
                    MessageBox.Show("Incorrect File Loaded: Not a SAV file (1MB).", "Error");
                return 0;
            }
            
            TB_SAV.Text = path;

            // Go ahead and load the save file into RAM...
            byte[] input = File.ReadAllBytes(path);
            Array.Copy(input, input.Length % 0x100000, savefile, 0, 0x100000);

            // Fetch Stamp
            ulong stamp = BitConverter.ToUInt64(savefile, 0x10);
            string keyfile = fetchKey(stamp, 0xB4AD4);
            if (keyfile == "")
            {
                if (showUI)
                {
                    L_KeySAV.Text = "Key not found. Please break for this SAV first.";
                    B_GoSAV.Enabled = false;
                }
                return 0;
            }
            else
            {
                if (showUI)
                {
                    B_GoSAV.Enabled = true;
                    L_KeySAV.Text = new FileInfo(keyfile).Name;
                }
                savkeypath = keyfile;
            }
            if (showUI)
                B_GoSAV.Enabled = CB_BoxEnd.Enabled = CB_BoxStart.Enabled = B_BKP_SAV.Enabled = !(keyfile == "");
            byte[] key = File.ReadAllBytes(keyfile);
            byte[] empty = new Byte[232];
            
            // Save file is already loaded.
            // If slot one was used for the last save copy the boxes to slot 2 and apply key
            if(BitConverter.ToUInt32(key, 0x80000) == BitConverter.ToUInt32(savefile, 0x168))
            {
                int boxoffset = BitConverter.ToInt32(key, 0x1C);
                for(int i = 0, j = boxoffset; i<232*30*31; ++i, ++j)
                {
                    savefile[j] = (byte)(savefile[j - 0x7F000] ^ key[0x80004 + i]);
                }
            }

            // Get our empty file set up.
            Array.Copy(key, 0x10, empty, 0xE0, 0x4);
            string nick = eggnames[empty[0xE3] - 1];
            // Stuff in the nickname to our blank EKX.
            byte[] nicknamebytes = Encoding.Unicode.GetBytes(nick);
            Array.Resize(ref nicknamebytes, 24);
            Array.Copy(nicknamebytes, 0, empty, 0x40, nicknamebytes.Length);
            // Fix CHK
            uint chk = 0;
            for (int i = 8; i < 232; i += 2) // Loop through the entire PKX
                chk += BitConverter.ToUInt16(empty, i);
            // Apply New Checksum
            Array.Copy(BitConverter.GetBytes(chk), 0, empty, 06, 2);
            empty = encryptArray(empty);
            Array.Resize(ref empty, 0xE8);

            // Scan the save and update keys
            scanSAV(savefile, key, empty, showUI);
            File.WriteAllBytes(keyfile, key); // Key has been scanned for new slots, re-save key.
            return 1;
        }

        private void openBIN(string path)
        {
            // File size already checked, so we're good to "Go"; load it in to RAM
            byte[] input = File.ReadAllBytes(path);
            Array.Copy(input, savefile, input.Length);
            TB_SAV.Text = path;
            L_KeySAV.Text = "Decrypted; no key neeeded.";
            CB_BoxEnd.Enabled = CB_BoxStart.Enabled = B_BKP_SAV.Enabled = B_GoSAV.Enabled = true;
        }

        private void openVID(string path)
        {
            // check to see if good input file
            B_GoBV.Enabled = CB_Team.Enabled = false;
            long len = new FileInfo(path).Length;
            if (len != 28256)
            {
                MessageBox.Show("Incorrect File Loaded: Not a Battle Video (~27.5KB).", "Error");
                return;
            }

            TB_BV.Text = path;

            // Go ahead and load the save file into RAM...
            batvideo = File.ReadAllBytes(path);

            // Fetch Stamp
            ulong stamp = BitConverter.ToUInt64(batvideo, 0x10);
            string keyfile = fetchKey(stamp, 0x1000);
            B_GoBV.Enabled = CB_Team.Enabled = B_BKP_BV.Enabled = (keyfile != "");
            if (keyfile == "")
            { L_KeyBV.Text = "Key not found. Please break for this BV first."; return; }
            else
            {
                L_KeyBV.Text = new FileInfo(keyfile).Name;
                vidkeypath = keyfile;
            }

            // Check up on the key file...
            CB_Team.Items.Clear();
            CB_Team.Items.Add("My Team");
            byte[] bvkey = File.ReadAllBytes(vidkeypath);
            if (BitConverter.ToUInt64(bvkey, 0x800) != 0)
                CB_Team.Items.Add("Enemy Team");
            CB_Team.SelectedIndex = 0;
        }

        private string fetchKey(ulong stamp, int length)
        {
            // Find the Key in the datapath (program//data folder)
            string[] files = Directory.GetFiles(datapath,"*.bin", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                FileInfo fi = new FileInfo(files[i]);
                {
                    if (fi.Length == length)
                    {
                        byte[] data = File.ReadAllBytes(files[i]);
                        ulong newstamp = BitConverter.ToUInt64(data, 0x0);
                        if (newstamp == stamp)
                            return files[i];
                    }
                }
            }
            // else return nothing
            return "";
        }

        // File Dumping
        // SAV
        private byte[] fetchpkx(byte[] input, byte[] keystream, int pkxoffset, int key1off, int key2off, byte[] blank)
        {
            // Auto updates the keystream when it dumps important data!
            ghost = true;
            byte[] ekx = new Byte[232];
            byte[] key1 = new Byte[232]; Array.Copy(keystream, key1off, key1, 0, 232);
            byte[] key2 = new Byte[232]; Array.Copy(keystream, key2off, key2, 0, 232);
            byte[] encrypteddata = new Byte[232]; Array.Copy(input, pkxoffset, encrypteddata, 0, 232);

            byte[] zeros = new Byte[232];
            byte[] ezeros = encryptArray(zeros); Array.Resize(ref ezeros, 0xE8);
            if (zeros.SequenceEqual(key1) && zeros.SequenceEqual(key2))
                return null;
            else if (zeros.SequenceEqual(key1))
            {
                // Key2 is confirmed to dump the data.
                ekx = xortwos(key2, encrypteddata);
                ghost = false;
            }
            else if (zeros.SequenceEqual(key2))
            {
                // Haven't dumped from this slot yet.
                if (key1.SequenceEqual(encrypteddata))
                {
                    // Slot hasn't changed.
                    return null;
                }
                else
                {
                    // Try and decrypt the data...
                    ekx = xortwos(key1, encrypteddata);
                    if (verifyCHK(decryptArray(ekx)))
                    {
                        // Data has been dumped!
                        // Fill keystream data with our log.
                        Array.Copy(encrypteddata, 0, keystream, key2off, 232);
                    }
                    else
                    {
                        // Try xoring with the empty data.
                        if (verifyCHK(decryptArray(xortwos(ekx, blank))))
                        {
                            ekx = xortwos(ekx, blank);
                            Array.Copy(xortwos(encrypteddata, blank), 0, keystream, key2off, 232);
                        }
                        else if (verifyCHK(decryptArray(xortwos(ekx, ezeros))))
                        {
                            ekx = xortwos(ekx, ezeros);
                            Array.Copy(xortwos(encrypteddata, ezeros), 0, keystream, key2off, 232);
                        }
                        else return null; // Not a failed decryption; we just haven't seen new data here yet.
                    }
                }
            }
            else
            {
                // We've dumped data at least once.
                if (key1.SequenceEqual(encrypteddata) || key1.SequenceEqual(xortwos(encrypteddata,blank)) || key1.SequenceEqual(xortwos(encrypteddata,ezeros)))
                {
                    // Data is back to break state, but we can still dump with the other key.
                    ekx = xortwos(key2, encrypteddata);
                    if (!verifyCHK(decryptArray(ekx)))
                    {
                        if (verifyCHK(decryptArray(xortwos(ekx, blank))))
                        {
                            ekx = xortwos(ekx, blank);
                            Array.Copy(xortwos(key2, blank), 0, keystream, key2off, 232);
                        }
                        else if (verifyCHK(decryptArray(xortwos(ekx, ezeros))))
                        {
                            // Key1 decrypts our data after we remove encrypted zeros.
                            // Copy Key1 to Key2, then zero out Key1.
                            ekx = xortwos(ekx, ezeros);
                            Array.Copy(xortwos(key2, ezeros), 0, keystream, key2off, 232);
                        }
                        else return null; // Decryption Error
                    }
                }
                else if (key2.SequenceEqual(encrypteddata) || key2.SequenceEqual(xortwos(encrypteddata, blank)) || key2.SequenceEqual(xortwos(encrypteddata, ezeros)))
                {
                    // Data is changed only once to a dumpable, but we can still dump with the other key.
                    ekx = xortwos(key1, encrypteddata); 
                    if (!verifyCHK(decryptArray(ekx)))
                    {
                        if (verifyCHK(decryptArray(xortwos(ekx, blank))))
                        {
                            ekx = xortwos(ekx, blank);
                            Array.Copy(xortwos(key1, blank), 0, keystream, key1off, 232);
                        }
                        else if (verifyCHK(decryptArray(xortwos(ekx, ezeros))))
                        {
                            ekx = xortwos(ekx, ezeros);
                            Array.Copy(xortwos(key1, ezeros), 0, keystream, key1off, 232);
                        }
                        else return null; // Decryption Error
                    }
                }
                else
                {
                    // Data has been observed to change twice! We can get our exact keystream now!
                    // Either Key1 or Key2 or Save is empty. Whichever one decrypts properly is the empty data.
                    // Oh boy... here we go...
                    ghost = false;
                    bool keydata1, keydata2 = false;
                    byte[] data1 = xortwos(encrypteddata, key1);
                    byte[] data2 = xortwos(encrypteddata, key2);

                    keydata1 = 
                        (verifyCHK(decryptArray(data1))
                        ||
                        verifyCHK(decryptArray(xortwos(data1, ezeros)))
                        ||
                        verifyCHK(decryptArray(xortwos(data1, blank)))
                        );
                    keydata2 = 
                        (verifyCHK(decryptArray(data2))
                        ||
                        verifyCHK(decryptArray(xortwos(data2, ezeros)))
                        ||
                        verifyCHK(decryptArray(xortwos(data2, blank)))
                        );
                    if (!keydata1 && !keydata2) 
                        return null; // All 3 are occupied.
                    if (keydata1 && keydata2)
                    {
                        // Save file is currently empty...
                        // Copy key data from save file if it decrypts with Key1 data properly.

                        if (verifyCHK(decryptArray(data1)))
                        {
                            // No modifications necessary.
                            ekx = data1;
                            Array.Copy(encrypteddata, 0, keystream, key2off, 232);
                            Array.Copy(zeros, 0, keystream, key1off, 232);
                        }
                        else if (verifyCHK(decryptArray(xortwos(data1, ezeros))))
                        {
                            ekx = ezeros;
                            Array.Copy(xortwos(encrypteddata,ezeros), 0, keystream, key2off, 232);
                            Array.Copy(zeros, 0, keystream, key1off, 232);
                        }
                        else if (verifyCHK(decryptArray(xortwos(data1, blank))))
                        {
                            ekx = ezeros;
                            Array.Copy(xortwos(encrypteddata, blank), 0, keystream, key2off, 232);
                            Array.Copy(zeros, 0, keystream, key1off, 232);
                        }
                        else return null; // unreachable
                    }
                    else if (keydata1) // Key 1 data is empty
                    {
                        if (verifyCHK(decryptArray(data1)))
                        {
                            ekx = data1;
                            Array.Copy(key1, 0, keystream, key2off, 232);
                            Array.Copy(zeros, 0, keystream, key1off, 232);
                        }
                        else if (verifyCHK(decryptArray(xortwos(data1, ezeros))))
                        {
                            ekx = xortwos(data1, ezeros);
                            Array.Copy(xortwos(key1, ezeros), 0, keystream, key2off, 232);
                            Array.Copy(zeros, 0, keystream, key1off, 232);
                        }
                        else if (verifyCHK(decryptArray(xortwos(data1, blank))))
                        {
                            ekx = xortwos(data1, blank);
                            Array.Copy(xortwos(key1, blank), 0, keystream, key2off, 232);
                            Array.Copy(zeros, 0, keystream, key1off, 232);
                        }
                        else return null; // unreachable
                    }
                    else if (keydata2)
                    {
                        if (verifyCHK(decryptArray(data2)))
                        {
                            ekx = data2;
                            Array.Copy(key2, 0, keystream, key2off, 232);
                            Array.Copy(zeros, 0, keystream, key1off, 232);
                        }
                        else if (verifyCHK(decryptArray(xortwos(data2, ezeros))))
                        {
                            ekx = xortwos(data2, ezeros);
                            Array.Copy(xortwos(key2, ezeros), 0, keystream, key2off, 232);
                            Array.Copy(zeros, 0, keystream, key1off, 232);
                        }
                        else if (verifyCHK(decryptArray(xortwos(data2, blank))))
                        {
                            ekx = xortwos(data2, blank);
                            Array.Copy(xortwos(key2, blank), 0, keystream, key2off, 232);
                            Array.Copy(zeros, 0, keystream, key1off, 232);
                        }
                        else return null; // unreachable
                    }
                }
            }
            byte[] pkx = decryptArray(ekx);
            if (verifyCHK(pkx))
            {
                slots++;
                return pkx;
            }
            else 
                return null; // Slot Decryption error?!
        }

        private void scanSAV(byte[] input, byte[] keystream, byte[] blank, bool showUI)
        {
            slots = 0;
            int boxoffset = BitConverter.ToInt32(keystream, 0x1C);
            for (int i = 0; i < 930; i++)
                fetchpkx(input, keystream, boxoffset + i * 232, 0x100 + i * 232, 0x40000 + i * 232, blank);
            if(showUI)
                L_SAVStats.Text = String.Format("{0}/930", slots);
        }

        private void dumpPKX(bool isSAV, byte[] pkx, int dumpnum, int dumpstart)
        {
            if (isSAV && ghost && CHK_HideFirst.Checked) return;
            if (pkx == null || !verifyCHK(pkx))
                return;
            Structures.PKX data = new Structures.PKX(pkx);

            // Printout Parsing
            if (data.species == 0)
                return;

            // Unicode stuff
            string checkmark = (CHK_Unicode.Checked) ? "✓" : "#";
            string shinymark = (CHK_Unicode.Checked) ? "★" : "*";
            string gen6mark = (CHK_Unicode.Checked) ? "⬟" : "#";
            string femalemark = (CHK_Unicode.Checked) ? "♀" : "F";
            string malemark = (CHK_Unicode.Checked) ? "♂" : "M";
            
            // Parse Pokemon info
            string box = (isSAV) ? "B"+(dumpstart + (dumpnum/30)).ToString("00") : "-";
            string slot = (isSAV) ? (((dumpnum%30) / 6 + 1).ToString() + "," + (dumpnum % 6 + 1).ToString()) : dumpnum.ToString();
            string species = specieslist[data.species];
            string gender;
            if (data.genderflag == 0)
                gender = malemark;
            else if (data.genderflag == 1)
                gender = femalemark;
            else gender = "-";
            string nature = natures[data.nature];
            string ability = abilitylist[data.ability];
            string hiddenability = (data.abilitynum == 4) ? checkmark : "";
            string hp = data.HP_IV.ToString();
            string atk = data.ATK_IV.ToString();
            string def = data.DEF_IV.ToString();
            string spa = data.SPA_IV.ToString();
            string spd = data.SPD_IV.ToString();
            string spe = data.SPE_IV.ToString();
            string hptype = types[data.hptype];
            string ESV = data.ESV.ToString("0000");
            string TSV = data.TSV.ToString("0000");
            string ball = balls[data.ball];
            string ballimg = "[](/" + ball.Replace(" ", "").Replace("é", "e").ToLower() + ")";
            string nickname = data.nicknamestr;
            string otname = data.ot;
            string TID = data.TID.ToString("00000");
            string SID = data.SID.ToString("00000");
            string ev_hp = data.HP_EV.ToString();
            string ev_at = data.ATK_EV.ToString();
            string ev_de = data.DEF_EV.ToString();
            string ev_sa = data.SPA_EV.ToString();
            string ev_sd = data.SPD_EV.ToString();
            string ev_se = data.SPE_EV.ToString();
            string number = (isSAV) ? (dumpnum % 30 + 1).ToString() : slot;
            string overallCount = (dumpedcounter+1).ToString();
            string isshiny = (data.isshiny) ? shinymark : "";
            string isegg = (data.isegg) ? checkmark : "";
            
            // Handle bad decryption on moves
            string move1;
            string move2;
            string move3;
            string move4;
            string relearn1;
            string relearn2;
            string relearn3;
            string relearn4;
            try { move1 = movelist[data.move1]; } catch { move1 = "ERROR"; }
            try { move2 = movelist[data.move2]; } catch { move2 = "ERROR"; }
            try { move3 = movelist[data.move3]; } catch { move3 = "ERROR"; }
            try { move4 = movelist[data.move4]; } catch { move4 = "ERROR"; }
            try { relearn1 = movelist[data.eggmove1]; } catch { relearn1 = "ERROR"; }
            try { relearn2 = movelist[data.eggmove2]; } catch { relearn2 = "ERROR"; }
            try { relearn3 = movelist[data.eggmove3]; } catch { relearn3 = "ERROR"; }
            try { relearn4 = movelist[data.eggmove4]; } catch { relearn4 = "ERROR"; }
            
            // Extra fields for CSV custom output
            // TODO: add an option to use actual markers below instead of numbers
            // string[] markers = new string[] { "●", "▲", "■", "♥", "★", "♦" };
            int IVcounter = 0;
            string hpm = ""; if (data.HP_IV == 31) { hpm = "1"; IVcounter++; }
            string atkm = ""; if (data.ATK_IV == 31) { atkm = "2"; IVcounter++; }
            string defm = ""; if (data.DEF_IV == 31) { defm = "3"; IVcounter++; }
            string spam = ""; if (data.SPA_IV == 31) { spam = "4"; IVcounter++; }
            string spdm = ""; if (data.SPD_IV == 31) { spdm = "5"; IVcounter++; }
            string spem = ""; if (data.SPE_IV == 31) { spem = "6"; IVcounter++; }
            string IVs = (IVcounter == 1) ? IVcounter.ToString() + " IV" : IVcounter.ToString() + " IVs";
            string IVsum = (data.HP_IV + data.ATK_IV + data.DEF_IV + data.SPA_IV + data.SPD_IV + data.SPE_IV).ToString();
            string EVsum = (data.HP_EV + data.ATK_EV + data.DEF_EV + data.SPA_EV + data.SPD_EV + data.SPE_EV).ToString();
            string eggDate = (data.egg_year.ToString() == "0") ? "" : "20" + data.egg_year.ToString("00") + "-" + data.egg_month.ToString("00") + "-" + data.egg_day.ToString("00");
            string metDate = (data.isegg) ? "" : "20" + data.met_year.ToString("00") + "-" + data.met_month.ToString("00") + "-" + data.met_day.ToString("00");
            string experience = data.exp.ToString();
            string level = (data.isegg) ? "" : getLevel(Convert.ToInt32(data.species), Convert.ToInt32(data.exp)).ToString();
            string region = regionList[data.gamevers];
            string pgame = gameList[data.gamevers];
            string country = countryList[data.countryID];
            string helditem = (data.helditem == 0) ? "" : itemlist[data.helditem];
            string language = languageList[data.otlang];
            string mark = (data.gamevers >= 24 && data.gamevers <= 27) ? gen6mark : ""; // Mark is for Gen 6 Pokemon, so X Y OR AS
            string PID = data.PID.ToString();
            string dex = data.species.ToString();
            string form = data.altforms.ToString();
            string pkrsInfected = (data.PKRS_Strain > 0) ? checkmark : "";
            string pkrsCured = (data.PKRS_Strain > 0 && data.PKRS_Duration == 0) ? checkmark : "";
            string OTgender = (data.otgender == 1) ? femalemark : malemark;
            string metLevel = data.metlevel.ToString();
            string OTfriendship = data.OTfriendship.ToString();
            string OTaffection = data.OTaffection.ToString();
            string stepsToHatch = (!data.isegg) ? "" : (data.OTfriendship * 255).ToString();
            
            // Do the Filtering
            bool satisfiesFilters = true;
            if (isSAV)
            {
                while (CHK_Enable_Filtering.Checked)
                {
                    if (CHK_Egg.Checked && !data.isegg) { satisfiesFilters = false; break; }
                    
                    if (CHK_Has_HA.Checked && data.abilitynum != 4) { satisfiesFilters = false; break; }

                    if (CB_Abilities.Text != "" && CB_Abilities.SelectedIndex != 0 && CB_Abilities.Text != ability)
                    { satisfiesFilters = false; break; }

                    bool checkHP = CCB_HPType.GetItemCheckState(0) != CheckState.Checked;
                    byte checkHPDiff = (byte)Convert.ToInt16(checkHP);
                    int perfects = CB_No_IVs.SelectedIndex;
                    foreach(var iv in new [] {
                        new Tuple<uint, bool>(data.HP_IV, CHK_IV_HP.Checked), 
                        new Tuple<uint, bool>(data.DEF_IV, CHK_IV_Def.Checked), 
                        new Tuple<uint, bool>(data.SPA_IV, CHK_IV_SpAtk.Checked), 
                        new Tuple<uint, bool>(data.SPD_IV, CHK_IV_SpDef.Checked) })
                    {
                        if (31 - iv.Item1 <= checkHPDiff) --perfects;
                        else if (iv.Item2) { satisfiesFilters = false; break; }
                    }
                    foreach(var iv in new [] {
                        new Tuple<uint, bool, bool>(data.ATK_IV, CHK_IV_Atk.Checked, CHK_Special_Attacker.Checked), 
                        new Tuple<uint, bool, bool>(data.SPE_IV, CHK_IV_Spe.Checked, CHK_Trickroom.Checked) })
                    {
                        if (Math.Abs((iv.Item3 ? 0: 31) - iv.Item1) <= checkHPDiff) --perfects;
                        else if (iv.Item2) { satisfiesFilters = false; break; }
                    }

                    if(perfects > 0) { satisfiesFilters = false; break; }

                    if(checkHP && !CCB_HPType.GetItemChecked((int)data.hptype)) { satisfiesFilters = false; break; }

                    if(!CCB_Natures.GetItemChecked((int)data.nature+1)) { satisfiesFilters = false; break; }

                    if (CHK_Is_Shiny.Checked || CHK_Hatches_Shiny_For_Me.Checked || CHK_Hatches_Shiny_For.Checked)
                    {
                        if (!(CHK_Is_Shiny.Checked && data.isshiny ||
                            data.isegg && CHK_Hatches_Shiny_For_Me.Checked && ESV == TSV ||
                            data.isegg && CHK_Hatches_Shiny_For.Checked && Array.IndexOf(selectedTSVs, data.ESV) > -1))
                        { satisfiesFilters = false; break; }
                    }

                    if(RAD_Male.Checked && data.genderflag != 0 || RAD_Female.Checked && data.genderflag != 1)
                    { satisfiesFilters = false; break; }

                    break;
                }
            }

            // If it satisfies filters or we're doing a battle video, print it out
            if (satisfiesFilters || !isSAV)
            {
                if (!data.isegg && !CHK_ShowESV.Checked) ESV = "";

                // Vivillon Forms
                if (data.species >= 664 && data.species <= 666)
                    species += "-" + vivlist[data.altforms];

                // Unown Forms
                if (data.species == 201)
                    species += "-" + unownlist[data.altforms];

                // Bold the IVs if Reddit and option is checked
                if (CB_ExportStyle.SelectedIndex >= 1 && CB_ExportStyle.SelectedIndex <= 5 && CHK_BoldIVs.Checked)
                {
                    if (hp == "31") hp = "**31**";
                    if (atk == "31") atk = "**31**";
                    if (def == "31") def = "**31**";
                    if (spa == "31") spa = "**31**";
                    if (spd == "31") spd = "**31**";
                    if (spe == "31") spe = "**31**";
                }

                // Get the output format from the input text box
                string format = RTB_OPTIONS.Text;
                
                // For PK6 output, display default format and output PK6 files
                if (CB_ExportStyle.SelectedIndex == 8)
                {
                    format = "{0} - {1} - {2} ({3}) - {4} - {5} - {6}.{7}.{8}.{9}.{10}.{11} - {12} - {13}";
                    isshiny = (data.isshiny) ? " " + shinymark: "";

                    // For nicknamed Pokemon, append the species name to the file name
                    if (data.isnick)
                        data.nicknamestr += String.Format(" ({0})", specieslist[data.species]);
                    string savedname = (dumpnum % 30 + 1).ToString("00") + " - " + 
                        data.species.ToString("000") + isshiny + " "
                        + data.nicknamestr + " - "
                        + data.chk.ToString("X4") + data.EC.ToString("X8");
                    if (isSAV) savedname = box + " " + savedname;
                    File.WriteAllBytes(Path.Combine(dbpath, CleanFileName(savedname) + ".pk6"), pkx);
                }
                
                // Add brackets to ESV for defaults (0 and 8) and custom (3-5) if Table is NOT checked
                if (CB_ExportStyle.SelectedIndex == 0 || CB_ExportStyle.SelectedIndex == 8 || (!CHK_R_Table.Checked && CB_ExportStyle.SelectedIndex >= 3 && CB_ExportStyle.SelectedIndex <= 5))
                {
                    if (ESV != "")
                        ESV = "[" + ESV + "]";
                }
                
                // Escape any quotes so we can add quotes in the CSV export to avoid errors with commas in nicknames and trainer names
                if (CHK_NameQuotes.Checked && (CB_ExportStyle.SelectedIndex == 6 || CB_ExportStyle.SelectedIndex == 7))
                {
                    nickname.Replace("\"", "\\\"");
                    otname.Replace("\"", "\\\"");
                    nickname = "\"" + nickname + "\"";
                    otname = "\"" + otname + "\"";
                }
                
                // Generate result for this Pokemon
                string result = String.Format(format, box, slot, species, gender, nature, ability, hp, atk, def, spa, spd, spe, hptype, ESV, TSV, nickname, otname, ball, TID, SID, ev_hp, ev_at, ev_de, ev_sa, ev_sd, ev_se, move1, move2, move3, move4, relearn1, relearn2, relearn3, relearn4, isshiny, isegg, level, region, country, helditem, language, pgame, number, PID, mark, dex, form, hpm, atkm, defm, spam, spdm, spem, IVs, IVsum, EVsum, eggDate, metDate, experience, overallCount, pkrsInfected, pkrsCured, OTgender, metLevel, OTfriendship, OTaffection, stepsToHatch, ballimg, hiddenability);
                
                // Add the result to the CSV data if needed
                if (CB_ExportStyle.SelectedIndex == 6 || CB_ExportStyle.SelectedIndex == 7)
                    csvdata += result + "\n";

                if (isSAV && ghost && CHK_MarkFirst.Checked) result = "~" + result;
                dumpedcounter++;
                if (isSAV) RTB_SAV.AppendText(result + "\n"); else RTB_VID.AppendText(result + "\n");
            }
        }

        private void dumpSAV(object sender, EventArgs e)
        {
            dumpData(true);
        }

        private void dumpBV(object sender, EventArgs e)
        {
            dumpData(false);
        }

        private void dumpData(bool isSAV)
        {
            // Get the output format from the input text box
            string format = RTB_OPTIONS.Text;
            
            // For PK6 output, display default format
            if (CB_ExportStyle.SelectedIndex == 8)
                format = "{0} - {1} - {2} ({3}) - {4} - {5} - {6}.{7}.{8}.{9}.{10}.{11} - {12} - {13}";
                
            // Get the header
            string header = getHeaderString(format, isSAV);
            if (CHK_Header.Checked) csvdata = header + "\n";
            
            // Add header if Reddit, or if custom and Reddit table checked
            string toAppend = "";
            if (CB_ExportStyle.SelectedIndex == 1 || CB_ExportStyle.SelectedIndex == 2 || (CB_ExportStyle.SelectedIndex >= 1 && CB_ExportStyle.SelectedIndex <= 5 && CHK_R_Table.Checked))
            {
                int args = Regex.Split(RTB_OPTIONS.Text, "{").Length;
                header += "\n|";
                for (int i = 0; i < args; i++)
                    header += ":---:|";

                // Still append the header if we aren't doing it for every box.
                if (!CHK_Split.Checked || !isSAV)
                {
                    // Add header if reddit
                    if (CHK_ColorBox.Checked)
                    {
                        if (CB_BoxColor.SelectedIndex == 0)
                            toAppend += boxcolors[1 + (rnd32() % 4)];
                        else
                            toAppend += boxcolors[CB_BoxColor.SelectedIndex - 1];
                    }

                    // Append Box Name then Header
                    if (isSAV)
                        toAppend += (CB_BoxStart.Text == "All") ? "All Boxes" : ((CB_BoxStart.Text == CB_BoxEnd.Text) ? "Box " + CB_BoxStart.Text : "Boxes " + CB_BoxStart.Text + " to " + CB_BoxEnd.Text);
                    else
                        toAppend += (CB_Team.SelectedIndex == 1) ? "Enemy Team" : "My Team";
                    toAppend += "\n\n";
                    if (CHK_Header.Checked) toAppend += header + "\n";
                }
            }

            // Print out header at least once if "Split Boxes" is not checked
            else if ((!CHK_Split.Checked || !isSAV) && CHK_Header.Checked)
                toAppend += header + "\n";
            
            // Dump the actual Pokemon data for saves
            if (isSAV)
            {
                RTB_SAV.Clear();
                dumpedcounter = 0;
                int boxoffset = 0;
                byte[] keystream = new Byte[0xB4AD4];
                byte[] empty = new Byte[232];
                if (binType == "sav")
                {
                    // Load our Keystream file.
                    keystream = File.ReadAllBytes(savkeypath);
                    // Save file is already loaded.

                    // Get our empty file set up.
                    Array.Copy(keystream, 0x10, empty, 0xE0, 0x4);
                    string nick = eggnames[empty[0xE3] - 1];
                    // Stuff in the nickname to our blank EKX.
                    byte[] nicknamebytes = Encoding.Unicode.GetBytes(nick);
                    Array.Resize(ref nicknamebytes, 24);
                    Array.Copy(nicknamebytes, 0, empty, 0x40, nicknamebytes.Length);
                    // Fix CHK
                    uint chk = 0;
                    for (int i = 8; i < 232; i += 2) // Loop through the entire PKX
                        chk += BitConverter.ToUInt16(empty, i);

                    // Apply New Checksum
                    Array.Copy(BitConverter.GetBytes(chk), 0, empty, 06, 2);
                    empty = encryptArray(empty);
                    Array.Resize(ref empty, 0xE8);
                    boxoffset = BitConverter.ToInt32(keystream, 0x1C);
                }

                // Set our box data offset based on where the file came from
                int binOffset = 0;
                switch (binType)
                {
                    case "sav":
                        // Offset is already 0
                        break;
                        
                    case "yabd":
                        binOffset = 4;
                        byte[] test = new byte[232];
                        Array.Copy(savefile, binOffset, test, 0, 232);
                        if (!verifyCHK(decryptArray(test)))
                            binOffset = 8;
                        break;
                    
                    case "xy":
                        binOffset = 0x1EF38;
                        break;
                        
                    case "oras":
                        binOffset = 0x2F794;
                        byte[] test2 = new byte[232];
                        Array.Copy(savefile, binOffset, test2, 0, 232);
                        if (!verifyCHK(decryptArray(test2)))
                            binOffset = 0x1EF38;
                        break;
                }
                
                // Get our dumping parameters.
                int offset = 0;
                int count = 30;
                int boxstart = 1;
                if (CB_BoxStart.Text == "All")
                    count = 30 * 31;
                else
                {
                    boxoffset += (Convert.ToInt16(CB_BoxStart.Text) - 1) * 30 * 232;
                    offset += (Convert.ToInt16(CB_BoxStart.Text) - 1) * 30 * 232;
                    count = (Convert.ToInt16(CB_BoxEnd.Text) - Convert.ToInt16(CB_BoxStart.Text) + 1) * 30;
                    boxstart = Convert.ToInt16(CB_BoxStart.Text);
                }

                // Get our TSVs for filtering
                ushort tmp = 0;
                selectedTSVs = (from val in Regex.Split(TB_SVs.Text, @"\s*[\s,;.]\s*") where UInt16.TryParse(val, out tmp) select tmp).ToArray();
                
                // Print out the header (if any) and loop through selected boxes
                RTB_SAV.AppendText(toAppend);
                for (int i = 0; i < count; i++)
                {
                    if (i % 30 == 0 && CHK_Split.Checked)
                    {
                        if (i != 0) RTB_SAV.AppendText("\n");
                        
                        // Add Reddit coloring
                        if (CHK_ColorBox.Checked && (CB_ExportStyle.SelectedIndex == 1 || CB_ExportStyle.SelectedIndex == 2 || (CB_ExportStyle.SelectedIndex >= 1 && CB_ExportStyle.SelectedIndex <= 5 && CHK_R_Table.Checked)))
                        {
                            if (CB_BoxColor.SelectedIndex == 0)
                                RTB_SAV.AppendText(boxcolors[1 + ((i / 30 + boxstart) % 4)]);
                            else
                                RTB_SAV.AppendText(boxcolors[CB_BoxColor.SelectedIndex - 1]);
                        }

                        // Append Box Name then Header
                        RTB_SAV.AppendText("Box " + (i / 30 + boxstart).ToString() + "\n\n");
                        if (CHK_Header.Checked) RTB_SAV.AppendText(header + "\n");
                    }

                    // Get the pkx and dump it
                    byte[] pkx = new Byte[232];
                    if (binType == "sav")
                        pkx = fetchpkx(savefile, keystream, boxoffset + i * 232, 0x100 + offset + i * 232, 0x40000 + offset + i * 232, empty);
                    else
                    {
                        Array.Copy(savefile, binOffset + boxoffset + i * 232, pkx, 0, 232);
                        pkx = decryptArray(pkx);
                    }
                    dumpPKX(true, pkx, i, boxstart);
                }
            }

            // Dump the Pokemon data for videos
            else
            {
                RTB_VID.Clear();
                // player @ 0xX100, opponent @ 0x1800;
                byte[] keystream = File.ReadAllBytes(vidkeypath);
                byte[] key = new Byte[260];
                byte[] empty = new Byte[260];
                byte[] emptyekx = encryptArray(empty);
                byte[] ekx = new Byte[260];
                int offset = 0x4E18;
                int keyoff = 0x100;
                if (CB_Team.SelectedIndex == 1)
                {
                    offset = 0x5438;
                    keyoff = 0x800;
                }
                RTB_VID.AppendText(toAppend);
                for (int i = 0; i < 6; i++)
                {
                    Array.Copy(batvideo, offset + 260 * i, ekx, 0, 260);
                    Array.Copy(keystream, keyoff + 260 * i, key, 0, 260);
                    ekx = xortwos(ekx, key);
                    if (verifyCHK(decryptArray(ekx)))
                        dumpPKX(false, decryptArray(ekx), i+1, 0);
                    else
                        dumpPKX(false, null, i, 0);
                }
            }

            // Copy Results to Clipboard
            if (isSAV)
            {
                try { Clipboard.SetText(RTB_SAV.Text); }
                catch { };
                RTB_SAV.AppendText("\nData copied to clipboard!\nDumped: " + dumpedcounter);
                // RTB_SAV.Select(0, RTB_SAV.Text.Length - 1);
                RTB_SAV.ScrollToCaret();
            }
            else
            {
                try { Clipboard.SetText(RTB_VID.Text); }
                catch { };
                RTB_VID.AppendText("\nData copied to clipboard!"); 
                // RTB_VID.Select(0, RTB_VID.Text.Length - 1);
                RTB_VID.ScrollToCaret();
            }

            // Write the CSV file if selected
            if (CB_ExportStyle.SelectedIndex == 6 || CB_ExportStyle.SelectedIndex == 7)
            {
                SaveFileDialog savecsv = new SaveFileDialog();
                savecsv.Filter = "Spreadsheet|*.csv";
                savecsv.FileName = (lastOpenedFilename == "") ? "KeySAV Data Dump.csv" : lastOpenedFilename.Substring(0, lastOpenedFilename.Length - 4) + ".csv";
                if (savecsv.ShowDialog() == DialogResult.OK)
                    System.IO.File.WriteAllText(savecsv.FileName, csvdata, Encoding.UTF8);
            }
        }

        // Hide/show the Filtering group
        private void toggleFilter(object sender, EventArgs e)
        {
            GB_Filter.Visible = CHK_Enable_Filtering.Checked;
            if (CHK_Enable_Filtering.Checked)
            {
                RTB_SAV.Height = this.Height - 205 - 233;
                RTB_SAV.Location = new System.Drawing.Point(0, 334);
            }
            else
            {
                RTB_SAV.Height = this.Height - 205;
                RTB_SAV.Location = new System.Drawing.Point(0, 100);
            }
        }

        // Check sizes of break files
        private void loadBreak1(object sender, EventArgs e)
        {
            // Open Save File
            OpenFileDialog boxsave = new OpenFileDialog();
            boxsave.Filter = "Save File|*.*";
            if (boxsave.ShowDialog() == DialogResult.OK)
            {
                string path = boxsave.FileName;
                byte[] input = File.ReadAllBytes(path);
                if (input.Length == 0x100000 || input.Length == 0x10009C || input.Length == 0x10019A)
                {
                    Array.Copy(input, input.Length % 0x100000, break1, 0, 0x100000);
                    TB_File1.Text = path;
                }
                else
                    MessageBox.Show("Incorrect File Loaded: Not a SAV file (1MB).", "Error");
            } 
            togglebreak();
        }

        private void loadBreakBV1(object sender, EventArgs e)
        {
            // Open Video File
            OpenFileDialog boxsave = new OpenFileDialog();
            boxsave.Filter = "BV File|*.*";
            if (boxsave.ShowDialog() == DialogResult.OK)
            {
                string path = boxsave.FileName;
                byte[] input = File.ReadAllBytes(path);
                if (input.Length == 28256)
                {
                    Array.Copy(input, video1, 28256);
                    TB_FileBV1.Text = path;
                }
                else
                    MessageBox.Show("Incorrect File Loaded: Not a Battle Video (~27.5KB).", "Error");
            } 
            togglebreakBV();
        }

        private void loadBreak2(object sender, EventArgs e)
        {
            // Open Save File
            OpenFileDialog boxsave = new OpenFileDialog();
            boxsave.Filter = "Save File|*.*";
            if (boxsave.ShowDialog() == DialogResult.OK)
            {
                string path = boxsave.FileName;
                byte[] input = File.ReadAllBytes(path);
                if (input.Length == 0x100000 || input.Length == 0x10009C || input.Length == 0x10019A)
                {
                    Array.Copy(input, input.Length % 0x100000, break2, 0, 0x100000); // Force save to 0x100000
                    TB_File2.Text = path;
                }
                else
                    MessageBox.Show("Incorrect File Loaded: Not a SAV file (1MB).", "Error");
            }
            togglebreak();
        }
        
        private void loadBreakBV2(object sender, EventArgs e)
        {
            // Open Video File
            OpenFileDialog boxsave = new OpenFileDialog();
            boxsave.Filter = "BV File|*.*";
            if (boxsave.ShowDialog() == DialogResult.OK)
            {
                string path = boxsave.FileName;
                byte[] input = File.ReadAllBytes(path);
                if (input.Length == 28256)
                {
                    Array.Copy(input, video2, 28256);
                    TB_FileBV2.Text = path;
                }
                else
                    MessageBox.Show("Incorrect File Loaded: Not a Battle Video (~27.5KB).", "Error");
            }
            togglebreakBV();
        }

        private void loadBreak3(object sender, EventArgs e)
        {
            // Open Save File
            OpenFileDialog boxsave = new OpenFileDialog();
            boxsave.Filter = "Save File|*.*";
            if (boxsave.ShowDialog() == DialogResult.OK)
            {
                string path = boxsave.FileName;
                byte[] input = File.ReadAllBytes(path);
                if (input.Length == 0x100000 || input.Length == 0x10009C || input.Length == 0x10019A)
                {
                    Array.Copy(input, input.Length % 0x100000, break3, 0, 0x100000); // Force save to 0x100000
                    TB_File3.Text = path;
                }
                else
                    MessageBox.Show("Incorrect File Loaded: Not a SAV file (1MB).", "Error");
            }
            togglebreak();
        }

        // Enable Break button if all files are loaded
        private void togglebreak()
        {
            B_Break.Enabled = false;
            if (TB_File1.Text != "" && TB_File2.Text != "" && TB_File3.Text != "")
                B_Break.Enabled = true;
        }

        private void togglebreakBV()
        {
            B_BreakBV.Enabled = false;
            if (TB_FileBV1.Text != "" && TB_FileBV2.Text != "")
                B_BreakBV.Enabled = true;
        }

        // Enable break button if folder is selected
        private void loadBreakFolder(object sender, EventArgs e)
        {
            FolderBrowserDialog folder = new FolderBrowserDialog();
            if (folder.ShowDialog() == DialogResult.OK)
            {
                TB_Folder.Text = folder.SelectedPath;
                B_BreakFolder.Enabled = true;
            }
        }
        
        // This is where the battle video magic happens
        private void breakBV(object sender, EventArgs e)
        {
            // Do Trick
            byte[] ezeros = encryptArray(new Byte[260]);
            byte[] xorstream = new Byte[260 * 6];
            byte[] breakstream = new Byte[260 * 6];
            byte[] bvkey = new Byte[0x1000];

            // Validity Check to see what all is participating...
            Array.Copy(video1, 0x4E18, breakstream, 0, 260 * 6);

            // XOR them together at party offset
            for (int i = 0; i < (260 * 6); i++)
                xorstream[i] = (byte)(breakstream[i] ^ video2[i + 0x4E18]);

            // Retrieve EKX_1's data
            byte[] ekx1 = new Byte[260];
            for (int i = 0; i < (260); i++)
                ekx1[i] = (byte)(xorstream[i + 260] ^ ezeros[i]);
            for (int i = 0; i < 260; i++)
                xorstream[i] ^= ekx1[i];

            // If old exploit does not properly decrypt slot1...
            byte[] pkx = decryptArray(ekx1);
            if (!verifyCHK(pkx))
            { MessageBox.Show("Improperly set up Battle Videos. Please follow directions and try again", "Error"); return; }

            // Start filling up our key...
            // Copy in the unique CTR encryption data to ID the video...
            Array.Copy(video1, 0x10, bvkey, 0, 0x10);

            // Copy unlocking data
            byte[] key1 = new Byte[260]; Array.Copy(video1, 0x4E18, key1, 0, 260);
            Array.Copy(xortwos(ekx1, key1), 0, bvkey, 0x100, 260);
            Array.Copy(video1, 0x4E18 + 260, bvkey, 0x100 + 260, 260*5); // XORstream from save1 has just keystream.
                
            // See if Opponent first slot can be decrypted...
            Array.Copy(video1, 0x5438, breakstream, 0, 260 * 6);

            // XOR them together at party offset
            for (int i = 0; i < (260 * 6); i++)
                xorstream[i] = (byte)(breakstream[i] ^ video2[i + 0x5438]);

            // XOR through the empty data for the encrypted zero data.
            for (int i = 0; i < (260 * 5); i++)
                bvkey[0x100 + 260 + i] ^= ezeros[i % 260];

            // Retrieve EKX_2's data
            byte[] ekx2 = new Byte[260];
            for (int i = 0; i < (260); i++)
                ekx2[i] = (byte)(xorstream[i + 260] ^ ezeros[i]);
            for (int i = 0; i < 260; i++)
                xorstream[i] ^= ekx2[i];
            byte[] key2 = new Byte[260]; Array.Copy(video1,0x5438,key2,0,260);
            byte[] pkx2 = decryptArray(ekx2);
            if (verifyCHK(decryptArray(ekx2)) && (BitConverter.ToUInt16(pkx2,0x8) != 0))
            {
                Array.Copy(xortwos(ekx2,key2), 0, bvkey, 0x800, 260);
                Array.Copy(video1, 0x5438 + 260, bvkey, 0x800 + 260, 260 * 5); // XORstream from save1 has just keystream.
                for (int i = 0; i < (260 * 5); i++)
                    bvkey[0x800 + 260 + i] ^= ezeros[i % 260];
                MessageBox.Show("Can dump from Opponent Data on this key too!");
            }

            // Show some info and save the keystream
            string ot = TrimFromZero(Encoding.Unicode.GetString(pkx, 0xB0, 24));
            ushort tid = BitConverter.ToUInt16(pkx, 0xC);
            ushort sid = BitConverter.ToUInt16(pkx, 0xE);
            ushort tsv = (ushort)((tid ^ sid) >> 4);
            DialogResult sdr = MessageBox.Show(String.Format("Success!\nYour first Pokemon's TSV: {0}\nOT: {1}\n\nClick OK to save your keystream.", tsv.ToString("0000"),ot), "Prompt", MessageBoxButtons.OKCancel);
            if (sdr == DialogResult.OK)
            {
                FileInfo fi = new FileInfo(TB_FileBV1.Text);
                string bvnumber = Regex.Split(fi.Name, "(-)")[0];
                string newPath = CleanFileName(String.Format("BV Key - {0}.bin", bvnumber));
                newPath = Path.Combine(path_exe, "data", newPath);
                bool doit = true;
                if (File.Exists(newPath))
                {
                    DialogResult sdr2 = MessageBox.Show("Keystream already exists!\n\nOverwrite?", "Prompt", MessageBoxButtons.YesNo);
                    if (sdr2 == DialogResult.Yes)
                        File.Delete(newPath);
                    else
                    {
                        doit = false;
                        MessageBox.Show("Chose not to save keystream.", "Alert");
                    }
                }
                if (doit)
                {
                    File.WriteAllBytes(newPath, bvkey);
                    MessageBox.Show("Keystream saved to file:\n\n" + newPath, "Alert");
                }
            }
            else
            {
                MessageBox.Show("Chose not to save keystream.", "Alert");
            }
        }

        // This is where magic happens for encrypted saves!
        private void breakSAV(object sender, EventArgs e)
        {
            int[] offset = new int[2];
            byte[] empty = new Byte[232];
            byte[] emptyekx = new Byte[232];
            byte[] ekxdata = new Byte[232];
            byte[] pkx = new Byte[232];
            byte[] slotsKey = new byte[0];
            byte[] save1Save = break1;

            // Do Break. Let's first do some sanity checking to find out the 2 offsets we're dumping from.
            // Loop through save file to find
            int fo = savefile.Length / 2 + 0x20000; // Initial Offset, can tweak later.
            int success = 0;
            string result = "";
            for (int d = 0; d < 2; d++)
            {
                // Do this twice to get both box offsets.
                for (int i = fo; i < 0xEE000; i++)
                {
                    int err = 0;
                    // Start at findoffset and see if it matches pattern
                    if ((break1[i + 4] == break2[i + 4]) && (break1[i + 4 + 232] == break2[i + 4 + 232]))
                    {
                        // Sanity Placeholders are the same
                        for (int j = 0; j < 4; j++)
                            if (break1[i + j] == break2[i + j])
                                err++;
                        if (err < 4)
                        {
                            // Keystream ^ PID doesn't match entirely. Keep checking.
                            for (int j = 8; j < 232; j++)
                                if (break1[i + j] == break2[i + j])
                                    err++;
                            if (err < 20)
                            {
                                // Tolerable amount of difference between offsets. We have a result.
                                offset[d] = i;
                                break;
                            }
                        }
                    }
                }
                fo = offset[d] + 232 * 30;  // Fast forward out of this box to find the next.
            }

            // Now that we have our two box offsets...
            // Check to see if we actually have them.
            if ((offset[0] == 0) || (offset[1] == 0))
            {
                // We have a problem. Don't continue.
                result = "Unable to Find Box.\n";
            }
            else
            {
                // Let's go deeper. We have the two box offsets.
                // Chunk up the base streams.
                byte[,] estream1 = new Byte[30, 232];
                byte[,] estream2 = new Byte[30, 232];
                // Stuff 'em.
                for (int i = 0; i < 30; i++)    // Times we're iterating
                {
                    for (int j = 0; j < 232; j++)   // Stuff the Data
                    {
                        estream1[i, j] = break1[offset[0] + 232 * i + j];
                        estream2[i, j] = break2[offset[1] + 232 * i + j];
                    }
                }

                // Okay, now that we have the encrypted streams, formulate our EKX.
                string nick = eggnames[1];
                // Stuff in the nickname to our blank EKX.
                byte[] nicknamebytes = Encoding.Unicode.GetBytes(nick);
                Array.Resize(ref nicknamebytes, 24);
                Array.Copy(nicknamebytes, 0, empty, 0x40, nicknamebytes.Length);

                // Encrypt the Empty PKX to EKX.
                Array.Copy(empty, emptyekx, 232);
                emptyekx = encryptArray(emptyekx);
                // Not gonna bother with the checksum, as this empty file is temporary.

                // Sweet. Now we just have to find the E0-E3 values. Let's get our polluted streams from each.
                // Save file 1 has empty box 1. Save file 2 has empty box 2.
                byte[,] pstream1 = new Byte[30, 232]; // Polluted Keystream 1
                byte[,] pstream2 = new Byte[30, 232]; // Polluted Keystream 2
                for (int i = 0; i < 30; i++)    // Times we're iterating
                {
                    for (int j = 0; j < 232; j++)   // Stuff the Data
                    {
                        pstream1[i, j] = (byte)(estream1[i, j] ^ emptyekx[j]);
                        pstream2[i, j] = (byte)(estream2[i, j] ^ emptyekx[j]);
                    }
                }

                // Cool. So we have a fairly decent keystream to roll with. We now need to find what the E0-E3 region is.
                // 0x00000000 Encryption Constant has the D block last. 
                // We need to make sure our Supplied Encryption Constant Pokemon have the D block somewhere else (Pref in 1 or 3).

                // First, let's get out our polluted EKX's.
                byte[,] polekx = new Byte[6, 232];
                for (int i = 0; i < 6; i++)
                    for (int j = 0; j < 232; j++) // Save file 1 has them in the second box. XOR them out with the Box2 Polluted Stream
                        polekx[i, j] = (byte)(break1[offset[1] + 232 * i + j] ^ pstream2[i, j]);
                uint[] encryptionconstants = new uint[6]; // Array for all 6 Encryption Constants. 
                int valid = 0;
                for (int i = 0; i < 6; i++)
                {
                    encryptionconstants[i] = (uint)polekx[i, 0];
                    encryptionconstants[i] += (uint)polekx[i, 1] * 0x100;
                    encryptionconstants[i] += (uint)polekx[i, 2] * 0x10000;
                    encryptionconstants[i] += (uint)polekx[i, 3] * 0x1000000;
                    // EC Obtained. Check to see if Block D is not last.
                    if (getDloc(encryptionconstants[i]) != 3)
                    {
                        valid++;
                        // Find the Origin/Region data.
                        byte[] encryptedekx = new Byte[232];
                        byte[] decryptedpkx = new Byte[232];
                        for (int z = 0; z < 232; z++)
                            encryptedekx[z] = polekx[i, z];
                        decryptedpkx = decryptArray(encryptedekx);

                        // finalize data
                        // Okay, now that we have the encrypted streams, formulate our EKX.
                        nick = eggnames[decryptedpkx[0xE3] - 1];
                        // Stuff in the nickname to our blank EKX.
                        nicknamebytes = Encoding.Unicode.GetBytes(nick);
                        Array.Resize(ref nicknamebytes, 24);
                        Array.Copy(nicknamebytes, 0, empty, 0x40, nicknamebytes.Length);

                        // Dump it into our Blank EKX. We have won!
                        empty[0xE0] = decryptedpkx[0xE0];
                        empty[0xE1] = decryptedpkx[0xE1];
                        empty[0xE2] = decryptedpkx[0xE2];
                        empty[0xE3] = decryptedpkx[0xE3];
                        break;
                    }
                }

                if (valid == 0) // We didn't get any valid EC's where D was not in last. Tell the user to try again with different specimens.
                    result = "The 6 supplied Pokemon are not suitable. \nRip new saves with 6 different ones that originated from your save file.\n";
                else
                {
                    // We can continue to get our actual keystream.
                    // Let's calculate the actual checksum of our empty pkx.
                    uint chk = 0;
                    for (int i = 8; i < 232; i += 2) // Loop through the entire PKX
                        chk += BitConverter.ToUInt16(empty, i);

                    // Apply New Checksum
                    Array.Copy(BitConverter.GetBytes(chk), 0, empty, 06, 2);

                    // Okay. So we're now fixed with the proper blank PKX. Encrypt it!
                    Array.Copy(empty, emptyekx, 232);
                    emptyekx = encryptArray(emptyekx);
                    Array.Resize(ref emptyekx, 232); // ensure it's 232 bytes.

                    // Empty EKX obtained. Time to set up our key file.
                    savkey = new Byte[0xB4AD4];
                    // Copy over 0x10-0x1F (Save Encryption Unused Data so we can track data).
                    Array.Copy(break1, 0x10, savkey, 0, 0x10);
                    // Include empty data
                    savkey[0x10] = empty[0xE0]; savkey[0x11] = empty[0xE1]; savkey[0x12] = empty[0xE2]; savkey[0x13] = empty[0xE3];
                    // Copy over the scan offsets.
                    Array.Copy(BitConverter.GetBytes(offset[0]), 0, savkey, 0x1C, 4);
                    for (int i = 0; i < 30; i++)    // Times we're iterating
                    {
                        for (int j = 0; j < 232; j++)   // Stuff the Data temporarily...
                        {
                            savkey[0x100 + i * 232 + j] = (byte)(estream1[i, j] ^ emptyekx[j]);
                            savkey[0x100 + (30 * 232) + i * 232 + j] = (byte)(estream2[i, j] ^ emptyekx[j]);
                        }
                    }

                    // Let's extract some of the information now for when we set the Keystream filename.
                    byte[] data1 = new Byte[232];
                    byte[] data2 = new Byte[232];
                    for (int i = 0; i < 232; i++)
                    {
                        data1[i] = (byte)(savkey[0x100 + i] ^ break1[offset[0] + i]);
                        data2[i] = (byte)(savkey[0x100 + i] ^ break2[offset[0] + i]);
                    }
                    byte[] data1a = new Byte[232]; byte[] data2a = new Byte[232];
                    Array.Copy(data1, data1a, 232); Array.Copy(data2, data2a, 232);
                    byte[] pkx1 = decryptArray(data1);
                    byte[] pkx2 = decryptArray(data2);
                    ushort chk1 = 0;
                    ushort chk2 = 0;
                    for (int i = 8; i < 232; i += 2)
                    {
                        chk1 += BitConverter.ToUInt16(pkx1, i);
                        chk2 += BitConverter.ToUInt16(pkx2, i);
                    }
                    if (verifyCHK(pkx1) && Convert.ToBoolean(BitConverter.ToUInt16(pkx1, 8)))
                    {
                        // Save 1 has the box1 data
                        pkx = pkx1;
                        success = 1;
                    }
                    else if (verifyCHK(pkx2) && Convert.ToBoolean(BitConverter.ToUInt16(pkx2, 8)))
                    {
                        // Save 2 has the box1 data
                        pkx = pkx2;
                        success = 1;
                    }
                    else
                    {
                        // Data isn't decrypting right...
                        for (int i = 0; i < 232; i++)
                        {
                            data1a[i] ^= empty[i];
                            data2a[i] ^= empty[i];
                        }
                        pkx1 = decryptArray(data1a); pkx2 = decryptArray(data2a);
                        if (verifyCHK(pkx1) && Convert.ToBoolean(BitConverter.ToUInt16(pkx1, 8)))
                        {
                            // Save 1 has the box1 data
                            pkx = pkx1;
                            success = 1;
                        }
                        else if (verifyCHK(pkx2) && Convert.ToBoolean(BitConverter.ToUInt16(pkx2, 8)))
                        {
                            // Save 2 has the box1 data
                            pkx = pkx2;
                            success = 1;
                        }
                        else
                        {
                            // Sigh...
                        }
                    }
                }
            }
            if (success == 1)
            {
                byte[] diff1 = new byte[31*30*232];
                byte[] diff2 = new byte[31*30*232];
                for(uint i = 0; i < 31*30*232; ++i)
                {
                    diff1[i] = (byte)(break1[offset[0] + i] ^ break1[offset[0] + i - 0x7F000]);
                }
                for(uint i = 0; i < 31*30*232; ++i)
                {
                    diff2[i] = (byte)(break2[offset[0] + i] ^ break2[offset[0] + i - 0x7F000]);
                }
                if (diff1.SequenceEqual(diff2))
                {
                    bool break3is1 = true;
                    for(uint i = (uint)offset[0]; i<offset[0] + 31*30*232; ++i)
                    {
                        if(!(break2[i] == break3[i]))
                        {
                            break3is1 = false;
                            break;
                        }
                    }
                    if (break3is1) save1Save = break3;
                    slotsKey = diff1;
                }
                else success = 0;
            }
            if (success == 1)
            {
                // Markup the save to know that boxes 1 & 2 are dumpable.
                savkey[0x20] = 3; // 00000011 (boxes 1 & 2)

                // Clear the keystream file...
                for (int i = 0; i < 31; i++)
                {
                    Array.Copy(zerobox, 0, savkey, 0x00100 + i * (232 * 30), 232 * 30);
                    Array.Copy(zerobox, 0, savkey, 0x40000 + i * (232 * 30), 232 * 30);
                }

                // Copy the key for the slot selector
                Array.Copy(save1Save, 0x168, savkey, 0x80000, 4);

                // Copy the key for the other save slot
                Array.Copy(slotsKey, 0, savkey, 0x80004, 232*30*31);

                // Since we don't know if the user put them in in the wrong order, let's just markup our keystream with data.
                byte[] data1 = new Byte[232];
                byte[] data2 = new Byte[232];
                for (int i = 0; i < 31; i++)
                {
                    for (int j = 0; j < 30; j++)
                    {
                        Array.Copy(break1, offset[0] + i * (232 * 30) + j * 232, data1, 0, 232);
                        Array.Copy(break2, offset[0] + i * (232 * 30) + j * 232, data2, 0, 232);
                        if (data1.SequenceEqual(data2))
                        {
                            // Just copy data1 into the key file.
                            Array.Copy(data1, 0, savkey, 0x00100 + i * (232 * 30) + j * 232, 232);
                        }
                        else
                        {
                            // Copy both datas into their keystream spots.
                            Array.Copy(data1, 0, savkey, 0x00100 + i * (232 * 30) + j * 232, 232);
                            Array.Copy(data2, 0, savkey, 0x40000 + i * (232 * 30) + j * 232, 232);
                        }
                    }
                }

                // Save file diff is done, now we're essentially done. Save the keystream.
                result = "Keystreams were successfully bruteforced!\n\n";
                result += "Click OK to save your keystream.";
                DialogResult sdr = MessageBox.Show(result, "Prompt", MessageBoxButtons.OKCancel);
                if (sdr == DialogResult.OK)
                {
                    // From our PKX data, fetch some details to name our key file...
                    string ot = TrimFromZero(Encoding.Unicode.GetString(pkx, 0xB0, 24));
                    ushort tid = BitConverter.ToUInt16(pkx, 0xC);
                    ushort sid = BitConverter.ToUInt16(pkx, 0xE);
                    ushort tsv = (ushort)((tid ^ sid) >> 4);
                    string newPath = CleanFileName(String.Format("SAV Key - {0} - ({1}.{2}) - TSV {3}.bin", ot, tid.ToString("00000"), sid.ToString("00000"), tsv.ToString("0000")));
                    newPath = Path.Combine(path_exe, "data", newPath);
                    bool doit = true;
                    if (File.Exists(newPath))
                    {
                        DialogResult sdr2 = MessageBox.Show("Keystream already exists!\n\nOverwrite?", "Prompt", MessageBoxButtons.YesNo);
                        if (sdr2 == DialogResult.Yes)
                            File.Delete(newPath);
                        else
                        {
                            doit = false;
                            MessageBox.Show("Chose not to save keystream.", "Alert");
                        }
                    }
                    if (doit)
                    {
                        File.WriteAllBytes(newPath, savkey);
                        MessageBox.Show("Keystream saved to file:\n\n" + newPath, "Alert");
                    }
                }
                else
                {
                    MessageBox.Show("Chose not to save keystream.", "Alert");
                }
            }
            else // Failed
                MessageBox.Show(result + "Keystreams were NOT bruteforced!\n\nStart over and try again :(");
        }

        // Utility
        private byte[] xortwos(byte[] arr1, byte[] arr2)
        {
            if (arr1.Length != arr2.Length) return null;
            byte[] arr3 = new Byte[arr1.Length];
            for (int i = 0; i < arr1.Length; i++)
                arr3[i] = (byte)(arr1[i] ^ arr2[i]);
            return arr3;
        }

        private static string TrimFromZero(string input)
        {
            int index = input.IndexOf('\0');
            if (index < 0)
                return input;

            return input.Substring(0, index);
        }

        private static string CleanFileName(string fileName)
        {
            return Path.GetInvalidFileNameChars().Aggregate(fileName, (current, c) => current.Replace(c.ToString(), string.Empty));
        }

        private static FileInfo GetNewestFile(DirectoryInfo directory)
        {
            return directory.GetFiles()
                .Union(directory.GetDirectories().Select(d => GetNewestFile(d)))
                .OrderByDescending(f => (f == null ? DateTime.MinValue : f.LastWriteTime))
                .FirstOrDefault();
        }

        // Get the header string
        private string getHeaderString(string format, bool isSAV)
        {
            // Only output Row,Col for CSV output for SAVs
            string slotString = (isSAV && (CB_ExportStyle.SelectedIndex == 6 || CB_ExportStyle.SelectedIndex == 7)) ? "Row,Col" : "Slot";

            return String.Format(format, "Box", slotString, "Species", "Gender", "Nature", "Ability", "HP", "ATK", "DEF", "SPA", "SPD", "SPE", "HiddenPower", "ESV", "TSV", "Nickname", "OT", "Ball", "TID", "SID", "HP EV", "ATK EV", "DEF EV", "SPA EV", "SPD EV", "SPE EV", "Move 1", "Move 2", "Move 3", "Move 4", "Relearn 1", "Relearn 2", "Relearn 3", "Relearn 4", "Shiny", "Egg", "Level", "Region", "Country", "Held Item", "Language", "Game", "Slot", "PID", "Mark", "Dex Number", "Form", "1", "2", "3", "4", "5", "6", "IVs", "IV Sum", "EV Sum", "Egg Received", "Met/Hatched", "Exp", "Count", "Infected", "Cured", "OTG", "Met Level", "Friendship", "Affection", "Steps to Hatch", "Ball", "HA");
        }

        // SD Detection
        private void changedetectgame(object sender, EventArgs e)
        {
            game = CB_Game.SelectedIndex;
            myTimer.Start();
        }

        private void detectMostRecent()
        {
            // Fetch the selected save file and video
            if (game == 0)
            {
                // X
                savpath = Path.Combine(path_3DS, "title", "00040000", "00055d00", "data"); 
                vidpath = Path.Combine(path_3DS, "extdata", "00000000", "0000055d", "00000000"); 
            }
            else if (game == 1)
            {
                // Y
                savpath = Path.Combine(path_3DS, "title", "00040000", "00055e00", "data"); 
                vidpath = Path.Combine(path_3DS, "extdata", "00000000", "0000055e", "00000000"); 
            }
            else if (game == 2) 
            {
                // OR
                savpath = Path.Combine(path_3DS, "title", "00040000", "0011c400", "data");
                vidpath = Path.Combine(path_3DS, "extdata", "00000000", "000011c4", "00000000");
            }
            else if (game == 3)
            {
                // AS
                savpath = Path.Combine(path_3DS, "title", "00040000", "0011c500", "data");
                vidpath = Path.Combine(path_3DS, "extdata", "00000000", "000011c5", "00000000");
            }

            // Go ahead and open the save automatically if found
            if (Directory.Exists(savpath))
            {
                if (File.Exists(Path.Combine(savpath,"00000001.sav")))
                    this.Invoke(new MethodInvoker(delegate { openSAV(Path.Combine(savpath, "00000001.sav")); }));
            }

            // Fetch the latest video
            if (Directory.Exists(vidpath))
            {
                try
                {
                    FileInfo BV = GetNewestFile(new DirectoryInfo(vidpath));
                    if (BV.Length == 28256)
                    { this.Invoke(new MethodInvoker(delegate { openVID(BV.FullName); })); }
                }
                catch { }
            }
        }

        private void find3DS()
        {
            // start by checking if the 3DS file path exists or not.
            string[] DriveList = Environment.GetLogicalDrives();
            for (int i = 1; i < DriveList.Length; i++)
            {
                path_3DS = DriveList[i] + "Nintendo 3DS";
                if (Directory.Exists(path_3DS))
                    break;
                path_3DS = null;
            }
            if (path_3DS == null) // No 3DS SD Card Detected
                return;
            else
            {
                // 3DS data found in SD card reader. Let's get the title folder location!
                string[] folders = Directory.GetDirectories(path_3DS, "*", System.IO.SearchOption.AllDirectories);

                // Loop through all the folders in the Nintendo 3DS folder to see if any of them contain 'title'.
                for (int i = 0; i < folders.Length; i++)
                {
                    DirectoryInfo di = new DirectoryInfo(folders[i]);
                    if (di.Name == "title" || di.Name == "extdata")
                    {
                        path_3DS = di.Parent.FullName.ToString();
                        myTimer.Stop();
                        detectMostRecent();
                        return;
                    }
                }
            }
        }

        // UI Prompted Updates
        private void changeboxsetting(object sender, EventArgs e)
        {
            CB_BoxEnd.Visible = CB_BoxEnd.Enabled = L_BoxThru.Visible = !(CB_BoxStart.Text == "All");
            if (CB_BoxEnd.Enabled)
            {
                int start = Convert.ToInt16(CB_BoxStart.Text);
                int oldValue = 0;
                try {oldValue = Convert.ToInt16(CB_BoxEnd.SelectedItem); } catch {oldValue = 1;}
                CB_BoxEnd.Items.Clear();
                for (int i = start; i < 32; i++)
                    CB_BoxEnd.Items.Add(i.ToString());
                CB_BoxEnd.SelectedIndex = (start >= oldValue ? 0 : oldValue-start);
            }
        }

        private void B_ShowOptions_Click(object sender, EventArgs e)
        {
            Help.GetHelp.Show();
        }

        private void changeExportStyle(object sender, EventArgs e)
        {
            /*
                Default
                Reddit
                TSV
                Custom 1
                Custom 2
                Custom 3
                CSV default
                CSV custom
                To .PK6 File 
             */
            if (CB_ExportStyle.SelectedIndex == 0) // Default
            {
                CHK_BoldIVs.Enabled = CHK_ColorBox.Enabled = CB_BoxColor.Enabled = false;
                CHK_R_Table.Enabled = false;
                CHK_NameQuotes.Enabled = false;
                B_ResetCSV.Enabled = false;
                RTB_OPTIONS.ReadOnly = true; RTB_OPTIONS.Text =
                    "{0} - {1} - {2} ({3}) - {4} - {5} - {6}.{7}.{8}.{9}.{10}.{11} - {12} - {13}";
            }
            else if (CB_ExportStyle.SelectedIndex == 1) // Reddit
            {
                CHK_BoldIVs.Enabled = CHK_ColorBox.Enabled = CB_BoxColor.Enabled = true;
                CHK_R_Table.Enabled = false;
                CHK_R_Table.Checked = true;
                CHK_NameQuotes.Enabled = false;
                B_ResetCSV.Enabled = false;
                RTB_OPTIONS.ReadOnly = true; RTB_OPTIONS.Text =
                "{0} | {1} | {2} ({3}) | {4} | {5} | {6}.{7}.{8}.{9}.{10}.{11} | {12} | {13} |";
            }
            else if (CB_ExportStyle.SelectedIndex == 2) // TSV
            {
                CHK_BoldIVs.Enabled = CHK_ColorBox.Enabled = CB_BoxColor.Enabled = true;
                CHK_R_Table.Enabled = false;
                CHK_R_Table.Checked = true;
                CHK_NameQuotes.Enabled = false;
                B_ResetCSV.Enabled = false;
                RTB_OPTIONS.ReadOnly = true; RTB_OPTIONS.Text =
                "{0} | {1} | {16} | {18} | {14} |";
            }
            else if (CB_ExportStyle.SelectedIndex == 3) // Custom 1
            {
                CHK_NameQuotes.Enabled = false;
                B_ResetCSV.Enabled = false;
                CHK_R_Table.Enabled = true; CHK_R_Table.Checked = custom1b;
                CHK_BoldIVs.Enabled = CHK_ColorBox.Enabled = CB_BoxColor.Enabled = true;
                RTB_OPTIONS.ReadOnly = false;
                RTB_OPTIONS.Text = custom1;
            }
            else if (CB_ExportStyle.SelectedIndex == 4) // Custom 2
            {
                CHK_NameQuotes.Enabled = false;
                B_ResetCSV.Enabled = false;
                CHK_R_Table.Enabled = true; CHK_R_Table.Checked = custom2b;
                CHK_BoldIVs.Enabled = CHK_ColorBox.Enabled = CB_BoxColor.Enabled = true;
                RTB_OPTIONS.ReadOnly = false;
                RTB_OPTIONS.Text = custom2;
            }
            else if (CB_ExportStyle.SelectedIndex == 5) // Custom 3
            {
                CHK_NameQuotes.Enabled = false;
                B_ResetCSV.Enabled = false;
                CHK_R_Table.Enabled = true; CHK_R_Table.Checked = custom3b;
                CHK_BoldIVs.Enabled = CHK_ColorBox.Enabled = CB_BoxColor.Enabled = true;
                RTB_OPTIONS.ReadOnly = false;
                RTB_OPTIONS.Text = custom3;
            }
            else if (CB_ExportStyle.SelectedIndex == 6) // CSV
            {
                CHK_BoldIVs.Enabled = CHK_ColorBox.Enabled = CB_BoxColor.Enabled = false;
                CHK_R_Table.Enabled = false;
                B_ResetCSV.Enabled = false;
                CHK_NameQuotes.Enabled = true;
                RTB_OPTIONS.ReadOnly = true; RTB_OPTIONS.Text ="{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21},{22},{23},{24},{25},{26},{27},{28},{29},{30},{31},{32},{33},{34},{35}";
            }
            else if (CB_ExportStyle.SelectedIndex == 7) // CSV custom
            {
                CHK_BoldIVs.Enabled = CHK_ColorBox.Enabled = CB_BoxColor.Enabled = false;
                CHK_R_Table.Enabled = false;
                CHK_NameQuotes.Enabled = true;
                RTB_OPTIONS.ReadOnly = false;
                B_ResetCSV.Enabled = true;
                // If nothing is saved, fill with all columns by default
                RTB_OPTIONS.Text = (customcsv == "") ? defaultCSVcustom : customcsv;
            }
            else if (CB_ExportStyle.SelectedIndex == 8) // PK6
            {
                CHK_BoldIVs.Enabled = CHK_ColorBox.Enabled = CB_BoxColor.Enabled = false;
                CHK_R_Table.Enabled = false;
                CHK_NameQuotes.Enabled = false;
                B_ResetCSV.Enabled = false;
                RTB_OPTIONS.ReadOnly = true; RTB_OPTIONS.Text =
                "Files will be saved in .PK6 format, and the default method will display.";
            }
            
            // Update the format preview on format change
            updatePreview();
        }

        private void changeFormatText(object sender, EventArgs e)
        {
            if (CB_ExportStyle.SelectedIndex == 3) // Custom 1
                custom1 = RTB_OPTIONS.Text;
            else if (CB_ExportStyle.SelectedIndex == 4) // Custom 2
                custom2 = RTB_OPTIONS.Text;
            else if (CB_ExportStyle.SelectedIndex == 5) // Custom 3
                custom3 = RTB_OPTIONS.Text;
            else if (CB_ExportStyle.SelectedIndex == 7) // CSV custom
                customcsv = RTB_OPTIONS.Text;
            
            // Update format preview whenever it's changed
            updatePreview();
        }

        private void changeTableStatus(object sender, EventArgs e)
        {
            if (CB_ExportStyle.SelectedIndex == 3) // Custom 1
                custom1b = CHK_R_Table.Checked;
            else if (CB_ExportStyle.SelectedIndex == 4) // Custom 2
                custom2b = CHK_R_Table.Checked;
            else if (CB_ExportStyle.SelectedIndex == 5) // Custom 3
                custom3b = CHK_R_Table.Checked;
        }

        private void changeReadOnly(object sender, EventArgs e)
        {
            RichTextBox rtb = sender as RichTextBox;
            if (rtb.ReadOnly) rtb.BackColor = Color.FromKnownColor(KnownColor.Control);
            else rtb.BackColor = Color.FromKnownColor(KnownColor.White);
        }

        // Update Text Format Preview
        private void updatePreview()
        {
            // Catch a format exception to let the user finish typing formats
            try { RTB_Preview.Text = getHeaderString(RTB_OPTIONS.Text, true); }
            catch { };
        }

        // Translation
        private void changeLanguage(object sender, EventArgs e)
        {
            InitializeStrings();
        }

        private string[] getStringList(string f, string l)
        {
            object txt = Properties.Resources.ResourceManager.GetObject("text_" + f + "_" + l); // Fetch File, \n to list.
            List<string> rawlist = ((string)txt).Split(new char[] { '\n' }).ToList();
            string[] stringdata = new string[rawlist.Count];
            for (int i = 0; i < rawlist.Count; i++)
                stringdata[i] = rawlist[i].Trim();
            return stringdata;
        }

        private void InitializeStrings()
        {
            int curAbility;
            if (CB_Abilities.Text != "")
            {
                try
                {
                    for (curAbility = 0; abilitylist[curAbility] != CB_Abilities.Text; ++curAbility) ;
                }
                catch
                {
                    curAbility = -1;
                }
            }
            else
                curAbility = -1;
            string[] lang_val = { "en", "ja", "fr", "it", "de", "es", "ko" };
            string l = lang_val[CB_MainLanguage.SelectedIndex];
            natures = getStringList("Natures", l);
            types = getStringList("Types", l);
            abilitylist = getStringList("Abilities", l);
            movelist = getStringList("Moves", l);
            itemlist = getStringList("Items", l);
            specieslist = getStringList("Species", l);
            formlist = getStringList("Forms", l);
            countryList = getStringList("Countries", l);
            regionList = getStringList("Regions", l);
            gameList = getStringList("Games", l);
            expGrowth = getStringList("expGrowth", "all");
            int[] ballindex = {
                                  0,1,2,3,4,5,6,7,8,9,0xA,0xB,0xC,0xD,0xE,0xF,0x10,
                                  0x1EC,0x1ED,0x1EE,0x1EF,0x1F0,0x1F1,0x1F2,0x1F3,
                                  0x240 
                              };
            balls = new string[ballindex.Length];
            for (int i = 0; i < ballindex.Length; i++)
                balls[i] = itemlist[ballindex[i]];

            // vivillon pattern list
            vivlist = new string[20];
            vivlist[0] = formlist[666];
            for (int i = 1; i < 20; i++)
                vivlist[i] = formlist[835+i];

            // Populate natures in filters
            if (CCB_Natures.Items.Count == 0)
            {
                CCB_Natures.Items.Add(new CCBoxItem("All", 0));
                for (byte i = 0; i < natures.Length;)
                    CCB_Natures.Items.Add(new CCBoxItem(natures[i], ++i));
                CCB_Natures.DisplayMember = "Name";
                CCB_Natures.SetItemChecked(0, true);
            }
            else
            {
                for (byte i = 0; i < natures.Length; ++i)
                    (CCB_Natures.Items[i+1] as CCBoxItem).Name = natures[i];
            }

            // Populate HP types in filters
            if (CCB_HPType.Items.Count == 0)
            {
                CCB_HPType.Items.Add(new CCBoxItem("Any", 0));
                for (byte i = 1; i < types.Length-1;)
                    CCB_HPType.Items.Add(new CCBoxItem(types[i], ++i));
                CCB_HPType.DisplayMember = "Name";
                CCB_HPType.SetItemChecked(0, true);
            }
            else
            {
                for (byte i = 1; i < types.Length-1; ++i)
                    (CCB_HPType.Items[i] as CCBoxItem).Name = types[i];
            }

            // Populate ability list
            string[] sortedAbilities = (string[])abilitylist.Clone();
            Array.Sort(sortedAbilities);
            CB_Abilities.Items.Clear();
            CB_Abilities.Items.AddRange(sortedAbilities);
            if (curAbility != -1) CB_Abilities.Text = abilitylist[curAbility];
        }
        
        // Based on method in PkHex
        private int getLevel(int species, int exp)
        {
            if (exp == 0) return 1;
            int growth = Convert.ToInt16(expGrowth[species]);
            
            // Iterate upwards to find the level above our current level
            int level = 0; // Initial level, immediately incremented before loop
            while ((uint)expTable[++level][growth] <= exp)
            {
                if (level == 100)
                    return level;
                // After we find the level above ours, we're done
            }
            return --level;
        }
        
        // Structs
        public class Structures
        {
            public struct PKX
            {
                public uint EC, PID, IV32,

                    exp,
                    HP_EV, ATK_EV, DEF_EV, SPA_EV, SPD_EV, SPE_EV,
                    HP_IV, ATK_IV, DEF_IV, SPE_IV, SPA_IV, SPD_IV,
                    cnt_cool, cnt_beauty, cnt_cute, cnt_smart, cnt_tough, cnt_sheen,
                    markings, hptype;

                public string
                    nicknamestr, notOT, ot, genderstring;

                public int
                    ability, abilitynum, nature, feflag, genderflag, altforms, PKRS_Strain, PKRS_Duration,
                    metlevel, otgender;

                public bool
                    isegg, isnick, isshiny;

                public ushort
                    species, helditem, TID, SID, TSV, ESV,
                    move1, move2, move3, move4,
                    move1_pp, move2_pp, move3_pp, move4_pp,
                    move1_ppu, move2_ppu, move3_ppu, move4_ppu,
                    eggmove1, eggmove2, eggmove3, eggmove4,
                    chk,

                    OTfriendship, OTaffection,
                    egg_year, egg_month, egg_day,
                    met_year, met_month, met_day,
                    eggloc, metloc,
                    ball, encountertype,
                    gamevers, countryID, regionID, dsregID, otlang;

                public PKX(byte[] pkx)
                {
                    nicknamestr = "";
                    notOT = "";
                    ot = "";
                    EC = BitConverter.ToUInt32(pkx, 0);
                    chk = BitConverter.ToUInt16(pkx, 6);
                    species = BitConverter.ToUInt16(pkx, 0x08);
                    helditem = BitConverter.ToUInt16(pkx, 0x0A);
                    TID = BitConverter.ToUInt16(pkx, 0x0C);
                    SID = BitConverter.ToUInt16(pkx, 0x0E);
                    exp = BitConverter.ToUInt32(pkx, 0x10);
                    ability = pkx[0x14];
                    abilitynum = pkx[0x15];
                    // 0x16, 0x17 - unknown
                    PID = BitConverter.ToUInt32(pkx, 0x18);
                    nature = pkx[0x1C];
                    feflag = pkx[0x1D] % 2;
                    genderflag = (pkx[0x1D] >> 1) & 0x3;
                    altforms = (pkx[0x1D] >> 3);
                    HP_EV = pkx[0x1E];
                    ATK_EV = pkx[0x1F];
                    DEF_EV = pkx[0x20];
                    SPA_EV = pkx[0x22];
                    SPD_EV = pkx[0x23];
                    SPE_EV = pkx[0x21];
                    cnt_cool = pkx[0x24];
                    cnt_beauty = pkx[0x25];
                    cnt_cute = pkx[0x26];
                    cnt_smart = pkx[0x27];
                    cnt_tough = pkx[0x28];
                    cnt_sheen = pkx[0x29];
                    markings = pkx[0x2A];
                    PKRS_Strain = pkx[0x2B] >> 4;
                    PKRS_Duration = pkx[0x2B] % 0x10;

                    // Block B
                    nicknamestr = TrimFromZero(Encoding.Unicode.GetString(pkx, 0x40, 24));
                    // 0x58, 0x59 - unused
                    move1 = BitConverter.ToUInt16(pkx, 0x5A);
                    move2 = BitConverter.ToUInt16(pkx, 0x5C);
                    move3 = BitConverter.ToUInt16(pkx, 0x5E);
                    move4 = BitConverter.ToUInt16(pkx, 0x60);
                    move1_pp = pkx[0x62];
                    move2_pp = pkx[0x63];
                    move3_pp = pkx[0x64];
                    move4_pp = pkx[0x65];
                    move1_ppu = pkx[0x66];
                    move2_ppu = pkx[0x67];
                    move3_ppu = pkx[0x68];
                    move4_ppu = pkx[0x69];
                    eggmove1 = BitConverter.ToUInt16(pkx, 0x6A);
                    eggmove2 = BitConverter.ToUInt16(pkx, 0x6C);
                    eggmove3 = BitConverter.ToUInt16(pkx, 0x6E);
                    eggmove4 = BitConverter.ToUInt16(pkx, 0x70);

                    // 0x72 - Super Training Flag - Passed with pkx to new form

                    // 0x73 - unused/unknown
                    IV32 = BitConverter.ToUInt32(pkx, 0x74);
                    HP_IV = IV32 & 0x1F;
                    ATK_IV = (IV32 >> 5) & 0x1F;
                    DEF_IV = (IV32 >> 10) & 0x1F;
                    SPE_IV = (IV32 >> 15) & 0x1F;
                    SPA_IV = (IV32 >> 20) & 0x1F;
                    SPD_IV = (IV32 >> 25) & 0x1F;
                    isegg = Convert.ToBoolean((IV32 >> 30) & 1);
                    isnick = Convert.ToBoolean((IV32 >> 31));

                    // Block C
                    notOT = TrimFromZero(Encoding.Unicode.GetString(pkx, 0x78, 24));
                    bool notOTG = Convert.ToBoolean(pkx[0x92]);
                    // Memory Editor edits everything else with pkx in a new form

                    // Block D
                    ot = TrimFromZero(Encoding.Unicode.GetString(pkx, 0xB0, 24));
                    // 0xC8, 0xC9 - unused
                    OTfriendship = pkx[0xCA];
                    OTaffection = pkx[0xCB]; // Handled by Memory Editor
                    // 0xCC, 0xCD, 0xCE, 0xCF, 0xD0
                    egg_year = pkx[0xD1];
                    egg_month = pkx[0xD2];
                    egg_day = pkx[0xD3];
                    met_year = pkx[0xD4];
                    met_month = pkx[0xD5];
                    met_day = pkx[0xD6];
                    // 0xD7 - unused
                    eggloc = BitConverter.ToUInt16(pkx, 0xD8);
                    metloc = BitConverter.ToUInt16(pkx, 0xDA);
                    ball = pkx[0xDC];
                    metlevel = pkx[0xDD] & 0x7F;
                    otgender = (pkx[0xDD]) >> 7;
                    encountertype = pkx[0xDE];
                    gamevers = pkx[0xDF];
                    countryID = pkx[0xE0];
                    regionID = pkx[0xE1];
                    dsregID = pkx[0xE2];
                    otlang = pkx[0xE3];

                    if (genderflag == 0)
                        genderstring = "♂";
                    else if (genderflag == 1)
                        genderstring = "♀";
                    else genderstring = "-";

                    hptype = (15 * ((HP_IV & 1) + 2 * (ATK_IV & 1) + 4 * (DEF_IV & 1) + 8 * (SPE_IV & 1) + 16 * (SPA_IV & 1) + 32 * (SPD_IV & 1))) / 63 + 1;

                    TSV = (ushort)((TID ^ SID) >> 4);
                    ESV = (ushort)(((PID >> 16) ^ (PID & 0xFFFF)) >> 4);

                    isshiny = (TSV == ESV);
                }
            }
        }

        // UI button actions
        private void B_BKP_SAV_Click(object sender, EventArgs e)
        {
            TextBox tb = TB_SAV;
            FileInfo fi = new FileInfo(tb.Text);
            DateTime dt = fi.LastWriteTime;
            int year = dt.Year;
            int month = dt.Month;
            int day = dt.Day;
            int hour = dt.Hour;
            int minute = dt.Minute;
            int second = dt.Second;
            string bkpdate = year.ToString("0000") + month.ToString("00") + day.ToString("00") + hour.ToString("00") + minute.ToString("00") + second.ToString("00") + " ";
            string newpath = bakpath + Path.DirectorySeparatorChar + bkpdate + fi.Name;
            if (File.Exists(newpath))
            {
                DialogResult sdr = MessageBox.Show("File already exists!\n\nOverwrite?", "Prompt", MessageBoxButtons.YesNo);
                if (sdr == DialogResult.Yes)
                    File.Delete(newpath);
                else 
                    return;
            }
            File.Copy(tb.Text, newpath);
            MessageBox.Show("Copied to Backup Folder.\n\nFile named:\n" + newpath, "Alert");
        }

        private void B_BKP_BV_Click(object sender, EventArgs e)
        {
            TextBox tb = TB_BV;
            FileInfo fi = new FileInfo(tb.Text);
            DateTime dt = fi.LastWriteTime;
            int year = dt.Year;
            int month = dt.Month;
            int day = dt.Day;
            int hour = dt.Hour;
            int minute = dt.Minute;
            int second = dt.Second;
            string bkpdate = year.ToString("0000") + month.ToString("00") + day.ToString("00") + hour.ToString("00") + minute.ToString("00") + second.ToString("00") + " ";
            string newpath = bakpath + Path.DirectorySeparatorChar + bkpdate + fi.Name;
            if (File.Exists(newpath))
            {
                DialogResult sdr = MessageBox.Show("File already exists!\n\nOverwrite?", "Prompt", MessageBoxButtons.YesNo);
                if (sdr == DialogResult.Yes)
                    File.Delete(newpath);
                else 
                    return;
            }
            File.Copy(tb.Text, newpath);
            MessageBox.Show("Copied to Backup Folder.\n\nFile named:\n" + newpath, "Alert");
        }

        private void B_BreakFolder_Click(object sender, EventArgs e)
        {
            int i = 0;
            DialogResult sdr = MessageBox.Show("This will improve your keystream by scanning saves in the folder you selected.\n\nThis may take some time. Press OK to continue.", "Prompt", MessageBoxButtons.OKCancel);
            if (sdr == DialogResult.OK)
            {
                byte[] savefile = new byte[0x10009C];
                string savkeypath = "";
                binType = "sav";
                string[] files = Directory.GetFiles(TB_Folder.Text);
                FolderBar.Maximum = files.Length;
                FolderBar.Step = 1;
                foreach (string path in files)
                {
                    i += openSAV_(path, ref savefile, ref savkeypath, false);
                    FolderBar.PerformStep();
                }
            }
            MessageBox.Show("Processed " + i + " saves in folder:\n\n" + TB_Folder.Text, "Prompt");
            FolderBar.Value = 0;
        }

        private void B_ResetCSV_Click(object sender, EventArgs e)
        {
            DialogResult box = MessageBox.Show("This will erase your current CSV custom format and replace it with the default CSV custom format, which includes ALL columns.\n\nContinue?", "Warning", MessageBoxButtons.YesNo);
            if (box == DialogResult.Yes)
            {
                customcsv = defaultCSVcustom;
                RTB_OPTIONS.Text = defaultCSVcustom;
                updatePreview();
                return;
            }
            else return;
        }

        private void toggleIVAll(object sender, EventArgs e)
        {
            if(updateIVCheckboxes)
                switch ((new [] {CHK_IV_HP, CHK_IV_Atk, CHK_IV_Def, CHK_IV_SpAtk, CHK_IV_SpDef, CHK_IV_Spe}).Count(c => c.Checked))
                {
                    case 0:
                        CHK_IVsAny.CheckState = CheckState.Unchecked;
                        break;
                    case 6:
                        CHK_IVsAny.CheckState = CheckState.Checked;
                        break;
                    default:
                        CHK_IVsAny.CheckState = CheckState.Indeterminate;
                        break;
                }
        }

        private void toggleIVsAny(object sender, EventArgs e)
        {
            updateIVCheckboxes = false;
            if (CHK_IVsAny.CheckState != CheckState.Indeterminate)
                foreach (var box in new [] {CHK_IV_HP, CHK_IV_Atk, CHK_IV_Def, CHK_IV_SpAtk, CHK_IV_SpDef, CHK_IV_Spe})
                    box.Checked = CHK_IVsAny.Checked;
            updateIVCheckboxes = true;
        }

        private void toggleTrickroom(object sender, EventArgs e)
        {
            CHK_IV_Spe.Anchor = (AnchorStyles.Top | AnchorStyles.Left);
            CHK_IV_Spe.Text = (CHK_Trickroom.Checked ? "Spe (= 0)" : "Speed");
            CHK_IV_Spe.Anchor = (AnchorStyles.Top | AnchorStyles.Right);
        }

        private void toggleSpecialAttacker(object sender, EventArgs e)
        {
            CHK_IV_Atk.Anchor = (AnchorStyles.Top | AnchorStyles.Left);
            CHK_IV_Atk.Text = (CHK_Special_Attacker.Checked ? "Atk (= 0)" : "Attack");
            CHK_IV_Atk.Anchor = (AnchorStyles.Top | AnchorStyles.Right);
        }
    }
}
