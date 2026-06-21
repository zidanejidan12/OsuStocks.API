using FluentValidation;
using MediatR;
using OsuStocks.Application.Common.Models;
using System.Reflection;

namespace OsuStocks.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var failures = validators
            .Select(validator => validator.Validate(context))
            .SelectMany(result => result.Errors)
            .Where(error => error is not null)
            .Select(error => error.ErrorMessage)
            .Distinct()
            .ToArray();

        if (failures.Length == 0)
        {
            return await next();
        }

        var message = string.Join("; ", failures);
        return CreateValidationResult(message);
    }

    private static TResponse CreateValidationResult(string message)
    {
        if (typeof(TResponse) == typeof(Result))
        {
            return (TResponse)(object)Result.Failure("VALIDATION_ERROR", message);
        }

        if (typeof(TResponse).IsGenericType &&
            typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
        {
            var valueType = typeof(TResponse).GetGenericArguments()[0];
            var failureMethod = typeof(Result)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(method => method.Name == nameof(Result.Failure)
                                  && method.IsGenericMethodDefinition
                                  && method.GetParameters().Length == 2)
                .MakeGenericMethod(valueType);

            var failure = failureMethod.Invoke(null, ["VALIDATION_ERROR", message])
                ?? throw new InvalidOperationException("Failed to create validation failure result.");

            return (TResponse)failure;
        }

        throw new InvalidOperationException(
            $"ValidationBehavior expects response type Result or Result<T>, but got {typeof(TResponse).Name}.");
    }
}
