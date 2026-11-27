#nullable disable

using StackExchange.Redis;
using System.Text.Json;

namespace PharmazapPlus.API.Services.Queue;

public class RedisMessageQueue : IMessageQueue, IDisposable
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisMessageQueue> _logger;

    public RedisMessageQueue(
        IConnectionMultiplexer redis,
        ILogger<RedisMessageQueue> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task EnqueueAsync<T>(string queueName, T message)
    {
        try
        {
            var db = _redis.GetDatabase();
            var serialized = JsonSerializer.Serialize(message);

            await db.ListLeftPushAsync(queueName, serialized);

            _logger.LogInformation("EnqueueAsync: mensagem enfileirada em {QueueName}. Conteúdo: {Content}", queueName, serialized);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enqueuing message to {QueueName}", queueName);
            throw;
        }
    }

    public async Task<T> DequeueAsync<T>(string queueName)
    {
        try
        {
            var db = _redis.GetDatabase();
            var result = await db.ListRightPopAsync(queueName);

            if (result.HasValue)
            {
                var message = JsonSerializer.Deserialize<T>(result.ToString());
                _logger.LogInformation("DequeueAsync: mensagem removida de {QueueName}. Conteúdo: {Content}", queueName, result.ToString());
                return message;
            }

            return default;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dequeuing message from {QueueName}", queueName);
            throw;
        }
    }

    public async Task<long> GetQueueLengthAsync(string queueName)
    {
        try
        {
            var db = _redis.GetDatabase();
            var length = await db.ListLengthAsync(queueName);
            _logger.LogInformation("GetQueueLengthAsync: fila {QueueName} contém {Length} item(ns)", queueName, length);
            return length;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting queue length for {QueueName}", queueName);
            return 0;
        }
    }

    public async Task<IEnumerable<T>> PeekAsync<T>(string queueName, int count = 10)
    {
        try
        {
            var db = _redis.GetDatabase();
            var items = await db.ListRangeAsync(queueName, 0, count - 1);

            var result = items
                .Where(item => item.HasValue)
                .Select(item => JsonSerializer.Deserialize<T>(item.ToString()))
                .ToList();

            _logger.LogInformation("PeekAsync: consultados {Count} item(ns) da fila {QueueName}. Conteúdo: {Content}",
                result.Count, queueName, JsonSerializer.Serialize(result));

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error peeking queue {QueueName}", queueName);
            return Enumerable.Empty<T>();
        }
    }

    public async Task ClearQueueAsync(string queueName)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(queueName);
            _logger.LogInformation("Queue {QueueName} cleared", queueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing queue {QueueName}", queueName);
            throw;
        }
    }

    public void Dispose()
    {
        _redis?.Dispose();
    }
}