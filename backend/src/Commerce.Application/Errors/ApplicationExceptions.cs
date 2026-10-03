using Commerce.Application.Models;

namespace Commerce.Application.Errors;

/// <summary>The request is wrong (400) or too large (413). The API turns it into ProblemDetails.</summary>
public sealed class RequestValidationException(
    string title,
    string detail,
    int statusCode = 400,
    IReadOnlyList<FileError>? errors = null) : Exception(detail)
{
    public string Title { get; } = title;

    public int StatusCode { get; } = statusCode;

    public IReadOnlyList<FileError> Errors { get; } = errors ?? [];
}

/// <summary>Some keys already exist in the database (409).</summary>
public sealed class ConflictException(string detail) : Exception(detail);

/// <summary>The user could not be identified (401).</summary>
public sealed class AuthenticationFailedException(string detail) : Exception(detail);
