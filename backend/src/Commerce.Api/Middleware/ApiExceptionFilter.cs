using Commerce.Application.Errors;
using Commerce.Application.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Commerce.Api.Middleware;

/// <summary>
/// Turns expected errors into ProblemDetails inside the MVC pipeline, so CORS headers are kept.
/// Anything else goes to GlobalExceptionHandler.
/// </summary>
public sealed class ApiExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var problem = context.Exception switch
        {
            RequestValidationException ex => Build(ex.StatusCode, ex.Title, ex.Message, ex.Errors),
            AuthenticationFailedException ex => Build(StatusCodes.Status401Unauthorized, "No autorizado", ex.Message, null),
            ConflictException ex => Build(StatusCodes.Status409Conflict, "Registros existentes", ex.Message, null),
            BadHttpRequestException ex when ex.StatusCode == StatusCodes.Status413PayloadTooLarge =>
                Build(ex.StatusCode, "Archivo demasiado grande", "El archivo supera el tamaño máximo permitido.", null),
            BadHttpRequestException ex => Build(ex.StatusCode, "Solicitud inválida", "La solicitud no se pudo leer.", null),
            InvalidDataException => Build(StatusCodes.Status400BadRequest, "Archivo inválido", "El formulario enviado no es válido.", null),
            _ => null,
        };

        if (problem is null)
        {
            return;
        }

        context.Result = new ObjectResult(problem)
        {
            StatusCode = problem.Status,
            ContentTypes = { "application/problem+json" },
        };
        context.ExceptionHandled = true;
    }

    private static ProblemDetails Build(int status, string title, string detail, IReadOnlyList<FileError>? errors)
    {
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };

        if (errors is { Count: > 0 })
        {
            problem.Extensions["errors"] = errors;
        }

        return problem;
    }
}
