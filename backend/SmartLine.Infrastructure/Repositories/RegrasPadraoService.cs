using Microsoft.EntityFrameworkCore;
using SmartLine.Core.Entities.Global;
using SmartLine.Core.Enums;
using SmartLine.Core.Interfaces;
using SmartLine.Core.Iot;
using SmartLine.Infrastructure.Data;

namespace SmartLine.Infrastructure.Repositories;

public class RegrasPadraoService : IRegrasPadraoService
{
    private readonly SmartLineDbContext _context;

    public RegrasPadraoService(SmartLineDbContext context)
    {
        _context = context;
    }

    public async Task<bool> GarantirAsync(Guid maquinaId, CancellationToken cancellationToken = default)
    {
        if (await _context.ConjuntosRegras.AnyAsync(c => c.MaquinaId == maquinaId, cancellationToken))
            return false;

        await CriarPadraoAsync(maquinaId, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> GarantirParaTodasAsync(CancellationToken cancellationToken = default)
    {
        var semRegras = await _context.Maquinas
            .Where(m => m.Ativo && !_context.ConjuntosRegras.Any(c => c.MaquinaId == m.Id))
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        foreach (var maquinaId in semRegras)
            await CriarPadraoAsync(maquinaId, cancellationToken);

        if (semRegras.Count > 0)
            await _context.SaveChangesAsync(cancellationToken);

        return semRegras.Count;
    }

    private async Task CriarPadraoAsync(Guid maquinaId, CancellationToken cancellationToken)
    {
        var motivosExistentes = await _context.MotivosParada
            .Where(m => m.MaquinaId == maquinaId)
            .ToListAsync(cancellationToken);

        var conjunto = new ConjuntoRegras
        {
            Id = RegrasPadraoIot.IdDeterministico(maquinaId, "conjunto"),
            MaquinaId = maquinaId,
            CriadoEm = DateTime.UtcNow
        };

        foreach (var padrao in RegrasPadraoIot.Todas)
        {
            // Reaproveita, nesta ordem:
            // 1. o motivo padrão com o Id determinístico (veio do PC central por importação,
            //    talvez renomeado lá — procurar só por nome tentaria criar de novo com o mesmo
            //    Id e derrubaria a inicialização);
            // 2. um motivo externo com exatamente o mesmo nome.
            // Inativo também conta: a regra aponta para ele e fica visível no editor, em vez
            // de duplicar. Só cria se não achar nenhum dos dois.
            var idPadrao = RegrasPadraoIot.IdDeterministico(maquinaId, $"motivo:{padrao.Chave}");
            var motivo = motivosExistentes.FirstOrDefault(m => m.Id == idPadrao)
                         ?? motivosExistentes.FirstOrDefault(m =>
                             m.Nome == padrao.NomeMotivo && m.Tipo == TipoParada.Externa);
            if (motivo is null)
            {
                motivo = new MotivoParada
                {
                    Id = idPadrao,
                    MaquinaId = maquinaId,
                    Nome = padrao.NomeMotivo,
                    Tipo = TipoParada.Externa,
                    Ativo = true
                };
                _context.MotivosParada.Add(motivo);
            }

            var regraId = RegrasPadraoIot.IdDeterministico(maquinaId, $"regra:{padrao.Chave}");
            conjunto.Regras.Add(new RegraClassificacao
            {
                Id = regraId,
                Prioridade = padrao.Prioridade,
                Nome = padrao.NomeMotivo,
                MotivoParadaId = motivo.Id,
                Ativa = true,
                Condicoes =
                [
                    new CondicaoRegra
                    {
                        Id = RegrasPadraoIot.IdDeterministico(regraId, "condicao:1"),
                        Tipo = TipoCondicao.SensorEmAlarme,
                        Canal = padrao.Sensor
                    }
                ]
            });
        }

        _context.ConjuntosRegras.Add(conjunto);
    }
}
