using Microsoft.EntityFrameworkCore;
using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Interfaces;
using SmartLine.Core.Iot;
using SmartLine.Infrastructure.Data;

namespace SmartLine.Infrastructure.Repositories;

public class DispositivoIotService : IDispositivoIotService
{
    private readonly SmartLineDbContext _context;

    public DispositivoIotService(SmartLineDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<DispositivoIotDto>> ListarAsync(CancellationToken cancellationToken = default) =>
        await ConsultarAsync(null, cancellationToken);

    public async Task<ResultadoCadastro<DispositivoIotDto>> CriarAsync(
        SalvarDispositivoIotRequest request,
        CancellationToken cancellationToken = default)
    {
        var (ip, erro) = await ValidarAsync(null, request, cancellationToken);
        if (erro is not null)
            return ResultadoCadastro<DispositivoIotDto>.Falha(erro);

        var dispositivo = new DispositivoIot
        {
            Id = Guid.NewGuid(),
            MaquinaLinhaId = request.MaquinaLinhaId,
            Nome = request.Nome.Trim(),
            EnderecoIp = ip!,
            Ativo = request.Ativo
        };
        _context.DispositivosIot.Add(dispositivo);
        await _context.SaveChangesAsync(cancellationToken);

        return ResultadoCadastro<DispositivoIotDto>.Ok((await ConsultarAsync(dispositivo.Id, cancellationToken)).Single());
    }

    public async Task<ResultadoCadastro<DispositivoIotDto>> EditarAsync(
        Guid id,
        SalvarDispositivoIotRequest request,
        CancellationToken cancellationToken = default)
    {
        var dispositivo = await _context.DispositivosIot.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (dispositivo is null)
            return ResultadoCadastro<DispositivoIotDto>.Inexistente();

        var (ip, erro) = await ValidarAsync(id, request, cancellationToken);
        if (erro is not null)
            return ResultadoCadastro<DispositivoIotDto>.Falha(erro);

        var saiDaMaquina = request.MaquinaLinhaId != dispositivo.MaquinaLinhaId || (dispositivo.Ativo && !request.Ativo);
        if (saiDaMaquina && await TemColetaLigadaAsync(dispositivo.MaquinaLinhaId, cancellationToken))
            return ResultadoCadastro<DispositivoIotDto>.Falha(
                "A máquina deste WISE está com coleta Semi Automática ligada. Finalize a coleta antes de trocar o WISE de máquina ou desativá-lo.");

        dispositivo.MaquinaLinhaId = request.MaquinaLinhaId;
        dispositivo.Nome = request.Nome.Trim();
        dispositivo.EnderecoIp = ip!;
        dispositivo.Ativo = request.Ativo;
        await _context.SaveChangesAsync(cancellationToken);

        return ResultadoCadastro<DispositivoIotDto>.Ok((await ConsultarAsync(id, cancellationToken)).Single());
    }

    public async Task<ResultadoCadastro<bool>> ExcluirAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dispositivo = await _context.DispositivosIot.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (dispositivo is null)
            return ResultadoCadastro<bool>.Inexistente();

        if (await TemColetaLigadaAsync(dispositivo.MaquinaLinhaId, cancellationToken))
            return ResultadoCadastro<bool>.Falha(
                "A máquina deste WISE está com coleta Semi Automática ligada. Finalize a coleta antes de excluir o WISE.");

        _context.DispositivosIot.Remove(dispositivo);
        await _context.SaveChangesAsync(cancellationToken);
        return ResultadoCadastro<bool>.Ok(true);
    }

    // ── Apoio ───────────────────────────────────────────────────────

    /// <returns>IP canônico, ou a mensagem de erro para o usuário.</returns>
    private async Task<(string? Ip, string? Erro)> ValidarAsync(
        Guid? id,
        SalvarDispositivoIotRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Nome))
            return (null, "Informe o nome do WISE.");

        var ip = EnderecoRede.Normalizar(request.EnderecoIp);
        if (ip is null || ip.Contains(':'))
            return (null, $"IP inválido: \"{request.EnderecoIp}\". Use o IPv4 fixo configurado no WISE, no formato 192.168.10.21.");

        var maquina = await _context.MaquinasLinha
            .AsNoTracking()
            .Where(m => m.Id == request.MaquinaLinhaId)
            .Select(m => new { m.Id })
            .FirstOrDefaultAsync(cancellationToken);
        if (maquina is null)
            return (null, "Máquina não encontrada.");

        var mesmoIp = await Descrever(_context.DispositivosIot.Where(d => d.EnderecoIp == ip && d.Id != id))
            .FirstOrDefaultAsync(cancellationToken);
        if (mesmoIp is not null)
            return (null, $"O IP {ip} já está cadastrado no WISE \"{mesmoIp.Nome}\" ({mesmoIp.Maquina}, {mesmoIp.Linha}).");

        var mesmaMaquina = await Descrever(_context.DispositivosIot.Where(d => d.MaquinaLinhaId == request.MaquinaLinhaId && d.Id != id))
            .FirstOrDefaultAsync(cancellationToken);
        if (mesmaMaquina is not null)
            return (null, $"Esta máquina já tem o WISE \"{mesmaMaquina.Nome}\" (IP {mesmaMaquina.EnderecoIp}). Cada máquina tem um WISE só.");

        return (ip, null);
    }

    private static IQueryable<DescricaoWise> Descrever(IQueryable<DispositivoIot> consulta) =>
        consulta.AsNoTracking().Select(d => new DescricaoWise
        {
            Nome = d.Nome,
            EnderecoIp = d.EnderecoIp,
            Maquina = d.MaquinaLinha.Maquina.Nome,
            Linha = d.MaquinaLinha.Linha.Nome
        });

    private sealed class DescricaoWise
    {
        public string Nome { get; init; } = "";
        public string EnderecoIp { get; init; } = "";
        public string Maquina { get; init; } = "";
        public string Linha { get; init; } = "";
    }

    private Task<bool> TemColetaLigadaAsync(Guid maquinaLinhaId, CancellationToken cancellationToken) =>
        _context.Acompanhamentos.AnyAsync(a => a.MaquinaLinhaId == maquinaLinhaId && a.FinalizadoEm == null, cancellationToken);

    private async Task<IReadOnlyList<DispositivoIotDto>> ConsultarAsync(Guid? id, CancellationToken cancellationToken)
    {
        var comColeta = (await _context.Acompanhamentos
            .Where(a => a.FinalizadoEm == null)
            .Select(a => a.MaquinaLinhaId)
            .ToListAsync(cancellationToken)).ToHashSet();

        var linhas = await _context.DispositivosIot
            .AsNoTracking()
            .Where(d => id == null || d.Id == id)
            .Select(d => new
            {
                d.Id,
                d.Nome,
                d.EnderecoIp,
                d.Ativo,
                d.MaquinaLinhaId,
                Maquina = d.MaquinaLinha.Maquina.Nome,
                d.MaquinaLinha.LinhaId,
                Linha = d.MaquinaLinha.Linha.Nome,
                Cliente = d.MaquinaLinha.Linha.Cliente.Nome,
                d.MaquinaLinha.Ordem,
                d.UltimaMensagemEm
            })
            .ToListAsync(cancellationToken);

        return linhas
            .OrderBy(d => d.Cliente).ThenBy(d => d.Linha).ThenBy(d => d.Ordem).ThenBy(d => d.Nome)
            .Select(d => new DispositivoIotDto(
                d.Id, d.Nome, d.EnderecoIp, d.Ativo, d.MaquinaLinhaId, d.Maquina, d.LinhaId, d.Linha, d.Cliente,
                d.UltimaMensagemEm, comColeta.Contains(d.MaquinaLinhaId)))
            .ToList();
    }
}
