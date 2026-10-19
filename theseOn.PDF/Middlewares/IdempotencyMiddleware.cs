using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using theseOn.PDF.Models;

namespace theseOn.PDF.Middlewares;

public class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDistributedCache _cache;
    private readonly SemaphoreSlim _cacheGate = new(1, 1);

    public IdempotencyMiddleware(RequestDelegate next, IDistributedCache cache)
    {
        _next = next;
        _cache = cache;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Method != HttpMethods.Post
            && context.Request.Method != HttpMethods.Patch
            && context.Request.Method != HttpMethods.Put
            && context.Request.Method != HttpMethods.Delete)
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-Idempotency-Value", out var keyValues))
        {
            await _next(context);
            return;
        }

        context.Request.EnableBuffering();
        var hashBytes = await SHA256.HashDataAsync(context.Request.Body, context.RequestAborted);
        var payloadHash = Convert.ToBase64String(hashBytes);
        context.Request.Body.Position = 0;

        var cacheKeyScope = $"{context.Request.Method}:{context.Request.Path}{context.Request.QueryString}:{keyValues}";
        var cacheKey = $"idempotency:v2:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cacheKeyScope)))}";
        await _cacheGate.WaitAsync(context.RequestAborted);
        try
        {
            var cachedString = await _cache.GetStringAsync(cacheKey, context.RequestAborted);
            if (cachedString is not null)
            {
                var cachedItem = JsonSerializer.Deserialize<IdempotencyCacheItem>(cachedString)
                    ?? throw new InvalidOperationException("The cached idempotency response is invalid.");

                if (!string.Equals(cachedItem.PayloadHash, payloadHash, StringComparison.Ordinal))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsJsonAsync(
                        new { error = ResponseMessage.GetResponseMessage(context.Response.StatusCode) },
                        context.RequestAborted);
                    return;
                }

                if (cachedItem.Status == ProcessStatus.Processing)
                {
                    context.Response.StatusCode = StatusCodes.Status409Conflict;
                    await context.Response.WriteAsJsonAsync(
                        new { error = ResponseMessage.GetResponseMessage(context.Response.StatusCode) },
                        context.RequestAborted);
                    return;
                }

                context.Response.StatusCode = cachedItem.StatusCode;
                context.Response.ContentType = cachedItem.ContentType;
                foreach (var header in cachedItem.ResponseHeaders)
                {
                    context.Response.Headers[header.Key] = header.Value;
                }

                if (cachedItem.ResponseBody is not null)
                {
                    await context.Response.Body.WriteAsync(
                        Convert.FromBase64String(cachedItem.ResponseBody),
                        context.RequestAborted);
                }
                return;
            }

            var processingItem = new IdempotencyCacheItem
            {
                Status = ProcessStatus.Processing,
                PayloadHash = payloadHash
            };
            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(processingItem),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2) },
                context.RequestAborted);
        }
        finally
        {
            _cacheGate.Release();
        }

        var originalBodyStream = context.Response.Body;
        using var responseBodyMemoryStream = new MemoryStream();
        context.Response.Body = responseBodyMemoryStream;

        try
        {
            await _next(context);

            if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
            {
                var completedItem = new IdempotencyCacheItem
                {
                    Status = ProcessStatus.Completed,
                    PayloadHash = payloadHash,
                    StatusCode = context.Response.StatusCode,
                    ContentType = context.Response.ContentType ?? "application/json",
                    ResponseBody = Convert.ToBase64String(responseBodyMemoryStream.ToArray()),
                    ResponseHeaders = GetReplayableHeaders(context.Response.Headers)
                };

                await _cache.SetStringAsync(
                    cacheKey,
                    JsonSerializer.Serialize(completedItem),
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) },
                    context.RequestAborted);
            }
            else
            {
                await _cache.RemoveAsync(cacheKey);
            }
        }
        catch
        {
            await _cache.RemoveAsync(cacheKey);
            throw;
        }
        finally
        {
            responseBodyMemoryStream.Position = 0;
            try
            {
                await responseBodyMemoryStream.CopyToAsync(originalBodyStream, context.RequestAborted);
            }
            finally
            {
                context.Response.Body = originalBodyStream;
            }
        }
    }

    private static Dictionary<string, string> GetReplayableHeaders(IHeaderDictionary headers)
    {
        string[] replayableHeaderNames =
        [
            "Cache-Control",
            "Content-Disposition",
            "Content-Language",
            "Content-Location",
            "ETag",
            "Last-Modified",
            "Location"
        ];

        return replayableHeaderNames
            .Where(headers.ContainsKey)
            .ToDictionary(name => name, name => headers[name].ToString(), StringComparer.OrdinalIgnoreCase);
    }
}
