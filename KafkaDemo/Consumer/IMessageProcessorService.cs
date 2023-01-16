using System.Diagnostics.CodeAnalysis;
using Confluent.Kafka;

namespace Consumer
{
    public interface IMessageProcessor
    {
        Task<ProcessResponse> ProcessAsync([NotNull] ConsumeResult<string, string>? consumeResult, CancellationToken cancellationToken);
    }
}
