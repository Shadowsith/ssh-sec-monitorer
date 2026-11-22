namespace Monitorer.Data.Result;

/// <summary>Outcome of a service call - mapped to an HTTP status code by ServiceResultExtensions.</summary>
public enum ResultStatus
{
    Ok,             // 200
    Created,        // 201
    Changed,        // 202
    Unauthorized,   // 401
    Forbidden,      // 403
    NotFound,       // 404
    NotAllowed,     // 405
    InvalidValue,   // 406
    Conflict,       // 409
    Locked          // 423
}
