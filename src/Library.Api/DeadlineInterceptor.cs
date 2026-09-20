using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Options;

namespace Library.Api
{
    /// <summary>
    /// Without a deadline the mapped 504 is unreachable and a wedged service holds API requests open
    /// forever. No retries: BorrowBook is not idempotent, so a retry after a timeout can lend two copies.
    /// </summary>
    public sealed class DeadlineInterceptor : Interceptor
    {
        private readonly IOptions<LibraryClientOptions> _options;
        private readonly TimeProvider _clock;

        public DeadlineInterceptor(IOptions<LibraryClientOptions> options, TimeProvider clock)
        {
            _options = options;
            _clock = clock;
        }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            TRequest request,
            ClientInterceptorContext<TRequest, TResponse> context,
            AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            if (context.Options.Deadline.HasValue)
            {
                return continuation(request, context);
            }

            var deadline = _clock.GetUtcNow().UtcDateTime.AddSeconds(_options.Value.GrpcDeadlineSeconds);

            return continuation(request, new ClientInterceptorContext<TRequest, TResponse>(
                context.Method, context.Host, context.Options.WithDeadline(deadline)));
        }
    }
}
