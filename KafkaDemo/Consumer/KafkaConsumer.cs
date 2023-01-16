using System.Net;
using System.Timers;

using Confluent.Kafka;
using Microsoft.VisualBasic;
using Polly;
using Polly.CircuitBreaker;
using Polly.Contrib.Simmy;
using Polly.Contrib.Simmy.Outcomes;
using Polly.Retry;
using Polly.Wrap;

using Timer = System.Timers.Timer;

namespace Consumer
{
    public class KafkaConsumer : BackgroundService
    {
        private readonly AsyncCircuitBreakerPolicy<ProcessResponse> circuitBreakerPolicy;
        private readonly AsyncRetryPolicy<ProcessResponse> retryPolicy;
        private readonly AsyncInjectOutcomePolicy<ProcessResponse> chaosPolicy;
        private int retryCount = 2;
        private TimeSpan durationOfBreak = TimeSpan.FromSeconds(30);
        private ILogger<KafkaConsumer> logger;
        private IMessageProcessor messageProcessor;

        private IConsumer<string, string>? consumer;
        public KafkaConsumer(ILogger<KafkaConsumer> logger, IMessageProcessor messageProcessor)
        {
            this.logger = logger;
            this.messageProcessor = messageProcessor;
            int exceptionsAllowedBeforeBreaking = retryCount + 1;
            circuitBreakerPolicy = Policy.HandleResult<ProcessResponse>(r => !r.IsSuccess)
                .CircuitBreakerAsync<ProcessResponse>(exceptionsAllowedBeforeBreaking, durationOfBreak,
                                                      async (outcome, state, timespan, context) => await OnBreak(outcome, state, timespan, context),
                                                      OnReset, OnHalfOpen);

            retryPolicy = Policy.HandleResult<ProcessResponse>(r => !r.IsSuccess &&
                                                                    circuitBreakerPolicy.CircuitState != CircuitState.Open)
                    .WaitAndRetryAsync(
                        retryCount,
                    retryAttempt =>
                    {
                        TimeSpan timeToWait = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
                        logger.LogInformation($"Waiting {timeToWait.TotalSeconds} seconds, before retrying.");
                        return timeToWait;
                    });
            var faultResponse = new ProcessResponse { IsSuccess = false, Message = "Chaos Monkey" };
            chaosPolicy = MonkeyPolicy.InjectResultAsync<ProcessResponse>(with =>
                with.Result(faultResponse)
                    .InjectionRate(0.5)
                    .Enabled(true)
            );

        }
        protected override async Task ExecuteAsync(CancellationToken stopToken)
        {
            var config = new ConsumerConfig
            {
                BootstrapServers = "localhost:9092",
                GroupId = "foo",
                EnableAutoCommit = true,
                EnableAutoOffsetStore = false,
                //AutoOffsetReset = AutoOffsetReset.Latest,

                //MaxPollIntervalMs = 2*1000
                // EnableAutoCommit = true
            };
            using (consumer = new ConsumerBuilder<string, string>(config).Build())
            {
                consumer.Subscribe(
                    new List<string>
                    {
                        "my-topic"
                    });
               

                while (!stopToken.IsCancellationRequested)
                {
                    await Task.Delay(100);
                    ConsumeResult<string, string>? consumeResult = consumer.Consume(stopToken);
                    try
                    {
                        if (consumeResult != null)
                        {
                            logger.LogInformation($"Received message {consumeResult.Offset.Value}, Partition: {consumeResult.TopicPartition.Partition.Value}");
                            //if (circuitBreakerPolicy.CircuitState != CircuitState.Open)
                            //{
                            var requestData = new Dictionary<string, object>
                                {
                                    { "ConsumeResult", consumeResult }
                                };
                            AsyncPolicyWrap<ProcessResponse> wrapPolicy = Policy.WrapAsync<ProcessResponse>(retryPolicy, circuitBreakerPolicy, chaosPolicy);
                            ProcessResponse responseDto = await wrapPolicy.ExecuteAsync(action: (async (context) => await messageProcessor!.ProcessAsync(context["ConsumeResult"] as ConsumeResult<string, string>, stopToken))
                                                                                        , contextData: requestData);
                            if (responseDto.IsSuccess)
                            {


                                consumer.StoreOffset(consumeResult);

                                //consumer.Commit(consumeResult);
                                logger.LogInformation($"Processed message {consumeResult.Offset.Value}");
                            }
                            //}
                            //else
                            //{
                            //    logger.LogError($"Not allowed to process {consumeResult.Message.Key}, because Circuit state is {circuitBreakerPolicy.CircuitState}");
                            //    // PauseConsumerAtRequest(consumeResult);
                            //}
                        }

                    }
                    catch (BrokenCircuitException ex)
                    {
                        const string exMsg = $"Exception:The circuit is broken, not allowed to make calls until circuit resets.";
                        logger.LogError(ex, exMsg);
                    }
                    catch (Exception ex)
                    {
                        string exMsg = FormattableString.Invariant($"Unable to process {consumeResult.Offset.Value}");
                        logger.LogError(ex, exMsg);
                        //throw;
                    }


                }
            }
        }
        async Task OnBreak(DelegateResult<ProcessResponse> result, CircuitState state, TimeSpan timespan, Context context)
        {

            var consumerResult = context["ConsumeResult"] as ConsumeResult<string, string>;
            TopicPartitionOffset? partition = consumerResult?.TopicPartitionOffset;
            logger.LogInformation($"Circuit broken");
            logger.LogInformation($"Pausing the consumer for partition {consumerResult?.TopicPartition.Partition.Value}");
            consumer?.Pause(new[] { consumerResult?.TopicPartition });

            await Task.Delay(timespan);
            logger.LogInformation($"Resuming the consumer for partition {consumerResult?.TopicPartition.Partition.Value}");
            consumer?.Resume(new[] { partition?.TopicPartition });
            consumer?.Seek(partition);

        }



        void OnReset(Context context)
        {
            logger.LogInformation("Circuit resets ");
        }

        void OnHalfOpen()
        {
            logger.LogInformation("Circuit half opens ");
        }
    }
}
