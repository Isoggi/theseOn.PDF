using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using theseOn.PDF.Models;

namespace theseOn.PDF.Middlewares;

public class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDistributedCache _cache;
    public IdempotencyMiddleware(RequestDelegate next, IDistributedCache cache)
    {
        _next = next;
        _cache = cache;
    }
    public async Task InvokeAsync(HttpContext context)
    {
        if (
            context.Request.Method != HttpMethods.Post
            && context.Request.Method != HttpMethods.Patch
            && context.Request.Method != HttpMethods.Put
            && context.Request.Method != HttpMethods.Delete
            )
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-Idempotency-Value", out var keyValues))
        {
            await _next(context);
            return;
        }

        var key = keyValues.ToString();
        var cacheKey = $"idempotency:{key}";

        // Hash incoming payload for fast comparation
        context.Request.EnableBuffering();
        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(context.Request.Body);
        var payloadHash = Convert.ToBase64String(hashBytes);
        context.Request.Body.Position = 0;

        var cachedString = await _cache.GetStringAsync(cacheKey);
        if (cachedString != null)
        {
            IdempotencyCacheItem? cachedItem = JsonSerializer.Deserialize<IdempotencyCacheItem>(cachedString);
            if (cachedItem?.Status == ProcessStatus.Processed)
            {
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                await context.Response.WriteAsJsonAsync(new { error = ResponseMessage.GetResponseMessage(context.Response.StatusCode) });
                return;
            }

            if (cachedItem?.PayloadHash != payloadHash)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { error = ResponseMessage.GetResponseMessage(context.Response.StatusCode) });
                return;
            }

            // Return the cached successful response
            context.Response.StatusCode = cachedItem.StatusCode;
            context.Response.ContentType = cachedItem.ContentType;
            if (cachedItem.ResponseBody != null)
            {
                await context.Response.WriteAsync(cachedItem.ResponseBody);
            }
            return;

        }
        var processingItem = new IdempotencyCacheItem { Status = ProcessStatus.Processed, PayloadHash = payloadHash };
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(processingItem), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
        });

        // 4. Swap response body to intercept the output
        var originalBodyStream = context.Response.Body;
        using var responseBodyMemoryStream = new MemoryStream();
        context.Response.Body = responseBodyMemoryStream;

        try
        {
            await _next(context);

            // 5. Handle Validation Errors & Success States
            if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
            {
                // Success: Cache the response
                responseBodyMemoryStream.Position = 0;
                var responseContent = await new StreamReader(responseBodyMemoryStream).ReadToEndAsync();

                var completedItem = new IdempotencyCacheItem
                {
                    Status = ProcessStatus.Completed,
                    PayloadHash = payloadHash,
                    StatusCode = context.Response.StatusCode,
                    ContentType = context.Response.ContentType ?? "application/json",
                    ResponseBody = responseContent
                };

                await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(completedItem), new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
                });
            }
            else
            {
                // Failure (e.g., 400 Validation Error or 500 Server Error): 
                // Remove the lock so the client can fix the data and try again.
                await _cache.RemoveAsync(cacheKey);
            }
        }
        catch
        {
            // Unhandled exception: Drop the lock
            await _cache.RemoveAsync(cacheKey);
            throw;
        }
        finally
        {
            // Copy the intercepted response back to the original client stream
            responseBodyMemoryStream.Position = 0;
            await responseBodyMemoryStream.CopyToAsync(originalBodyStream);
            context.Response.Body = originalBodyStream;
        }
    }
}
