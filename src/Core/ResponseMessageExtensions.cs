using System.Net;
using System.Text.Json;

namespace Delegateas.DeveloperExperience.Core;

public static class ResponseMessageExtensions
{
    public static Task<TResult> TryReadWithProblemDetailsAsync<TResult>(
        this HttpResponseMessage response,
        CancellationToken cancellationToken)
        where TResult : notnull
    {
#pragma warning disable CA1062
        if (response.StatusCode is HttpStatusCode.NoContent)
#pragma warning restore CA1062
        {
            throw new InvalidOperationException(
                $"No Content (204) was returned but method return value is a not nullable type of '{typeof(TResult).Name}'. Consider using the TryReadNullableOrThrow instead");
        }

        return ReadOrThrowWithProblemDetailsAsync<TResult>(response, cancellationToken);
    }

    public static async Task<TResult?> TryReadNullableWithProblemDetailsAsync<TResult>(
        this HttpResponseMessage response, CancellationToken cancellationToken)
    {
#pragma warning disable CA1062
        if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound)
#pragma warning restore CA1062
        {
            return default;
        }

        return await ReadOrThrowWithProblemDetailsAsync<TResult?>(response, cancellationToken);
    }

    public static async Task<IEnumerable<TResult>> TryReadEnumerableWithProblemDetailsAsync<TResult>(
        this HttpResponseMessage response, CancellationToken cancellationToken)
        where TResult : notnull
    {
#pragma warning disable CA1062
        if (response.StatusCode is HttpStatusCode.NoContent)
#pragma warning restore CA1062
        {
            return [];
        }

        return await ReadOrThrowWithProblemDetailsAsync<IEnumerable<TResult>>(response, cancellationToken);
    }

    private static async Task<TResult> ReadOrThrowWithProblemDetailsAsync<TResult>(
        HttpResponseMessage responseMessage,
        CancellationToken cancellationToken)
    {
        var jsonStream = await responseMessage.Content.ReadAsStreamAsync(cancellationToken);

        if (!responseMessage.IsSuccessStatusCode)
        {
            throw new ProblemDetailsException(
                responseMessage.StatusCode,
                (await JsonSerializer.DeserializeAsync<ProblemDetails>(jsonStream, JsonDefaults.Options, cancellationToken))!);
        }

        return (await JsonSerializer.DeserializeAsync<TResult>(jsonStream, JsonDefaults.Options, cancellationToken))!;
    }

    /// <summary>
    /// If a success http status code is received, then return void.
    /// If not success, then read and deserialize message content as ProblemDetails and throw ProblemDetailsException
    /// </summary>
    /// <param name="responseMessage">Response Message</param>
    /// <param name="cancellationToken">Cancellation Token</param>
    /// <returns>Awaitable task</returns>
    public static async Task ThrowOnError(this HttpResponseMessage responseMessage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(responseMessage);

        if (responseMessage.IsSuccessStatusCode)
        {
            return;
        }

        var jsonStream = await responseMessage.Content.ReadAsStreamAsync(cancellationToken);

        throw new ProblemDetailsException(
            responseMessage.StatusCode,
            (await JsonSerializer.DeserializeAsync<ProblemDetails>(jsonStream, JsonDefaults.Options, cancellationToken))!);
    }
}
