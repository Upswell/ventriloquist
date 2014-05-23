using System;
using System.Collections;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ventriloquist
{
	public class InstrumentationEvent
	{

		public string EventName { get; set; }
		public Hashtable Data { get; set; }

		public InstrumentationEvent ()
		{

		}

		public string EventMessage()
		{

			JObject json = new JObject(
				new JProperty("event", this.EventName),
				new JProperty("eventdata", JObject.FromObject(Data))
			);
			return json.ToString();
		}
	}
}
