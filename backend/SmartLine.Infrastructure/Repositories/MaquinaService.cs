using Microsoft.EntityFrameworkCore;
using SmartLine.Core.Enums;
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

    public async Task<MotivoParadaDto> CriarMotivoParadaAsync(Guid maquinaId, string nome, string tipo)
    {
        var tipoEnum = tipo == "Externa" ? TipoParada.Externa : TipoParada.Interna;
        var motivo = new SmartLine.Core.Entities.Global.MotivoParada
        {
            Id = Guid.NewGuid(),
            MaquinaId = maquinaId,
            Nome = nome,
            Tipo = tipoEnum,
            Ativo = true,
        };
        _context.MotivosParada.Add(motivo);
        await _context.SaveChangesAsync();
        return new MotivoParadaDto(motivo.Id.ToString(), motivo.Nome, motivo.Tipo.ToString());
    }
}