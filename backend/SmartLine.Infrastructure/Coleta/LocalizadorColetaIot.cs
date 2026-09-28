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

    public async Task<Guid?> AcompanhamentoEmAndamentoAsync(Guid maquinaLinhaId, CancellationToken cancellationToken = default) =>
        await _context.Acompanhamentos
            .AsNoTracking()
            .Where(a => a.MaquinaLinhaId == maquinaLinhaId && a.FinalizadoEm == null)
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync(cancellationToken);
}
