using System;
using MonoMac.AppKit;
using log4net;

namespace Ventriloquist
{
	public class SpeechDelegate : NSSpeechSynthesizerDelegate
	{

		public event EventHandler DidComplete;
		private static readonly ILog logger = LogManager.GetLogger(typeof(SpeechDelegate));

		public override void DidFinishSpeaking (NSSpeechSynthesizer sender, bool finishedSpeaking)
		{
			EventHandler handler = DidComplete;
			if (null != handler)
				handler (this, EventArgs.Empty);
		}

		public override void DidEncounterError (NSSpeechSynthesizer sender, uint characterIndex, string theString, string message)
		{
			logger.Warn (message);
		}
	}
}

