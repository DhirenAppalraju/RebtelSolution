using Grpc.Core;
using Library.Domain.Exceptions;
using Library.Service.Grpc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Library.UnitTests.Service
{
    public class DomainExceptionInterceptorTests
    {
        private static readonly DomainExceptionInterceptor Interceptor =
            new DomainExceptionInterceptor(NullLogger<DomainExceptionInterceptor>.Instance);

        public static TheoryData<DomainException, StatusCode> DomainFailures
        {
            get
            {
                return new TheoryData<DomainException, StatusCode>
                {
                    { new ValidationException("bad input"), StatusCode.InvalidArgument },
                    { new NotFoundException("Book", 7), StatusCode.NotFound },
                    { new ConflictException("every copy is out"), StatusCode.FailedPrecondition },
                };
            }
        }

        [Theory]
        [MemberData(nameof(DomainFailures))]
        public async Task UnaryServerHandler_DomainFailure_BecomesItsStatusAndKeepsTheMessage(
            DomainException error, StatusCode expected)
        {
            var thrown = await Should.ThrowAsync<RpcException>(() => Invoke(_ => throw error));

            thrown.StatusCode.ShouldBe(expected);
            thrown.Status.Detail.ShouldBe(error.Message);
        }

        [Fact]
        public async Task UnaryServerHandler_UnknownFailure_IsInternalAndDoesNotLeakTheMessage()
        {
            var thrown = await Should.ThrowAsync<RpcException>(() =>
                Invoke(_ => throw new InvalidOperationException("connection string: secret")));

            // The framework's default is Unknown, not Internal.
            thrown.StatusCode.ShouldBe(StatusCode.Internal);
            thrown.Status.Detail.ShouldBe("An unexpected error occurred.");
            thrown.Status.Detail.ShouldNotContain("secret");
        }

        [Fact]
        public async Task UnaryServerHandler_RpcException_PassesThroughUntouched()
        {
            var original = new RpcException(new Status(StatusCode.PermissionDenied, "nope"));

            var thrown = await Should.ThrowAsync<RpcException>(() => Invoke(_ => throw original));

            thrown.ShouldBeSameAs(original);
        }

        [Fact]
        public async Task UnaryServerHandler_Success_ReturnsTheResponse()
        {
            (await Invoke(_ => Task.FromResult("ok"))).ShouldBe("ok");
        }

        [Fact]
        public async Task UnaryServerHandler_ClientWentAway_LetsTheCancellationThrough()
        {
            // Client disconnect: abandoned, not failed. Without this clause it becomes an Internal
            // 500; the framework maps it to Cancelled, which the API turns into 499.
            using (var cancelled = new CancellationTokenSource())
            {
                await cancelled.CancelAsync();

                var context = new FakeServerCallContext(cancelled.Token);

                await Should.ThrowAsync<OperationCanceledException>(() =>
                    Interceptor.UnaryServerHandler<string, string>(
                        "request",
                        context,
                        (_, ctx) =>
                        {
                            ctx.CancellationToken.ThrowIfCancellationRequested();
                            return Task.FromResult("never reached");
                        }));
            }
        }

        [Fact]
        public async Task UnaryServerHandler_CancelledWithoutTheClientLeaving_IsStillAFailure()
        {
            // Mirror case: cancellation without the call's token cancelled is a downstream defect,
            // not a disconnect.
            var thrown = await Should.ThrowAsync<RpcException>(() =>
                Invoke(_ => throw new OperationCanceledException("an inner timeout")));

            thrown.StatusCode.ShouldBe(StatusCode.Internal);
            thrown.Status.Detail.ShouldBe("An unexpected error occurred.");
        }

        private static Task<string> Invoke(Func<string, Task<string>> continuation)
        {
            return Interceptor.UnaryServerHandler("request", new FakeServerCallContext(), (r, _) => continuation(r));
        }

        private sealed class FakeServerCallContext : ServerCallContext
        {
            private readonly CancellationToken _cancellationToken;

            public FakeServerCallContext()
                : this(CancellationToken.None)
            {
            }

            public FakeServerCallContext(CancellationToken cancellationToken)
            {
                _cancellationToken = cancellationToken;
            }

            protected override string MethodCore
            {
                get { return "/library.v1.LendingService/Test"; }
            }

            protected override string HostCore
            {
                get { return "localhost"; }
            }

            protected override string PeerCore
            {
                get { return "ipv4:127.0.0.1:0"; }
            }

            protected override DateTime DeadlineCore
            {
                get { return DateTime.MaxValue; }
            }

            protected override Metadata RequestHeadersCore
            {
                get { return new Metadata(); }
            }

            protected override CancellationToken CancellationTokenCore
            {
                get { return _cancellationToken; }
            }

            protected override Metadata ResponseTrailersCore { get; } = new Metadata();

            protected override Status StatusCore { get; set; }

            protected override WriteOptions? WriteOptionsCore { get; set; }

            protected override AuthContext AuthContextCore
            {
                get
                {
                    return new AuthContext(null, new Dictionary<string, List<AuthProperty>>());
                }
            }

            protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options)
            {
                throw new NotSupportedException();
            }

            protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
            {
                return Task.CompletedTask;
            }
        }
    }
}
