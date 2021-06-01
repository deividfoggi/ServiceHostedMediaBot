using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Communications.Common.Telemetry;
using ServiceHostedMediaBot.Logging;

namespace ServiceHostedMediaBot
{
    public class Startup
    {
        private readonly GraphLogger logger;
        private readonly SampleObserver observer;
        public Startup(IConfiguration configuration)
        {
            this.Configuration = configuration;
            this.logger = new GraphLogger(typeof(Startup).Assembly.GetName().Name);
            this.observer = new SampleObserver(this.logger);
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddSingleton(this.observer)
                .AddSingleton<IGraphLogger>(this.logger);

            services
                .AddBot(options => this.Configuration.Bind("Bot", options))
                .AddMvc()
                .SetCompatibilityVersion(CompatibilityVersion.Latest);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            this.logger.BindToILoggerFactory(loggerFactory);

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();

            app.UseMvc();
        }
    }
}
