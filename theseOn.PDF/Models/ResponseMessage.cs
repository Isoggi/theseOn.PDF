using System;
using Microsoft.AspNetCore.Http;

namespace theseOn.PDF.Models;

public class ResponseMessage
{
    public static readonly Dictionary<int, string> StatusMessages = new()
    {
        { StatusCodes.Status200OK, "Success" },
        { StatusCodes.Status400BadRequest, "Payload mismatch for existing idempotency key." },
        { StatusCodes.Status401Unauthorized, "Unauthorized" },
        { StatusCodes.Status403Forbidden, "Forbidden" },
        { StatusCodes.Status404NotFound, "Resource Not Found" },
        { StatusCodes.Status500InternalServerError, "Internal Server Error" },
        { StatusCodes.Status409Conflict, "Request is currently processing." }
    };

    public static string GetResponseMessage(int value)
    {
        if (StatusMessages.TryGetValue(value, out string? result))
            return result ?? string.Empty;
        else
            return string.Empty;
    }
}
