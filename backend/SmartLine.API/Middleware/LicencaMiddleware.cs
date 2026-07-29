using SmartLine.Core.Interfaces;

namespace SmartLine.API.Middleware;

// Bloqueia toda a API se a licença não estiver ativa nesta máquina —
// exceto os próprios endpoints de licença (status/ativar), que precisam
// funcionar mesmo sem ativação, e o Swagger em ambiente de desenvolvimento.
public class LicencaMiddleware
{
    private readonly RequestDelegate _next;

    public LicencaMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ILicencaService licencaService)
    {
        var caminho = context.Request.Path.Value ?? "";

        var rotaLiberada = caminho.StartsWith("/api/licenca")
            || caminho.StartsWith("/swagger");

        if (!rotaLiberada)
        {
            var status = await licencaService.ObterStatusAsync();
            if (!status.Ativa)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { mensagem = "Sistema não licenciado para esta máquina." });
                return;
            }
        }

        await _next(context);
    }
}