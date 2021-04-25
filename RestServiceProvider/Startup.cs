using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;


namespace RestServiceProvider
{
    #region Description
    // 
    // Ordinary startup class for asp.net. Nothing to see here.
    // 
    #endregion

    public class Startup : IStartup
    {
        public IServiceProvider ConfigureServices(IServiceCollection services) => services.BuildServiceProvider();
        private RequestDispatcher Dispatcher;
        public Startup(RequestDispatcher requestDispatcher) => Dispatcher = requestDispatcher;
        public void Configure(IApplicationBuilder app) => app.UseRestServiceProviderMiddleware(Dispatcher);
    }
}
