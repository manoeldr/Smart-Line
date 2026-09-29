using Microsoft.AspNetCore.Authorization;

namespace SmartLine.API.Autorizacao;

/// <summary>
/// Quem pode o quê, pelo nível do usuário (claim "nivel" do token).
/// Sem política: qualquer usuário logado (inclusive Cliente, que só visualiza).
/// </summary>
public static class Politicas
{
    /// <summary>Cadastro de infraestrutura (WISE): só Administrador e Desenvolvedor.</summary>
    public const string AdministradorOuDesenvolvedor = nameof(AdministradorOuDesenvolvedor);

    /// <summary>
    /// Operar a coleta: iniciar, finalizar, classificar paradas, criar motivo,
    /// editar regras e textos das entradas. Administrador, Desenvolvedor e Auditor.
    /// </summary>
    public const string Operacao = nameof(Operacao);

    public static void Registrar(AuthorizationOptions opcoes)
    {
        opcoes.AddPolicy(AdministradorOuDesenvolvedor, p => p.RequireClaim("nivel", "Administrador", "Desenvolvedor"));
        opcoes.AddPolicy(Operacao, p => p.RequireClaim("nivel", "Administrador", "Desenvolvedor", "Auditor"));
    }
}
