namespace ServiceHostedMediaBot.Bot
{
    using System;

    public class BotOptions
    {
        public string AppId { get; set; }

        public string AppSecret { get; set; }

        public string TenantId { get; set; }

        public string AppInstanceObjectId { get; set; }

        public string AppInstanceObjectName { get; set; }

        public Uri BotBaseUrl { get; set; }

        public Uri PlaceCallEndpointUrl { get; set; }
    }
}
