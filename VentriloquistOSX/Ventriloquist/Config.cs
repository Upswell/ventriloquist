using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Mono.Data.Sqlite;
using System.ComponentModel;
using log4net;
#if __WIN32__
using SpeechEngine=System.Speech.Synthesis.SpeechSynthesizer;
#else
using SpeechEngine=MonoMac.AppKit.NSSpeechSynthesizer;
using MonoMac.AppKit;
#endif

namespace Ventriloquist
{
	public class Config : INotifyPropertyChanged
	{
		#if __WIN32__
		public const string DefaultVoice = "Microsoft David Desktop";
		private readonly string databasepath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ventriloquist");
		#else
		public const string DefaultVoice = "com.apple.speech.synthesis.voice.Agnes";
		private readonly string databasepath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library", "Application Support", "Ventriloquist");
		#endif
		private static readonly Config instance = new Config();
		private readonly string connection;
		private static ILog logger;

		private Config ()
		{
			// default constructor
			logger = LogManager.GetLogger(typeof(Config));
			string database = Path.Combine(databasepath, "config.db");
			connection = String.Format("Data Source={0};Version=3;", database);
			if(!Directory.Exists(databasepath)) {
				Directory.CreateDirectory (databasepath);
			}
			if (!File.Exists (database)) {
				CreateDatabase ();
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void CreateDatabase()
		{
			logger.Info ("Initializing config database at " + databasepath);
			using (var conn= new SqliteConnection(connection))
			{
				conn.Open();
				using (var cmd = conn.CreateCommand())
				{
					cmd.CommandText = "CREATE TABLE Config (ConfigId INTEGER PRIMARY KEY AUTOINCREMENT, Setting VARCHAR(20), SettingValue varchar(200))";
					cmd.CommandType = CommandType.Text;
					cmd.ExecuteNonQuery();
					cmd.CommandText = "INSERT INTO Config(Setting, SettingValue) VALUES('outputdevice', 'Built-in Output')";
					cmd.ExecuteNonQuery();
					cmd.CommandText = "INSERT INTO Config(Setting, SettingValue) VALUES('localonly', '0')";
					cmd.ExecuteNonQuery();
					cmd.CommandText = "CREATE TABLE Voices (VoiceId INTEGER PRIMARY KEY AUTOINCREMENT, voice VARCHAR(255), lang varchar(2), id integer(4))";
					cmd.ExecuteNonQuery();
					for(var i=0; i<=19; i++) {
						cmd.CommandText = string.Format ("INSERT INTO Voices(voice, lang, id) VALUES('{0}', 'en', {1})", DefaultVoice, i);
						cmd.ExecuteNonQuery();
					}
					for(var i=0; i<=19; i++) {
						cmd.CommandText = string.Format ("INSERT INTO Voices(voice, lang, id) VALUES('{0}', 'fr', {1})", DefaultVoice, i);
						cmd.ExecuteNonQuery();
					}
				}
			}
		}

		private string GetSetting(string setting)
		{
			string retVal = "";
			using (var conn= new SqliteConnection(connection))
			{
				conn.Open();
				using (var cmd = conn.CreateCommand())
				{
					cmd.CommandText = String.Format("SELECT SettingValue FROM Config WHERE Setting = '{0}'", setting);
					retVal = (string)cmd.ExecuteScalar ();
				}
			}
			return retVal;
		}

		private void SetSetting(string setting, string value)
		{
			using (var conn= new SqliteConnection(connection))
			{
				conn.Open();
				using (var cmd = conn.CreateCommand())
				{
					cmd.CommandText = String.Format("UPDATE Config SET SettingValue = '{0}' WHERE Setting = '{1}'", value, setting);
					cmd.CommandType = CommandType.Text;
					cmd.ExecuteNonQuery();
				}
			}
		}

		public static Config GetInstance()
		{
			return instance;
		}

		private void OnPropertyChanged(string property)
		{
			if (PropertyChanged != null) {
				PropertyChanged (this, new PropertyChangedEventArgs (property));
			}
		}

		public string OutputDevice {
			get {
				return GetSetting ("outputdevice");
			}
			set {
				Console.WriteLine ("set output to: " + value);
				SetSetting ("outputdevice", value);
				OnPropertyChanged ("outputdevice");
			}
		}

		public bool LocalOnly {
			get {
				var thing = GetSetting ("localonly");
				return Convert.ToBoolean(int.Parse((string)thing));
			}
			set {
				SetSetting ("localonly", Convert.ToInt16(value).ToString());
				OnPropertyChanged ("localonly");
			}
		}

		public string getVoice(string language, int index)
		{
			string retVal = "";
			using (var conn= new SqliteConnection(connection))
			{
				conn.Open();
				using (var cmd = conn.CreateCommand())
				{
					cmd.CommandText = String.Format("SELECT COUNT(*) as test FROM Voices WHERE lang = '{0}' AND id = {1} ORDER BY VoiceId ASC LIMIT 1;", language, index);
					var exists = Convert.ToBoolean(cmd.ExecuteScalar ());
					if(exists) {
						cmd.CommandText = String.Format("SELECT voice FROM Voices WHERE lang = '{0}' AND id = {1} ORDER BY VoiceId ASC LIMIT 1;", language, index);
						retVal = (string)cmd.ExecuteScalar ();
					} else {
						// just stuff a default voice in
						retVal = "com.apple.speech.synthesis.voice.Agnes";
					}
				}
			}
			return retVal;
		}

		public List<Voice> getAllVoices()
		{
			List<Voice> voices = new List<Voice> ();
			using (var conn= new SqliteConnection(connection))
			{
				conn.Open();
				using (var cmd = conn.CreateCommand())
				{
					cmd.CommandText = "SELECT * FROM Voices ORDER BY lang, id;";
					IDataReader reader = cmd.ExecuteReader ();
					while(reader.Read()) {
						var v = new Voice ();
						v.VoiceId = reader.GetInt32 (0);
						v.VoiceString = reader.GetString (1);
						v.Language = reader.GetString (2);
						v.Id = reader.GetInt32 (3);
						voices.Add (v);
					}
					reader.Close ();
					reader.Dispose ();
				}
			}
			return voices;
		}

		public void setVoice(int id, string language, int index, string voice)
		{
			using (var conn= new SqliteConnection(connection))
			{
				conn.Open();
				using (var cmd = conn.CreateCommand())
				{
					cmd.CommandText = String.Format("UPDATE Voices SET voice = '{0}', id = '{1}', lang = '{2}' WHERE VoiceId = {3};", voice, index, language, id);
					cmd.CommandType = CommandType.Text;
					cmd.ExecuteNonQuery();
				}
			}
		}

		public List<string> getSystemVoices()
		{
			List<string> voices = new List<string> ();
			#if __WIN32__
			using(var speech = new SpeechEngine()) {
				var voicelist = speech.GetInstalledVoices();
				foreach(InstalledVoice v in voicelist) {
					if (v.Enabled)
					{
						Console.WriteLine(v.VoiceInfo.Name);
						voices.Add(v.VoiceInfo.Name);
					}
				}
			}
			#else
			NSApplication.SharedApplication.InvokeOnMainThread (() => {
				foreach (var voice in SpeechEngine.AvailableVoices) {
					voices.Add (voice);
				}
			});
			#endif
				
			return voices;
		}

	}
}



