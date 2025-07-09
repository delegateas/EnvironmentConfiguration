using System.Net;

namespace Delegateas.EnvironmentConfiguration.Core;

#pragma warning disable CA1032
public class ProblemDetailsException(HttpStatusCode statusCode, ProblemDetails problemDetails) : Exception
#pragma warning restore CA1032
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    public ProblemDetails ProblemDetails { get; } = problemDetails;
}
