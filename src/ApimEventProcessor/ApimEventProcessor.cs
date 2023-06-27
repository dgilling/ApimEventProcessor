using Microsoft.ServiceBus.Messaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using ApimEventProcessor.Helpers;
using System.Threading.Tasks;

namespace ApimEventProcessor
{
    /// <summary>
    ///  Allows the EventProcessor instances to have services injected into the constructor
    /// </summary>
    public class ApimHttpEventProcessorFactory : IEventProcessorFactory
    {
        private IHttpMessageProcessor _HttpMessageProcessor;
        private ILogger _Logger;

        public ApimHttpEventProcessorFactory(IHttpMessageProcessor httpMessageProcessor, ILogger logger)
        {
            _HttpMessageProcessor = httpMessageProcessor;
            _Logger = logger;
            _Logger.LogDebug("Initialize ApimHttpEventProcessorFactory");
        }

        public IEventProcessor CreateEventProcessor(PartitionContext context)
        {
            var p = new ApimEventProcessor(_HttpMessageProcessor, _Logger);
            _Logger.LogDebug("CreateEventProcessors: Consumer Group: " + context.ConsumerGroupName);
            _Logger.LogDebug("CreateEventProcessors: EventHubPaths: " + context.EventHubPath);
            return p;
        }
    }


    /// <summary>
    /// Accepts EventData from EventHubs, converts to a HttpMessage instances and forwards it to a IHttpMessageProcessor
    /// </summary>
    public class ApimEventProcessor : IEventProcessor
    {
        Stopwatch checkpointStopWatch;
        private ILogger _Logger;
        private IHttpMessageProcessor _MessageContentProcessor;
        private int MAX_EVENTS_ADD_TOCACHE_BEFORE_SEND = 100;

        public ApimEventProcessor(IHttpMessageProcessor messageContentProcessor, ILogger logger)
        {
            _MessageContentProcessor = messageContentProcessor;
            _Logger = logger;
            _Logger.LogDebug("Initialize ApimEventProcessor");
        }


        async Task IEventProcessor.ProcessEventsAsync(PartitionContext context, IEnumerable<EventData> messages)
        {
            _Logger.LogDebug($"Begin: ProcessEventsAsync. PartitionId: {context.Lease.PartitionId}");
            var processedCount = 0;
            foreach (EventData eventData in messages)
            {
                var evt = displayableEvent(context, eventData);
                _Logger.LogDebug("Event received: " + evt);
                try
                {
                    var httpMessage = HttpMessage.Parse(eventData.GetBodyStream());
                    await _MessageContentProcessor.ProcessHttpMessage(httpMessage); // just cache dont send
                    processedCount += 1;
                    var isSend = processedCount >= MAX_EVENTS_ADD_TOCACHE_BEFORE_SEND;
                    if (isSend) {
                        processedCount = 0;
                        await _MessageContentProcessor.ProcessHttpMessage(null); // dont add just send
                    }
                }
                catch (Exception ex)
                {
                    // Policy.xml errors may result in this exception.
                    _Logger.LogError("Error: " + evt + " - " + ex.Message);
                }
            }
            await _MessageContentProcessor.ProcessHttpMessage(null); // dont add just send

            //Call checkpoint every CHECKPOINT_MINIMUM_INTERVAL_MINUTES minutes,
            // so that worker can resume processing from that time back if it restarts.
            if (this.checkpointStopWatch.Elapsed > TimeSpan.FromMinutes(RunParams.CHECKPOINT_MINIMUM_INTERVAL_MINUTES))
            {
                _Logger.LogInfo($"Saving checkpoint for partition:[{context.Lease.PartitionId}] offset[{context.Lease.Offset}] seq[{context.Lease.SequenceNumber}] owner[{context.Lease.Owner}]. "
                                + $"Actual elapsed time: [{this.checkpointStopWatch.Elapsed}] mins. minimum configured is : [{RunParams.CHECKPOINT_MINIMUM_INTERVAL_MINUTES}] mins");
                await context.CheckpointAsync();
                this.checkpointStopWatch.Restart();
            }
            _Logger.LogDebug($"End: ProcessEventsAsync. PartitionId: {context.Lease.PartitionId}");
        }

        public static string displayableEvent(PartitionContext context, EventData evt)
        {
            string t = "";
            try {
                t = string.Format("partition Id: [{0}] Seq: [{1}] Offset:[{2}] Partition Key: [{3}]",
                                                context.Lease.PartitionId,
                                                evt.SequenceNumber,
                                                evt.Offset,
                                                evt.PartitionKey);
            }
            catch (Exception ex){
                Console.WriteLine("Exception in displayableEvent: " + ex.Message);
            }
            return t;
        }


        async Task IEventProcessor.CloseAsync(PartitionContext context, CloseReason reason)
        {
            _Logger.LogInfo("Processor Shutting Down. Eventhub PartitionId: ['{0}'], Reason: '{1}'.", context.Lease.PartitionId, reason);
            if (reason == CloseReason.Shutdown)
            {
                await context.CheckpointAsync();
            }
        }

        Task IEventProcessor.OpenAsync(PartitionContext context)
        {
            _Logger.LogInfo("EventProcessor initialized. Eventhub PartitionId: ['{0}'], Offset: ['{1}']", context.Lease.PartitionId, context.Lease.Offset);
            _Logger.LogInfo("Checkpoints will be after [" 
                            + RunParams.CHECKPOINT_MINIMUM_INTERVAL_MINUTES 
                            + "] mins");
            this.checkpointStopWatch = new Stopwatch();
            this.checkpointStopWatch.Start();
            return Task.FromResult<object>(null);
        }
    }
}