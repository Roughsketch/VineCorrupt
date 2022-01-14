using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System.Reflection;
using System.ComponentModel;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Windows.Controls.Primitives;
using System.Threading;
using Microsoft.Win32;


namespace VineCorrupt
{
    public class DialogInfo
    {
        private string m_filter = "";
        private string m_extension = "";

        public DialogInfo(string filter, string extension)
        {
            m_filter = filter;
            m_extension = extension;
        }

        public string Filter()
        {
            return m_filter;
        }

        public string Extension()
        {
            return m_extension;
        }
    }

    public class MetaInfo
    {
        private string m_corruption = "";
        private string m_extension = "";
        private bool m_files = false;
        private Func<string> m_func = null;
        private DialogInfo m_info = null;

        public MetaInfo(string corruption, string extension, DialogInfo info, bool has_files = false)
        {
            m_corruption = corruption;
            m_extension = extension;
            m_files = has_files;
            m_info = info;
        }

        public MetaInfo(string corruption, DialogInfo info, Func<string> func, bool has_files = false)
        {
            m_corruption = corruption;
            m_func = func;
            m_files = has_files;
            m_info = info;
        }

        public string Corruption()
        {
            return m_corruption;
        }

        public string Extension()
        {
            if (m_func == null)
            {
                return m_extension;
            }
            else
            {
                return m_func();
            }
        }

        public bool HasFiles()
        {
            return m_files;
        }

        public string DefaultExt()
        {
            return m_info.Extension();
        }

        public string Filter()
        {
            return m_info.Filter();
        }
    }

    public class DiscEntry
    {
        public string name;
        public int id;
        public int parent;

        public string Name() { return name; }
        public int Id() { return id; }
        public int Parent() { return parent; }

        public static int RootId() { return 0; }
    }

    public class DiscEntryList
    {
        public List<DiscEntry> directories;
        public List<DiscEntry> files;

        public List<DiscEntry> Directories() { return directories; }
        public List<DiscEntry> Files() { return files; }
    }

    public class GamecubeEntry
    {
        public string name;
        public int id;
        public int parent;
        public string type;

        public string Name() { return name; }
        public int Id() { return id; }
        public int Parent() { return parent; }
        public bool IsDir() { return type == "d"; }
        public bool IsFile() { return type == "f"; }

        public static int RootId() { return 0; }
    }

    public class GamecubeEntryList
    {
        public List<GamecubeEntry> entries;

        public List<GamecubeEntry> Files() { return entries; }
    }

    public class NDSEntryList
    {
        public string[] entries;

        public string[] Files() { return entries; }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region External Declarations
        public delegate IntPtr LowLevelKeyboardProc(int nCode, UIntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook,
            LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            UIntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll")]
        public static extern uint GetLastError();

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)] /* unnecessary, isn't it? */
        static extern bool FreeLibrary(IntPtr hModule);
        #endregion

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private LowLevelKeyboardProc _proc;
        //private static IntPtr _hookID = IntPtr.Zero;

        private IntPtr winHandle;
        private Dictionary<string, Hotkey> hkGlobals;
        private bool hkNESPRG;  //  Determines whether a hotkey will act on PRG or CHR section
                                //  true = PRG, false = CHR
        private Process emulator;   //  Keep track of current emulator so we can automatically close it.

        private string Version = "0.8.7";
        public MainWindow()
        {
            //ComboBoxFix.Initialize();
            InitializeComponent();

            IntPtr handle = LoadLibrary("MSVCP120.dll");

            //  If handle is null then the Visual C++ redist was not installed (or some other error)
            if (handle == IntPtr.Zero)
            {
                var result = MessageBox.Show("Could not load MSVCP120.dll.\n\nYou must install the VC++ redist package found on the downloads page to run the corrupter.\n\n" +
                    "Do you want to open the download page now?", "VineCorrupt", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    Process.Start("http://www.maiddog.com/projects/corrupter/download.php");
                }

                Environment.Exit(0);
            }
            else
            {
                //  If it loaded successfully then we need to free it
                FreeLibrary(handle);
            }


            //  Check the system for the .NET 4.5 runtime
            try
            {
                //  Open the v4 registry entry
                using (RegistryKey ndpKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
           RegistryView.Registry32).OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\"))
                {
                    //  If value Release doesn't exist then 4.5 is not installed.
                    if (ndpKey == null || ndpKey.GetValue("Release") == null)
                    {
                        var result = MessageBox.Show("You do not have .NET 4.5 installed which is required for this program to run properly. " +
                            "You can find a download link to the .NET 4.5 framework on the downloads page.\n\n Do you want to open the download page now?",
                            "VineCorrupt", MessageBoxButton.YesNo, MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            Process.Start("http://www.maiddog.com/projects/corrupter/download.php");
                        }

                        Environment.Exit(0);
                    }
                }
            }
            catch
            {
                var result = MessageBox.Show("There was an error determining what version of .NET is running. This program requires " +
                    ".NET 4.5 to run properly so if you don't have .NET 4.5 or don't know which version you have then you can find a link to it on the download page." +
                    "Do you want to open the download page now?", "VineCorrupt", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    Process.Start("http://www.maiddog.com/projects/corrupter/download.php");
                }
            }


            tabNES.Tag = new MetaInfo("NES", ".nes", new DialogInfo("NES ROM (*.nes,*.zip)|*.nes;*.zip", ".nes"));
            tabSNES.Tag = new MetaInfo("SNES", ".sfc", new DialogInfo("SNES ROM (*.smc,*.sfc,*.zip)|*.smc;*.sfc;*.zip", ".smc|.sfc"));
            tabN64.Tag = new MetaInfo("N64", ".z64", new DialogInfo("N64 ROM (*.z64,*.n64,*.v64,*.zip)|*.z64;*.n64;*.v64;*.zip", ".z64"));
            tabNDS.Tag = new MetaInfo("NDS", ".nds", new DialogInfo("NDS Image (*.nds,*.zip)|*.nds", ".nds;*.zip"), true);
            tabGamecube.Tag = new MetaInfo("Gamecube", ".gcm", new DialogInfo("GameCube Image (*.gcm,*.iso)|*.gcm;*.iso", ".gcm"), true);
            tabWii.Tag = new MetaInfo("Wii", ".wbfs", new DialogInfo("Wii Image (*.iso,*.wbfs)|*.iso;*.wbfs", ".wbfs"), true);
            tabGameboy.Tag = new MetaInfo("Gameboy", new DialogInfo("Game Boy ROM (*.gb,*.gbc,*.gba,*.zip)|*.gb;*.gbc;*.gba;*.zip", ".gbc"), delegate
            {
                if (txtGameboyFile.Text.EndsWith(".gba"))
                {
                    return ".gba";
                }
                else
                {
                    return ".gbc";
                }
            });
            tabPlaystation.Tag = new MetaInfo("Playstation", ".img", new DialogInfo("PSX Image (*.img,*.bin)|*.img;*.bin", ".img|.bin"), true);
            tabGenesis.Tag = new MetaInfo("Genesis", ".md", new DialogInfo("Genesis ROM (*.md,*.smd,*.bin)|*.md;*.smd;*.bin", ".md|.smd|.bin"));
            tabMiscellaneous.Tag = new MetaInfo("Miscellaneous", "", new DialogInfo("", ""), true);

            hkGlobals = new Dictionary<string, Hotkey>();
            hkNESPRG = true;

            //_proc = Hotkey_Pressed;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory("bin");
                Directory.CreateDirectory("games/nds");
                Directory.CreateDirectory("games/gc");
                Directory.CreateDirectory("games/wii");
                Directory.CreateDirectory("log");
                Directory.CreateDirectory("wit");

                //  Write the main corruption executable to a local exe so we can run it
                File.WriteAllBytes("bin/updater.exe", VineCorrupt.Properties.Resources.Updater);
                File.WriteAllBytes("bin/mdcorrupt.exe", VineCorrupt.Properties.Resources.mdcorrupt);
                File.WriteAllBytes("bin/mdgcm.exe", VineCorrupt.Properties.Resources.mdgcm);
                File.WriteAllBytes("bin/mdnds.exe", VineCorrupt.Properties.Resources.mdnds);

                if (Properties.Settings.Default.CheckWIT && !File.Exists("wit.exe") && !File.Exists("wit/wit.exe"))
                {
                    var result = MessageBox.Show("To do Wii corruptions you must download a third party tool called WIT which handles Wii discs.\n\n" +
                                                 "Do you want to download and extract the files required?", "VineCorrupt", MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        Updater.GetWIT();

                        if (File.Exists("wit.exe"))
                        {
                            MessageBox.Show("WIT has been downloaded and extracted.");
                        }
                        else
                        {
                            MessageBox.Show("WIT could not be extracted automatically. You must download it manually " +
                                "from the downloads page and extract the contents to the corrupter's directory to use Wii features.");
                        }
                    }
                    else
                    {
                        result = MessageBox.Show("Do you want to stop being asked this question?", "VineCorrupt", MessageBoxButton.YesNo, MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            Properties.Settings.Default.CheckWIT = false;
                            Properties.Settings.Default.Save();
                            MessageBox.Show("Will do.", "VineCorrupt");
                        }
                    }
                }

                //  If they had an older version, delete the files
                if (File.Exists("md_corrupter.exe"))
                {
                    File.Delete("md_corrupter.exe");
                }

                if (File.Exists("updater.exe"))
                {
                    File.Delete("updater.exe");
                }

                //  Delete files with old names
                if (File.Exists("bin/md_corrupter.exe"))
                {
                    File.Delete("bin/md_corrupter.exe");
                }

                if (File.Exists("bin/gcm.exe"))
                {
                    File.Delete("bin/gcm.exe");
                }

                //  If WIT was just downloaded, move it to the correct directory.
                if (File.Exists("wit.exe"))
                {
                    File.Move("wit.exe", "wit/wit.exe");
                }

                if (File.Exists("wit_source.txt"))
                {
                    File.Move("wit_source.txt", "wit/wit_source.txt");
                }

                if (File.Exists("cyggcc_s-1.dll"))
                {
                    File.Move("cyggcc_s-1.dll", "wit/cyggcc_s-1.dll");
                }

                if (File.Exists("cygwin1.dll"))
                {
                    File.Move("cygwin1.dll", "wit/cygwin1.dll");
                }

                if (File.Exists("cygz.dll"))
                {
                    File.Move("cygz.dll", "wit/cygz.dll");
                }

                //  Restore previous saved emulators
                txtNESEmulator.Text = Properties.Settings.Default.NESEmulator;
                txtSNESEmulator.Text = Properties.Settings.Default.SNESEmulator;
                txtN64Emulator.Text = Properties.Settings.Default.N64Emulator;
                txtNDSEmulator.Text = Properties.Settings.Default.NDSEmulator;
                txtGamecubeEmulator.Text = Properties.Settings.Default.GamecubeEmulator;
                txtGameboyEmulator.Text = Properties.Settings.Default.GameboyEmulator;
                txtWiiEmulator.Text = Properties.Settings.Default.WiiEmulator;
                txtPlaystationEmulator.Text = Properties.Settings.Default.PlaystationEmulator;
                txtGenesisEmulator.Text = Properties.Settings.Default.GenesisEmulator;

                winHandle = new WindowInteropHelper(this).Handle;
                //_hookID = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, LoadLibrary("user32.dll"), 0);

                //if (GetLastError() != 0)
                //{
                //    MessageBox.Show("SetWindowsHookEx error: " + GetLastError().ToString() + "\nHotkeys will not work.");
                //}

                if (Properties.Settings.Default.Hotkeys != "")
                {
                    var hotkeys = JsonConvert.DeserializeObject<Dictionary<string, string>>(Properties.Settings.Default.Hotkeys);

                    hkGlobals["corrupt"] = new Hotkey(hotkeys["corrupt"]);
                    hkGlobals["inc_step"] = new Hotkey(hotkeys["inc_step"]);
                    hkGlobals["dec_step"] = new Hotkey(hotkeys["dec_step"]);
                    hkGlobals["inc_value"] = new Hotkey(hotkeys["inc_value"]);
                    hkGlobals["dec_value"] = new Hotkey(hotkeys["dec_value"]);
                    hkGlobals["change_corruption"] = new Hotkey(hotkeys["change_corruption"]);
                    hkGlobals["inc_start"] = new Hotkey(hotkeys["inc_start"]);
                    hkGlobals["dec_start"] = new Hotkey(hotkeys["dec_start"]);
                    hkGlobals["inc_end"] = new Hotkey(hotkeys["inc_end"]);
                    hkGlobals["dec_end"] = new Hotkey(hotkeys["dec_end"]);
                    hkGlobals["nes_swap"] = new Hotkey(hotkeys["nes_swap"]);

                    txtCorrupt.Text = hkGlobals["corrupt"].Original();
                    txtIncStep.Text = hkGlobals["inc_step"].Original();
                    txtDecStep.Text = hkGlobals["dec_step"].Original();
                    txtIncValue.Text = hkGlobals["inc_value"].Original();
                    txtDecValue.Text = hkGlobals["dec_value"].Original();
                    txtChangeCorruption.Text = hkGlobals["change_corruption"].Original();
                    txtIncStart.Text = hkGlobals["inc_start"].Original();
                    txtDecStart.Text = hkGlobals["dec_start"].Original();
                    txtIncEnd.Text = hkGlobals["inc_end"].Original();
                    txtDecEnd.Text = hkGlobals["dec_end"].Original();
                    txtSwapNES.Text = hkGlobals["nes_swap"].Original();
                }
                else
                {
                    hkGlobals["corrupt"] = new Hotkey("");
                    hkGlobals["inc_step"] = new Hotkey("");
                    hkGlobals["dec_step"] = new Hotkey("");
                    hkGlobals["inc_value"] = new Hotkey("");
                    hkGlobals["dec_value"] = new Hotkey("");
                    hkGlobals["change_corruption"] = new Hotkey("");
                    hkGlobals["inc_start"] = new Hotkey("");
                    hkGlobals["dec_start"] = new Hotkey("");
                    hkGlobals["inc_end"] = new Hotkey("");
                    hkGlobals["dec_end"] = new Hotkey("");
                    hkGlobals["nes_swap"] = new Hotkey("");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Corrupter: " + ex.Message + "\n" + ex.StackTrace);
                Environment.Exit(0);
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            //UnhookWindowsHookEx(_hookID);
            Environment.Exit(0);
        }

        private int GetValue(TextBox text)
        {
            return int.Parse(text.Text, System.Globalization.NumberStyles.HexNumber);
        }

        private void SetValue(TextBox text, int value)
        {
            text.Text = Convert.ToInt32(value).ToString("X");
        }

        private void SetValue(TextBox text, string value)
        {
            try
            {
                text.Text = Convert.ToInt32(value, 16).ToString("X");
            }
            catch(System.OverflowException ex)
            {
                MessageBox.Show("You cannot enter a number above the 32 bit integer limit.");
            }
        }

        #region Button and Control Logic

        private void btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (Updater.IsUpdateAvailable(Version))
            {
                MessageBoxResult result = MessageBox.Show("You do not have the latest version. Do you want to update now?", "Updater", MessageBoxButton.YesNo);

                if (result == MessageBoxResult.Yes)
                {
                    Updater.Update();
                }
            }
            else
            {
                MessageBox.Show("No updates available.");
            }
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();

            MetaInfo info = (tabMain.SelectedItem as TabItem).Tag as MetaInfo;

            dialog.DefaultExt = info.DefaultExt();
            dialog.Filter = info.Filter();

            Nullable<bool> result = dialog.ShowDialog();

            if (result == true)
            {
                //  Get the text box that relates to the current tab
                string txtName = (tabMain.SelectedItem as TabItem).Name.Replace("tab", "txt") + "File";
                TextBox text = (tabMain.SelectedItem as TabItem).FindName(txtName) as TextBox;
                Label size = (tabMain.SelectedItem as TabItem).FindName("lbl" + info.Corruption() + "Size") as Label;
                Label total = (tabMain.SelectedItem as TabItem).FindName("lbl" + info.Corruption() + "Total") as Label;

                text.Text = dialog.FileName;
                size.Content = new FileInfo(dialog.FileName).Length.ToString("X8") + "h";
                total.Content = (0).ToString("X8") + "h";
            }
        }

        private void Emulator_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();

            MetaInfo info = (tabMain.SelectedItem as TabItem).Tag as MetaInfo;

            //  Only accept .exe files for emulators
            dialog.DefaultExt = ".exe";
            dialog.Filter = "Executable (*.exe)|*.exe";

            Nullable<bool> result = dialog.ShowDialog();

            if (result == true)
            {
                //  Get the text box that relates to the current tab
                TextBox text = (tabMain.SelectedItem as TabItem).FindName("txt" + info.Corruption() + "Emulator") as TextBox;

                Properties.Settings.Default[info.Corruption() + "Emulator"] = dialog.FileName;
                Properties.Settings.Default.Save();

                text.Text = dialog.FileName;
            }
        }

        private void Extract_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            MetaInfo info = (tabMain.SelectedItem as TabItem).Tag as MetaInfo;

            var result = dialog.ShowDialog();

            if (result == System.Windows.Forms.DialogResult.OK)
            {
                //  Get the text box that relates to the current tab
                TextBox text = (tabMain.SelectedItem as TabItem).FindName("txt" + info.Corruption() + "ExtractTo") as TextBox;

                try
                {
                    Properties.Settings.Default[info.Corruption() + "Emulator"] = dialog.SelectedPath;
                    Properties.Settings.Default.Save();
                }
                catch { }

                text.Text = dialog.SelectedPath;
            }
        }

        private void Increase_Click(object sender, RoutedEventArgs e)
        {
            //  Get the button that was pressed
            Button source = e.Source as Button;

            //  Get the text box that relates to the button.
            //  To relate a button to a textbox it will replace the btn profix with txt, then substring until the first underscore.
            //  (Ex: btnGamecubeStep_Inc relates to txtGamecubeStep)
            TextBox text = (tabMain.SelectedItem as TabItem).FindName(source.Name.Replace("btn", "txt").Substring(0, source.Name.IndexOf('_'))) as TextBox;

            //  Add one to the string and put it back
            SetValue(text, GetValue(text) + 1);
        }

        private void Decrease_Click(object sender, RoutedEventArgs e)
        {
            //  Get the button that was pressed;
            Button source = e.Source as Button;

            //  Get the text box that relates to the button.
            //  To relate a button to a textbox it will replace the btn profix with txt, then substring until the first underscore.
            //  (Ex: btnGamecubeStep_Dec relates to txtGamecubeStep)
            TextBox text = (tabMain.SelectedItem as TabItem).FindName(source.Name.Replace("btn", "txt").Substring(0, source.Name.IndexOf('_'))) as TextBox;

            //  Subtract one from the string and put it back
            SetValue(text, GetValue(text) - 1);
        }

        private void Load_Button_Click(object sender, RoutedEventArgs e)
        {
            TabItem tab = tabMain.SelectedItem as TabItem;
            MetaInfo info = tab.Tag as MetaInfo;

            Grid inputbox = tab.FindName(info.Corruption() + "InputBox") as Grid;
            TextBox input = tab.FindName("txt" + info.Corruption() + "Input") as TextBox;

            try
            {
                input.Text = "";
                inputbox.Visibility = System.Windows.Visibility.Visible;
            }
            catch
            {
                Logger.Error(string.Format("Could not display input box: {0}", info.Corruption()));
                MessageBox.Show("Error displaying input box.");
            }
        }

        private void Submit_Click(object sender, RoutedEventArgs e)
        {
            try
            { 
                if (tabNES.IsSelected == true)
                {
                    NESInputBox.Visibility = System.Windows.Visibility.Collapsed;

                    string[] split = txtNESInput.Text.Split(',');

                    cmbNESPRGType.SelectedIndex = Convert.ToInt32(split[0]);
                    SetValue(txtNESPRGStep, split[1]);
                    cmbNESCHRType.SelectedIndex = Convert.ToInt32(split[2]);
                    SetValue(txtNESCHRStep, split[3]);

                    chkNESPRG.IsChecked = (Convert.ToInt32(split[4]) & 1) != 0;
                    chkNESCHR.IsChecked = (Convert.ToInt32(split[4]) & 2) != 0;

                    SetValue(txtNESPRGStart, split[5]);
                    SetValue(txtNESPRGStop, split[6]);
                    SetValue(txtNESPRGValue, split[7]);

                    SetValue(txtNESCHRValue, split[8]);
                    SetValue(txtNESCHRValue, split[9]);
                    SetValue(txtNESCHRValue, split[10]);
                }
                else
                {
                    try
                    {
                        TabItem tab = tabMain.SelectedItem as TabItem;
                        MetaInfo info = tab.Tag as MetaInfo;

                        Grid inputbox = tab.FindName(info.Corruption() + "InputBox") as Grid;
                        TextBox input = tab.FindName("txt" + info.Corruption() + "Input") as TextBox;
                        TextBox step = tab.FindName("txt" + info.Corruption() + "Step") as TextBox;
                        TextBox start = tab.FindName("txt" + info.Corruption() + "Start") as TextBox;
                        TextBox stop = tab.FindName("txt" + info.Corruption() + "Stop") as TextBox;
                        TextBox value = tab.FindName("txt" + info.Corruption() + "Value") as TextBox;
                        ComboBox type = tab.FindName("cmb" + info.Corruption() + "Type") as ComboBox;

                        inputbox.Visibility = System.Windows.Visibility.Collapsed;

                        string[] data = input.Text.Split(',');

                        type.SelectedIndex = Convert.ToInt32(data[0]);
                        SetValue(step, data[1]);
                        SetValue(start, data[2]);
                        SetValue(stop, data[3]);
                        SetValue(value, data[4]);
                    }
                    catch
                    {
                        MessageBox.Show("Error loading save.");
                    }
                }
            }
            catch
            {
                MessageBox.Show("Invalid code.");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            TabItem tab = tabMain.SelectedItem as TabItem;
            MetaInfo info = tab.Tag as MetaInfo;

            try
            {
                (tab.FindName(info.Corruption() + "InputBox") as Grid).Visibility = System.Windows.Visibility.Collapsed;
            }
            catch
            {
                MessageBox.Show("Error closing input box.");
            }
        }

        private void NES_Save_Button_Click(object sender, RoutedEventArgs e)
        {
            string code = "";

            code += cmbNESPRGType.SelectedIndex + "," + GetValue(txtNESPRGStep).ToString("X") + ",";
            code += cmbNESCHRType.SelectedIndex + "," + GetValue(txtNESCHRStep).ToString("X") + ",";
            code += (Convert.ToInt32(chkNESPRG.IsChecked) + 2 * Convert.ToInt32(chkNESCHR.IsChecked)).ToString() + ",";

            code += GetValue(txtNESPRGStart).ToString("X") + "," + GetValue(txtNESPRGStop).ToString("X") + ",";
            code += GetValue(txtNESPRGValue).ToString("X") + ",";

            code += GetValue(txtNESCHRStart).ToString("X") + "," + GetValue(txtNESCHRStop).ToString("X") + ",";
            code += GetValue(txtNESCHRValue).ToString("X");

            txtNESInput.Text = code;
            NESInputBox.Visibility = System.Windows.Visibility.Visible;

            txtNESInput.Focus();
            txtNESInput.SelectionStart = 0;
            txtNESInput.SelectionLength = txtNESInput.Text.Length;
        }

        private void Save_Button_Click(object sender, RoutedEventArgs e)
        {
            TabItem tab = tabMain.SelectedItem as TabItem;
            MetaInfo info = tab.Tag as MetaInfo;

            ComboBox type = tab.FindName("cmb" + info.Corruption() + "Type") as ComboBox;
            TextBox value = tab.FindName("txt" + info.Corruption() + "Value") as TextBox;
            TextBox step = tab.FindName("txt" + info.Corruption() + "Step") as TextBox;
            TextBox start = tab.FindName("txt" + info.Corruption() + "Start") as TextBox;
            TextBox stop = tab.FindName("txt" + info.Corruption() + "Stop") as TextBox;
            TextBox input = tab.FindName("txt" + info.Corruption() + "Input") as TextBox;
            Grid inputbox = tab.FindName(info.Corruption() + "InputBox") as Grid;

            string code = "";

            try
            {
                code = type.SelectedIndex + "," + GetValue(step).ToString("X") + ",";
                code += GetValue(start).ToString("X") + "," + GetValue(stop).ToString("X") + "," + GetValue(value).ToString("X");

                input.Text = code;
                input.Focus();
                input.SelectionStart = 0;
                input.SelectionLength = input.Text.Length;
                inputbox.Visibility = System.Windows.Visibility.Visible;
            }
            catch
            {
                MessageBox.Show("Error saving.");
            }
        }

        private void Save_ROM_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog save = new Microsoft.Win32.SaveFileDialog();
            MetaInfo info = (tabMain.SelectedItem as TabItem).Tag as MetaInfo;

            if (File.Exists("output" + info.Extension()) == false)
            {
                MessageBox.Show("You must corrupt a rom before you can save it.");
                return;
            }

            save.DefaultExt = info.DefaultExt();
            save.Filter = info.Filter();

            var result = save.ShowDialog();

            if (result == true)
            {
                try
                {
                    if (File.Exists(save.FileName))
                    {
                        File.Delete(save.FileName);
                    }

                    File.Move("output" + info.Extension(), save.FileName);
                }
                catch(Exception ex)
                {
                    Logger.Error(ex.Message);
                    MessageBox.Show("Error saving rom.");
                }
            }
        }

        private void Number_Validation(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !TextBoxTextAllowed(e.Text);
        }

        private void Text_Validation(object sender, TextChangedEventArgs e)
        {
            TextBox textbox = (e.Source as TextBox);
            string text = textbox.Text;

            if (text.Length == 0)
            {
                SetValue(textbox, "0");
            }

            string valid = "";

            for (int i = 0; i < text.Length; i++)
            {
                if (Char.IsSeparator(text[i]) == false)
                {
                    if (text[i] == '-' && i == 0)
                    {
                        valid = "-";
                    }
                    else
                    {
                        try
                        {
                            int.Parse(text[i].ToString(), System.Globalization.NumberStyles.HexNumber);
                            valid += text[i].ToString().ToUpper();
                        }
                        catch { }
                    }
                }
            }

            if (valid == "")
            {
                valid = "0";
            }

            SetValue(textbox, valid);
        }

        private Boolean TextBoxTextAllowed(String Text2)
        {
            return Array.TrueForAll<Char>(Text2.ToCharArray(),
                delegate(Char c) { return Char.IsDigit(c) || c == '-' || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f'); });
        }

        private void txtPlaystationFile_TextChanged(object sender, TextChangedEventArgs e)
        {
            PlaystationLoadingBox.Visibility = System.Windows.Visibility.Visible;

            DiscEntryList obj = null;
            //string json = Corrupter.Corrupt("\"" + txtPlaystationFile.Text + "\" --list --playstation");
            Corrupter c = new Corrupter();

            c.Corrupt("\"" + txtPlaystationFile.Text + "\" --list --playstation");
            string json = c.Output();


            //  Find offset of JSON data
            json = json.Substring(json.IndexOf("JSON File Listing: ") + "JSON File Listing: ".Length);
            //  Cut off any lines after it.
            json = json.Split('\n')[0];

            try
            {
                obj = JsonConvert.DeserializeObject<DiscEntryList>(json);
            }
            catch
            {
                PlaystationLoadingBox.Visibility = System.Windows.Visibility.Collapsed;
                return; // Invalid JSON data.
            }

            treePlaystationFileList.Items.Clear();

            List<TreeViewItem> structure = new List<TreeViewItem>();

            foreach (var dir in obj.Directories())
            {
                TreeViewItem current = null;
                CheckBox newcheck = new CheckBox() { Content = dir.Name() };
                newcheck.Checked += new RoutedEventHandler(TreeChecked);
                newcheck.Unchecked += new RoutedEventHandler(TreeUnchecked);

                current = new TreeViewItem()
                {
                    Header = newcheck,
                    Tag = dir,
                    IsExpanded = dir.Name() == "."
                };

                structure.Add(current);
                newcheck.Tag = current;
            }

            var tree = structure.OrderByDescending(x => (x.Tag as DiscEntry).Parent()).ToList();
            TreeViewItem root = null;

            foreach (TreeViewItem item in tree)
            {
                bool child = false;
                DiscEntry entry = item.Tag as DiscEntry;
                foreach (TreeViewItem item2 in tree)
                {
                    DiscEntry entry2 = item2.Tag as DiscEntry;

                    if (item != item2 && !child && entry.Parent() == entry2.Id())
                    {
                        item2.Items.Add(item);
                        foreach (var file in obj.Files())
                        {
                            if (file.Parent() == entry.Id())
                            {
                                item.Items.Add(new TreeViewItem() { Header = new CheckBox() { Content = file.Name() } });
                            }
                        }
                        child = true;
                    }
                }


                if (!child)
                {
                    treePlaystationFileList.Items.Add(item);
                    root = item;
                }
            }

            foreach (var file in obj.Files())
            {
                if (file.Parent() == DiscEntry.RootId())
                {
                    root.Items.Add(new TreeViewItem() { Header = new CheckBox() { Content = file.Name() } });
                }
            }

            PlaystationLoadingBox.Visibility = System.Windows.Visibility.Collapsed;
        }

        private void txtGamecubeFile_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (File.Exists(txtGamecubeFile.Text) == false)
            {
                return;
            }

            List<string> obj = GCM.Files(txtGamecubeFile.Text);
            treeGamecubeFileList.Items.Clear();

            CheckBox rootcheck = new CheckBox() { Content = "." };
            rootcheck.Checked += new RoutedEventHandler(TreeChecked);
            rootcheck.Unchecked += new RoutedEventHandler(TreeUnchecked);

            TreeViewItem root = new TreeViewItem() { Header = rootcheck };
            rootcheck.Tag = root;

            foreach (string entry in obj)
            {
                bool changed = false;
                TreeViewItem current = root;

                string temp = entry.Replace("./", "");
                string file = entry.Split('/').Last();
                string[] directories = null;

                if (temp.LastIndexOf(file) - 1 > 0)
                {
                    directories = temp.Substring(0, temp.LastIndexOf(file) - 1).Split('/');

                    foreach (var dir in directories)
                    {
                        foreach (var item in current.Items)
                        {
                            if (((item as TreeViewItem).Header as CheckBox).Content.ToString() == dir)
                            {
                                current = item as TreeViewItem;
                                changed = true;
                            }
                        }

                        if (changed == false)
                        {
                            CheckBox newcheck = new CheckBox() { Content = dir };
                            newcheck.Checked += new RoutedEventHandler(TreeChecked);
                            newcheck.Unchecked += new RoutedEventHandler(TreeUnchecked);

                            TreeViewItem newitem = new TreeViewItem() { Header = newcheck };
                            newcheck.Tag = newitem;

                            current.Items.Add(newitem);
                            current = newitem;
                        }

                        changed = false;
                    }
                }

                current.Items.Add(new TreeViewItem() { Header = new CheckBox() { Content = file } });
            }

            root.IsExpanded = true;
            treeGamecubeFileList.Items.Add(root);

            /*
            //GamecubeLoadingBox.Visibility = System.Windows.Visibility.Visible;

            GamecubeEntryList obj = null;
            string json = Corrupter.Corrupt("\"" + txtGamecubeFile.Text + "\" --list --gamecube");

            //  Find offset of JSON data
            json = json.Substring(json.IndexOf("JSON File Listing: ") + "JSON File Listing: ".Length);
            //  Cut off any lines after it.
            json = json.Split('\n')[0];

            try
            {
                obj = JsonConvert.DeserializeObject<GamecubeEntryList>(json);
            }
            catch
            {
                //GamecubeLoadingBox.Visibility = System.Windows.Visibility.Collapsed;
                MessageBox.Show("Invalid JSON Data.");
                return; // Invalid JSON data.
            }

            treeGamecubeFileList.Items.Clear();

            List<TreeViewItem> structure = new List<TreeViewItem>();

            foreach (var dir in obj.Files().OrderByDescending(x => x.Parent()))
            {
                if (dir.IsDir())
                {
                    TreeViewItem current = null;

                    CheckBox newcheck = new CheckBox() { Content = dir.Name() };
                    newcheck.Checked += new RoutedEventHandler(TreeChecked);
                    newcheck.Unchecked += new RoutedEventHandler(TreeUnchecked);

                    current = new TreeViewItem()
                    {
                        Header = newcheck,
                        IsExpanded = dir.Name() == ".",
                        Tag = dir
                    };

                    structure.Add(current);
                    newcheck.Tag = current;
                }
            }

            //var tree = structure.OrderByDescending(x => (x.Content as GamecubeEntry).Parent()).ToList();
            TreeViewItem root = null;

            foreach (TreeViewItem item in structure)
            {
                bool child = false;
                GamecubeEntry entry = item.Tag as GamecubeEntry;
                foreach (TreeViewItem item2 in structure)
                {
                    GamecubeEntry entry2 = item2.Tag as GamecubeEntry;

                    if (item != item2 && !child && entry.Parent() == entry2.Id())
                    {
                        item2.Items.Add(item);
                        foreach (var file in obj.Files())
                        {
                            if (file.IsFile() && file.Parent() == entry.Id())
                            {
                                item.Items.Add(new TreeViewItem() { Header = new CheckBox() { Content = file.Name() } });
                            }
                        }
                        child = true;
                    }
                }


                if (!child)
                {
                    treeGamecubeFileList.Items.Add(item);
                    root = item;
                }
            }

            foreach (var file in obj.Files())
            {
                if (file.IsFile() && file.Parent() == GamecubeEntry.RootId())
                {
                    root.Items.Add(new TreeViewItem() { Header = new CheckBox() { Content = file.Name() } });
                }
            }

            //GamecubeLoadingBox.Visibility = System.Windows.Visibility.Collapsed;
            */
        }

        private void txtWiiFile_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (File.Exists(txtWiiFile.Text) == false)
            {
                return;
            }

            List<string> obj = WIT.Files(txtWiiFile.Text);
            treeWiiFileList.Items.Clear();

            CheckBox rootcheck = new CheckBox() { Content = "." };
            rootcheck.Checked += new RoutedEventHandler(TreeChecked);
            rootcheck.Unchecked += new RoutedEventHandler(TreeUnchecked);

            TreeViewItem root = new TreeViewItem() { Header = rootcheck };
            rootcheck.Tag = root;

            foreach (string entry in obj)
            {
                bool changed = false;
                TreeViewItem current = root;

                string temp = entry.Replace("./files/", "");
                string file = entry.Split('/').Last();
                string[] directories = null;

                if (temp.LastIndexOf(file) - 1 > 0)
                {
                    directories = temp.Substring(0, temp.LastIndexOf(file) - 1).Split('/');

                    foreach (var dir in directories)
                    {
                        foreach (var item in current.Items)
                        {
                            if (((item as TreeViewItem).Header as CheckBox).Content.ToString() == dir)
                            {
                                current = item as TreeViewItem;
                                changed = true;
                            }
                        }

                        if (changed == false)
                        {
                            CheckBox newcheck = new CheckBox() { Content = dir };
                            newcheck.Checked += new RoutedEventHandler(TreeChecked);
                            newcheck.Unchecked += new RoutedEventHandler(TreeUnchecked);

                            TreeViewItem newitem = new TreeViewItem() { Header = newcheck };
                            newcheck.Tag = newitem;

                            current.Items.Add(newitem);
                            current = newitem;
                        }

                        changed = false;
                    }
                }

                current.Items.Add(new TreeViewItem() { Header = new CheckBox() { Content = file } });
            }

            root.IsExpanded = true;
            treeWiiFileList.Items.Add(root);
        }

        private void txtNDSFile_TextChanged(object sender, RoutedEventArgs e)
        {
            NDSEntryList obj = null;
            //string json = Corrupter.Corrupt("\"" + txtNDSFile.Text + "\" --list");
            Corrupter c = new Corrupter();

            c.Corrupt("\"" + txtNDSFile.Text + "\" --list");
            string json = c.Output();

            //  Find offset of JSON data
            json = json.Substring(json.IndexOf("JSON File Listing: ") + "JSON File Listing: ".Length);
            //  Cut off any lines after it.
            json = json.Split('\n')[0];

            try
            {
                obj = JsonConvert.DeserializeObject<NDSEntryList>(json);
            }
            catch
            {
                MessageBox.Show("Invalid JSON Data.");
                return; // Invalid JSON data.
            }

            treeNDSFileList.Items.Clear();

            CheckBox rootcheck = new CheckBox() { Content = "." };
            rootcheck.Checked += new RoutedEventHandler(TreeChecked);
            rootcheck.Unchecked += new RoutedEventHandler(TreeUnchecked);

            TreeViewItem root = new TreeViewItem() { Header = rootcheck };
            rootcheck.Tag = root;

            foreach(string entry in obj.entries)
            {
                bool changed = false;
                TreeViewItem current = root;

                string temp = entry.Replace("./", "");
                string file = entry.Split('/').Last();
                string[] directories = null;

                if (temp.LastIndexOf(file) - 1 > 0)
                {
                    directories = temp.Substring(0, temp.LastIndexOf(file) - 1).Split('/');

                    foreach (var dir in directories)
                    {
                        foreach (var item in current.Items)
                        {
                            if (((item as TreeViewItem).Header as CheckBox).Content.ToString() == dir)
                            {
                                current = item as TreeViewItem;
                                changed = true;
                            }
                        }

                        if (changed == false)
                        {
                            CheckBox newcheck = new CheckBox() { Content = dir };
                            newcheck.Checked += new RoutedEventHandler(TreeChecked);
                            newcheck.Unchecked += new RoutedEventHandler(TreeUnchecked);

                            TreeViewItem newitem = new TreeViewItem() { Header = newcheck };
                            newcheck.Tag = newitem;

                            current.Items.Add(newitem);
                            current = newitem;
                        }

                        changed = false;
                    }
                }

                current.Items.Add(new TreeViewItem() { Header = new CheckBox() { Content = file } });
            }

            root.IsExpanded = true;
            treeNDSFileList.Items.Add(root);
        }

        private void btnMiscAdd_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();

            //  Only accept .exe files for emulators
            dialog.Filter = "All Files (*.*)|*.*";
            dialog.Multiselect = true;

            Nullable<bool> result = dialog.ShowDialog();

            if (result == true)
            {
                foreach(var file in dialog.FileNames)
                {
                    lstMiscFiles.Items.Add(file);
                }
            }
        } 

        private void btnMiscRemove_Click(object sender, RoutedEventArgs e)
        {
            var selected = lstMiscFiles.SelectedItems.Cast<Object>().ToArray();

            foreach (var item in selected)
            {
                lstMiscFiles.Items.Remove(item);
            }
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (chkMiscInPlace.IsChecked == true)
            {
                MessageBox.Show("This will overwrite all of the original files provided above.\n\nMake sure you want to do this.");
            }
        }

        private void btnMiscOutputBrowse_Click(object sender, RoutedEventArgs e)
        {
            //  Extremely hacky way to get a folder dialog.
            //  Need to rework it to use WPF instead of WinForms.

            var dialog = new System.Windows.Forms.FolderBrowserDialog();

            System.Windows.Forms.DialogResult result = dialog.ShowDialog();

            if (result.ToString() == "OK")
            {
                txtMiscOutput.Text = dialog.SelectedPath + "\\";
            }

        }

        #endregion

        #region TreeView File Logic
        private List<string> GetFiles(TreeViewItem item, string parent = "")
        {
            List<string> selected = new List<string>();

            if (item.HasItems)
            {
                foreach (TreeViewItem child in item.Items)
                {
                    selected.AddRange(GetFiles(child, parent + (item.Header as CheckBox).Content as string + "/"));
                }
            }
            else
            {
                CheckBox check = item.Header as CheckBox;

                if (check.IsChecked == true)
                {
                    selected.Add(parent + check.Content as string);
                }
            }

            return selected;
        }

        private void TreeChecked(object sender, EventArgs e)
        {
            TreeViewItem item = (sender as CheckBox).Tag as TreeViewItem;

            foreach (var sub in item.Items)
            {
                TreeViewItem subitem = sub as TreeViewItem;

                ChangeCheck(subitem as TreeViewItem, (item.Header as CheckBox).IsChecked == true);

                if (subitem.Items.Count > 0)
                {
                    foreach (var s in subitem.Items)
                    {
                        ChangeCheck(s as TreeViewItem, (item.Header as CheckBox).IsChecked == true);
                    }
                }
            }
        }

        private void TreeUnchecked(object sender, EventArgs e)
        {
            TreeViewItem item = (sender as CheckBox).Tag as TreeViewItem;

            foreach (var sub in item.Items)
            {
                TreeViewItem subitem = sub as TreeViewItem;

                ChangeCheck(subitem as TreeViewItem, (item.Header as CheckBox).IsChecked == true);

                if (subitem.Items.Count > 0)
                {
                    foreach (var s in subitem.Items)
                    {
                        ChangeCheck(s as TreeViewItem, (item.Header as CheckBox).IsChecked == true);
                    }
                }
            }
        }

        private void ChangeCheck(TreeViewItem item, bool value)
        {
            CheckBox check = item.Header as CheckBox;
            check.IsChecked = value;

            foreach(var subitem in item.Items)
            {
                ChangeCheck(subitem as TreeViewItem, value);
            }
        }
        #endregion

        #region Hotkey Logic
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (chkHotkeyEnabled.IsChecked == false)
            {
                return;
            }

            if (hkGlobals["corrupt"].IsActive())
            {
                Hotkey_Corrupt();
                e.Handled = true;
            }
            else if (hkGlobals["inc_step"].IsActive())
            {
                Hotkey_Increase_Step();
                e.Handled = true;
            }
            else if (hkGlobals["dec_step"].IsActive())
            {
                Hotkey_Decrease_Step();
                e.Handled = true;
            }
            else if (hkGlobals["inc_value"].IsActive())
            {
                Hotkey_Increase_Value();
                e.Handled = true;
            }
            else if (hkGlobals["dec_value"].IsActive())
            {
                Hotkey_Decrease_Value();
                e.Handled = true;
            }
            else if (hkGlobals["change_corruption"].IsActive())
            {
                Hotkey_Change_Corruption();
                e.Handled = true;
            }
            else if (hkGlobals["inc_start"].IsActive())
            {
                Hotkey_Increase_Start();
                e.Handled = true;
            }
            else if (hkGlobals["dec_start"].IsActive())
            {
                Hotkey_Decrease_Start();
                e.Handled = true;
            }
            else if (hkGlobals["inc_end"].IsActive())
            {
                Hotkey_Increase_End();
                e.Handled = true;
            }
            else if (hkGlobals["dec_end"].IsActive())
            {
                Hotkey_Decrease_End();
                e.Handled = true;
            }
            else if (hkGlobals["nes_swap"].IsActive())
            {
                hkNESPRG = !hkNESPRG;
                e.Handled = true;
            }
        }

        private void Hotkey_TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            TextBox textbox = sender as TextBox;

            textbox.Text = "";

            foreach (Key key in Enum.GetValues(typeof(Key)))
            {
                try
                {
                    if (Keyboard.IsKeyDown(key) && (int)key >= 13 && key.ToString().Contains("Oem") == false && key.ToString().Contains("Dbe") == false)
                    {
                        if(textbox.Text == "")
                        {
                            textbox.Text = key.ToString();
                        }
                        else 
                        {
                            textbox.Text += " + " + key.ToString();
                        }
                    }
                }
                catch { }
            }

            e.Handled = true;
        }

        private void Hotkey_TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox control = sender as TextBox;
            string text = control.Text;

            try
            {
                Hotkey hk = new Hotkey(text);

                foreach (var c in LogicalTreeHelper.GetChildren(gridHotkeys))
                {
                    if (c is TextBox && (c as TextBox).Tag != control.Tag && (c as TextBox).Text != "")
                    {
                        Hotkey temp = new Hotkey((c as TextBox).Text);
                        if (hk.Key() == temp.Key() && hk.Modifiers() == temp.Modifiers())
                        {
                            string error = "Cannot assign the same hotkey twice.\n";

                            foreach (var k in hk.Key())
                            {
                                error += k.ToString();
                            }
                            error += "\n";
                            foreach (var k in temp.Key())
                            {
                                error += k.ToString();
                            }

                            MessageBox.Show(error);
                            control.Text =  "";
                            return;
                        }
                    }
                }
                /*
                foreach(var h in hkGlobals)
                {
                    if (h.Value == hk && h.Key != control.Tag.ToString())
                    {
                        MessageBox.Show("Cannot assign the same hotkey twice.");
                        control, "";
                        return;
                    }
                }

                hkGlobals[control.Tag.ToString()] = hk;
                  * */
            }
            catch(Exception ex)
            {
                MessageBox.Show("Invalid hotkey: " + ex.Message);
                control.Text = "";
            }
        }

        private void Save_Hotkeys_Click(object sender, RoutedEventArgs e)
        {
            foreach (var c in LogicalTreeHelper.GetChildren(gridHotkeys))
            {
                if (c is TextBox)
                {
                    TextBox tmp = c as TextBox;
                    hkGlobals[tmp.Tag as string] = new Hotkey(tmp.Text);
                }
            }

            string json = "{";

            foreach(var hk in hkGlobals)
            {
                json += "\"" + hk.Key + "\":\"" + hk.Value.Original() + "\",";
            }

            json = json.Substring(0, json.Length - 1) + "}";

            Properties.Settings.Default.Hotkeys = json;
            Properties.Settings.Default.Save();
        }

        /*
        private IntPtr Hotkey_Pressed(int nCode, UIntPtr wParam, IntPtr lParam)
        {
            if (chkHotkeyEnabled.IsChecked == true && tabHotkeys.IsSelected == false && nCode >= 0 && wParam == (UIntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Key pressed = (Key)vkCode;

                int modifiers = 0;
                Action proc = null;

                if (hkGlobals["corrupt"].IsActive())
                {
                    MessageBox.Show("Corrupt is active");
                    modifiers = hkGlobals["corrupt"].Modifiers();
                    proc = Hotkey_Corrupt;
                }
                else if (hkGlobals["inc_step"].IsActive())
                {
                    modifiers = hkGlobals["inc_step"].Modifiers();
                    proc = Hotkey_Increase_Step;
                }
                else if (hkGlobals["dec_step"].IsActive())
                {
                    modifiers = hkGlobals["dec_step"].Modifiers();
                    proc = Hotkey_Decrease_Step;
                }
                else if (hkGlobals["inc_value"].IsActive())
                {
                    modifiers = hkGlobals["inc_value"].Modifiers();
                    proc = Hotkey_Increase_Value;
                }
                else if (hkGlobals["dec_value"].IsActive())
                {
                    modifiers = hkGlobals["dec_value"].Modifiers();
                    proc = Hotkey_Decrease_Value;
                }
                else if (hkGlobals["change_corruption"].IsActive())
                {
                    modifiers = hkGlobals["change_corruption"].Modifiers();
                    proc = Hotkey_Change_Corruption;
                }
                else if (hkGlobals["inc_start"].IsActive())
                {
                    modifiers = hkGlobals["inc_start"].Modifiers();
                    proc = Hotkey_Increase_Start;
                }
                else if (hkGlobals["dec_start"].IsActive())
                {
                    modifiers = hkGlobals["dec_start"].Modifiers();
                    proc = Hotkey_Decrease_Start;
                }
                else if (hkGlobals["inc_end"].IsActive())
                {
                    modifiers = hkGlobals["inc_end"].Modifiers();
                    proc = Hotkey_Increase_End;
                }
                else if (hkGlobals["dec_end"].IsActive())
                {
                    modifiers = hkGlobals["dec_end"].Modifiers();
                    proc = Hotkey_Decrease_End;
                }
                else
                {
                    return CallNextHookEx(_hookID, nCode, wParam, lParam);
                }

                proc();
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }
        */

        private void Hotkey_Corrupt()
        {
            try
            {
                TabItem tab = (tabMain.SelectedItem as TabItem);
                MetaInfo info = tab.Tag as MetaInfo;

                string type = info.Corruption();

                Button corrupt = tab.FindName("btn" + type + "Corrupt") as Button;
                corrupt.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            }
            catch
            {
                MessageBox.Show("Error running corruption hotkey.");
            }
        }

        private void Hotkey_Increase_Step()
        {
            try
            {
                TabItem tab = (tabMain.SelectedItem as TabItem);
                MetaInfo info = tab.Tag as MetaInfo;

                string type = info.Corruption();
                string nes = "";

                if (tabNES.IsSelected == true)
                {
                    nes = hkNESPRG ? "PRG" : "CHR";
                }

                TextBox step = tab.FindName("txt" + type + nes + "Step") as TextBox;
                SetValue(step, GetValue(step) + 1);
            }
            catch
            {
                //MessageBox.Show("Error running step hotkey.");
            }
        }

        private void Hotkey_Decrease_Step()
        {
            try
            {
                TabItem tab = (tabMain.SelectedItem as TabItem);
                MetaInfo info = tab.Tag as MetaInfo;

                string type = info.Corruption();
                string nes = "";

                if (tabNES.IsSelected == true)
                {
                    nes = hkNESPRG ? "PRG" : "CHR";
                }

                TextBox step = tab.FindName("txt" + type + nes + "Step") as TextBox;
                SetValue(step, GetValue(step) - 1);
            }
            catch
            {
                //MessageBox.Show("Error running step hotkey.");
            }
        }

        private void Hotkey_Increase_Value()
        {
            try
            {
                TabItem tab = (tabMain.SelectedItem as TabItem);
                MetaInfo info = tab.Tag as MetaInfo;

                string type = info.Corruption();
                string nes = "";

                if (tabNES.IsSelected == true)
                {
                    nes = hkNESPRG ? "PRG" : "CHR";
                }

                TextBox value = tab.FindName("txt" + type + nes + "Value") as TextBox;
                SetValue(value, GetValue(value) + 1);
            }
            catch
            {
                //MessageBox.Show("Error running step hotkey.");
            }
        }

        private void Hotkey_Decrease_Value()
        {
            try
            {
                TabItem tab = (tabMain.SelectedItem as TabItem);
                MetaInfo info = tab.Tag as MetaInfo;

                string type = info.Corruption();
                string nes = "";

                if (tabNES.IsSelected == true)
                {
                    nes = hkNESPRG ? "PRG" : "CHR";
                }

                TextBox value = tab.FindName("txt" + type + nes + "Value") as TextBox;
                SetValue(value, GetValue(value) - 1);
            }
            catch
            {
                //MessageBox.Show("Error running step hotkey.");
            }
        }

        private void Hotkey_Change_Corruption()
        {
            try
            {
                TabItem tab = (tabMain.SelectedItem as TabItem);
                MetaInfo info = tab.Tag as MetaInfo;

                string type = info.Corruption();
                string nes = "";

                if (tabNES.IsSelected == true)
                {
                    nes = hkNESPRG ? "PRG" : "CHR";
                }

                ComboBox types = tab.FindName("cmb" + type + nes + "Type") as ComboBox;

                types.SelectedIndex = (types.SelectedIndex + 1) % types.Items.Count;
            }
            catch
            {
                //MessageBox.Show("Error running step hotkey.");
            }
        }

        private void Hotkey_Increase_Start()
        {
            try
            {
                TabItem tab = (tabMain.SelectedItem as TabItem);
                MetaInfo info = tab.Tag as MetaInfo;

                string type = info.Corruption();
                string nes = "";

                if (tabNES.IsSelected == true)
                {
                    nes = hkNESPRG ? "PRG" : "CHR";
                }

                TextBox start = tab.FindName("txt" + type + nes + "Start") as TextBox;
                SetValue(start, GetValue(start) + 1);
            }
            catch
            {
                //MessageBox.Show("Error running step hotkey.");
            }
        }

        private void Hotkey_Decrease_Start()
        {
            try
            {
                TabItem tab = (tabMain.SelectedItem as TabItem);
                MetaInfo info = tab.Tag as MetaInfo;

                string type = info.Corruption();
                string nes = "";

                if (tabNES.IsSelected == true)
                {
                    nes = hkNESPRG ? "PRG" : "CHR";
                }

                TextBox stop = tab.FindName("txt" + type + nes + "Start") as TextBox;
                SetValue(stop, GetValue(stop) - 1);
            }
            catch
            {
                //MessageBox.Show("Error running step hotkey.");
            }
        }

        private void Hotkey_Increase_End()
        {
            try
            {
                TabItem tab = (tabMain.SelectedItem as TabItem);
                MetaInfo info = tab.Tag as MetaInfo;

                string type = info.Corruption();
                string nes = "";

                if (tabNES.IsSelected == true)
                {
                    nes = hkNESPRG ? "PRG" : "CHR";
                }

                TextBox stop = tab.FindName("txt" + type + nes + "Stop") as TextBox;
                SetValue(stop, GetValue(stop) + 1);
            }
            catch
            {
                //MessageBox.Show("Error running step hotkey.");
            }
        }

        private void Hotkey_Decrease_End()
        {
            try
            {
                TabItem tab = (tabMain.SelectedItem as TabItem);
                MetaInfo info = tab.Tag as MetaInfo;

                string type = info.Corruption();
                string nes = "";

                if (tabNES.IsSelected == true)
                {
                    nes = hkNESPRG ? "PRG" : "CHR";
                }

                TextBox stop = tab.FindName("txt" + type + nes + "Stop") as TextBox;
                SetValue(stop, GetValue(stop) - 1);
            }
            catch
            {
                //MessageBox.Show("Error running step hotkey.");
            }
        }

        #endregion

        #region Corruption Logic


        private void Corrupt_Click(object sender, RoutedEventArgs e)
        {
            TabItem tab = (tabMain.SelectedItem as TabItem);
            MetaInfo info = tab.Tag as MetaInfo;

            string args = "";
            string type = tab.Name.Replace("tab", "");

            TextBox rom = tab.FindName("txt" + type + "File") as TextBox;
            TextBox step = tab.FindName("txt" + type + "Step") as TextBox;
            TextBox start = tab.FindName("txt" + type + "Start") as TextBox;
            TextBox stop = tab.FindName("txt" + type + "Stop") as TextBox;

            int step_value;
            int start_value;
            int stop_value;

            List<string> files = new List<string>();

            if (File.Exists(rom.Text) == false)
            {
                MessageBox.Show("You must select a rom before corrupting.");
                return;
            }

            /*
            if (File.Exists(Directory.GetCurrentDirectory() + "\\output" + info.Extension()))
            {
                File.Delete(Directory.GetCurrentDirectory() + "\\output" + info.Extension());
            }
            */

            try
            {
                step_value = GetValue(step);
                start_value = GetValue(start);
                stop_value = GetValue(stop);
                
                if (step_value <= 0)
                {
                    MessageBox.Show("Step value must be greater than 0.");
                    return;
                }

                if (start_value < 0)
                {
                    MessageBox.Show("Start value must be positive.");
                    return;
                }

                if (stop_value < 0)
                {
                    MessageBox.Show("Stop value must be positive.");
                    return;
                }
            }
            catch
            {
                MessageBox.Show("Invalid step size. Value is greater than the integer limit.");
                return;
            }

            args = "\"" + rom.Text + "\" --step " + step_value.ToString() + " ";

            if (start_value > 0)
            {
                args += "--start " + start_value.ToString() + " ";
            }

            if (stop_value > 0)
            {
                args += "--stop " + stop_value.ToString() + " ";
            }

            if (info.HasFiles())
            {
                string file_args = "";

                foreach (TreeViewItem item in (tab.FindName("tree" + type + "FileList") as TreeView).Items)
                {
                    files.AddRange(GetFiles(item));
                }

                foreach (string file in files)
                {
                    file_args += file + " ";
                }

                //  Command line arguments have a set limit based on the OS used.
                //  General cap is 8196, but on some windows systems it is slightly over 2000.
                if (file_args.Length > 2000)
                {
                    //  Write down all the args to a file which the corrupter will read
                    File.WriteAllText("__files.txt", file_args);
                    args += " --filelist __files.txt ";
                }
                else
                {
                    args += " --files " + file_args + " ";
                }
            }

            try
            {
                ComboBox corruption_type = tab.FindName("cmb" + type + "Type") as ComboBox;
                TextBox value = tab.FindName("txt" + type + "Value") as TextBox;

                string type_str = (corruption_type.SelectedItem as ComboBoxItem).Tag.ToString();
                args += type_str;

                if (type_str != "--random" && type_str != "--logical-complement")
                {
                    args += " " + GetValue(value);
                }
            }
            catch
            {
                MessageBox.Show("Error: Could not find corruption type.");
                return;
            }

            if (type == "Playstation")
            {
                args += " --playstation";
            }

            args += " --debug";

            //string output = Corrupter.Corrupt(args);
            Corrupter c = new Corrupter();
            Clipboard.SetText(args);
            c.Corrupt(args);

            if (c.Output().Contains("Exception: "))
            {
                Logger.Error(c.Output());
                MessageBox.Show("An exception occured in mdcorrupt. Details saved to error.log.");
            }

            try
            {
                Regex corruptions = new Regex(@"(\d+) bytes.");

                MatchCollection result = corruptions.Matches(c.Output());

                int total = 0;

                if (result.Count != 0)
                {
                    total = Convert.ToInt32(result[0].Value.Substring(0, result[0].Value.IndexOf("bytes") - 1));
                }

                (tab.FindName("lbl" + type + "Total") as Label).Content = Convert.ToInt32(total).ToString("X8") + "h";
            }
            catch { }

            CheckBox run = tab.FindName("chk" + type + "Run") as CheckBox;

            if (run.IsChecked == true && File.Exists("\"" + Directory.GetCurrentDirectory() + "\\output" + info.Extension() + "\""))
            {
                string emu = (tab.FindName("txt" + type + "Emulator") as TextBox).Text;

                if (String.IsNullOrEmpty(emu) == false)
                {
                    try
                    {
                        //  Close previous emulator if there was one
                        emulator.CloseMainWindow();
                    }
                    catch { }
                    emulator = new Process();
                    emulator.StartInfo.FileName = emu;
                    emulator.StartInfo.Arguments = "\"" + Directory.GetCurrentDirectory() + "\\output" + info.Extension() + "\"";
                    emulator.Start();
                }
            }
        }

        private void NES_Corrupt_Click(object sender, RoutedEventArgs e)
        {
            string args = "";
            string rom = '"' + txtNESFile.Text + '"';
            string emu = txtNESEmulator.Text;

            //  Do nothing if file doesn't exist
            if (File.Exists(txtNESFile.Text) == false)
            {
                MessageBox.Show("You must select a rom before corrupting.");
                return;
            }

            //  If no corruptions selected then tell them to choose one
            if (chkNESPRG.IsChecked == false && chkNESCHR.IsChecked == false)
            {
                MessageBox.Show("You must choose to corrupt either PRG and/or CHR rom.");
                return;
            }

            args = rom + " ";

            if (chkNESPRG.IsChecked == true)
            {
                try
                {
                    if (GetValue(txtNESPRGStep) <= 0)
                    {
                        MessageBox.Show("Step value must be greater than 0.");
                        return;
                    }

                    args += "--prg-step " + txtNESPRGStep.Text + " ";

                    if (int.Parse(txtNESPRGStart.Text, System.Globalization.NumberStyles.HexNumber) > 0)
                    {
                        args += "--prg-start " + txtNESPRGStart.Text + " ";
                    }

                    if (int.Parse(txtNESPRGStop.Text, System.Globalization.NumberStyles.HexNumber) > 0)
                    {
                        args += "--prg-stop " + txtNESPRGStop.Text + " ";
                    }
                }
                catch(System.OverflowException ex)
                {
                    MessageBox.Show("Values over 0xFFFFFFFF are invalid.");
                    return;
                }

                args += (cmbNESPRGType.SelectedItem as ComboBoxItem).Tag.ToString() + " " + int.Parse(txtNESPRGValue.Text, System.Globalization.NumberStyles.HexNumber).ToString();

                args += " ";
            }

            if (chkNESCHR.IsChecked == true)
            {
                if (Convert.ToInt32(txtNESPRGStep.Text) <= 0)
                {
                    MessageBox.Show("Step value must be greater than 0.");
                    return;
                }

                args += "--chr-step " + txtNESCHRStep.Text + " ";

                if (int.Parse(txtNESCHRStart.Text, System.Globalization.NumberStyles.HexNumber) > 0)
                {
                    args += "--chr-start " + txtNESCHRStart.Text + " ";
                }

                if (int.Parse(txtNESCHRStop.Text, System.Globalization.NumberStyles.HexNumber) > 0)
                {
                    args += "--chr-stop " + txtNESCHRStop.Text + " ";
                }

                args += (cmbNESCHRType.SelectedItem as ComboBoxItem).Tag.ToString() + " " + int.Parse(txtNESCHRValue.Text, System.Globalization.NumberStyles.HexNumber).ToString();

                args += " ";
            }

            //string output = Corrupter.Corrupt(args);

            Corrupter c = new Corrupter();

            c.Corrupt(args);

            if (c.Output().Contains("Exception: "))
            {
                Logger.Error(c.Output());
                MessageBox.Show("An exception occured in mdcorrupt. Details saved to error.log.");
            }

            Regex prg_corruptions = new Regex(@"(\d+) bytes in PRG-ROM.");
            Regex chr_corruptions = new Regex(@"(\d+) bytes in CHR-ROM.");

            MatchCollection prg_result = prg_corruptions.Matches(c.Output());
            MatchCollection chr_result = chr_corruptions.Matches(c.Output());

            string prg_total = "0";
            string chr_total = "0";

            if (prg_result.Count != 0)
            {
                prg_total = prg_result[0].Value.Replace(" bytes in PRG-ROM.", "");
            }
            if (chr_result.Count != 0)
            {
                chr_total = chr_result[0].Value.Replace(" bytes in CHR-ROM.", "");
            }


            lblNESTotal.Content = (Convert.ToInt32(prg_total) + Convert.ToInt32(chr_total)).ToString("X8") + "h";
            lblNESPRG.Content = Convert.ToInt32(prg_total).ToString("X8") + "h";
            lblNESCHR.Content = Convert.ToInt32(chr_total).ToString("X8") + "h";

            if (chkNESRun.IsChecked == true)
            {
                if (String.IsNullOrEmpty(emu) == false)
                {
                    Process emulator = new Process();
                    emulator.StartInfo.FileName = emu;
                    emulator.StartInfo.Arguments = "\"" + Directory.GetCurrentDirectory() + "\\output.nes\"";
                    emulator.Start();
                }
            }
        }

        private void NDS_Corrupt_Click(object sender, RoutedEventArgs e)
        {
            double available;
            string drive = "";

            if (txtNDSExtractTo.Text == "")
            {
                txtNDSExtractTo.Text = Environment.CurrentDirectory + "/games/nds";
            }

            drive = System.IO.Path.GetPathRoot(txtNDSExtractTo.Text);

            foreach (System.IO.DriveInfo label in System.IO.DriveInfo.GetDrives())
            {
                if (label.Name.Contains(drive))
                {
                    available = label.TotalFreeSpace;

                    //  If under 512MB is available, then we can't extract and re-compile.
                    if (available <= 0x20000000)
                    {
                        MessageBox.Show("Not enough free space to safely extract and re-compile disc. You need at least 512MB of free space.");
                        return;
                    }
                }
            }

            string file = txtNDSFile.Text;
            string dest = txtNDSExtractTo.Text;

            new Thread(
                new ThreadStart(
                    delegate
                    {
                        NDS.Extract(file, dest, NDS_Corrupt);
                    })).Start();

            lblNDSStatus.Content = "Extracting files";
        }

        private void NDS_Corrupt(string basefolder)
        {
            try
            {
                if (!CheckAccess())
                {
                    // On a different thread
                    Dispatcher.Invoke(() => NDS_Corrupt(basefolder));
                    return;
                }
                else
                {
                    string folder = basefolder;
                    string args = "";

                    int step_value;
                    int start_value;
                    int stop_value;

                    List<string> files = new List<string>();

                    if (Directory.Exists(basefolder + "/files"))
                    {
                        folder += "/files";
                    }
                    else if (Directory.Exists(basefolder + "/DATA/files"))
                    {
                        folder += "/DATA/files";
                    }
                    else
                    {
                        ListDirectory(basefolder);

                        MessageBox.Show("Could not find the files directory. Directory listing saved to error.log");
                        return;
                    }

                    if (File.Exists(txtNDSFile.Text) == false)
                    {
                        MessageBox.Show("You must select a valid rom before corrupting.");
                        return;
                    }

                    try
                    {
                        step_value = GetValue(txtNDSStep);
                        start_value = GetValue(txtNDSStart);
                        stop_value = GetValue(txtNDSStop);

                        if (step_value <= 0)
                        {
                            MessageBox.Show("Step value must be greater than 0.");
                            return;
                        }

                        if (start_value < 0)
                        {
                            MessageBox.Show("Start value must be positive.");
                            return;
                        }

                        if (stop_value < 0)
                        {
                            MessageBox.Show("Stop value must be positive.");
                            return;
                        }
                    }
                    catch
                    {
                        MessageBox.Show("Invalid step size. Value is greater than the integer limit.");
                        return;
                    }

                    args = " --step " + step_value.ToString() + " ";

                    if (start_value > 0)
                    {
                        args += "--start " + start_value.ToString() + " ";
                    }

                    if (stop_value > 0)
                    {
                        args += "--stop " + stop_value.ToString() + " ";
                    }

                    string type_str = (cmbNDSType.SelectedItem as ComboBoxItem).Tag.ToString();
                    args += type_str;

                    if (type_str != "--random" && type_str != "--logical-complement")
                    {
                        args += " " + GetValue(txtNDSValue);
                    }

                    foreach (TreeViewItem item in treeNDSFileList.Items)
                    {
                        files.AddRange(GetFiles(item));
                    }

                    lblNDSStatus.Content = "Corrupting files";
                    lblNDSStatus.Refresh();

                    int total = 0;
                    Corrupter c = new Corrupter();
                    string batch = "";

                    foreach (string f in files)
                    {
                        string file = "\"" + folder + f.Substring(1) + "\"";

                        System.IO.FileInfo fi = new System.IO.FileInfo(basefolder + "-backup" + f.Substring(1));
                        fi.Directory.Create(); // If the directory already exists, this method does nothing.
                        System.IO.File.WriteAllBytes(fi.FullName, File.ReadAllBytes(folder + f.Substring(1)));

                        //string output = Corrupter.Corrupt(file + " " + args + " --nintendo --out " + file);
                        batch += file + " " + args + " --nintendo --out " + file + "\n";
                    }

                    File.WriteAllText("__batch.txt", batch);
                    c.Corrupt("__batch.txt --batch");

                    if (c.Output().Contains("Exception: "))
                    {
                        Logger.Error(c.Output());
                        MessageBox.Show("An exception occured in mdcorrupt. Details saved to error.log.");
                    }

                    Regex corruptions = new Regex(@"(\d+) bytes.");

                    MatchCollection result = corruptions.Matches(c.Output());

                    if (result.Count != 0)
                    {
                        foreach (Match r in result)
                        {
                            total += Convert.ToInt32(r.Value.Replace(" bytes.", ""));
                        }
                    }

                    lblNDSTotal.Content = total.ToString("X8") + "h";

                    lblNDSStatus.Content = "Removing old";
                    lblNDSStatus.Refresh();

                    if (File.Exists("output.nds"))
                    {
                        File.Delete("output.nds");
                    }

                    new Thread(
                        new ThreadStart(
                            delegate
                            {
                                NDS.Create(basefolder, "output.nds", NDS_Corrupt_Done);
                            })).Start();

                    lblNDSStatus.Content = "Creating new disc";
                    lblNDSStatus.Refresh();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                MessageBox.Show("Failure in NDS_Corrupt. Details saved to error.log");
            }
        }

        private void NDS_Corrupt_Done(string folder, string destination)
        {
            try
            {
                if (!CheckAccess())
                {
                    // On a different thread
                    Dispatcher.Invoke(() => NDS_Corrupt_Done(folder, destination));
                    return;
                }
                else
                {

                    MetaInfo info = tabNDS.Tag as MetaInfo;

                    lblNDSStatus.Content = "Restoring original";
                    lblNDSStatus.Refresh();

                    if (Directory.Exists(folder + "-backup"))
                    {
                        DirectoryCopy(folder + "-backup", folder + "/files", true);
                        Directory.Delete(folder + "-backup", true);
                    }

                    lblNDSStatus.Content = "Done!";
                    lblNDSStatus.Refresh();

                    if (chkNDSRun.IsChecked == true)
                    {
                        string emu = txtNDSEmulator.Text;

                        if (String.IsNullOrEmpty(emu) == false)
                        {
                            try
                            {
                                //  Close previous emulator if there was one
                                emulator.CloseMainWindow();
                            }
                            catch { }
                            emulator = new Process();
                            emulator.StartInfo.FileName = emu;
                            emulator.StartInfo.Arguments = "\"" + Directory.GetCurrentDirectory() + "\\output" + info.Extension() + "\"";
                            emulator.Start();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                MessageBox.Show("Failure in NDS_Corrupt_Done. Details saved to error.log");
            }
        }

        private void Gamecube_Corrupt_Click(object sender, RoutedEventArgs e)
        {
            double available;
            string drive = "";

            if (txtGamecubeExtractTo.Text == "")
            {
                txtGamecubeExtractTo.Text = Environment.CurrentDirectory + "/games/gc";
            }

            drive = System.IO.Path.GetPathRoot(txtGamecubeExtractTo.Text);

            foreach (System.IO.DriveInfo label in System.IO.DriveInfo.GetDrives())
            {
                if (label.Name.Contains(drive))
                {
                    available = label.TotalFreeSpace;

                    //  If under 4GB is available, then we can't extract and re-compile.
                    if (available <= 0x100000000)
                    {
                        MessageBox.Show("Not enough free space to safely extract and re-compile disc. You need at least 4GB of free space.");
                        return;
                    }
                }
            }

            string file = txtGamecubeFile.Text;
            string dest = txtGamecubeExtractTo.Text;

            new Thread(
                new ThreadStart(
                    delegate
                    {
                        GCM.Extract(file, dest, Gamecube_Corrupt);
                    })).Start();

            lblGamecubeStatus.Content = "Extracting files";
            lblGamecubeStatus.Refresh();
        }

        private void Gamecube_Corrupt(string basefolder)
        {
            try
            {
                if (!CheckAccess())
                {
                    // On a different thread
                    Dispatcher.Invoke(() => Gamecube_Corrupt(basefolder));
                    return;
                }
                else
                {
                    string folder = basefolder;
                    string args = "";

                    int step_value;
                    int start_value;
                    int stop_value;

                    List<string> files = new List<string>();

                    if (Directory.Exists(basefolder + "/files"))
                    {
                        folder += "/files";
                    }
                    else if (Directory.Exists(basefolder + "/DATA/files"))
                    {
                        folder += "/DATA/files";
                    }
                    else
                    {
                        ListDirectory(basefolder);

                        MessageBox.Show("Could not find the files directory. Directory listing saved to error.log");
                        return;
                    }

                    if (File.Exists(txtGamecubeFile.Text) == false)
                    {
                        MessageBox.Show("You must select a valid rom before corrupting.");
                        return;
                    }

                    try
                    {
                        step_value = GetValue(txtGamecubeStep);
                        start_value = GetValue(txtGamecubeStart);
                        stop_value = GetValue(txtGamecubeStop);

                        if (step_value <= 0)
                        {
                            MessageBox.Show("Step value must be greater than 0.");
                            return;
                        }

                        if (start_value < 0)
                        {
                            MessageBox.Show("Start value must be positive.");
                            return;
                        }

                        if (stop_value < 0)
                        {
                            MessageBox.Show("Stop value must be positive.");
                            return;
                        }
                    }
                    catch
                    {
                        MessageBox.Show("Invalid step size. Value is greater than the integer limit.");
                        return;
                    }

                    args = " --step " + step_value.ToString() + " ";

                    if (start_value > 0)
                    {
                        args += "--start " + start_value.ToString() + " ";
                    }

                    if (stop_value > 0)
                    {
                        args += "--stop " + stop_value.ToString() + " ";
                    }

                    string type_str = (cmbGamecubeType.SelectedItem as ComboBoxItem).Tag.ToString();
                    args += type_str;

                    if (type_str != "--random" && type_str != "--logical-complement")
                    {
                        args += " " + GetValue(txtGamecubeValue);
                    }

                    foreach (TreeViewItem item in treeGamecubeFileList.Items)
                    {
                        files.AddRange(GetFiles(item));
                    }

                    lblGamecubeStatus.Content = "Corrupting files";
                    lblGamecubeStatus.Refresh();

                    int total = 0;
                    Corrupter c = new Corrupter();
                    string batch = "";

                    foreach (string f in files)
                    {
                        string file = "\"" + folder + f.Substring(1) + "\"";

                        System.IO.FileInfo fi = new System.IO.FileInfo(basefolder + "-backup" + f.Substring(1));
                        fi.Directory.Create(); // If the directory already exists, this method does nothing.
                        System.IO.File.WriteAllBytes(fi.FullName, File.ReadAllBytes(folder + f.Substring(1)));

                        //string output = Corrupter.Corrupt(file + " " + args + " --nintendo --out " + file);
                        batch += file + " " + args + " --nintendo --out " + file + "\n";
                    }

                    File.WriteAllText("__batch.txt", batch);
                    c.Corrupt("__batch.txt --batch");

                    if (c.Output().Contains("Exception: "))
                    {
                        Logger.Error(c.Output());
                        MessageBox.Show("An exception occured in mdcorrupt. Details saved to error.log.");
                    }

                    Regex corruptions = new Regex(@"(\d+) bytes.");

                    MatchCollection result = corruptions.Matches(c.Output());

                    if (result.Count != 0)
                    {
                        foreach (Match r in result)
                        {
                            total += Convert.ToInt32(r.Value.Replace(" bytes.", ""));
                        }
                    }

                    lblGamecubeTotal.Content = total.ToString("X8") + "h";

                    lblGamecubeStatus.Content = "Removing old";
                    lblGamecubeStatus.Refresh();

                    if (File.Exists("output.gcm"))
                    {
                        File.Delete("output.gcm");
                    }

                    new Thread(
                        new ThreadStart(
                            delegate
                            {
                                GCM.Create(basefolder, "output.gcm", Gamecube_Corrupt_Done);
                            })).Start();

                    lblGamecubeStatus.Content = "Creating new disc";
                    lblGamecubeStatus.Refresh();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                MessageBox.Show("Failure in Gamecube_Corrupt. Details saved to error.log");
            }
        }

        private void Gamecube_Corrupt_Done(string folder, string destination)
        {
            try
            {
                if (!CheckAccess())
                {
                    // On a different thread
                    Dispatcher.Invoke(() => Gamecube_Corrupt_Done(folder, destination));
                    return;
                }
                else
                {

                    MetaInfo info = tabGamecube.Tag as MetaInfo;

                    lblGamecubeStatus.Content = "Restoring original";
                    lblGamecubeStatus.Refresh();

                    if (Directory.Exists(folder + "-backup"))
                    {
                        DirectoryCopy(folder + "-backup", folder + "/files", true);
                        Directory.Delete(folder + "-backup", true);
                    }

                    lblGamecubeStatus.Content = "Done!";
                    lblGamecubeStatus.Refresh();

                    if (chkGamecubeRun.IsChecked == true)
                    {
                        string emu = txtGamecubeEmulator.Text;

                        if (String.IsNullOrEmpty(emu) == false)
                        {
                            try
                            {
                                //  Close previous emulator if there was one
                                emulator.CloseMainWindow();
                            }
                            catch { }
                            emulator = new Process();
                            emulator.StartInfo.FileName = emu;
                            emulator.StartInfo.Arguments = "/e \"" + Directory.GetCurrentDirectory() + "\\output" + info.Extension() + "\"";
                            emulator.Start();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                MessageBox.Show("Failure in Gamecube_Corrupt_Done. Details saved to error.log");
            }
        }

        private void Wii_Corrupt_Click(object sender, RoutedEventArgs e)
        {
            double available;
            string drive = "";

            if (txtWiiExtractTo.Text == "")
            {
                txtWiiExtractTo.Text = Environment.CurrentDirectory + "/games/wii";
            }

            drive = System.IO.Path.GetPathRoot(txtWiiExtractTo.Text);

            foreach (System.IO.DriveInfo label in System.IO.DriveInfo.GetDrives())
            {
                if (label.Name.Contains(drive))
                {
                    available = label.TotalFreeSpace;

                    //  If under 8GB is available, then we can't extract and re-compile.
                    if (available <= 0x200000000)
                    {
                        MessageBox.Show("Not enough free space to safely extract and re-compile disc. You need at least 8GB of free space.");
                        return;
                    }
                }
            }

            string file = txtWiiFile.Text;
            string dest = txtWiiExtractTo.Text;

            new Thread(
                new ThreadStart(
                    delegate
                    {
                        WIT.Extract(file, dest, Wii_Corrupt);
                    })).Start();

            lblWiiStatus.Content = "Extracting files";
            lblWiiStatus.Refresh();
        }

        private void Wii_Corrupt(string basefolder)
        {
            try
            {
                if (!CheckAccess())
                {
                    // On a different thread
                    Dispatcher.Invoke(() => Wii_Corrupt(basefolder));
                    return;
                }
                else
                {
                    string folder = basefolder;
                    string args = "";

                    int step_value;
                    int start_value;
                    int stop_value;

                    List<string> files = new List<string>();

                    if (Directory.Exists(basefolder + "/files"))
                    {
                        folder += "/files";
                    }
                    else if (Directory.Exists(basefolder + "/DATA/files"))
                    {
                        folder += "/DATA/files";
                    }
                    else
                    {
                        if (File.Exists("error.log"))
                        {
                            File.Delete("error.log");
                        }

                        ListDirectory(basefolder);

                        MessageBox.Show("Could not find the files directory. Directory listing saved to error.log");
                        return;
                    }

                    if (File.Exists(txtWiiFile.Text) == false)
                    {
                        MessageBox.Show("You must select a valid rom before corrupting.");
                        return;
                    }

                    try
                    {
                        step_value = GetValue(txtWiiStep);
                        start_value = GetValue(txtWiiStart);
                        stop_value = GetValue(txtWiiStop);

                        if (step_value <= 0)
                        {
                            MessageBox.Show("Step value must be greater than 0.");
                            return;
                        }

                        if (start_value < 0)
                        {
                            MessageBox.Show("Start value must be positive.");
                            return;
                        }

                        if (stop_value < 0)
                        {
                            MessageBox.Show("Stop value must be positive.");
                            return;
                        }
                    }
                    catch
                    {
                        MessageBox.Show("Invalid step size. Value is greater than the integer limit.");
                        return;
                    }

                    args = " --step " + step_value.ToString() + " ";

                    if (start_value > 0)
                    {
                        args += "--start " + start_value.ToString() + " ";
                    }

                    if (stop_value > 0)
                    {
                        args += "--stop " + stop_value.ToString() + " ";
                    }

                    string type_str = (cmbWiiType.SelectedItem as ComboBoxItem).Tag.ToString();
                    args += type_str;

                    if (type_str != "--random" && type_str != "--logical-complement")
                    {
                        args += " " + GetValue(txtWiiValue);
                    }

                    foreach (TreeViewItem item in treeWiiFileList.Items)
                    {
                        files.AddRange(GetFiles(item));
                    }

                    lblWiiStatus.Content = "Corrupting files";
                    lblWiiStatus.Refresh();

                    int total = 0;
                    Corrupter c = new Corrupter();
                    string batch = "";

                    foreach (string f in files)
                    {
                        string file = "\"" + folder + f.Substring(1) + "\"";

                        System.IO.FileInfo fi = new System.IO.FileInfo(basefolder + "-backup" + f.Substring(1));
                        fi.Directory.Create(); // If the directory already exists, this method does nothing.
                        System.IO.File.WriteAllBytes(fi.FullName, File.ReadAllBytes(folder + f.Substring(1)));

                        //string output = Corrupter.Corrupt(file + " " + args + " --nintendo --out " + file);
                        batch += file + " " + args + " --nintendo --out " + file + "\n";
                    }

                    File.WriteAllText("__batch.txt", batch);
                    c.Corrupt("__batch.txt --batch");

                    if (c.Output().Contains("Exception: "))
                    {
                        Logger.Error(c.Output());
                        MessageBox.Show("An exception occured in mdcorrupt. Details saved to error.log.");
                    }

                    Regex corruptions = new Regex(@"(\d+) bytes.");

                    MatchCollection result = corruptions.Matches(c.Output());

                    if (result.Count != 0)
                    {
                        foreach (Match r in result)
                        {
                            total += Convert.ToInt32(r.Value.Replace(" bytes.", ""));
                        }
                    }

                    lblWiiTotal.Content = total.ToString("X8") + "h";

                    lblWiiStatus.Content = "Removing old";
                    lblWiiStatus.Refresh();

                    if (File.Exists("output.wbfs"))
                    {
                        File.Delete("output.wbfs");
                    }

                    new Thread(
                        new ThreadStart(
                            delegate
                            {
                                WIT.Create(basefolder, "output.wbfs", Wii_Corrupt_Done);
                            })).Start();

                    lblWiiStatus.Content = "Creating new disc";
                    lblWiiStatus.Refresh();
                }
            }
            catch(Exception ex)
            {
                Logger.Error(ex.Message);
                MessageBox.Show("Failure in Wii_Corrupt. Details saved to error.log");
            }
        }

        private void Wii_Corrupt_Done(string folder, string destination)
        {
            try
            {
                if (!CheckAccess())
                {
                    // On a different thread
                    Dispatcher.Invoke(() => Wii_Corrupt_Done(folder, destination));
                    return;
                }
                else
                {

                    MetaInfo info = tabWii.Tag as MetaInfo;

                    lblWiiStatus.Content = "Restoring original";
                    lblWiiStatus.Refresh();

                    if (Directory.Exists(folder + "-backup"))
                    {
                        DirectoryCopy(folder + "-backup", folder + "/files", true);
                        Directory.Delete(folder + "-backup", true);
                    }

                    lblWiiStatus.Content = "Done!";
                    lblWiiStatus.Refresh();

                    if (chkWiiRun.IsChecked == true)
                    {
                        string emu = txtWiiEmulator.Text;

                        if (String.IsNullOrEmpty(emu) == false)
                        {
                            try
                            {
                                //  Close previous emulator if there was one
                                emulator.CloseMainWindow();
                            }
                            catch { }
                            emulator = new Process();
                            emulator.StartInfo.FileName = emu;
                            emulator.StartInfo.Arguments = "/e \"" + Directory.GetCurrentDirectory() + "\\output" + info.Extension() + "\"";
                            emulator.Start();
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                Logger.Error(ex.Message);
                MessageBox.Show("Failure in Wii_Corrupt_Done. Details saved to error.log");
            }
        }

        private void Miscellaneous_Corrupt_Click(object sender, RoutedEventArgs e)
        {
            if (lstMiscFiles.Items.Count == 0)
            {
                return;
            }

            string args = "--normal ";

            string type_str = (cmbMiscellaneousType.SelectedItem as ComboBoxItem).Tag.ToString();
            args += type_str;

            if (type_str != "--random" && type_str != "--logical-complement")
            {
                args += " " + GetValue(txtMiscellaneousValue).ToString();
            } 
            
            int step_value;
            int start_value;
            int stop_value;

            try
            {
                step_value = GetValue(txtMiscellaneousStep);
                start_value = GetValue(txtMiscellaneousStart);
                stop_value = GetValue(txtMiscellaneousStop);

                if (step_value <= 0)
                {
                    MessageBox.Show("Step value must be greater than 0.");
                    return;
                }

                if (start_value < 0)
                {
                    MessageBox.Show("Start value must be positive.");
                    return;
                }

                if (stop_value < 0)
                {
                    MessageBox.Show("Stop value must be positive.");
                    return;
                }
            }
            catch
            {
                MessageBox.Show("Invalid step size. Value is greater than the integer limit.");
                return;
            }

            args += " --step " + step_value.ToString() + " ";

            if (start_value > 0)
            {
                args += "--start " + start_value.ToString() + " ";
            }

            if (stop_value > 0)
            {
                args += "--stop " + stop_value.ToString() + " ";
            }

            int total = 0;
            Regex corruptions = new Regex(@"(\d+) bytes.");

            foreach (string file in lstMiscFiles.Items)
            {
                if (File.Exists(file))
                {
                    Corrupter c = new Corrupter();

                    if (chkMiscInPlace.IsChecked == true)
                    {
                        //output = Corrupter.Corrupt("\"" + file + "\" " + args + " --out \"" + file + "\"");
                        c.Corrupt("\"" + file + "\" " + args + " --out \"" + file + "\"");
                    }
                    else
                    {
                        //output = Corrupter.Corrupt("\"" + file + "\" " + args + " --out \"" + txtMiscOutput.Text + System.IO.Path.GetFileName(file) + "\"");
                        c.Corrupt("\"" + file + "\" " + args + " --out \"" + txtMiscOutput.Text + System.IO.Path.GetFileName(file) + "\"");
                    }

                    if (c.Output().Contains("Exception: "))
                    {
                        Logger.Error(c.Output());
                        MessageBox.Show("An exception occured in mdcorrupt. Details saved to error.log.");
                    }

                    MatchCollection result = corruptions.Matches(c.Output());
                    if (result.Count != 0)
                    {
                        total += Convert.ToInt32(result[0].Value.Replace(" bytes.", ""));
                    }
                }
            }

            lblMiscellaneousTotal.Content = total.ToString();
        }
        #endregion

        // From MSDN Aricle "How to: Copy Directories"
        // Link: http://msdn.microsoft.com/en-us/library/bb762914.aspx
        private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);
            DirectoryInfo[] dirs = dir.GetDirectories();

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = System.IO.Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, true);
            }

            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string temppath = System.IO.Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, temppath, copySubDirs);
                }
            }
        }
        
        private static void ListDirectory(string basefolder)
        {
            try
            {
                foreach (string d in Directory.GetDirectories(basefolder))
                {
                    foreach (string f in Directory.GetFiles(d))
                    {
                        File.AppendAllText("log/error.log", f);
                    }
                    ListDirectory(d);
                }
            }
            catch (System.Exception excpt)
            {
                File.AppendAllText("log/error.log", excpt.Message);
            }
        }
    }

    public static class ExtensionMethods
    {
        private static Action EmptyDelegate = delegate() { };

        public static void Refresh(this UIElement uiElement)
        {
            uiElement.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Render, EmptyDelegate);
        }
    }
}
