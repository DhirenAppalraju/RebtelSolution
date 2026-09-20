using Grpc.Core;
using Grpc.Core.Interceptors;
using Library.Domain.Exceptions;

namespace Library.Service.Grpc
{
    /// <summary>
    /// One place where the domain's failure vocabulary becomes gRPC status. Without it the framework's
    /// default for an unhandled exception is Unknown, not Internal, and the message leaks.
    /// </summary>
    public sealed partial class DomainExceptionInterceptor : Interceptor
    {
        private readonly ILogger<DomainExceptionInterceptor> _logger;

        public DomainExceptionInterceptor(ILogger<DomainExceptionInterceptor> logger)
        {
            _logger = logger;
        }

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
            TRequest request,
            ServerCallContext context,
            UnaryServerMethod<TRequest, TResponse> continuation)
        {
            try
            {
                return await continuation(request, context);
            }
            catch (RpcException)
            {
                throw;
            }
            catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
            {
                // The client went away; the framework maps this to Cancelled, which the API turns into 499.
                throw;
            }
            catch (DomainException error)
            {
                StatusCode status;
                if (error is ValidationException)
                {
                    status = StatusCode.InvalidArgument;
                }
                else if (error is NotFoundException)
                {
                    status = StatusCode.NotFound;
                }
                else if (error is ConflictException)
                {
                    status = StatusCode.FailedPrecondition;
                }
                else
                {
                    status = StatusCode.Internal;
                }

                Rejected(_logger, context.Method, status, error.Message);
                throw new RpcException(new Status(status, error.Message));
            }
            catch (Exception error)
            {
                Failed(_logger, context.Method, error);
                throw new RpcException(new Status(StatusCode.Internal, "An unexpected error occurred."));
            }
        }

        [LoggerMessage(Level = LogLevel.Warning, Message = "{Method} rejected with {Status}: {Detail}")]
        private static partial void Rejected(ILogger logger, string method, StatusCode status, string detail);

        [LoggerMessage(Level = LogLevel.Error, Message = "{Method} failed.")]
        private static partial void Failed(ILogger logger, string method, Exception error);
    }
}
