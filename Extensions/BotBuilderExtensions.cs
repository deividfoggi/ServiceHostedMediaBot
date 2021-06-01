namespace Microsoft.Extensions.DependencyInjection
{
    using System;
    using ServiceHostedMediaBot.Bot;

    public static class BotBuilderExtensions
    {
        public static IServiceCollection AddBot(this IServiceCollection services)
            => services.AddBot(_ => { });

        public static IServiceCollection AddBot(this IServiceCollection services, Action<BotOptions> botOptionsAction)
        {
            var options = new BotOptions();
            botOptionsAction(options);
            services.AddSingleton(options);

            return services.AddSingleton<Bot>();
        }
    }
}
