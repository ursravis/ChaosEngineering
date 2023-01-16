using Confluent.Kafka;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Producer.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {

        }
        protected void print()
        {

        }

        public async Task OnPostPrint()
        {
            try
            {


                var config = new ProducerConfig
                {
                    BootstrapServers = "localhost:9092",
                    //MessageSendMaxRetries = 1,
                    //MessageTimeoutMs = 10000,
                    //Acks = Acks.Leader

                };
                using (IProducer<string, string>? producer = new ProducerBuilder<string, string>(config).Build())
                {
                    for (int i = 0; i < 10; i++)
                    {
                        DeliveryResult<string, string>? result = await producer.ProduceAsync(
                                                                     "my-topic",
                                                                     new Message<string, string>
                                                                     {
                                                                         Key = DateTimeOffset.Now.ToUnixTimeSeconds().ToString(),
                                                                         Value = Guid.NewGuid().ToString()
                                                                     });
                        if (result != null)
                        {
                            ViewData["Message"] = "Sent";
                        }
                    }



                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

        }
    }
}