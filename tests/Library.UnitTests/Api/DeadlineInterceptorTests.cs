using Google.Protobuf;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Library.Api;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Proto = Library.Contracts.V1;

namespace Library.UnitTests.Api
{
    /// <summary>Asserted on the call context: no server needed.</summary>
    public class DeadlineInterceptorTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero);

        [Fact]
        public void AsyncUnaryCall_NoDeadlineSet_AppliesTheConfiguredDefault()
        {
            var seen = Invoke(new CallOptions(), deadlineSeconds: 10);

            seen.ShouldBe(Now.UtcDateTime.AddSeconds(10));
        }

        [Fact]
        public void AsyncUnaryCall_CallerSetADeadline_LeavesItAlone()
        {
            var explicitDeadline = Now.UtcDateTime.AddSeconds(1);

            var seen = Invoke(new CallOptions(deadline: explicitDeadline), deadlineSeconds: 10);

            seen.ShouldBe(explicitDeadline);
        }

        private static DateTime? Invoke(CallOptions options, int deadlineSeconds)
        {
            var interceptor = new DeadlineInterceptor(
                Options.Create(new LibraryClientOptions { GrpcDeadlineSeconds = deadlineSeconds }),
                new FakeTimeProvider(Now));

            var method = new Method<Proto.GetBookRequest, Proto.Book>(
                MethodType.Unary,
                "library.v1.LendingService",
                "GetBook",
                Marshal(Proto.GetBookRequest.Parser),
                Marshal(Proto.Book.Parser));

            DateTime? seen = null;

            interceptor.AsyncUnaryCall(
                new Proto.GetBookRequest { BookId = 1 },
                new ClientInterceptorContext<Proto.GetBookRequest, Proto.Book>(method, host: null, options),
                (_, context) =>
                {
                    seen = context.Options.Deadline;
                    return Completed(new Proto.Book());
                });

            return seen;
        }

        /// <summary>An already-succeeded call; the response is never read.</summary>
        private static AsyncUnaryCall<T> Completed<T>(T value)
        {
            return new AsyncUnaryCall<T>(
                Task.FromResult(value),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
        }

        private static Marshaller<T> Marshal<T>(MessageParser<T> parser)
            where T : class, IMessage<T>
        {
            return Marshallers.Create(message => message.ToByteArray(), parser.ParseFrom);
        }
    }
}
