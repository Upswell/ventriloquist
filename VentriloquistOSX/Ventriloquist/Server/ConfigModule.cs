using System;
using Nancy;
using Nancy.ModelBinding;

namespace Ventriloquist
{
	public class ConfigModule : NancyModule
	{

		public ConfigModule () : base("/config")
		{

			Config config = Config.GetInstance ();

			Get ["/"] = _ => {
				return View["Config"];
			};

			Get ["/voicelist"] = _ => {
				return Response.AsJson(config.getAllVoices());
			};

			Get ["/syslist"] = _ => {
				return Response.AsJson(config.getSystemVoices());
			};

			Post ["/"] = _ => {
				// bind model
				var voice = this.Bind<Voice> ();
				voice.Save();
				return HttpStatusCode.OK;
			};
		}
	}
}


