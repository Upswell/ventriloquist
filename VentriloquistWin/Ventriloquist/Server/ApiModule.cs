using System;
using Nancy;
using Nancy.ModelBinding;

namespace Ventriloquist
{
    public class ApiModule : NancyModule
    {

        public ApiModule(TTSProducer producer)
        {

            Get["/"] = _ =>
            {
                return HttpStatusCode.NotFound;
            };

            Get["/api"] = _ =>
            {
                return HttpStatusCode.NotFound;
            };

            Get["/status"] = _ =>
            {
                return View["Status"];
            };

            Get["/api/test"] = _ =>
            {
                return View["Test"];
            };

            Get["/api/tts"] = _ =>
            {
                return HttpStatusCode.NotFound;
            };

            Post["/api/tts", true] = async (parameters, context) =>
            {
                // bind model
                var ttsrequest = this.Bind<TTSRequest>();
                if (ttsrequest.Text.Trim().Length == 0)
                {
                    return HttpStatusCode.BadRequest;
                }
                // queue the tts request
                producer.QueueRequest(ttsrequest);
                return HttpStatusCode.OK;
            };
        }
    }
}