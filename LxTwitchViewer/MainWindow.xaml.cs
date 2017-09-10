using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Diagnostics;
using CefSharp;
using Vlc.DotNet.Wpf;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace LxTwitchViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private Process streamlinkProcess;
        private string mainWindowTitle;
        private string quality;
        private string channelName;
        private Visibility chatVisibility;
        private Visibility channelBoxVisibility;
        private bool channelBoxEnabled;
        private int volume;

        public event PropertyChangedEventHandler PropertyChanged;

        public MainWindow()
        {
            InitializeComponent();

            MainWindowTitle = "LxTwitchViewer";
            Quality = Properties.Settings.Default.LastQuality;
            ChannelName = Properties.Settings.Default.LastChannel;
            ChatVisibility = Visibility.Collapsed;
            ChannelBoxVisibility = Visibility.Visible;
            ChannelBoxEnabled = true;
            Volume = Properties.Settings.Default.Volume;

            Chat.MenuHandler = new CustomMenuHandler();
            Chat.RequestHandler = new CustomRequestHandler();
            Chat.FrameLoadEnd += Chat_FrameLoadEnd;

            string libDirectory;
            if (IntPtr.Size == 4)
                // Use 32 bits library
                libDirectory = Path.Combine(Environment.CurrentDirectory, "lib", "x86");
            else
                // Use 64 bits library
                libDirectory = Path.Combine(Environment.CurrentDirectory, "lib", "x64");
            Player.MediaPlayer.VlcLibDirectory = new DirectoryInfo(libDirectory);
            var options = new string[]
            {
                // VLC options can be given here. Please refer to the VLC command line documentation.
                #if DEBUG
                "--verbose=2",
                "--file-logging",
                "--logfile=vlc-log.txt"
                //#else
                //"--"
                #endif
            };
            Player.MediaPlayer.VlcMediaplayerOptions = options;
            Player.MediaPlayer.EncounteredError += MediaPlayer_EncounteredError;
            Player.MediaPlayer.EndInit();
        }
        
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        public string MainWindowTitle
        {
            get { return mainWindowTitle; }
            set { mainWindowTitle = value; OnPropertyChanged(); }
        }
        
        public string Quality
        {
            get { return quality; }
            set { quality = value; OnPropertyChanged(); }
        }
        
        public string ChannelName
        {
            get { return channelName; }
            set { channelName = value; OnPropertyChanged(); }
        }
        
        public Visibility ChatVisibility
        {
            get { return chatVisibility; }
            set { chatVisibility = value; OnPropertyChanged(); }
        }
        
        public Visibility ChannelBoxVisibility
        {
            get { return channelBoxVisibility; }
            set { channelBoxVisibility = value; OnPropertyChanged(); }
        }
        
        public bool ChannelBoxEnabled
        {
            get { return channelBoxEnabled; }
            set { channelBoxEnabled = value; OnPropertyChanged(); }
        }
        
        public int Volume
        {
            get { return volume; }
            set { volume = value; SetPlayerVolume(value); OnPropertyChanged(); }
        }

        private void SetPlayerVolume(int volume)
        {
            if (Player.MediaPlayer.Audio != null)
                Player.MediaPlayer.Audio.Volume = volume;
        }

        private void StartPlayer(string url)
        {
            var replaced = url.Replace("[cli][info]  ", "");
            //Player.MediaPlayer.Play(new Uri(replaced), "--file-caching=5000", "--live-caching=5000");
            string[] args = new string[Properties.Settings.Default.VlcArgs.Count];
            Properties.Settings.Default.VlcArgs.CopyTo(args, 0);
            Player.MediaPlayer.Play(new Uri(replaced), args);
            SetPlayerVolume(Volume);
        }

        private void StartLivestreamer()
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "streamlink",
                WorkingDirectory = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory),
                Arguments = $"twitch.tv/{ChannelName} {Quality} --player-external-http {Properties.Settings.Default.StreamlinkArgs}",
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            streamlinkProcess = new Process();
            streamlinkProcess.StartInfo = psi;
            streamlinkProcess.OutputDataReceived += P_OutputDataReceived;
            streamlinkProcess.EnableRaisingEvents = true;
            streamlinkProcess.Start();
            streamlinkProcess.BeginOutputReadLine();
            Chat.Address = $"https://www.twitch.tv/{ChannelName}/chat?popout=";
        }

        const string js_bttv = @"
            function betterttv_init()
            {
                script = document.createElement('script');
                script.type = 'text/javascript';
                script.src = 'https://cdn.betterttv.net/betterttv.js?' + Math.random();
                thehead = document.getElementsByTagName('head')[0];
                if (thehead) thehead.appendChild(script);
            }

            betterttv_init();";

        const string js_ffz = @"
            function ffz_init()
            {
	            var script = document.createElement('script');
	            script.type = 'text/javascript';

                script.src = '//cdn.frankerfacez.com/script/script.min.js';
	            document.head.appendChild(script);
            }

            ffz_init();

            function ffzapInit()
            {
                var script = document.createElement('script');

                script.id = 'ffzap_script';
                script.type = 'text/javascript';

                script.src = 'https://cdn.ffzap.download/ffz-ap.min.js';
                document.head.appendChild(script);
            }

            ffzapInit();";

        private void Taskkill(int pid)
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = $"/F /T /PID {pid}",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(psi);//("taskkill", $"/F /T /PID {pid}")
        }

        private void P_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                if (e.Data.StartsWith("[cli][info]  http://127"))
                {
                    StartPlayer(e.Data);
                }
            }
        }

        private void MediaPlayer_EncounteredError(object sender, Vlc.DotNet.Core.VlcMediaPlayerEncounteredErrorEventArgs e)
        {
            Console.WriteLine("err");
        }

        private void OnVlcControlNeedsLibDirectory(object sender, VlcLibDirectoryNeededEventArgs e)
        {
            var assembly = Assembly.GetEntryAssembly();
            var currentDirectory = new FileInfo(assembly.Location).DirectoryName;
            if (currentDirectory == null)
                return;
            if (AssemblyName.GetAssemblyName(assembly.Location).ProcessorArchitecture == ProcessorArchitecture.X86)
                e.VlcLibDirectory = new DirectoryInfo(Path.Combine(currentDirectory, @"lib\x86\"));
            else
                e.VlcLibDirectory = new DirectoryInfo(Path.Combine(currentDirectory, @"lib\x64\"));
        }

        private void Chat_FrameLoadEnd(object sender, FrameLoadEndEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (e.Frame.IsMain)
                    Chat.GetBrowser().MainFrame.ExecuteJavaScriptAsync(js_ffz);
            });
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Properties.Settings.Default.LastChannel = ChannelName;
            Properties.Settings.Default.LastQuality = Quality;
            Properties.Settings.Default.Volume = Volume;
            Properties.Settings.Default.Save();
            Player.MediaPlayer.Stop();
            if (streamlinkProcess != null && !streamlinkProcess.HasExited)
            {
                Taskkill(streamlinkProcess.Id);
                streamlinkProcess.Dispose();
                streamlinkProcess = null;
            }

        }

        private void Slider_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
                (sender as Slider).Value++;
            else /*(e.Delta < 0)*/
                (sender as Slider).Value--;
        }

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ChannelName) || string.IsNullOrWhiteSpace(Quality))
                return;

            MainWindowTitle = ChannelName;
            StartLivestreamer();
            ChannelBoxEnabled = false;
            ChannelBoxVisibility = Visibility.Hidden;
            ChatVisibility = Visibility.Visible;
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            if (Player.MediaPlayer.IsPlaying)
            {
                Player.MediaPlayer.Stop();
                if (streamlinkProcess != null && !streamlinkProcess.HasExited)
                {
                    Taskkill(streamlinkProcess.Id);
                    streamlinkProcess.Dispose();
                    streamlinkProcess = null;
                }
                ChannelBoxEnabled = true;
                ChatVisibility = Visibility.Collapsed;
                ChannelBoxVisibility = Visibility.Visible;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            txtChannel.Focus();
            txtChannel.SelectAll();
        }
    }
}
