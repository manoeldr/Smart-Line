using Microsoft.EntityFrameworkCore;
using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Interfaces;
using SmartLine.Core.Iot;
using SmartLine.Infrastructure.Data;

namespace SmartLine.Infrastructure.Repositories;

public class WiseService : IWiseService
{
    private readonly SmartLineDbContext _context;
    private readonly TimeProvider _tempo;

    public WiseService(SmartLineDbContext context, TimeProvider tempo)
    {
        _context = context;
        _tempo = tempo;
    }

    public async Task<IReadOnlyList<WiseCadastradoDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var wises = await _context.Wises
            .AsNoTracking()
            .Select(w => new WiseCadastradoDto(w.Id, w.EnderecoIp, w.Nome, w.CriadoEm))
            .ToListAsync(cancellationToken);
        return wises.OrderBy(w => w.EnderecoIp, ComparadorIp.Instancia).ToList();
    }

    public async Task<ResultadoCadastro<WiseCadastradoDto>> AdicionarAsync(
        SalvarWiseRequest request,
        CancellationToken cancellationToken = default)
    {
        var (ip, nome, erro) = await ValidarAsync(null, request, cancellationToken);
        if (erro is not null)
            return ResultadoCadastro<WiseCadastradoDto>.Falha(erro);

        var wise = new Wise { Id = Guid.NewGuid(), EnderecoIp = ip!, Nome = nome, CriadoEm = _tempo.GetUtcNow().UtcDateTime };
        _context.Wises.Add(wise);
        if (!await SalvarAsync(cancellationToken))
            return ResultadoCadastro<WiseCadastradoDto>.Falha($"Já existe um WISE cadastrado com o IP {ip}.");

        return ResultadoCadastro<WiseCadastradoDto>.Ok(ParaDto(wise));
    }

    public async Task<ResultadoCadastro<WiseCadastradoDto>> EditarAsync(
        Guid id,
        SalvarWiseRequest request,
        CancellationToken cancellationToken = default)
    {
        var wise = await _context.Wises.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (wise is null)
            return ResultadoCadastro<WiseCadastradoDto>.Inexistente();

        var (ip, nome, erro) = await ValidarAsync(id, request, cancellationToken);
        if (erro is not null)
            return ResultadoCadastro<WiseCadastradoDto>.Falha(erro);

        if (ip != wise.EnderecoIp && await EmMedicaoAsync(wise.EnderecoIp, cancellationToken) is { } medicao)
            return ResultadoCadastro<WiseCadastradoDto>.Falha(
                $"O WISE {wise.EnderecoIp} está em uso na medição da {medicao}. Finalize a medição antes de trocar o IP.");

        wise.EnderecoIp = ip!;
        wise.Nome = nome;
        if (!await SalvarAsync(cancellationToken))
            return ResultadoCadastro<WiseCadastradoDto>.Falha($"Já existe um WISE cadastrado com o IP {ip}.");

        return ResultadoCadastro<WiseCadastradoDto>.Ok(ParaDto(wise));
    }

    public async Task<ResultadoCadastro<bool>> RemoverAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var wise = await _context.Wises.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (wise is null)
            return ResultadoCadastro<bool>.Inexistente();

        if (await EmMedicaoAsync(wise.EnderecoIp, cancellationToken) is { } medicao)
            return ResultadoCadastro<bool>.Falha(
                $"O WISE {wise.EnderecoIp} está em uso na medição da {medicao}. Finalize a medição antes de removê-lo.");

        _context.Wises.Remove(wise);
        await _context.SaveChangesAsync(cancellationToken);
        return ResultadoCadastro<bool>.Ok(true);
    }

    // ── Apoio ───────────────────────────────────────────────────────

    private async Task<(string? Ip, string? Nome, string? Erro)> ValidarAsync(
        Guid? id,
        SalvarWiseRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.EnderecoIp))
            return (null, null, "Informe o IP do WISE.");

        var ip = EnderecoRede.Normalizar(request.EnderecoIp);
        if (ip is null)
            return (null, null, $"IP inválido: \"{request.EnderecoIp.Trim()}\". Use o formato 192.168.10.21.");

        var nome = string.IsNullOrWhiteSpace(request.Nome) ? null : request.Nome.Trim();
        if (nome?.Length > LimitesWise.TamanhoMaximoNome)
            return (null, null, $"O nome pode ter no máximo {LimitesWise.TamanhoMaximoNome} caracteres.");

        var existente = await _context.Wises
            .AsNoTracking()
            .Where(w => w.EnderecoIp == ip && w.Id != id)
            .Select(w => new { w.Nome })
            .FirstOrDefaultAsync(cancellationToken);
        if (existente is not null)
            return (null, null, $"Já existe um WISE cadastrado com o IP {ip}{(existente.Nome is null ? "" : $" ({existente.Nome})")}.");

        return (ip, nome, null);
    }

    /// <summary>"Enchedora (Linha 1 · Cliente)" se há medição em andamento com o IP; senão nulo.</summary>
    private async Task<string?> EmMedicaoAsync(string ip, CancellationToken cancellationToken)
    {
        var medicao = await _context.Acompanhamentos
            .AsNoTracking()
            .Where(a => a.FinalizadoEm == null && a.EnderecoIpWise == ip)
            .Select(a => new
            {
                Maquina = a.MaquinaLinha.Maquina.Nome,
                Linha = a.MaquinaLinha.Linha.Nome,
                Cliente = a.MaquinaLinha.Linha.Cliente.Nome
            })
            .FirstOrDefaultAsync(cancellationToken);
        return medicao is null ? null : $"{medicao.Maquina} ({medicao.Linha} · {medicao.Cliente})";
    }

    /// <summary>Falso se o índice único do IP recusou (outra estação cadastrou o mesmo IP agora).</summary>
    private async Task<bool> SalvarAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            _context.ChangeTracker.Clear();
            return false;
        }
    }

    private static WiseCadastradoDto ParaDto(Wise w) => new(w.Id, w.EnderecoIp, w.Nome, w.CriadoEm);
}
