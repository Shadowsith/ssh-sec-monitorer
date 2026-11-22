using Microsoft.AspNetCore.Mvc;
using Monitorer.Data.Result;

namespace Monitorer.Extensions;

/// <summary>Maps a ServiceResult to the HTTP response - the only place with status-code logic.</summary>
public static class ServiceResultExtensions
{
    public static ActionResult<T> ToActionResult<T>(this ServiceResult<T> result) => result.Status switch
    {
        ResultStatus.Ok => new OkObjectResult(result.Value),
        ResultStatus.Created => new ObjectResult(result.Value) { StatusCode = StatusCodes.Status201Created },
        ResultStatus.Changed => new ObjectResult(result.Value) { StatusCode = StatusCodes.Status202Accepted },
        ResultStatus.Unauthorized => Message(StatusCodes.Status401Unauthorized, result.Message),
        ResultStatus.Forbidden => Message(StatusCodes.Status403Forbidden, result.Message),
        ResultStatus.NotFound => Message(StatusCodes.Status404NotFound, result.Message),
        ResultStatus.NotAllowed => Message(StatusCodes.Status405MethodNotAllowed, result.Message),
        ResultStatus.InvalidValue => Message(StatusCodes.Status406NotAcceptable, result.Message),
        ResultStatus.Conflict => Message(StatusCodes.Status409Conflict, result.Message),
        ResultStatus.Locked => Message(StatusCodes.Status423Locked, result.Message),
        _ => throw new ArgumentOutOfRangeException(nameof(result), result.Status, "Unknown result status")
    };

    private static ObjectResult Message(int statusCode, string? message) =>
        new(message ?? "") { StatusCode = statusCode };
}
