namespace ServiceHostedMediaBot.Authentication
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Graph;
    using Microsoft.Graph.Communications.Client.Authentication;
    using Microsoft.Graph.Communications.Common;

    public class AuthenticationWrapper : IRequestAuthenticationProvider, IAuthenticationProvider
    {

        private readonly IRequestAuthenticationProvider authenticationProvider;
        private readonly string tenant;

        public AuthenticationWrapper(IRequestAuthenticationProvider authenticationProvider, string tenant = null)
        {
            this.authenticationProvider = authenticationProvider.NotNull(nameof(authenticationProvider));
            this.tenant = tenant;
        }

        public Task AuthenticateOutboundRequestAsync(HttpRequestMessage request, string tenant)
        {
            return this.authenticationProvider.AuthenticateOutboundRequestAsync(request, tenant);
        }

        public Task<RequestValidationResult> ValidateInboundRequestAsync(HttpRequestMessage request)
        {
            return this.authenticationProvider.ValidateInboundRequestAsync(request);
        }

        public Task AuthenticateRequestAsync(HttpRequestMessage request)
        {
            return this.AuthenticateOutboundRequestAsync(request, this.tenant);
        }

    }
}
