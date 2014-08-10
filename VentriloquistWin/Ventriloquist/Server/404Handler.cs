using System;
using Nancy;
using Nancy.ErrorHandling;
using Nancy.Responses;

namespace Ventriloquist
{
    public class N404Handler : IStatusCodeHandler
    {
        public bool HandlesStatusCode(HttpStatusCode statusCode, NancyContext context)
        {
            return statusCode == HttpStatusCode.NotFound;
        }

        public void Handle(HttpStatusCode statusCode, NancyContext context)
        {
            var response = new GenericFileResponse("views/404.html", "text/html");
            response.StatusCode = statusCode;
            context.Response = response;
        }
    }
}
