using Microsoft.EntityFrameworkCore;
using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Interfaces;
using SmartLine.Infrastructure.Data;

namespace SmartLine.Infrastructure.Repositories;

public class ParadaRegistroService : IParadaRegistroService
{
    private readonly SmartLineDbContext _context;

    public ParadaRegistroService(SmartLineDbContext context)
    {
        _context = context;
    }

    public async Task<ParadaDto> AbrirAsync(Guid sessaoId, DateTime inicio)
    {
        var parada = new Parada
        {
            Id = Guid.NewGuid(),
            SessaoId = sessaoId,
            MotivoId = null,
            Inicio = inicio,
            Fim = null,
        };

        _context.Paradas.Add(parada);
        await _context.SaveChangesAsync();

        return ToDto(parada);
    }

    public async Task<ParadaDto?> FecharAsync(Guid paradaId, Guid motivoId, DateTime fim)
    {
        var parada = await _context.Paradas.FindAsync(paradaId);
        if (parada is null) return null;

        parada.MotivoId = motivoId;
        parada.Fim = fim;
        await _context.SaveChangesAsync();

        return ToDto(parada);
    }

    private static ParadaDto ToDto(Parada p) => new(
        Id: p.Id.ToString(),
        SessaoId: p.SessaoId.ToString(),
        MotivoId: p.MotivoId == Guid.Empty ? null : p.MotivoId.ToString(),
        Inicio: p.Inicio,
        Fim: p.Fim
    );
}
