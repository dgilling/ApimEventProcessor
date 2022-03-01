using Moesif.Api;
using Moesif.Api.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using ApimEventProcessor.Helpers;
using System.Threading;


namespace ApimEventProcessor
{
    public class MoesifHttpMessageProcessor : IHttpMessageProcessor
    {
        private readonly string RequestTimeName = "MoRequestTime";
        private readonly string ReqHeadersName = "ReqHeaders";
        private readonly string UserIdName = "UserId";
        private readonly string CompanyIdName = "CompanyId";
        private readonly string MetadataName = "Metadata";
        private MoesifApiClient _MoesifClient;
        private ILogger _Logger;
        private string _SessionTokenKey;
        private string _ApiVersion;
        // Initialize config dictionary
        private AppConfig appConfig = new AppConfig();
        // Initialized config response
        private Moesif.Api.Http.Response.HttpStringResponse config;
        // App Config samplingPercentage
        private int samplingPercentage;
        // App Config configETag
        private string configETag;
        // App Config lastUpdatedTime
        private DateTime lastUpdatedTime;
        private DateTime lastWorkerRun = DateTime.MinValue;
        ConcurrentDictionary<Guid, HttpMessage> requestsCache = new ConcurrentDictionary<Guid, HttpMessage>();
        ConcurrentDictionary<Guid, HttpMessage> responsesCache = new ConcurrentDictionary<Guid, HttpMessage>();
        private readonly object qLock = new object();
        
        public MoesifHttpMessageProcessor(ILogger logger)
        {
            var appId = ParamConfig.loadNonEmpty(MoesifAppParamNames.APP_ID);
            _MoesifClient = new MoesifApiClient(appId);
            _SessionTokenKey = ParamConfig.loadDefaultEmpty(MoesifAppParamNames.SESSION_TOKEN);
            _ApiVersion = ParamConfig.loadDefaultEmpty(MoesifAppParamNames.API_VERSION);
            _Logger = logger;
            ScheduleWorkerToFetchConfig();
        }

        private void ScheduleWorkerToFetchConfig()
        {
            try {
                new Thread(async () =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    lastWorkerRun = DateTime.UtcNow;
                    // Get Application config
                    config = await appConfig.getConfig(_MoesifClient, _Logger);
                    if (!string.IsNullOrWhiteSpace(config.ToString()))
                    {
                        (configETag, samplingPercentage, lastUpdatedTime) = appConfig.parseConfiguration(config, _Logger);
                    }
                }).Start();
            } catch (Exception ex) {
                _Logger.LogError("Error while parsing application configuration on initialization - " + ex.ToString());
            }
        }

        public async Task ProcessHttpMessage(HttpMessage message)
        {
            // message will probably contain either HttpRequestMessage or HttpResponseMessage
            // So we cache both request and response and cache them. 
            // Note, response message might be processed before request
            try {
                if (message.HttpRequestMessage != null){
                    _Logger.LogDebug("Received [request] with messageId: [" + message.MessageId + "]");
                    message.HttpRequestMessage.Properties.Add(RequestTimeName, message.ContextTimestamp);
                    requestsCache.TryAdd(message.MessageId, message);
                }
                if (message.HttpResponseMessage != null){
                    _Logger.LogDebug("Received [response] with messageId: [" + message.MessageId + "]");
                    responsesCache.TryAdd(message.MessageId, message);
                }
                await SendCompletedMessagesToMoesif();
            }
            catch (Exception ex) {
                 _Logger.LogError("Error Processing and sending message to Moesif:  " + ex.Message);
                 throw ex;
            }
        }

        /*
        From requestCache and responseCache, find all messages that have request and response
        Send them to Moesif asynchronously.
        */
        public async Task SendCompletedMessagesToMoesif()
        {
            var completedMessages = RemoveCompletedMessages();
            if (completedMessages.Count > 0)
            {
                _Logger.LogInfo("Sending completed Messages to Moesif. Count: [" + completedMessages.Count + "]");
                var moesifEvents = await BuildMoesifEvents(completedMessages);
                // Send async to Moesif. To send synchronously, use CreateEventsBatch instead
                await _MoesifClient.Api.CreateEventsBatchAsync(moesifEvents);
            }
        }

        public Dictionary<Guid, KeyValuePair<HttpMessage, HttpMessage>> RemoveCompletedMessages(){
            Dictionary<Guid, KeyValuePair<HttpMessage, HttpMessage>> messages = new Dictionary<Guid, KeyValuePair<HttpMessage, HttpMessage>>();
            lock(qLock){
                foreach(Guid messageId in requestsCache.Keys.Intersect(responsesCache.Keys))
                {
                    HttpMessage reqm, respm;
                    requestsCache.TryRemove(messageId, out reqm);
                    responsesCache.TryRemove(messageId, out respm);
                    messages.Add(messageId, new KeyValuePair<HttpMessage, HttpMessage>(reqm, respm));
                }
            }
            return messages;
        }

        public async Task<List<EventModel>> BuildMoesifEvents(Dictionary<Guid, KeyValuePair<HttpMessage, HttpMessage>> completedMessages)
        {
            List<EventModel> events = new List<EventModel>();
            foreach(KeyValuePair<HttpMessage, HttpMessage> kv in completedMessages.Values)
            {
                var moesifEvent = await BuildMoesifEvent(kv.Key, kv.Value);
                try {
                    // Get Sampling percentage
                    samplingPercentage = appConfig.getSamplingPercentage(config,
                                                                        moesifEvent.UserId,
                                                                        moesifEvent.CompanyId);
                    double randomPercentage;
                    if (isSelectedRandomly(samplingPercentage, out randomPercentage))
                    {
                        moesifEvent.Weight = appConfig.calculateWeight(samplingPercentage);
                        // Add event to the batch
                        events.Add(moesifEvent);
                        runConfigFreshnessCheck();
                    }
                    else
                    {
                        _Logger.LogDebug("Skipped Event due to sampling percentage: ["
                                            + samplingPercentage.ToString() 
                                            + "] and random percentage: [" 
                                            + randomPercentage.ToString()
                                            + "]");
                    }
                } catch (Exception ex) {
                    _Logger.LogError("Error adding event to the batch - " + ex.ToString());
                }
            }
            return events;
        }

        public void runConfigFreshnessCheck()
        {
            int fetchInterval = ParamConfig.loadWithDefault(
                                    MoesifAppParamNames.CONFIG_FETCH_INTERVAL_MINS,
                                    RunParams.CONFIG_FETCH_INTERVAL_MINUTES);
            if (lastWorkerRun.AddMinutes(fetchInterval) < DateTime.UtcNow)
            {
                _Logger.LogDebug("Scheduling worker thread. lastWorkerRun=" + lastWorkerRun.ToString("o"));
                ScheduleWorkerToFetchConfig();
            }
        }

        public static bool isSelectedRandomly(int samplingPercentage, out double randomPercentage)
        {
            randomPercentage = new Random().NextDouble() * 100;
            return samplingPercentage >= randomPercentage;
        }

        /**
        From Http request and response, construct the moesif EventModel
        */
        public async Task<EventModel> BuildMoesifEvent(HttpMessage request, HttpMessage response){
            _Logger.LogDebug("Building Moesif event: [" + request.MessageId + "]");
            EventRequestModel moesifRequest = await genEventRequestModel(request,
                                                                        ReqHeadersName,
                                                                        RequestTimeName,
                                                                        _ApiVersion);
            EventResponseModel moesifResponse = await genEventResponseModel(response);
            Dictionary<string, object> metadata = genMetadata(request, MetadataName);
            string skey = safeGetHeaderFirstOrDefault(request, _SessionTokenKey);
            string userId = safeGetOrNull(request, UserIdName);
            string companyId = safeGetOrNull(request, CompanyIdName);
            ContextModel context = genContext(request.ContextRequestUser);
            EventModel moesifEvent = new EventModel()
            {
                Request = moesifRequest,
                Response = moesifResponse,
                Context = context,
                SessionToken = skey,
                Tags = null,
                UserId = userId,
                CompanyId = companyId,
                Metadata = metadata,
                Direction = "Incoming"
            };
            return moesifEvent;
        }

        public async static Task<EventRequestModel> genEventRequestModel(HttpMessage request,
                                                                        string ReqHeadersName,
                                                                        string RequestTimeName,
                                                                        string _ApiVersion)
        {
            var h = request.HttpRequestMessage;
            var reqBody = h.Content != null 
                            ? await h.Content.ReadAsStringAsync() 
                            : null;
            var reqHeaders = HeadersUtils.deSerializeHeaders(h.Properties[ReqHeadersName]);
            var reqBodyWrapper = BodyUtil.Serialize(reqBody);
            EventRequestModel moesifRequest = new EventRequestModel()
            {
                Time = request.ContextTimestamp,
                Uri = h.RequestUri.OriginalString,
                Verb = h.Method.ToString(),
                Headers = reqHeaders,
                ApiVersion = _ApiVersion,
                IpAddress = request.ContextRequestIpAddress,
                Body = reqBodyWrapper.Item1,
                TransferEncoding = reqBodyWrapper.Item2
            };
            return moesifRequest;
        }

        public async static Task<EventResponseModel> genEventResponseModel(HttpMessage response)
        {
            var h = response.HttpResponseMessage;
            var respBody = h.Content != null 
                                ? await h.Content.ReadAsStringAsync() 
                                : null;
            var respHeaders = ToResponseHeaders(h.Headers,
                                                h.Content.Headers);
            var respBodyWrapper = BodyUtil.Serialize(respBody);
            EventResponseModel moesifResponse = new EventResponseModel()
            {
                Time = response.ContextTimestamp,
                Status = (int) h.StatusCode,
                IpAddress = null,
                Headers = respHeaders,
                Body = respBodyWrapper.Item1,
                TransferEncoding = respBodyWrapper.Item2
            };
            return moesifResponse;
        }

        private static string safeGetHeaderFirstOrDefault(HttpMessage request,
                                                        string headerKey)
        {
            var h = request.HttpRequestMessage.Headers;
            String val = null;
            if(!string.IsNullOrWhiteSpace(headerKey)
                    && h.Contains(headerKey))
                val = h.GetValues(headerKey).FirstOrDefault();
            return val;
        }

        private static string safeGetOrNull(HttpMessage request,
                                            string propertyName)
        {
            var p = request.HttpRequestMessage.Properties;
            return (p.ContainsKey(propertyName) && p[propertyName] != null)
                    ? (string) p[propertyName] 
                    : null;
        }

        private static Dictionary<string, object> genMetadata(HttpMessage request, string MetadataName)
        {
            Dictionary<string, object> metadata = new Dictionary<string, object>();
            var p = request.HttpRequestMessage.Properties;
            if (p[MetadataName] != null)
                metadata = (Dictionary<string, object>) p[MetadataName];
            metadata.Add("ApimMessageId", request.MessageId.ToString());
            return metadata;
        }

        private static Dictionary<string, string> ToResponseHeaders(HttpHeaders headers, HttpContentHeaders contentHeaders)
        {
            Dictionary<string, string> responseHeaders = headerToDict(headers);
            Dictionary<string, string> responseContentHeaders = headerToDict(contentHeaders);
            return responseHeaders.Concat(responseContentHeaders.Where( x=> !responseHeaders.Keys.Contains(x.Key)))
                                    .ToDictionary(k => k.Key, v => v.Value);
        }

        private static Dictionary<string, string> headerToDict(
            IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
        {
            IEnumerable<KeyValuePair<string, IEnumerable<string>>> enumerable = headers.GetEnumerator()
                                                                                        .ToEnumerable();
            return enumerable.ToDictionary(p => p.Key, p => p.Value.GetEnumerator()
                            .ToEnumerable()
                            .ToList()
                            .Aggregate((i, j) => i + ", " + j));
        }

        private ContextModel genContext(String b64EncodedStr)
        {
            ContextModel context = null;
            try {
                if (!string.IsNullOrWhiteSpace(b64EncodedStr)){
                    var jStr = BodyUtil.b64Decode(b64EncodedStr);
                    jStr = jStr.Replace("\r\n", "").Replace("\\", "");
                    if (!(jStr.Trim().Replace(" ", "") == "{}")) // Empty Json
                    {
                        ContextUserModel m = ContextUserModel.deserialize(jStr);
                        if (null != m)
                        {
                            context = new ContextModel();
                            context.User = m;
                        }
                    }
                }
            }
            catch (Exception ex){
                _Logger.LogWarning("Error extracting context.Request.User: " + ex.Message);
            }
            return context;
        }
    }
}
