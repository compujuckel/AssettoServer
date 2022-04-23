using System;
using AssettoServer.Server;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace AssettoServer.Network.Http;

public class AuthInterceptor : Interceptor
{
    private readonly string _key;

    public AuthInterceptor(ACServer server)
    {
        _key = server.Configuration.Extra.HubConnection?.Key ?? throw new InvalidOperationException("Cannot make hub call without key");
    }
    
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var headers = new Metadata();
        headers.Add(new Metadata.Entry("X-Api-Key", _key));

        var newOptions = context.Options.WithHeaders(headers);

        var newContext = new ClientInterceptorContext<TRequest, TResponse>(
            context.Method,
            context.Host,
            newOptions);

        return base.AsyncUnaryCall(request, newContext, continuation);
    }
}
