using System;
using System.Collections.Generic;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using System.Speech.AudioFormat;
using System.Speech.Synthesis;
using log4net;
using log4net.Config;
using NAudio.Wave;
using Nancy;
using Nancy.Hosting.Self;
using Fleck;

namespace Ventriloquist
{
    public class Ventriloquist : Form
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.Run(new Ventriloquist());
        }

        public static ConcurrentQueue<TTSRequest> Queue = new ConcurrentQueue<TTSRequest>();
        private readonly Queue<TTSRequest> SpeechQueue = new Queue<TTSRequest>();
        private static readonly ILog logger = LogManager.GetLogger(typeof(Ventriloquist));
        private System.Timers.Timer queuetimer;
        private System.Timers.Timer speechtimer;
        private int RequestCount = 0;

        private bool IsSpeaking = false;
        private bool IsSounding = false;

        private AutoResetEvent synthesis = new AutoResetEvent(false);
        private AutoResetEvent playback = new AutoResetEvent(false);

        private NancyHost server;
        private WebSocketServer websocketserver;
        private List<IWebSocketConnection> allSockets = new List<IWebSocketConnection>();

        private readonly string appsupportpath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ventriloquist");
        private Config config;

        private SpeechSynthesizer speech = new SpeechSynthesizer();
        private SpeechAudioFormatInfo format;
        private int OutputDeviceId = 0;
        private MemoryStream stream;
        private WaveOutEvent sound;

        private NotifyIcon trayIcon;
        private ContextMenu trayMenu;
        public Ventriloquist()
        {
            // Configure logger
            string path = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), "log4net.config");
            XmlConfigurator.ConfigureAndWatch(new FileInfo(path));
            logger.Info("Ventriliquest 1.0 Starting up...");

            if (!Directory.Exists(appsupportpath))
            {
                Directory.CreateDirectory(appsupportpath);
            }
            config = Config.GetInstance();

            trayMenu = new ContextMenu();

            MenuItem outputDevice = new MenuItem("Output device");

            for (int deviceId = 0; deviceId < WaveOut.DeviceCount; deviceId++)
            {
                var capabilities = WaveOut.GetCapabilities(deviceId);
                //Console.WriteLine(String.Format("Device {0} ({1})", deviceId, capabilities.ProductName));
                var deviceItem = new MenuItem(capabilities.ProductName, OnDeviceConfig);
                deviceItem.Tag = deviceId;
                //if(deviceId == int.Parse(config.OutputDevice)) {
                //    deviceItem.Checked = true;
                //}
                if(capabilities.ProductName == config.OutputDevice) {
                    deviceItem.Checked = true;
                    OutputDeviceId = deviceId;
                    Console.WriteLine("Output Device: " + OutputDeviceId);
                }
                outputDevice.MenuItems.Add(deviceItem);
            }

            var networkconfigItem = new MenuItem("Local Connections Only", OnNetworkConfig);

            Console.WriteLine("network config'd as: " + config.LocalOnly);

            if(config.LocalOnly) {
                networkconfigItem.Checked = true;
            }

            var voiceconfigItem = new MenuItem("Voice Configuration", OnVoiceConfig);

            trayMenu.MenuItems.Add(outputDevice);
            trayMenu.MenuItems.Add(networkconfigItem);
            trayMenu.MenuItems.Add(voiceconfigItem);
            trayMenu.MenuItems.Add("Exit", OnExit);

            trayIcon = new NotifyIcon();
            trayIcon.Text = "Ventriloquist TTS Server";
            trayIcon.Icon = new Icon(SystemIcons.Application, 40, 40);

            trayIcon.ContextMenu = trayMenu;
            trayIcon.Visible = true;

        }

        protected override void OnLoad(EventArgs e)
        {
            Visible = false;
            ShowInTaskbar = false;
            base.OnLoad(e);
 
            /*
             *  Get all installed voices
             * 
             */
            var voices = speech.GetInstalledVoices();
            string voice = "";
 
            foreach (InstalledVoice v in voices)
            {
                if (v.Enabled)
                    //voice = v.VoiceInfo.Name;
                    Console.WriteLine(v.VoiceInfo.Name);
                    
            }

            queuetimer = new System.Timers.Timer(250);
            queuetimer.Elapsed += (object sender, ElapsedEventArgs ev) => 
            {
                TTSRequest r;
                if (Queue.TryDequeue(out r))
                {
                    Console.WriteLine("dequeing off of concurrent queue...");
                    if (r.Interrupt)
                    {
                        // stop current TTS
                            if (IsSpeaking)
                            {
                                //speech.StopSpeaking();
                            }
                            if (IsSounding)
                            {
                                //sound.Stop();
                                if(sound.PlaybackState == PlaybackState.Playing) {
                                    sound.Stop(); 
                                }
                            }
                        // clear queue
                        SpeechQueue.Clear();
                    }
                    SpeechQueue.Enqueue(r);
                    RequestCount++;
                }
                
                var eventdata = new Hashtable();
                eventdata.Add("ProcessedRequests", RequestCount);
                eventdata.Add("QueuedRequests", SpeechQueue.Count);
                eventdata.Add("IsSpeaking", IsSounding);
                InstrumentationEvent blam = new InstrumentationEvent();
                blam.EventName = "status";
                blam.Data = eventdata;
                NotifyGui(blam.EventMessage());  
            };

            // when this timer fires, it will pull off of the speech queue and speak it
            // the long delay also adds a little pause between tts requests.
            speechtimer = new System.Timers.Timer(1000);
            speechtimer.Elapsed += (object sender, ElapsedEventArgs ev) =>
            {
                if (IsSpeaking.Equals(false))
                {
                    if (SpeechQueue.Count > 0)
                    {
                        TTSRequest r = SpeechQueue.Dequeue();
                        Console.WriteLine("dequeuing off of speech queue");
                        IsSpeaking = true;
                        speechtimer.Enabled = false;

                        //speech.SpeakAsync(r.Text);

                        //using (speech = new SpeechSynthesizer()) {
                        speech = new SpeechSynthesizer();
                            speech.SpeakCompleted += speech_SpeakCompleted;
                            format = new SpeechAudioFormatInfo(EncodingFormat.ALaw, 8000, 8, 1, 1, 2, null);
                            //format = new SpeechAudioFormatInfo(11025, AudioBitsPerSample.Sixteen, AudioChannel.Mono);
                           // var si = speech.GetType().GetMethod("SetOutputStream", BindingFlags.Instance | BindingFlags.NonPublic);
                            stream = new MemoryStream();
                            //si.Invoke(speech, new object[] { stream, format, true, true });
                            //speech.SetOutputToWaveStream(stream);
                            speech.SetOutputToAudioStream(stream, format);
                            speech.SelectVoice(config.getVoice (r.Language, r.Voice));
                            int rate = (r.Speed * 2 - 10);
                            
                            Console.WriteLine(rate);
                            try
                            {
                                speech.Rate = rate;
                            }
                            catch (ArgumentOutOfRangeException ex)
                            {
                                speech.Rate = 0;
                            }
                            speech.SpeakAsync(r.Text);
                        //}

                        synthesis.WaitOne();
                        speech.SpeakCompleted -= speech_SpeakCompleted;
                        speech.SetOutputToNull();
                        speech.Dispose();
                        //IsSpeaking = false;
                        IsSounding = true;
                        stream.Position = 0;
                        //WaveFormat.CreateCustomFormat(WaveFormatEncoding.WmaVoice9, 11025, 1, 16000, 2, 16)
                        using(RawSourceWaveStream reader = new RawSourceWaveStream(stream, WaveFormat.CreateALawFormat(8000, 1))) {
                            WaveStream ws = WaveFormatConversionStream.CreatePcmStream(reader);

                            //var waveProvider = new MultiplexingWaveProvider(new IWaveProvider[] { ws }, 4);
                            //waveProvider.ConnectInputToOutput(0, 3);

                            sound = new WaveOutEvent();
                            // set output device *before* init
                            Console.WriteLine("Output Device: " + OutputDeviceId);
                            sound.DeviceNumber = OutputDeviceId;
                            sound.Init(ws);
                            //sound.Init(waveProvider);
                            sound.PlaybackStopped += output_PlaybackStopped;
                           // Console.WriteLine("playing here " + ws.Length);
                            sound.Play();
                        }
                        playback.WaitOne();
                        //IsSounding = false;
                        speechtimer.Enabled = true;
                    }
                }
            };

            queuetimer.Enabled = true;
            queuetimer.Start();
            speechtimer.Enabled = true;
            speechtimer.Start();

            /* Setup HTTP server and start */
            var hostConfiguration = new HostConfiguration
            {
                UrlReservations = new UrlReservations() { CreateAutomatically = true }
            };
            StaticConfiguration.DisableErrorTraces = false;
            server = new NancyHost(hostConfiguration, GetUriParams(7888, config.LocalOnly));
            server.Start();

            websocketserver = new WebSocketServer("ws://localhost:7889");

            websocketserver.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    allSockets.Add(socket);
                    var eventdata = new Hashtable();
                     eventdata.Add("ProcessedRequests", RequestCount);
                    eventdata.Add("QueuedRequests", SpeechQueue.Count);
                    eventdata.Add("IsSpeaking", IsSpeaking);
                    InstrumentationEvent ev = new InstrumentationEvent();
                    ev.EventName = "status";
                    ev.Data = eventdata;
                    socket.Send(ev.EventMessage());
                };
                socket.OnClose = () =>
                {
                    allSockets.Remove(socket);

                };
                socket.OnMessage = message =>
                {

                };

            });

        }

        void output_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            sound.PlaybackStopped -= output_PlaybackStopped;
            playback.Set();
            IsSounding = false;
        }

        void speech_SpeakCompleted(object sender, SpeakCompletedEventArgs e)
        {
            //this.Invoke((MethodInvoker)delegate
            //{
                Console.WriteLine("synth reset");
                synthesis.Set();
                IsSpeaking = false;
            //});
        }

        private void OnExit(object sender, EventArgs e)
        {
            trayIcon.Icon = null;
            Application.Exit();
        }

        private void OnVoiceConfig(object sender, EventArgs e)
        {
            Process.Start("http://localhost:7888/config");   
        }

        private void OnDeviceConfig(object sender, EventArgs e)
        {
            MenuItem mi = sender as MenuItem;
            config.OutputDevice = mi.Text;
            OutputDeviceId = int.Parse(mi.Tag.ToString());
            foreach(var item in mi.Parent.MenuItems)
            {
                var i = item as MenuItem;
                i.Checked = false;
            }
            mi.Checked = true;
            Console.WriteLine("Output Device: " + OutputDeviceId);
        }

        private void OnNetworkConfig(object sender, EventArgs e)
        {
            MenuItem mi = sender as MenuItem;
            if(mi.Checked) {
                config.LocalOnly = false;
                mi.Checked = false;
            }
            else
            {
                config.LocalOnly = true;
                mi.Checked = true;
            }
        }

        private void NotifyGui(string data)
        {
            foreach (var socket in allSockets)
            {
                try
                {
                    socket.Send(data);
                }
                catch (Exception ex)
                {

                }
            }
        }

        private static Uri[] GetUriParams(int port, bool localonly)
        {
            var uriParams = new List<Uri>();
            string hostName = Dns.GetHostName();

            if (localonly == false)
            {

                // Host address URI(s)
                var hostEntry = Dns.GetHostEntry(hostName);
                foreach (var ipAddress in hostEntry.AddressList)
                {
                    if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
                    {  // IPv4 addresses only
                        var addrBytes = ipAddress.GetAddressBytes();
                        string hostAddressUri = string.Format("http://{0}.{1}.{2}.{3}:{4}", addrBytes[0], addrBytes[1], addrBytes[2], addrBytes[3], port);
                        uriParams.Add(new Uri(hostAddressUri));
                    }
                }
            }
            else
            {
                logger.Warn("There is a bug in the current runtime that binds the HTTPListener to all interfaces, regardless of what you configure.  This will be fixed shortly.");
            }

            // Host name URI
            //string hostNameUri = string.Format("http://{0}:{1}", Dns.GetHostName(), port);
            //uriParams.Add(new Uri(hostNameUri));

            // Localhost URI
            uriParams.Add(new Uri(string.Format("http://localhost:{0}", port)));

            // 127.0.0.1 URI
            uriParams.Add(new Uri(string.Format("http://127.0.0.1:{0}", port)));

            foreach (Uri u in uriParams)
            {
                logger.Info("Listening at: " + u.AbsoluteUri);
            }

            return uriParams.ToArray();
        }

        protected override void Dispose(bool disposing)
        {
            if(disposing) {
                trayIcon.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
