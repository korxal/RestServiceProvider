using Microsoft.AspNetCore.Builder;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.IO;
using System;
using NLog;


namespace RestServiceProvider
{


    #region Description
    // 
    // This middleware just reads request parameters and calls for corresponding method in Dispatcher
    // 
    #endregion

    //Custom middleware type
    public static class MiddlewareExtensions
    {
        public static IApplicationBuilder UseRestServiceProviderMiddleware(this IApplicationBuilder app, RequestDispatcher requestDispatcher) => app.UseMiddleware<RestServiceMiddleware>(requestDispatcher);
    }


    public class RestServiceMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly RequestDispatcher Dispatcher;
        private static Logger l = NLog.LogManager.GetCurrentClassLogger();

        public RestServiceMiddleware(RequestDelegate next, RequestDispatcher dispatcher)
        {
            Dispatcher = dispatcher;
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            context.Response.ContentType = "application/json";//RFC4627
            string path = context.Request.Path;

            //Check if we know this method
            if (!Dispatcher.IsMethodRegistered(path))
            {
                context.Response.StatusCode = 404;
                l.Info($"Attempted to call unregistered method {path}");
                return;
            }

            //Fill parameters 
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            foreach (var v in context.Request.Query)
                parameters[v.Key] = v.Value.ToString();

            //Read request body
            string body = null;
            using (StreamReader reader = new StreamReader(context.Request.Body, System.Text.Encoding.UTF8, true, 1024, true))
                body = reader.ReadToEnd();

            // Should be used if we have more than one middleware
            // context.Request.EnableBuffering()
            // context.Request.Body.Position = 0;

            //Assign request id
            Guid rid = Guid.NewGuid();
            l.Trace($"Request {rid} Calling {path} with parameters {context.Request.QueryString} request body:\r\n{body}\r\n\r\n");
            string rez = "";

            try
            {
                rez = Dispatcher.InvokeMethod(path, parameters, body);
            }
            catch (Exception e)
            {
                l.Warn($"Error processing request {rid} :{e.Message}\r\n{e.StackTrace}\r\n\r\n");
                context.Response.StatusCode = 500;
                rez = $"RID:{rid}";
            }

            l.Trace($"Request result {rid} :\r\n{rez}\r\n\r\n");

            await context.Response.WriteAsync(rez);
        }
    }

}
