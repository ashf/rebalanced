using System.Net;
using System.Text.Json;

namespace ReBalanced.API.Middleware;

public class ErrorHandlerMiddleware
{
    private readonly RequestDelegate _next;

    public ErrorHandlerMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception error)
        {
            var response = context.Response;
            response.ContentType = "application/json";

            response.StatusCode = error switch
            {
                _ => (int) HttpStatusCode.InternalServerError // unhandled error
            };
            var result = JsonSerializer.Serialize(new
                {message = error?.Message + " " + error?.InnerException?.Message});
            await response.WriteAsync(result);
        }
    }
}