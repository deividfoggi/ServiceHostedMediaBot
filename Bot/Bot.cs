namespace ServiceHostedMediaBot.Bot
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Extensions;
    using Microsoft.Extensions.Primitives;
    using Microsoft.Graph;
    using Microsoft.Graph.Communications.Client.Authentication;
    using Microsoft.Graph.Communications.Client.Transport;
    using Microsoft.Graph.Communications.Common;
    using Microsoft.Graph.Communications.Common.Telemetry;
    using Microsoft.Graph.Communications.Common.Transport;
    using Microsoft.Graph.Communications.Core.Notifications;
    using Microsoft.Graph.Communications.Core.Serialization;
    using Newtonsoft.Json;
    using ServiceHostedMediaBot.Common;
    using ServiceHostedMediaBot.Transport;
    using ServiceHostedMediaBot.Authentication;
    using ServiceHostedMediaBot.Data;
    using ServiceHostedMediaBot.Controller;
    using ServiceHostedMediaBot.Extensions;
    using Microsoft.Graph.Communications.Client;
    using System.IO;

    public class Bot
    {
        private readonly Uri botBaseUri;
        private string appId;
        private string tenantId;
        private string appInstanceObjectId;
        private string appInstanceObjectIdName;

        public Bot(BotOptions options, IGraphLogger graphLogger)
        {
            this.botBaseUri = options.BotBaseUrl;
            this.appId = options.AppId;
            this.tenantId = options.TenantId;
            this.appInstanceObjectId = options.AppInstanceObjectId;
            this.appInstanceObjectIdName = options.AppInstanceObjectName;

            this.GraphLogger = graphLogger;
            var name = this.GetType().Assembly.GetName().Name;
            this.AuthenticationProvider = new AuthenticationProvider(name, options.AppId, options.AppSecret, graphLogger);
            this.Serializer = new CommsSerializer();

            var authenticationWrapper = new AuthenticationWrapper(this.AuthenticationProvider);
            this.NotificationProcessor = new NotificationProcessor(authenticationWrapper, this.Serializer);
            this.NotificationProcessor.OnNotificationReceived += this.NotificationProcessor_OnNotificationReceived;
            this.RequestBuilder = new GraphServiceClient(options.PlaceCallEndpointUrl.AbsoluteUri, authenticationWrapper);

            var defaultProperties = new List<IGraphProperty<IEnumerable<string>>>();
            using (HttpClient tempClient = GraphClientFactory.Create(authenticationWrapper))
            {
                defaultProperties.AddRange(tempClient.DefaultRequestHeaders.Select(header => GraphProperty.RequestProperty(header.Key, header.Value)));
            }

            var productInfo = new ProductInfoHeaderValue(
                typeof(Bot).Assembly.GetName().Name,
                typeof(Bot).Assembly.GetName().Version.ToString());
            this.GraphApiClient = new GraphAuthClient(
                this.GraphLogger,
                this.Serializer.JsonSerializerSettings,
                new HttpClient(),
                this.AuthenticationProvider,
                productInfo,
                defaultProperties);
        }

        public IGraphLogger GraphLogger { get; set; }
        
        public IRequestAuthenticationProvider AuthenticationProvider { get; }

        public INotificationProcessor NotificationProcessor { get; }

        public GraphServiceClient RequestBuilder { get; }

        public CommsSerializer Serializer { get; }

        public IGraphClient GraphApiClient { get; }

        public async Task BotCallsUsersAsync(UserRequestData userRequestData)
        {
            var scenarioId = Guid.NewGuid();

            var TargetIdentity = new IdentitySet();
            TargetIdentity.SetPhone(
                new Identity
                {
                    Id = userRequestData.UraNumber,
                    DisplayName = userRequestData.UraNumber
                });

            var requestCall = new Call
            {
                Source = new ParticipantInfo
                {
                    Identity = new IdentitySet
                    {
                        Application = new Identity
                        {
                            Id = this.appId,
                        },
                    },
                },
                Targets = new List<InvitationParticipantInfo>()
                {
                    new InvitationParticipantInfo
                    {
                        Identity = TargetIdentity
                    },
                },
                MediaConfig = new ServiceHostedMediaConfig()
                {
                },
                RequestedModalities = new List<Modality> { Modality.Audio },
                TenantId = this.tenantId,
                Direction = CallDirection.Outgoing,
                CallbackUri = new Uri(this.botBaseUri, ControllerConstants.CallbackPrefix).ToString(),
            };

            requestCall.Source.Identity.SetApplicationInstance(
                new Identity
                {
                    Id = this.appInstanceObjectId,
                    DisplayName = this.appInstanceObjectIdName,
                });

            var callRequest = this.RequestBuilder.Communications.Calls;
            var request = new GraphRequest<Call>(new Uri(callRequest.RequestUrl), requestCall, RequestType.Create);
            await this.GraphApiClient.SendAsync<Call, Call>(request, requestCall.TenantId, scenarioId).ConfigureAwait(false);
        }

        public async Task ProcessNotificationAsync(
            HttpRequest request,
            HttpResponse response)
        {
            var headers = request.Headers.Select(
                pair => new KeyValuePair<string, IEnumerable<string>>(pair.Key, pair.Value));

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            this.GraphLogger.LogHttpMessage(
                TraceLevel.Verbose,
                TransactionDirection.Incoming,
                HttpTraceType.HttpRequest,
                request.GetDisplayUrl(),
                request.Method,
                obfuscatedContent: null,
                headers: headers);

            try
            {
                var httpRequest = request.CreateRequestMessage();
                var results = await this.AuthenticationProvider.ValidateInboundRequestAsync(httpRequest).ConfigureAwait(false);
                if (results.IsValid)
                {
                    var httpResponse = await this.NotificationProcessor.ProcessNotificationAsync(httpRequest).ConfigureAwait(false);
                    await httpResponse.CreateHttpResponseAsync(response).ConfigureAwait(false);
                }
                else
                {
                    // This way is not working. Demands further investigation
                    //var httpResponse = httpRequest.CreateResponse(HttpStatusCode.Forbidden);
                    var httpResponse = new HttpResponseMessage(HttpStatusCode.Forbidden);
                    await httpResponse.CreateHttpResponseAsync(response).ConfigureAwait(false);
                }

                headers = response.Headers.Select(
                    pair => new KeyValuePair<string, IEnumerable<string>>(pair.Key, pair.Value));

                this.GraphLogger.LogHttpMessage(
                    TraceLevel.Verbose,
                    TransactionDirection.Incoming,
                    HttpTraceType.HttpResponse,
                    request.GetDisplayUrl(),
                    request.Method,
                    obfuscatedContent: null,
                    headers: headers,
                    responseCode: response.StatusCode,
                    responseTime: stopwatch.ElapsedMilliseconds);
            }
            catch (ServiceException e)
            {
                string obfuscatedContent = null;
                if ((int)e.StatusCode >= 300)
                {
                    response.StatusCode = (int)e.StatusCode;
                    await response.WriteAsync(e.ToString()).ConfigureAwait(false);
                    obfuscatedContent = this.GraphLogger.SerializeAndObfuscate(e, Formatting.Indented);
                }
                else if((int)e.StatusCode >= 200)
                {
                    response.StatusCode = (int)e.StatusCode;
                }
                else
                {
                    response.StatusCode = (int)e.StatusCode;
                    await response.WriteAsync(e.ToString()).ConfigureAwait(false);
                    obfuscatedContent = this.GraphLogger.SerializeAndObfuscate(e, Formatting.Indented);
                }

                headers = response.Headers.Select(
                    pair => new KeyValuePair<string, IEnumerable<string>>(pair.Key, pair.Value));

                if (e.ResponseHeaders?.Any() == true)
                {
                    foreach (var pair in e.ResponseHeaders)
                    {
                        response.Headers.Add(pair.Key, new StringValues(pair.Value.ToArray()));
                    }

                    headers = headers.Concat(e.ResponseHeaders);
                }

                this.GraphLogger.LogHttpMessage(
                    TraceLevel.Error,
                    TransactionDirection.Incoming,
                    HttpTraceType.HttpResponse,
                    request.GetDisplayUrl(),
                    request.Method,
                    obfuscatedContent,
                    headers,
                    response.StatusCode,
                    responseTime: stopwatch.ElapsedMilliseconds);
            }
            catch (Exception e)
            {
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                await response.WriteAsync(e.ToString()).ConfigureAwait(false);

                var obfuscatedContent = this.GraphLogger.SerializeAndObfuscate(e, Formatting.Indented);
                headers = response.Headers.Select(
                    pair => new KeyValuePair<string, IEnumerable<string>>(pair.Key, pair.Value));

                this.GraphLogger.LogHttpMessage(
                   TraceLevel.Error,
                   TransactionDirection.Incoming,
                   HttpTraceType.HttpResponse,
                   request.GetDisplayUrl(),
                   request.Method,
                   obfuscatedContent,
                   headers,
                   response.StatusCode,
                   responseTime: stopwatch.ElapsedMilliseconds);
            }
        }

        private void NotificationProcessor_OnNotificationReceived(NotificationEventArgs args)
        {
            this.NotificationProcessor_OnNotificationReceivedAsync(args).ForgetAndLogExceptionAsync(
                this.GraphLogger,
                $"Error processing notification {args.Notification.ResourceUrl} with scenario {args.ScenarioId}");
        }

        private async Task NotificationProcessor_OnNotificationReceivedAsync(NotificationEventArgs args)
        {
            this.GraphLogger.CorrelationId = args.ScenarioId;
            var headers = new[]
            {
                new KeyValuePair<string, IEnumerable<string>>(HttpConstants.HeaderNames.ScenarioId, new[] {args.ScenarioId.ToString() }),
                new KeyValuePair<string, IEnumerable<string>>(HttpConstants.HeaderNames.ClientRequestId, new[] {args.RequestId.ToString() }),
                new KeyValuePair<string, IEnumerable<string>>(HttpConstants.HeaderNames.Tenant, new[] {args.TenantId }),
            };

            var notifications = new CommsNotifications { Value = new[] { args.Notification } };
            var obfuscatedContent = this.GraphLogger.SerializeAndObfuscate(notifications, Formatting.Indented);
            this.GraphLogger.LogHttpMessage(
                TraceLevel.Info,
                TransactionDirection.Incoming,
                HttpTraceType.HttpRequest,
                args.CallbackUri.ToString(),
                HttpMethods.Post,
                obfuscatedContent,
                headers,
                correlationId: args.ScenarioId,
                requestId: args.RequestId);

            if (args.ResourceData is Call call)
            {
                if (call.State == CallState.Established && call.MediaState?.Audio == MediaState.Active)
                {
                    await this.BotRecordsOutgoingCallAsync(call.Id, args.TenantId, args.ScenarioId).ConfigureAwait(false);
                }
                else if (args.ChangeType == ChangeType.Deleted && call.State == CallState.Terminated)
                {
                    this.GraphLogger.Log(TraceLevel.Info, $"Call State:{call.State}");
                }
            }
            else if(args.ResourceData is PlayPromptOperation playPromptOperation)
            {
                if (string.IsNullOrWhiteSpace(playPromptOperation.ClientContext))
                {
                    throw new ServiceException(new Error()
                    {
                        Message = "No call id proided in PlayPromptOperation.ClientContext.",
                    });
                }
                else if( playPromptOperation.Status == OperationStatus.Completed){
                    await this.BotHangupCallAsync(playPromptOperation.ClientContext, args.TenantId, args.ScenarioId).ConfigureAwait(false);
                    this.GraphLogger.Log(TraceLevel.Info, $"Disconnecting the call.");
                }
            }
            else if(args.ResourceData is RecordOperation recordOperation)
            {
                if (recordOperation.Status == OperationStatus.Completed && recordOperation.ResultInfo.Code == 200)
                {
                    var recordingFileName = $"audio/recording-{recordOperation.ClientContext}.wav";

                    await this.DownloadRecording(recordingFileName, recordOperation).ConfigureAwait(false);

                    var prompts = new Prompt[]
                    {
                        new MediaPrompt
                        {
                            MediaInfo = new MediaInfo()
                            {
                                Uri = new Uri(this.botBaseUri, recordingFileName).ToString()
                            },
                        },
                    };

                    await this.BotHangupCallAsync(recordOperation.ClientContext, args.TenantId, args.ScenarioId).ConfigureAwait(false);
                    this.GraphLogger.Log(TraceLevel.Info, $"Disconnecting the call.");
                }
            }
        }

        private async Task BotHangupCallAsync(string callId, string tenantId, Guid scenarioId)
        {
            var hangupRequest = this.RequestBuilder.Communications.Calls[callId].Request();
            await this.GraphApiClient.SendAsync(hangupRequest, RequestType.Delete, tenantId, scenarioId).ConfigureAwait(false);
        }

        private async Task BotRecordsOutgoingCallAsync(string callId, string tenantId, Guid scenarioId)
        {
            var prompts = new Prompt[]
            {
                new MediaPrompt
                {
                    MediaInfo = new MediaInfo()
                    {
                        Uri = new Uri(this.botBaseUri, "audio/speech.wav").ToString(),
                        ResourceId = Guid.NewGuid().ToString(),
                    },
                },
            };

            IEnumerable<string> stopTones = new List<string>() { "#" };
            var recordRequest = this.RequestBuilder.Communications.Calls[callId].RecordResponse(
                bargeInAllowed: true,
                clientContext: callId,
                //prompts: prompts,
                maxRecordDurationInSeconds: 20,
                initialSilenceTimeoutInSeconds: 2,
                maxSilenceTimeoutInSeconds: 2,
                playBeep: true,
                stopTones: stopTones).Request();

            await this.GraphApiClient.SendAsync(recordRequest, RequestType.Create, tenantId, scenarioId).ConfigureAwait(false);
        }

        private async Task<User> GetMobilePhoneNumber(string userObjectId, string tenantId, Guid scenarioId)
        {
            var mobilePhone = await this.RequestBuilder.Users[userObjectId].Request()
                .Select(u => new
                {
                    u.MobilePhone
                }).GetAsync();

            return mobilePhone;
        }

        private async Task BotPlayPromptAsync(string callId, string tenantId, Guid scenarioId, string MediaFile)
        {
            var prompts = new Prompt[]
            {
                new MediaPrompt
                {
                    MediaInfo = new MediaInfo()
                    {
                        Uri = new Uri(this.botBaseUri, MediaFile).ToString(),
                        ResourceId = Guid.NewGuid().ToString(),
                    },
                },
            };

            var playPromptRequest = this.RequestBuilder.Communications.Calls[callId].PlayPrompt(
                prompts: prompts,
                clientContext: callId).Request();
            await this.GraphApiClient.SendAsync<PlayPromptOperation>(playPromptRequest, RequestType.Create, tenantId, scenarioId).ConfigureAwait(false);
        }

        private async Task DownloadRecording(string recordingFileName, RecordOperation recordOperation)
        {
            using (var httpClient = new HttpClient())
            {
                var requestMessage = new HttpRequestMessage(
                    HttpMethod.Get,
                    new Uri(recordOperation.RecordingLocation));
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", recordOperation.RecordingAccessToken);

                var httpResponse = await httpClient.SendAsync(requestMessage).ConfigureAwait(false);

                if (!httpResponse.IsSuccessStatusCode)
                {
                    throw new ServiceException(new Error()
                    {
                        Message = "Unable to download the recording file.",
                    });
                }
                using (var stream = await httpResponse.Content.ReadAsStreamAsync().ConfigureAwait(false))
                {
                    FileStream fileStream = null;
                    var fileInfo = new FileInfo($"wwwroot/{recordingFileName}");
                    using (fileStream = new FileStream(fileInfo.FullName, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await stream.CopyToAsync(fileStream).ConfigureAwait(false);
                    }                
                }
            }
        }
    }
}
