namespace ServiceHostedMediaBot.Authentication
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.IdentityModel.Tokens.Jwt;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Security.Claims;
    using Microsoft.Graph.Communications.Client.Authentication;
    using Microsoft.Graph.Communications.Common;
    using Microsoft.Graph.Communications.Common.Telemetry;
    using Microsoft.IdentityModel.Clients.ActiveDirectory;
    using Microsoft.IdentityModel.Protocols;
    using Microsoft.IdentityModel.Protocols.OpenIdConnect;
    using Microsoft.IdentityModel.Tokens;

    public class AuthenticationProvider : ObjectRoot, IRequestAuthenticationProvider
    {

        private readonly string appName;

        private readonly string appId;

        private readonly string appSecret;

        private readonly TimeSpan openIdConfigRefreshInterval = TimeSpan.FromHours(2);

        private DateTime prevOpenIdConfigUpdateTimestamp = DateTime.MinValue;

        private OpenIdConnectConfiguration openIdConnectConfiguration;

        public AuthenticationProvider(string appName, string appId, string appSecret, IGraphLogger logger)
            : base(logger.NotNull(nameof(logger)).CreateShim(nameof(AuthenticationProvider)))
        {
            this.appName = appName.NotNullOrWhitespace(nameof(appName));
            this.appId = appId.NotNullOrWhitespace(nameof(appId));
            this.appSecret = appSecret.NotNullOrWhitespace(nameof(appSecret));
        }

        public async Task AuthenticateOutboundRequestAsync(HttpRequestMessage request, string tenant)
        {
            const string schema = "Bearer";
            const string replaceString = "{tenant}";
            const string oauthV2TokenLink = "https://login.microsoft.com/{tenant}";
            const string resource = "https://graph.microsoft.com";

            tenant = string.IsNullOrWhiteSpace(tenant) ? "common" : tenant;
            var tokenLink = oauthV2TokenLink.Replace(replaceString, tenant);

            this.GraphLogger.Info("AuthenticationProvider: Generating OAuth token.");
            var context = new AuthenticationContext(tokenLink);
            var creds = new ClientCredential(this.appId, this.appSecret);

            AuthenticationResult result;
            try
            {
                result = await this.AcquireTokenWithRetryAsync(context, resource, creds, attempts: 3).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this.GraphLogger.Error(ex, $"Failed to generate token for client: {this.appId}");
                throw;
            }

            this.GraphLogger.Info($"Authentication Provider: Generated OAuth token. Expires in {result.ExpiresOn.Subtract(DateTimeOffset.UtcNow).TotalMinutes} minutes.");

            request.Headers.Authorization = new AuthenticationHeaderValue(schema, result.AccessToken);
        }

        [Obsolete]
        public async Task<RequestValidationResult> ValidateInboundRequestAsync(HttpRequestMessage request)
        {
            var token = request?.Headers?.Authorization?.Parameter;
            if (string.IsNullOrWhiteSpace(token))
            {
                return new RequestValidationResult { IsValid = false };
            }

            const string authDomain = "https://api.aps.skype.com/v1/.well-known/OpenIdConfiguration";
            if(this.openIdConnectConfiguration == null || DateTime.Now > this.prevOpenIdConfigUpdateTimestamp.Add(this.openIdConfigRefreshInterval))
            {
                this.GraphLogger.Info("Updating OpenID configuration");

                IConfigurationManager<OpenIdConnectConfiguration> configurationManager =
                    new ConfigurationManager<OpenIdConnectConfiguration>(

                        authDomain,
                        new OpenIdConnectConfigurationRetriever());
                this.openIdConnectConfiguration = await configurationManager.GetConfigurationAsync(CancellationToken.None).ConfigureAwait(false);

                this.prevOpenIdConfigUpdateTimestamp = DateTime.Now;
            }

            var authIssuers = new[]
            {
                "https://graph.microsoft.com",
                "https://api.botframework.com",
            };

            TokenValidationParameters validationParameters = new TokenValidationParameters
            {
                ValidIssuers = authIssuers,
                ValidAudience = this.appId,
                IssuerSigningKeys = this.openIdConnectConfiguration.SigningKeys,
            };

            ClaimsPrincipal claimsPrincipal;
            try
            {
                JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
                claimsPrincipal = handler.ValidateToken(token, validationParameters, out _);
            }
            catch(Exception ex)
            {
                this.GraphLogger.Error(ex, $"Failed to validate token for client: {this.appId}.");
                return new RequestValidationResult() { IsValid = false };
            }

            const string ClaimType = "https://schemas.microsoft.com/identity/claims/tenantid";
            var tenantClaim = claimsPrincipal.FindFirst(claim => claim.Type.Equals(ClaimType, StringComparison.Ordinal));

            if (string.IsNullOrEmpty(tenantClaim?.Value))
            {
                return new RequestValidationResult { IsValid = false };
            }

            request.Properties.Add(HttpConstants.HeaderNames.Tenant, tenantClaim.Value);
            return new RequestValidationResult { IsValid = true, TenantId = tenantClaim.Value };
        }

        private async Task<AuthenticationResult> AcquireTokenWithRetryAsync(AuthenticationContext context, string resource, ClientCredential creds, int attempts)
        {
            while (true)
            {
                attempts--;

                try
                {
                    return await context.AcquireTokenAsync(resource, creds).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    if(attempts < 1)
                    {
                        throw;
                    }
                }

                await Task.Delay(1000).ConfigureAwait(false);
            }
        }
    }
}
