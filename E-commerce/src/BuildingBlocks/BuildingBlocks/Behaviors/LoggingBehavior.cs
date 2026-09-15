using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Behaviors;

public class LoggingBehavior<TRequest,TResponse>(ILogger<LoggingBehavior<TRequest,TResponse>> logger)
    : IPipelineBehavior<TRequest,TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse :notnull
{
    public async Task<TResponse> Handle(TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
       var requestName = typeof(TRequest).Name;
        logger.LogInformation("[START] Starting mediaTR request: {RequestName}", requestName);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // 3. Move to the next behavior or the actual handler
            var response = await next();
            
            // Stop tracking time once the work finishes successfully
            stopwatch.Stop();
            var elapsedMs = stopwatch.ElapsedMilliseconds;

            // 4. Performance Check: If work takes longer than 3 seconds (3000ms)
            if (elapsedMs > 3000)
            {
                logger.LogWarning(
                    "[PERMORFANCE STATUS] PERFORMANCE WARNING: Request {RequestName} took {ElapsedMilliseconds}ms to complete! (Threshold exceeded: 3000ms)", 
                    requestName, 
                    elapsedMs);
            }
            else
            {
                logger.LogInformation(
                    "Successfully handled request {RequestName} in {ElapsedMilliseconds}ms", 
                    requestName, 
                    elapsedMs);
            }
            logger.LogInformation("[END] Ending mediaTR request: {RequestName}", requestName);
            return response;
        }
        catch(Exception ex)
        {
            // Stop tracking time even if an error crashes the request
            stopwatch.Stop();
            var elapsedMs = stopwatch.ElapsedMilliseconds;

            // 5. Log failures alongside execution duration
            logger.LogError(
                ex, 
                "Request {RequestName} failed unexpectedly after {ElapsedMilliseconds}ms", 
                requestName, 
                elapsedMs);
            
            // Re-throw so your IExceptionHandler catches it globally
            throw; 
        }

      
    }
}