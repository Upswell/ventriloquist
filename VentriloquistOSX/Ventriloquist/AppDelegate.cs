using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Timers;
using Fleck;
using MonoMac.AppKit;
using MonoMac.Foundation;
using MonoMac.ObjCRuntime;
using Nancy;
using Nancy.Hosting.Self;
using XamSpeech;
using log4net;
using log4net.Config;

namespace Ventriloquist
{

	public partial class Ventriloquist : NSApplicationDelegate
	{
		// Define TTSRequest queues to operate on
		public static ConcurrentQueue<TTSRequest> Queue = new ConcurrentQueue<TTSRequest> ();
		private readonly Queue<TTSRequest> SpeechQueue = new Queue<TTSRequest>();

		// OSX speech engine
		private NSSpeechSynthesizer speech = new NSSpeechSynthesizer ();
		private SpeechDelegate speechdelegate = new SpeechDelegate();
		private NSSound sound;
		private SoundDelegate sounddelegate = new SoundDelegate();

		// Audio output
		private NSDictionary OutputDevices;
		private string OutputDeviceUID = "Built-in Output";

		// Logger
		private static readonly ILog logger = LogManager.GetLogger(typeof(Ventriloquist));

		// Misc
		private System.Timers.Timer queuetimer;
		private System.Timers.Timer speechtimer;
		private bool IsSpeaking = false;
		private bool IsSounding = false;
		private float DefaultRate = 0;
		private static int RequestCount = 0;

		private static NSMenu statusMenu;
		private static NSStatusItem statusItem;
		private readonly string audiopath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), "tmp");
		//private readonly string appsupportpath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library", "Application Support", "Ventriloquist");

		private AutoResetEvent synthesis = new AutoResetEvent(false);
		private AutoResetEvent playback = new AutoResetEvent(false);

		// Server setup
		private NancyHost server;
		private WebSocketServer websocketserver;
		private List<IWebSocketConnection> allSockets = new List<IWebSocketConnection> ();

		// Configuration
		private Config config;

		public Ventriloquist ()
		{
			// Load the dynamic library for aggregating audio output devices
			var libPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "xamspeech.dylib");
			Dlfcn.dlopen(libPath, 0);

			// Esure paths exist
			if(!Directory.Exists(audiopath)) {
				Directory.CreateDirectory(audiopath);
			}
			//Console.WriteLine (appsupportpath);
			//if(!Directory.Exists(appsupportpath)) {
			//	Directory.CreateDirectory (appsupportpath);
			//}
			config = Config.GetInstance();
			config.PropertyChanged += async (object sender, System.ComponentModel.PropertyChangedEventArgs e) => {
				if(e.PropertyName.Equals("localonly")) {
					server.Stop();
					InitHTTPServer();
				}
			};
		}

		public override void FinishedLaunching (NSObject notification)
		{
			// Configure logger
			string path = Path.Combine (Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), "log4net.config");
			XmlConfigurator.ConfigureAndWatch (new FileInfo(path));
			logger.Info ("Ventriliquest 1.0 Starting up...");

			// Get list of available audio out devices
			xamspeech ham = new xamspeech ();
			OutputDevices = ham.GetDevices ();

			// Setup UI
			statusMenu = new NSMenu ();
			statusItem = NSStatusBar.SystemStatusBar.CreateStatusItem (30);

			var outputItem = new NSMenuItem ("Output Device", 
				(a, b) => {

				});
					
			var deviceList = new NSMenu ();
			outputItem.Submenu = deviceList;

			OutputDeviceUID = "Built-in Output";

			foreach(var entry in OutputDevices) {
				var test = new NSMenuItem (entry.Key.ToString(), 
					(a, b) => {
						foreach(NSMenuItem item in deviceList.ItemArray()) {
							item.State = NSCellStateValue.Off;
						}
						NSMenuItem theItem = (NSMenuItem)a;
						theItem.State = NSCellStateValue.On;
						config.OutputDevice = theItem.Title;
						foreach(var e in OutputDevices) {
							if(e.Key.ToString().Equals(theItem.Title)) {
								OutputDeviceUID = e.Value.ToString();
							}
						}
					});
				if(entry.Key.ToString().Equals(config.OutputDevice)) {
					test.State = NSCellStateValue.On;
					OutputDeviceUID = entry.Value.ToString ();
				}
				deviceList.AddItem (test);
			}
				
			var daItem = new NSMenuItem ("Local Connections Only", 
				(a, b) => {
					NSMenuItem theItem = (NSMenuItem)a;
					if(theItem.State == NSCellStateValue.On) {
						config.LocalOnly = false;
						theItem.State = NSCellStateValue.Off;
					} else {
						config.LocalOnly = true;
						theItem.State = NSCellStateValue.On;
					}
				});

			if(config.LocalOnly) {
				daItem.State = NSCellStateValue.On;
			}

			var quitItem = new NSMenuItem("Quit", 
				(a, b) => Shutdown ());

			var voiceconfigItem = new NSMenuItem("Voice Configuration", 
				(a, b) => Process.Start("http://127.0.0.1:7888/config"));

			statusMenu.AddItem (outputItem);
			statusMenu.AddItem (daItem);
			statusMenu.AddItem (voiceconfigItem);
			statusMenu.AddItem (quitItem);
			statusItem.Menu = statusMenu;
			statusItem.Image = NSImage.ImageNamed("tts-1.png");
			statusItem.AlternateImage = NSImage.ImageNamed ("tts-2.png");
			statusItem.HighlightMode = true;

			speechdelegate.DidComplete += delegate {
				synthesis.Set();
			};
			sounddelegate.DidComplete += delegate {
				playback.Set ();
				IsSounding = false;
				IsSpeaking = false;
				sound.Dispose();
			};
				
			speech.Delegate = speechdelegate;
				
			queuetimer = new System.Timers.Timer (250);
			queuetimer.Elapsed += (object sender, ElapsedEventArgs e) => {
				TTSRequest r;
				if(Queue.TryDequeue(out r)) {
					if(r.Interrupt) {
						// stop current TTS
						NSApplication.SharedApplication.InvokeOnMainThread( delegate {
							if(IsSpeaking) {
								speech.StopSpeaking();
							}
							if(IsSounding) {
								sound.Stop();
							}
						});
						// clear queue
						SpeechQueue.Clear();
					}
					SpeechQueue.Enqueue(r);
					RequestCount++;
				}
				var eventdata = new Hashtable();
				eventdata.Add("ProcessedRequests", RequestCount);
				eventdata.Add("QueuedRequests", SpeechQueue.Count);
				eventdata.Add("IsSpeaking", IsSpeaking);
				InstrumentationEvent ev = new InstrumentationEvent();
				ev.EventName = "status";
				ev.Data = eventdata;
				NotifyGui(ev.EventMessage());
			};

			// when this timer fires, it will pull off of the speech queue and speak it
			// the 1000ms delay also adds a little pause between tts requests.
			speechtimer = new System.Timers.Timer (1000);
			speechtimer.Elapsed += (object sender, ElapsedEventArgs e) => {
				if(IsSpeaking.Equals(false)) {
					if(SpeechQueue.Count > 0) {
						TTSRequest r = SpeechQueue.Dequeue();
						IsSpeaking = true;
						speechtimer.Enabled = false;
						var oink = Path.Combine(audiopath, "temp.aiff");
						NSApplication.SharedApplication.InvokeOnMainThread( delegate {
							ConfigureSpeechEngine(r);
							speech.StartSpeakingStringtoURL(r.Text, new NSUrl(oink, false));
						});
						synthesis.WaitOne();
						NSApplication.SharedApplication.InvokeOnMainThread( delegate {
							IsSounding = true;
							sound = new NSSound(Path.Combine(audiopath, "temp.aiff"), false);
							sound.Delegate = sounddelegate;
							//if(OutputDeviceUID != "Default") {
								sound.PlaybackDeviceID = OutputDeviceUID;
							//}
							sound.Play();
						});
						playback.WaitOne();
						IsSounding = false;
						speechtimer.Enabled = true;
					}
				}
			};

			queuetimer.Enabled = true;
			queuetimer.Start ();
			speechtimer.Enabled = true;
			speechtimer.Start ();

			InitHTTPServer ();

			websocketserver = new WebSocketServer ("ws://localhost:7889");

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

		private void InitHTTPServer()
		{
			/* Setup HTTP server and start */
			var hostConfiguration = new HostConfiguration {
				UrlReservations = new UrlReservations() { CreateAutomatically = true }
			};
			hostConfiguration.RewriteLocalhost = false;
			StaticConfiguration.DisableErrorTraces = false;
			server = new NancyHost (hostConfiguration, GetUriParams(7888, config.LocalOnly));
			server.Start ();
		}

		private float Scale(int value , int min, int max, int minScale, int maxScale)
		{
			float scaled = minScale + (float)(value - min)/(max-min) * (maxScale - minScale);
			return scaled;
		}

		private void ConfigureSpeechEngine(TTSRequest request)
		{
			speech.Voice = config.getVoice (request.Language, request.Voice);
			/* Setting the DefaultRate is not ideal, but voice properties do not provide
			 * the default rate, and it is currently not possible to send a reset message
			 * to the NSSpeechSynthesizer via MonoMac.
			 * This is a workaround that seems to suffice at the moment.
			 */
			DefaultRate = 170f;
			//speech.Rate = (DefaultRate + -(5 - request.Speed) * 5);
			speech.Rate = Scale (request.Speed, 0, 10, 130, 200);
			Console.WriteLine (speech.Rate);
		}

		/* this is how to do channel mapping
		 */
		//private void Speak() {

			/* something like
			 * 1.  NSSpeechSynth -> save to temp file
			 * 2.  using delegate, waitone() until finished
			 * 3.  load temp file as nssound
			 * 4.  re-map channes
			 * 5.  play nssound
			 * 6.  use delegate, waitone() until finished playing
			 * 7.  both the tts->file and nssound playback tasks need a cancellationtoken so they can be killed
			 *     if an interrupt request comes in.
			 */

			//NSSound sound = new NSSound (Path.Combine(audiopath, "temp.aiff"), false);
			//sound.PlaybackDeviceID;

			/* ChannelMapping is an NSArray of NSNumbers */
			//NSArray channelmap = NSArray.FromObjects (new NSNumber(1), new NSNumber(2));
			//sound.ChannelMapping = channelmap;
			//sound.Play();
		//}

		private void NotifyGui(string data)
		{
			foreach(var socket in allSockets)
			{
				try {
					socket.Send(data);	
				} catch(Exception ex) {

				}
			}
		}

		private void Shutdown()
		{
			logger.Info ("Shutting down.");

			foreach(var socket in allSockets) {
				try {
					socket.Close();
				} catch(Exception ex) {

				}
			}
			websocketserver.ListenerSocket.Close();
			websocketserver.Dispose();

			server.Stop ();
			NSApplication.SharedApplication.Terminate (this);
		}

		private static Uri[] GetUriParams(int port, bool localonly)
		{
			var uriParams = new List<Uri>();
			string hostName = Dns.GetHostName();

			if (localonly == false) {
	
				// Host address URI(s)
				var hostEntry = Dns.GetHostEntry (hostName);
				foreach (var ipAddress in hostEntry.AddressList) {
					if (ipAddress.AddressFamily == AddressFamily.InterNetwork) {  // IPv4 addresses only
						var addrBytes = ipAddress.GetAddressBytes ();
						string hostAddressUri = string.Format ("http://{0}.{1}.{2}.{3}:{4}", addrBytes [0], addrBytes [1], addrBytes [2], addrBytes [3], port);
						uriParams.Add (new Uri (hostAddressUri));
					}
				}
			}

			// Host name URI
			//string hostNameUri = string.Format("http://{0}:{1}", Dns.GetHostName(), port);
			//uriParams.Add(new Uri(hostNameUri));

			// Localhost URI
			uriParams.Add(new Uri(string.Format("http://localhost:{0}", port)));

			// 127.0.0.1 URI
			uriParams.Add(new Uri(string.Format("http://127.0.0.1:{0}", port)));

			foreach (Uri u in uriParams) {
				logger.Info ("Listening at: " + u.AbsoluteUri);
			}

			return uriParams.ToArray();
		}
	}
}

