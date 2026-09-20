using Grpc.Core;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Library.Api
{
    /// <summary>
    /// Turns a gRPC status into an RFC 9457 problem document. The 400/409 split is the one clients act
    /// on: 400 says fix the request, 409 says the same request may succeed later.
    /// </summary>
    public sealed partial class GrpcExceptionHandler : IExceptionHandler
    {
        private readonly IOptions<LibraryClientOptions> _options;
        private readonly IProblemDetailsService _problemDetails;
        private readonly ILogger<GrpcExceptionHandler> _logger;

        public GrpcExceptionHandler(
            IOptions<LibraryClientOptions> options,
            IProblemDetailsService problemDetails,
            ILogger<GrpcExceptionHandler> logger)
        {
            _options = options;
            _problemDetails = problemDetails;
            _logger = logger;
        }

        public const int ClientClosedRequest = StatusCodes.Status499ClientClosedRequest;

        public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
        {
            var rpc = exception as RpcException;
            if (rpc == null)
            {
                return false;
            }

            var mapped = Map(rpc.StatusCode);
            var status = mapped.Key;
            var title = mapped.Value;
            var detail = Detail(rpc, status);

            if (status >= StatusCodes.Status500InternalServerError)
            {
                Failed(_logger, context.Request.Path, rpc.StatusCode, rpc);
            }
            else if (status == ClientClosedRequest)
            {
                // The caller went away. Expected, and not an error anybody needs to page on.
                ClientLeft(_logger, context.Request.Path);
            }
            else
            {
                Rejected(_logger, context.Request.Path, rpc.StatusCode, detail);
            }

            context.Response.StatusCode = status;

            return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                Exception = rpc,
                ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail },
            });
        }

        private static KeyValuePair<int, string> Map(StatusCode code)
        {
            switch (code)
            {
                case StatusCode.InvalidArgument:
                    return Mapped(StatusCodes.Status400BadRequest, "Invalid request");
                case StatusCode.NotFound:
                    return Mapped(StatusCodes.Status404NotFound, "Not found");
                case StatusCode.FailedPrecondition:
                case StatusCode.AlreadyExists:
                case StatusCode.Aborted:
                    return Mapped(StatusCodes.Status409Conflict, "Conflict");
                case StatusCode.Cancelled:
                    return Mapped(ClientClosedRequest, "Client closed request");
                case StatusCode.Unavailable:
                    return Mapped(StatusCodes.Status503ServiceUnavailable, "Lending service unavailable");
                case StatusCode.DeadlineExceeded:
                    return Mapped(StatusCodes.Status504GatewayTimeout, "Lending service timed out");
                default:
                    return Mapped(StatusCodes.Status500InternalServerError, "Unexpected error");
            }
        }

        private static KeyValuePair<int, string> Mapped(int status, string title)
        {
            return new KeyValuePair<int, string>(status, title);
        }

        private string Detail(RpcException rpc, int status)
        {
            switch (status)
            {
                // The most likely demo failure is a reviewer who started one host.
                case StatusCodes.Status503ServiceUnavailable:
                    return $"The lending service at {_options.Value.GrpcEndpoint} is not reachable. " +
                        "Start it with `dotnet run --project src/Library.Service`.";
                case StatusCodes.Status504GatewayTimeout:
                    return $"The lending service at {_options.Value.GrpcEndpoint} did not answer within " +
                        $"{_options.Value.GrpcDeadlineSeconds} seconds.";
                // A defect: the cause stays server-side.
                case StatusCodes.Status500InternalServerError:
                    return "An unexpected error occurred.";
                default:
                    return rpc.Status.Detail;
            }
        }

        [LoggerMessage(Level = LogLevel.Information, Message = "{Path} rejected with {Status}: {Detail}")]
        private static partial void Rejected(ILogger logger, string path, StatusCode status, string detail);

        [LoggerMessage(Level = LogLevel.Information, Message = "{Path} abandoned by the client.")]
        private static partial void ClientLeft(ILogger logger, string path);

        [LoggerMessage(Level = LogLevel.Error, Message = "{Path} failed with {Status}.")]
        private static partial void Failed(ILogger logger, string path, StatusCode status, Exception error);
    }
}
