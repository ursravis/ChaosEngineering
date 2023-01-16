using Confluent.Kafka;

namespace Consumer
{
    public class MessageProcessor:IMessageProcessor 
    {
        public async Task<ProcessResponse> ProcessAsync(ConsumeResult<string, string>? consumeResult, CancellationToken cancellationToken)
        {
            //Make an Http call here
            await Task.Delay(100, cancellationToken);
            Console.WriteLine(consumeResult?.Offset);
            var response=new ProcessResponse
            {
                IsSuccess = true
            };
            return response;
        }
    }
}
