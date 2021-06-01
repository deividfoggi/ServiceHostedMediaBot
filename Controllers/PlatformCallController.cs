namespace ServiceHostedMediaBot.Controller
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Graph.Communications.Common.Telemetry;
    using ServiceHostedMediaBot.Bot;

    public class PlatformCallController : Controller
    {
        private readonly IGraphLogger graphLogger;
        private readonly Bot bot;

        public PlatformCallController(Bot bot)
        {
            this.bot = bot;
            this.graphLogger = bot.GraphLogger.CreateShim(nameof(PlatformCallController));
        }

        [HttpPost]
        [Route(ControllerConstants.CallbackPrefix)]
        public async Task OnIncomingBotCallUserRequestAsync()
        {
            await this.bot.ProcessNotificationAsync(this.Request, this.Response).ConfigureAwait(false);
        }

    }
}
