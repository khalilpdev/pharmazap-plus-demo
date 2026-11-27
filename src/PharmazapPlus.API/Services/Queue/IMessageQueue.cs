namespace PharmazapPlus.API.Services.Queue;

public interface IMessageQueue
{
    Task EnqueueAsync<T>(string queueName, T message);
    Task<T> DequeueAsync<T>(string queueName);
    Task<long> GetQueueLengthAsync(string queueName);
    Task<IEnumerable<T>> PeekAsync<T>(string queueName, int count = 10);
    Task ClearQueueAsync(string queueName);
}
