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

using System.Net;
using System.Threading;
using System.ComponentModel;
using System.Windows.Threading;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

using Newtonsoft.Json;
using System.Reflection;

namespace Updater
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public struct VersionInfo
        {
            public string md5;
            public string url;
            public string version;
            public string witmd5;
            public string wit;
        }

        public string UpdateURL = "http://maiddog.com/projects/corrupter/version.json";
        public VersionInfo Version;
        string extension = "";
        bool wit;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Version = JsonConvert.DeserializeObject<VersionInfo>(new WebClient().DownloadString(UpdateURL));
            }
            catch(Exception ex)
            {
                MessageBox.Show("Could not download version information.\n\n" + ex.Message);
                Application.Current.Shutdown();
            }

            if (Environment.GetCommandLineArgs().Contains("--wit"))
            {
                extension = "." + System.IO.Path.GetFileName(Version.wit).Split('.').Last();
                startDownload(Version.wit);
                wit = true;
            }
            else
            {
                extension = "." + System.IO.Path.GetFileName(Version.url).Split('.').Last();
                startDownload(Version.url);
                wit = false;
            }

            DispatcherTimer timer = new DispatcherTimer();

            timer.Tick += new EventHandler(timer_Tick);
            timer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            timer.Start();
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            if (lblDownload.Content.ToString() == "Completed")
            {
                try
                { 
                    using (var md5 = MD5.Create())
                    {
                        string updatehash = "";

                        using (FileStream stream = File.OpenRead("tmpupdate_Corrupter" + extension))
                        {
                            try
                            {
                                updatehash = BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToUpper();
                            }
                            catch (Exception exception)
                            {
                                MessageBox.Show("Update failed:\n" + exception.Message);
                            }
                        }

                        if ((!wit && updatehash == Version.md5) || (wit && updatehash == Version.witmd5))
                        {
                            try
                            {
                                if (extension == ".exe")
                                {
                                    File.Copy("tmpupdate_Corrupter.exe", "Corrupter.exe", true);
                                }
                                else
                                {
                                    using (ZipArchive archive = ZipFile.OpenRead("tmpupdate_Corrupter.zip"))
                                    {
                                        foreach(var entry in archive.Entries)
                                        {
                                            entry.ExtractToFile(System.IO.Path.Combine(Directory.GetCurrentDirectory(), entry.FullName), true);
                                        }
                                    }
                                    //System.IO.Compression.ZipFile.ExtractToDirectory("tmpupdate_Corrupter.zip", Directory.GetCurrentDirectory());
                                }
                                //MessageBox.Show("Updated VineCorrupt");
                            }
                            catch (Exception exception)
                            {
                                //MessageBox.Show("Update failed: Couldn't overwrite file.\n" + exception.Message);
                            }
                        }
                        else
                        {
                            MessageBox.Show("MD5 mismatch. Update aborted.\n--" + updatehash + "\n--" + Version.md5);
                        }
                    }

                    try
                    {
                        File.Delete("tmpupdate_Corrupter" + extension);
                    }
                    catch
                    {
                        MessageBox.Show("Could not delete temp file.");
                    }

                    Application.Current.Shutdown();
                }
                catch(Exception ex)
                {
                    MessageBox.Show("Error downloading file: " + ex.Message);
                    Application.Current.Shutdown();
                }
            }
        }

        private void startDownload(string url)
        {
            lblDownload.Content = url;

            Thread thread = new Thread(() =>
            {
                WebClient client = new WebClient();
                client.DownloadProgressChanged += new DownloadProgressChangedEventHandler(DownloadProgressChanged);
                client.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadFileCompleted);
                client.DownloadFileAsync(new Uri(url), @"tmpupdate_Corrupter" + extension);
            });

            thread.Start();
        }

        private void DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            this.Dispatcher.BeginInvoke(new Action(delegate
            {
                double recieved = double.Parse(e.BytesReceived.ToString());
                double total = double.Parse(e.TotalBytesToReceive.ToString());
                double percentage = 100 - (recieved / total * 100);

                progDownload.Value = int.Parse(Math.Truncate(percentage).ToString());
            }));
        }

        private void DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            this.Dispatcher.BeginInvoke(new Action(delegate
            {
                lblDownload.Content = "Completed";
            }));
        }
    }
}
