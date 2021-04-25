using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using System.Collections.Generic;
using System.Threading;
using System;



#region Description
/*
Main file for RestServiceProdvider.
RestProvider converts ANY c# class instacne to Rest service.

    Example:
    public class TestClass
    {
        public string Greet(string name) => "Hello, " + name;
    }
    
    To create a service from this class:

    var  tc = new TestClass();
    var restProvider = new RestServiceProvider.RestProvider();
    restProvider.RegisterApi(tc); //This is where magic happens
    restProvider.Start(); // You can also register additional api`s after start

    And that`s it, you can now open in browser localhost:5000/Greet?name=John
            
*/
#endregion

namespace RestServiceProvider
{
    public class RestProvider
    {
        private IWebHost host = null;
        private CancellationTokenSource StopTokenSouce;

        RequestDispatcher requestDispatcher;
        public RestProvider()
        {
            requestDispatcher = new RequestDispatcher();
        }


        public void RegisterApi(object Api, string ApiPrefix="")
        {
            CodeGen gen = new CodeGen();
            var asm = gen.BuildServiceProviderInstance(Api);

            foreach (string MethodName in gen.GetMethods)
            {
                var method = asm.GetType().GetMethod(MethodName); // FIXME Non public method filter 
                //create delegate for method
                var _delegate = Delegate.CreateDelegate(
                     type: typeof(Func<Dictionary<string, string>, string, string>),
                     method: method,
                     firstArgument: asm);
                //cast it to required type
                var castedDelegate = (Func<Dictionary<string, string>, string, string>)_delegate;
                //register it
                requestDispatcher.RegisterMethod(string.IsNullOrEmpty(ApiPrefix)? MethodName: ApiPrefix+"/"+MethodName, castedDelegate);
            }
        }


        public void Start(uint port=5000)
        {
            if (StopTokenSouce != null) return;
            StopTokenSouce = new CancellationTokenSource();
            Startup startup = new Startup(requestDispatcher);

            host = new WebHostBuilder()
                    .UseKestrel()
                    .ConfigureServices(services => { services.AddSingleton<IStartup>(startup); })
                    .UseUrls($"http://0.0.0.0:{port}/")
                    .Build();

            host.StartAsync(StopTokenSouce.Token);
        }



        public void Stop()
        {

            try
            {
                if (host != null)
                {
                    if (StopTokenSouce != null) StopTokenSouce.Cancel();
                    if (host != null) host.Dispose();
                    host = null;
                    if (StopTokenSouce != null) StopTokenSouce.Dispose();
                    StopTokenSouce = null;
                }
            }
            catch
            {

            }

        }

    }
}
