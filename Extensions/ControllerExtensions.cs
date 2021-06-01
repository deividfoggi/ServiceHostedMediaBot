namespace ServiceHostedMediaBot.Extensions
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Http.Headers;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Primitives;
    using Microsoft.Graph;

    public static class ControllerExtensions
    {
        public static IActionResult Exception(this Controller controller, Exception exception)
        {
            IActionResult result;

            if (exception is ServiceException e)
            {
                controller.HttpContext.Response.CopyHeaders(e.ResponseHeaders);

                int statusCode = (int)e.StatusCode;

                result = statusCode >= 300
                    ? controller.StatusCode(statusCode, e.ToString())
                    : controller.StatusCode((int)HttpStatusCode.InternalServerError, e.ToString());
            }
            else
            {
                result = controller.StatusCode((int)HttpStatusCode.InternalServerError, exception.ToString());
            }

            return result;
        }

        private static void CopyHeaders(this HttpResponse response, HttpHeaders headers)
        {
            if (headers == null)
            {
                return;
            }

            foreach (var header in headers)
            {
                var values = header.Value?.ToArray();
                if (values?.Any() == true)
                {
                    response.Headers.Add(header.Key, new StringValues(values));
                }
            }
        }
    }
}
