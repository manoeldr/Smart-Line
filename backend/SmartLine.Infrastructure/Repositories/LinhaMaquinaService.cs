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
        return await Projetar(_context.MaquinasLinha
                .Where(ml => ml.LinhaId == linhaId && ml.Ativo)
                .OrderBy(ml => ml.Ordem))
            .ToListAsync();
    }

    public async Task<MaquinaLinhaConfDto> AdicionarMaquinaAsync(Guid linhaId, Guid maquinaId, bool critica, decimal velocidadeNominal, decimal sobreVelocidade, bool medeProducao, int? tempoDeteccaoParadaSegundos = null)
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
            MedeProducao = medeProducao,
            Ordem = maiorOrdem + 1,
            Ativo = true,
        };
        if (tempoDeteccaoParadaSegundos is { } z)
            maquinaLinha.TempoDeteccaoParadaSegundos = z;

        _context.MaquinasLinha.Add(maquinaLinha);
        await _context.SaveChangesAsync();

        return await Projetar(_context.MaquinasLinha.Where(ml => ml.Id == maquinaLinha.Id)).SingleAsync();
    }

    public async Task<MaquinaLinhaConfDto?> AtualizarAsync(Guid maquinaLinhaId, bool critica, decimal velocidadeNominal, decimal sobreVelocidade, bool medeProducao, int? tempoDeteccaoParadaSegundos = null)
    {
        var maquinaLinha = await _context.MaquinasLinha
            .FirstOrDefaultAsync(ml => ml.Id == maquinaLinhaId);

        if (maquinaLinha is null) return null;

        maquinaLinha.Critica = critica;
        maquinaLinha.VelocidadeNominal = velocidadeNominal;
        maquinaLinha.SobreVelocidade = sobreVelocidade;
        maquinaLinha.MedeProducao = medeProducao;
        // Uma coleta Semi Automática em andamento continua com o Z copiado ao iniciar.
        if (tempoDeteccaoParadaSegundos is { } z)
            maquinaLinha.TempoDeteccaoParadaSegundos = z;

        await _context.SaveChangesAsync();

        return await Projetar(_context.MaquinasLinha.Where(ml => ml.Id == maquinaLinhaId)).SingleAsync();
    }

    /// <summary>DTO com o que a tela de configuração mostra, inclusive o WISE e as regras da máquina.</summary>
    private IQueryable<MaquinaLinhaConfDto> Projetar(IQueryable<MaquinaLinha> consulta) =>
        consulta.Select(ml => new MaquinaLinhaConfDto(
            ml.Id.ToString(),
            ml.LinhaId.ToString(),
            ml.MaquinaId.ToString(),
            ml.Maquina.Nome,
            ml.Ordem,
            ml.Critica,
            ml.VelocidadeNominal,
            ml.SobreVelocidade,
            ml.MedeProducao,
            ml.Ativo,
            ml.TempoDeteccaoParadaSegundos,
            _context.DispositivosIot
                .Where(d => d.MaquinaLinhaId == ml.Id && d.Ativo)
                .Select(d => d.EnderecoIp)
                .FirstOrDefault(),
            _context.ConjuntosRegras.Any(c => c.MaquinaLinhaId == ml.Id)));

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