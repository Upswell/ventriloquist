using System;
using Nancy.TinyIoc;
using Nancy;
using Nancy.Diagnostics;
using log4net;
using System.IO;

namespace Ventriloquist
{
	public class ServerBootstrapper : DefaultNancyBootstrapper
	{

		private static readonly ILog logger = LogManager.GetLogger("api");
		private readonly Config config = Config.GetInstance();

		// override initial startup and add TTSProducerConsumer as a singleton
		// this makes the TTSProducerConsumer available in each request
		// (NancyModule lifetime is per request, not for the life of the application)
		protected override void ConfigureApplicationContainer (TinyIoCContainer container)
		{
			container.Register<TTSProducer> ().AsSingleton ();
		}

		protected override void RequestStartup (TinyIoCContainer container, Nancy.Bootstrapper.IPipelines pipelines, NancyContext context)
		{
			base.RequestStartup (container, pipelines, context);

			// Log the API request w/ payload
			if(context.Request.Path.StartsWith("/api", StringComparison.InvariantCulture)) {
				var todoJson = new StreamReader (context.Request.Body).ReadToEnd ();
				logger.Info (context.Request.Method + " " + context.Request.Path + " -> " + todoJson);
			}
		}

		protected override DiagnosticsConfiguration DiagnosticsConfiguration
		{
			get { return new DiagnosticsConfiguration { Password = @"C0nFig!"}; }
		}

		// Kill the default favicon
		protected override byte[] FavIcon {
			get {
				return null;
			}
		}
	}
}

