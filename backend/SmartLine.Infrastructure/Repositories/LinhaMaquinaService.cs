using Microsoft.EntityFrameworkCore;
using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Enums;
using SmartLine.Core.Interfaces;
using SmartLine.Infrastructure.Data;

namespace SmartLine.Infrastructure.Repositories;

public class LinhaMaquinaService : ILinhaMaquinaService
{
    private readonly SmartLineDbContext _context;

    public LinhaMaquinaService(SmartLineDbContext context)
    {
        _context = context;
    }

    public async Task<IList<MaquinaLinhaConfDto>> GetMaquinasDaLinhaAsync(Guid linhaId)
    {
        return await _context.MaquinasLinha
            .Where(ml => ml.LinhaId == linhaId && ml.Ativo)
            .Include(ml => ml.Maquina)
            .OrderBy(ml => ml.Ordem)
            .Select(ml => new MaquinaLinhaConfDto(
                ml.Id.ToString(),
                ml.LinhaId.ToString(),
                ml.MaquinaId.ToString(),
                ml.Maquina.Nome,
                ml.Ordem,
                ml.Critica,
                ml.VelocidadeNominal,
                ml.SobreVelocidade,
                ml.Ativo
            ))
            .ToListAsync();
    }

    public async Task<MaquinaLinhaConfDto> AdicionarMaquinaAsync(Guid linhaId, Guid maquinaId, bool critica, decimal velocidadeNominal, decimal sobreVelocidade)
    {
        var maiorOrdem = await _context.MaquinasLinha
            .Where(ml => ml.LinhaId == linhaId)
            .Select(ml => (int?)ml.Ordem)
            .MaxAsync() ?? 0;

        var maquinaLinha = new MaquinaLinha
        {
            Id = Guid.NewGuid(),
            LinhaId = linhaId,
            MaquinaId = maquinaId,
            TipoColeta = TipoColeta.Manual,
            VelocidadeNominal = velocidadeNominal,
            SobreVelocidade = sobreVelocidade,
            Critica = critica,
            Ordem = maiorOrdem + 1,
            Ativo = true,
        };

        _context.MaquinasLinha.Add(maquinaLinha);
        await _context.SaveChangesAsync();

        var maquina = await _context.Maquinas.FindAsync(maquinaId);

        return new MaquinaLinhaConfDto(
            maquinaLinha.Id.ToString(),
            maquinaLinha.LinhaId.ToString(),
            maquinaLinha.MaquinaId.ToString(),
            maquina?.Nome ?? "",
            maquinaLinha.Ordem,
            maquinaLinha.Critica,
            maquinaLinha.VelocidadeNominal,
            maquinaLinha.SobreVelocidade,
            maquinaLinha.Ativo
        );
    }

    public async Task<MaquinaLinhaConfDto?> AtualizarAsync(Guid maquinaLinhaId, bool critica, decimal velocidadeNominal, decimal sobreVelocidade)
    {
        var maquinaLinha = await _context.MaquinasLinha
            .Include(ml => ml.Maquina)
            .FirstOrDefaultAsync(ml => ml.Id == maquinaLinhaId);

        if (maquinaLinha is null) return null;

        maquinaLinha.Critica = critica;
        maquinaLinha.VelocidadeNominal = velocidadeNominal;
        maquinaLinha.SobreVelocidade = sobreVelocidade;

        await _context.SaveChangesAsync();

        return new MaquinaLinhaConfDto(
            maquinaLinha.Id.ToString(),
            maquinaLinha.LinhaId.ToString(),
            maquinaLinha.MaquinaId.ToString(),
            maquinaLinha.Maquina.Nome,
            maquinaLinha.Ordem,
            maquinaLinha.Critica,
            maquinaLinha.VelocidadeNominal,
            maquinaLinha.SobreVelocidade,
            maquinaLinha.Ativo
        );
    }

    public async Task<bool> RemoverMaquinaAsync(Guid maquinaLinhaId)
    {
        var maquinaLinha = await _context.MaquinasLinha.FindAsync(maquinaLinhaId);
        if (maquinaLinha is null) return false;

        maquinaLinha.Ativo = false;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task ReordenarAsync(Guid linhaId, IList<ReordenarItem> ordens)
    {
        var ids = ordens.Select(o => o.MaquinaLinhaId).ToList();
        var maquinasLinha = await _context.MaquinasLinha
            .Where(ml => ml.LinhaId == linhaId && ids.Contains(ml.Id))
            .ToListAsync();

        foreach (var item in ordens)
        {
            var maquinaLinha = maquinasLinha.FirstOrDefault(ml => ml.Id == item.MaquinaLinhaId);
            if (maquinaLinha is not null)
            {
                maquinaLinha.Ordem = item.Ordem;
            }
        }

        await _context.SaveChangesAsync();
    }
}
