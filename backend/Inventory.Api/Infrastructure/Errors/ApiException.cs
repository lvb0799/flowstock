namespace Inventory.Api.Infrastructure.Errors;

public class ApiException : Exception
{
    public int StatusCode { get; }
    public ApiException(int statusCode, string message) : base(message) => StatusCode = statusCode;
}

public sealed class ValidationApiException : ApiException
{
    public ValidationApiException(string message) : base(StatusCodes.Status400BadRequest, message) { }
}

public sealed class NotFoundApiException : ApiException
{
    public NotFoundApiException(string message) : base(StatusCodes.Status404NotFound, message) { }
}

public sealed class ConflictApiException : ApiException
{
    public ConflictApiException(string message) : base(StatusCodes.Status409Conflict, message) { }
}
