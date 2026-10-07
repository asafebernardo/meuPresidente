using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Presidents.Domain.Common;

namespace Presidents.Api.Infrastructure;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, errors) = exception switch
        {
            ValidationException validation => (StatusCodes.Status400BadRequest, "Dados inválidos", validation.Errors.Select(error => error.ErrorMessage).Distinct().ToArray()),
            NotFoundException => (StatusCodes.Status404NotFound, "Não encontrado", Array.Empty<string>()),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Acesso negado", Array.Empty<string>()),
            DomainException => (StatusCodes.Status400BadRequest, "Regra não atendida", Array.Empty<string>()),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno", Array.Empty<string>())
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Falha não tratada em {Path}", httpContext.Request.Path);
        else
            logger.LogInformation(exception, "Requisição recusada em {Path}", httpContext.Request.Path);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status == StatusCodes.Status500InternalServerError ? "A operação não pôde ser concluída." : exception.Message
        };
        if (errors.Length > 0)
            problem.Extensions["errors"] = errors;

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
