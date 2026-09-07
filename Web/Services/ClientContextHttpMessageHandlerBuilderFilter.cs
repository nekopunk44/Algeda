using Microsoft.Extensions.Http;

namespace Web.Services;

public sealed class ClientContextHttpMessageHandlerBuilderFilter(IHttpContextAccessor httpContextAccessor)
    : IHttpMessageHandlerBuilderFilter
{
    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
    {
        return builder =>
        {
            next(builder);
            // Хендлер создаётся напрямую: экземпляром владеет цепочка HttpClientFactory,
            // и он освобождается вместе с ней (резолв transient IDisposable из корневого
            // провайдера удерживал бы каждый экземпляр до остановки процесса).
            builder.AdditionalHandlers.Insert(
                0,
                new ClientContextDelegatingHandler(httpContextAccessor));
        };
    }
}
