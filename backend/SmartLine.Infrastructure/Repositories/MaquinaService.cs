using Microsoft.EntityFrameworkCore;
using SmartLine.Core.Interfaces;
using SmartLine.Infrastructure.Data;

namespace SmartLine.Infrastructure.Repositories;

public class MaquinaService : IMaquinaService
{
    private readonly SmartLineDbContext _context;

    public MaquinaService(SmartLineDbContext context)
    {
        _context = context;
    }

    public async Task<IList<MotivoParadaDto>> GetMotivosParadaAsync(Guid maquinaId)
    {
        return await _context.MotivosParada
            .Where(m => m.MaquinaId == maquinaId && m.Ativo)
            .OrderBy(m => m.Nome)
            .Select(m => new MotivoParadaDto(
                m.Id.ToString(),
                m.Nome,
                m.Tipo.ToString()
            ))
            .ToListAsync();
    }
}
