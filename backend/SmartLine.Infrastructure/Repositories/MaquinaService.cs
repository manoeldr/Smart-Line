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
        // Motivos internos/externos: apenas da própria máquina
        var motivosProprios = await _context.MotivosParada
            .Where(m => m.MaquinaId == maquinaId && m.Ativo && m.Tipo != TipoParada.Planejada)
            .OrderBy(m => m.Nome)
            .Select(m => new MotivoParadaDto(m.Id.ToString(), m.Nome, m.Tipo.ToString()))
            .ToListAsync();

        // Motivos planejados: compartilhados entre todas as máquinas da mesma linha
        var linhaId = await _context.MaquinasLinha
            .Where(ml => ml.MaquinaId == maquinaId)
            .Select(ml => ml.LinhaId)
            .FirstOrDefaultAsync();

        IList<MotivoParadaDto> motivosPlanejados = new List<MotivoParadaDto>();

        if (linhaId != Guid.Empty)
        {
            var maquinasNaLinha = await _context.MaquinasLinha
                .Where(ml => ml.LinhaId == linhaId)
                .Select(ml => ml.MaquinaId)
                .ToListAsync();

            motivosPlanejados = await _context.MotivosParada
                .Where(m => maquinasNaLinha.Contains(m.MaquinaId) && m.Ativo && m.Tipo == TipoParada.Planejada)
                .OrderBy(m => m.Nome)
                .Select(m => new MotivoParadaDto(m.Id.ToString(), m.Nome, m.Tipo.ToString()))
                .Distinct()
                .ToListAsync();
        }

        return motivosProprios.Concat(motivosPlanejados).ToList();
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

    public async Task<MotivoParadaDto> CriarMotivoParadaPlanejadoAsync(Guid maquinaId, string nome)
    {
        var motivo = new SmartLine.Core.Entities.Global.MotivoParada
        {
            Id = Guid.NewGuid(),
            MaquinaId = maquinaId,
            Nome = nome,
            Tipo = TipoParada.Planejada,
            Ativo = true,
        };
        _context.MotivosParada.Add(motivo);
        await _context.SaveChangesAsync();
        return new MotivoParadaDto(motivo.Id.ToString(), motivo.Nome, motivo.Tipo.ToString());
    }
}