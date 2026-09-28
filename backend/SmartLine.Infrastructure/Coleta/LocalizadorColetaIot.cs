using Microsoft.EntityFrameworkCore;
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

    public async Task<Guid?> MaquinaDoDispositivoAsync(string enderecoIp, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(enderecoIp))
            return null;

        return await _context.DispositivosIot
            .AsNoTracking()
            .Where(d => d.EnderecoIp == enderecoIp && d.Ativo)
            .Select(d => (Guid?)d.MaquinaLinhaId)
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

    public async Task RegistrarUltimaMensagemAsync(string enderecoIp, DateTime instanteUtc, CancellationToken cancellationToken = default) =>
        await _context.DispositivosIot
            .Where(d => d.EnderecoIp == enderecoIp)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.UltimaMensagemEm, instanteUtc), cancellationToken);

    // O SQLite devolve DateTime sem Kind; o que se grava aqui é sempre UTC.
    // Filtro antes da projeção: depois do construtor o EF não consegue mais traduzir.
    private IQueryable<ColetaEmAndamento> EmAndamento(Guid? maquinaLinhaId) =>
        _context.Acompanhamentos
            .AsNoTracking()
            .Where(a => a.FinalizadoEm == null && (maquinaLinhaId == null || a.MaquinaLinhaId == maquinaLinhaId))
            .Select(a => new ColetaEmAndamento(a.Id, a.MaquinaLinhaId, DateTime.SpecifyKind(a.IniciadoEm, DateTimeKind.Utc)));
}
