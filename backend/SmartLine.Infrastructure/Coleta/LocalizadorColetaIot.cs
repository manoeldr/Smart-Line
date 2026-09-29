using Microsoft.EntityFrameworkCore;
using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Interfaces;
using SmartLine.Infrastructure.Data;

namespace SmartLine.Infrastructure.Coleta;

public class LocalizadorColetaIot : ILocalizadorColetaIot
{
    private readonly SmartLineDbContext _context;

    public LocalizadorColetaIot(SmartLineDbContext context)
    {
        _context = context;
    }

    public async Task<Guid?> MaquinaDoWiseAsync(string enderecoIp, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(enderecoIp))
            return null;

        return await _context.Acompanhamentos
            .AsNoTracking()
            .Where(a => a.FinalizadoEm == null && a.EnderecoIpWise == enderecoIp)
            .Select(a => (Guid?)a.MaquinaLinhaId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ColetaEmAndamento?> ColetaEmAndamentoAsync(Guid maquinaLinhaId, CancellationToken cancellationToken = default) =>
        await EmAndamento(maquinaLinhaId).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ColetaEmAndamento>> ColetasEmAndamentoAsync(CancellationToken cancellationToken = default) =>
        await EmAndamento(null).ToListAsync(cancellationToken);

    public async Task<Guid?> MaquinaDoAcompanhamentoAsync(Guid acompanhamentoId, CancellationToken cancellationToken = default) =>
        await _context.Acompanhamentos
            .AsNoTracking()
            .Where(a => a.Id == acompanhamentoId)
            .Select(a => (Guid?)a.MaquinaLinhaId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<Guid?> MaquinaDoCatalogoAsync(Guid maquinaLinhaId, CancellationToken cancellationToken = default) =>
        await _context.MaquinasLinha
            .AsNoTracking()
            .Where(m => m.Id == maquinaLinhaId)
            .Select(m => (Guid?)m.MaquinaId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<WiseEmMedicao>> WisesEmMedicaoAsync(CancellationToken cancellationToken = default) =>
        await Wises(_context.Acompanhamentos.Where(a => a.FinalizadoEm == null && a.EnderecoIpWise != null))
            .ToListAsync(cancellationToken);

    public async Task<WiseEmMedicao?> WiseEmMedicaoAsync(string enderecoIp, CancellationToken cancellationToken = default) =>
        await Wises(_context.Acompanhamentos.Where(a => a.FinalizadoEm == null && a.EnderecoIpWise == enderecoIp))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task RegistrarUltimaMensagemAsync(string enderecoIp, DateTime instanteUtc, CancellationToken cancellationToken = default) =>
        await _context.Acompanhamentos
            .Where(a => a.FinalizadoEm == null && a.EnderecoIpWise == enderecoIp)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.UltimaMensagemWiseEm, instanteUtc), cancellationToken);

    // Filtro antes da projeção: depois do construtor o EF não consegue mais traduzir.
    // As datas já saem em UTC (conversor do contexto).
    private static IQueryable<WiseEmMedicao> Wises(IQueryable<Acompanhamento> consulta) =>
        consulta
            .AsNoTracking()
            .Select(a => new WiseEmMedicao(
                a.EnderecoIpWise!,
                a.Id,
                a.MaquinaLinhaId,
                a.MaquinaLinha.MaquinaId,
                a.MaquinaLinha.Maquina.Nome,
                a.MaquinaLinha.Linha.Nome,
                a.MaquinaLinha.Linha.Cliente.Nome,
                a.Usuario.Nome,
                a.IniciadoEm,
                a.UltimaMensagemWiseEm));

    private IQueryable<ColetaEmAndamento> EmAndamento(Guid? maquinaLinhaId) =>
        _context.Acompanhamentos
            .AsNoTracking()
            .Where(a => a.FinalizadoEm == null && (maquinaLinhaId == null || a.MaquinaLinhaId == maquinaLinhaId))
            .Select(a => new ColetaEmAndamento(a.Id, a.MaquinaLinhaId, DateTime.SpecifyKind(a.IniciadoEm, DateTimeKind.Utc)));
}
