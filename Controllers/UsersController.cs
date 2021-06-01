namespace ServiceHostedMediaBot.Controller
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using ServiceHostedMediaBot.Bot;
    using ServiceHostedMediaBot.Data;
    using ServiceHostedMediaBot.Extensions;
    using Microsoft.Graph.Communications.Common;

    [Route("User")]
    public class UsersController : Controller
    {
        private Bot bot;

        public UsersController(Bot bot)
        {
            this.bot = bot;
        }

        [HttpPost("raise")]
        public async Task<IActionResult> PostNotificationsAsync([FromBody] UserRequestData userRequestData)
        {
            try
            {
                Validator.NotNull(userRequestData, nameof(userRequestData), "UserRequestData is Null.");
                Validator.NotNullOrWhitespace(userRequestData.ObjectId, nameof(userRequestData.ObjectId), "Object Id is Null or Whitespace.");

                await this.bot.BotCallsUsersAsync(userRequestData).ConfigureAwait(false);

                return this.Ok("Bot got a notification to call the user.");
            }
            catch (Exception e)
            {
                return this.Exception(e);
            }
        }
    }
}
