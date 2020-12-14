using Moesif.Api;
using Moesif.Api.Models;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Collections.Concurrent;


namespace ApimEventProcessor
{
    public class MoesifHttpMessageProcessor : IHttpMessageProcessor
    {
        private readonly string RequestTimeName = "MoRequestTime";
        private MoesifApiClient _MoesifClient;
        private ILogger _Logger;
        private string _SessionTokenKey;
        private string _ApiVersion;
        ConcurrentDictionary<Guid, HttpMessage> requestsCache = new ConcurrentDictionary<Guid, HttpMessage>();
        ConcurrentDictionary<Guid, HttpMessage> responsesCache = new ConcurrentDictionary<Guid, HttpMessage>();
        private readonly object qLock = new object();
        
        public MoesifHttpMessageProcessor(ILogger logger)
        {
            var appId = Environment.GetEnvironmentVariable("APIMEVENTS-MOESIF-APPLICATION-ID", EnvironmentVariableTarget.Process);
            _MoesifClient = new MoesifApiClient(appId);
            _SessionTokenKey = Environment.GetEnvironmentVariable("APIMEVENTS-MOESIF-SESSION-TOKEN", EnvironmentVariableTarget.Process);
            _ApiVersion = Environment.GetEnvironmentVariable("APIMEVENTS-MOESIF-API-VERSION", EnvironmentVariableTarget.Process);
            _Logger = logger;
        }

        public async Task ProcessHttpMessage(HttpMessage message)
        {
            // message will probably contain either HttpRequestMessage or HttpResponseMessage
            // So we cache both request and response and cache them. 
            // Note, response message might be processed before request
            if (message.HttpRequestMessage != null){
                _Logger.LogDebug("Received req: " + message.MessageId);
                message.HttpRequestMessage.Properties.Add(RequestTimeName, DateTime.UtcNow);
                requestsCache.TryAdd(message.MessageId, message);
            }
            if (message.HttpResponseMessage != null){
                _Logger.LogDebug("Received resp: " + message.MessageId);
                responsesCache.TryAdd(message.MessageId, message);
            }
            await SendCompletedMessagesToMoesif();
        }

        /*
        From requestCache and responseCache, find all messages that have request and response
        Send them to Moesif asynchronously.
        */
        public async Task SendCompletedMessagesToMoesif()
        {
            var completedMessages = RemoveCompletedMessages();
            _Logger.LogDebug("Sending completed Messages to Moesif. Count: " + completedMessages.Count);
            if (completedMessages.Count > 0)
            {
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
                events.Add(await BuildMoesifEvent(kv.Key, kv.Value));
            }
            return events;
        }

        /**
        From Http request and response, construct the moesif EventModel
        */
        public async Task<EventModel> BuildMoesifEvent(HttpMessage request, HttpMessage response){
            _Logger.LogDebug("Building Moesif event");
            EventRequestModel moesifRequest = new EventRequestModel()
            {
                Time = (DateTime) request.HttpRequestMessage.Properties[RequestTimeName],
                Uri = request.HttpRequestMessage.RequestUri.OriginalString,
                Verb = request.HttpRequestMessage.Method.ToString(),
                Headers = ToHeaders(request.HttpRequestMessage.Headers),
                ApiVersion = _ApiVersion,
                IpAddress = null,
                Body = request.HttpRequestMessage.Content != null ? System.Convert.ToBase64String(await request.HttpRequestMessage.Content.ReadAsByteArrayAsync()) : null,
                TransferEncoding = "base64"
            };

            EventResponseModel moesifResponse = new EventResponseModel()
            {
                Time = DateTime.UtcNow,
                Status = (int) response.HttpResponseMessage.StatusCode,
                IpAddress = Environment.MachineName,
                Headers = ToHeaders(response.HttpResponseMessage.Headers),
                Body = response.HttpResponseMessage.Content != null ? System.Convert.ToBase64String(await response.HttpResponseMessage.Content.ReadAsByteArrayAsync()) : null,
                TransferEncoding = "base64"
            };

            Dictionary<string, string> metadata = new Dictionary<string, string>();
            metadata.Add("ApimMessageId", request.MessageId.ToString());

            String skey = null;
            if((_SessionTokenKey != null) && (request.HttpRequestMessage.Headers.Contains(_SessionTokenKey)) )
                skey = request.HttpRequestMessage.Headers.GetValues(_SessionTokenKey).FirstOrDefault();
            
            EventModel moesifEvent = new EventModel()
            {
                Request = moesifRequest,
                Response = moesifResponse,
                SessionToken = skey,
                Tags = null,
                UserId = null,
                Metadata = metadata
            };
            return moesifEvent;
        }

        private static Dictionary<string, string> ToHeaders(HttpHeaders headers)
        {
            IEnumerable<KeyValuePair<string, IEnumerable<string>>> enumerable = headers.GetEnumerator().ToEnumerable();
            return enumerable.ToDictionary(p => p.Key, p => p.Value.GetEnumerator()
                                                             .ToEnumerable()
                                                             .ToList()
                                                             .Aggregate((i, j) => i + ", " + j));
        }
    }
}

