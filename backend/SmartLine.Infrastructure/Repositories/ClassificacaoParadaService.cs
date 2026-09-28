using Microsoft.EntityFrameworkCore;
using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Enums;
using SmartLine.Core.Interfaces;
using SmartLine.Infrastructure.Data;

namespace SmartLine.Infrastructure.Repositories;

public class ClassificacaoParadaService : IClassificacaoParadaService
{
    private readonly SmartLineDbContext _context;
    private readonly TimeProvider _tempo;

    public ClassificacaoParadaService(SmartLineDbContext context, TimeProvider tempo)
    {
        _context = context;
        _tempo = tempo;
    }

    public async Task<IReadOnlyList<ParadaColetaDto>> ListarPendentesAsync(
        FiltroParadasPendentes filtro,
        CancellationToken cancellationToken = default)
    {
        var limite = Math.Clamp(filtro.Limite, 1, 1000);
        var consulta = _context.Paradas.AsNoTracking()
            .Where(p => p.MotivoId == null && p.Sessao.AcompanhamentoId != null);

        if (filtro.MaquinaLinhaId is { } maquina)
            consulta = consulta.Where(p => p.Sessao.MaquinaLinhaId == maquina);
        if (filtro.LinhaId is { } linha)
            consulta = consulta.Where(p => p.Sessao.MaquinaLinha.LinhaId == linha);
        if (filtro.Desde is { } desde)
            consulta = consulta.Where(p => p.Inicio >= desde);
        if (filtro.Ate is { } ate)
            consulta = consulta.Where(p => p.Inicio <= ate);

        return await ProjetarAsync(consulta.OrderByDescending(p => p.Inicio).Take(limite), cancellationToken);
    }

    public async Task<ResultadoCadastro<ParadaColetaDto>> ReclassificarAsync(
        Guid paradaId,
        Guid motivoId,
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        var parada = await _context.Paradas
            .Include(p => p.Sessao).ThenInclude(s => s.MaquinaLinha)
            .FirstOrDefaultAsync(p => p.Id == paradaId, cancellationToken);
        if (parada is null)
            return ResultadoCadastro<ParadaColetaDto>.Inexistente();

        var motivo = await _context.MotivosParada.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == motivoId, cancellationToken);
        if (motivo is null || motivo.MaquinaId != parada.Sessao.MaquinaLinha.MaquinaId)
            return ResultadoCadastro<ParadaColetaDto>.Falha("Motivo não encontrado para esta máquina.");
        if (!motivo.Ativo)
            return ResultadoCadastro<ParadaColetaDto>.Falha($"O motivo \"{motivo.Nome}\" está inativo.");

        if (parada.MotivoId != motivoId)
        {
            _context.HistoricosClassificacaoParada.Add(new HistoricoClassificacaoParada
            {
                Id = Guid.NewGuid(),
                ParadaId = parada.Id,
                MotivoAnteriorId = parada.MotivoId,
                MotivoNovoId = motivoId,
                UsuarioId = usuarioId,
                AlteradoEm = _tempo.GetUtcNow().UtcDateTime
            });
            parada.MotivoId = motivoId;
            parada.RegraClassificacaoId = null; // agora é classificação de alguém, não da regra
            await _context.SaveChangesAsync(cancellationToken);
        }

        var dto = await ProjetarAsync(_context.Paradas.AsNoTracking().Where(p => p.Id == paradaId), cancellationToken);
        return ResultadoCadastro<ParadaColetaDto>.Ok(dto.Single());
    }

    public async Task<IReadOnlyList<HistoricoClassificacaoDto>?> HistoricoAsync(
        Guid paradaId,
        CancellationToken cancellationToken = default)
    {
        if (!await _context.Paradas.AnyAsync(p => p.Id == paradaId, cancellationToken))
            return null;

        var registros = await _context.HistoricosClassificacaoParada.AsNoTracking()
            .Where(h => h.ParadaId == paradaId)
            .OrderBy(h => h.AlteradoEm)
            .Select(h => new
            {
                h.AlteradoEm,
                h.MotivoAnteriorId,
                MotivoAnterior = h.MotivoAnterior == null ? null : h.MotivoAnterior.Nome,
                h.MotivoNovoId,
                MotivoNovo = h.MotivoNovo == null ? null : h.MotivoNovo.Nome,
                h.UsuarioId,
                Usuario = h.Usuario == null ? null : h.Usuario.Nome
            })
            .ToListAsync(cancellationToken);

        return registros
            .Select(h => new HistoricoClassificacaoDto(
                h.AlteradoEm, h.MotivoAnteriorId, h.MotivoAnterior, h.MotivoNovoId, h.MotivoNovo,
                h.UsuarioId, h.UsuarioId is null ? "Sistema" : h.Usuario ?? "Usuário removido"))
            .ToList();
    }

    private async Task<IReadOnlyList<ParadaColetaDto>> ProjetarAsync(IQueryable<Parada> consulta, CancellationToken cancellationToken)
    {
        var agora = _tempo.GetUtcNow().UtcDateTime;
        var linhas = await consulta
            .Select(p => new
            {
                p.Id,
                p.SessaoId,
                p.Sessao.MaquinaLinhaId,
                p.Sessao.MaquinaLinha.MaquinaId,
                Maquina = p.Sessao.MaquinaLinha.Maquina.Nome,
                Linha = p.Sessao.MaquinaLinha.Linha.Nome,
                Cliente = p.Sessao.MaquinaLinha.Linha.Cliente.Nome,
                p.Inicio,
                p.Fim,
                p.MotivoId,
                Motivo = p.Motivo == null ? null : p.Motivo.Nome,
                Tipo = p.Motivo == null ? (TipoParada?)null : p.Motivo.Tipo,
                p.RegraClassificacaoId
            })
            .ToListAsync(cancellationToken);

        return linhas
            .Select(p => new ParadaColetaDto(
                p.Id, p.SessaoId, p.MaquinaLinhaId, p.MaquinaId, p.Maquina, p.Linha, p.Cliente,
                p.Inicio, p.Fim, Math.Max(0, ((p.Fim ?? agora) - p.Inicio).TotalSeconds),
                p.MotivoId, p.Motivo, p.Tipo ?? TipoParada.Interna, p.RegraClassificacaoId is not null))
            .ToList();
    }
}
