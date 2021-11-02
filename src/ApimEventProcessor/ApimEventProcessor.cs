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
        }

        public IEventProcessor CreateEventProcessor(PartitionContext context)
        {
            return new ApimEventProcessor(_HttpMessageProcessor, _Logger);
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

        public ApimEventProcessor(IHttpMessageProcessor messageContentProcessor, ILogger logger)
        {
            _MessageContentProcessor = messageContentProcessor;
            _Logger = logger;
        }


        async Task IEventProcessor.ProcessEventsAsync(PartitionContext context, IEnumerable<EventData> messages)
        {

            foreach (EventData eventData in messages)
            {
                var evt = displayableEvent(context, eventData);
                _Logger.LogDebug("Event received: " + evt);
                try
                {
                    var httpMessage = HttpMessage.Parse(eventData.GetBodyStream());
                    await _MessageContentProcessor.ProcessHttpMessage(httpMessage);
                }
                catch (Exception ex)
                {
                    // Policy.xml errors may result in this exception.
                    _Logger.LogError("Error: " + evt + " - " + ex.Message);
                }
            }

            //Call checkpoint every CHECKPOINT_MINIMUM_INTERVAL_MINUTES minutes,
            // so that worker can resume processing from that time back if it restarts.
            if (this.checkpointStopWatch.Elapsed > TimeSpan.FromMinutes(RunParams.CHECKPOINT_MINIMUM_INTERVAL_MINUTES))
            {
                _Logger.LogInfo("Saving checkpoint. Actual: ["
                                + this.checkpointStopWatch.Elapsed
                                + "] mins. minimum configured is : ["
                                + RunParams.CHECKPOINT_MINIMUM_INTERVAL_MINUTES
                                + "] mins");
                await context.CheckpointAsync();
                this.checkpointStopWatch.Restart();
            }
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