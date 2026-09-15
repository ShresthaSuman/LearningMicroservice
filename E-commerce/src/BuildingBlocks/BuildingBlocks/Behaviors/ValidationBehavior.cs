using BuildingBlocks.CQRS;
using FluentValidation;
using MediatR;

namespace BuildingBlocks.Behaviors; // Fixed namespace typo

// 1. Changed second generic parameter from TRequest to TResponse
public class ValidationBehavior<TRequest, TResponse>: IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse> // Good practice to ensure TRequest isn't null
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    // 2. Updated method signature to use TResponse instead of TRequest
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!_validators.Any()) return await next();
        
        var context = new ValidationContext<TRequest>(request);
        
        var validationResults = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, cancellationToken)));
        
        // 3. Changed .Select to .SelectMany to flatten the errors into a single flat list
        var failures = validationResults
            .SelectMany(e => e.Errors)
            .Where(f => f != null)
            .ToList();

        // 4. Check if the flat list actually contains any failures
        if (failures.Count != 0)
        {
            // 5. Pass the flat list of ValidationFailures to the exception
            throw new ValidationException(failures);
        }

        return await next();
    }
}