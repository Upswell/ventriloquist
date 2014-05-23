using System;
using MonoMac.AppKit;

namespace Ventriloquist
{
	public class SoundDelegate : NSSoundDelegate
	{

		public event EventHandler DidComplete;

		public override void DidFinishPlaying (NSSound sound, bool finished)
		{
			EventHandler handler = DidComplete;
			if (null != handler)
				handler (this, EventArgs.Empty);
		}
	}
}

