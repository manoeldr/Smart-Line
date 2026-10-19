using SmartLine.Core.Interfaces;

namespace SmartLine.API.Middleware;

// Bloqueia as chamadas de API de negócio se a licença não estiver ativa nesta máquina —
// exceto os próprios endpoints de licença (status/ativar), que precisam funcionar mesmo
// sem ativação, e o Swagger em ambiente de desenvolvimento. Libera o frontend inteiro
// (HTML/JS/CSS, sem dado sensível) mesmo sem licença — sem isso, a própria tela de
// ativação nunca carrega numa máquina ainda não licenciada.
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

        var rotaLiberada = !caminho.StartsWith("/api")
            || caminho.StartsWith("/api/licenca")
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