using Microsoft.EntityFrameworkCore;
using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Enums;
using SmartLine.Core.Interfaces;
using SmartLine.Infrastructure.Data;

namespace SmartLine.Infrastructure.Repositories;

public class SessaoService : ISessaoService
{
    private readonly SmartLineDbContext _context;

    public SessaoService(SmartLineDbContext context)
    {
        _context = context;
    }

    public async Task<SessaoDto?> AbrirAsync(Guid maquinaLinhaId, Guid usuarioId, AbrirSessaoRequest req)
    {
        var sessaoExistente = await _context.Sessoes
            .AnyAsync(s => s.MaquinaLinhaId == maquinaLinhaId && s.Status == StatusSessao.EmAndamento);

        if (sessaoExistente) return null;

        var tipoColeta = Enum.TryParse<TipoColeta>(req.TipoColeta, out var tc) ? tc : TipoColeta.Manual;

        var sessao = new Sessao
        {
            Id = Guid.NewGuid(),
            MaquinaLinhaId = maquinaLinhaId,
            UsuarioId = usuarioId,
            Inicio = DateTime.UtcNow,
            Status = StatusSessao.EmAndamento,
            VelocidadeNominal = req.VelocidadeNominal,
            SobreVelocidade = req.SobreVelocidade,
            PrevisaoTermino = req.PrevisaoTermino,
            TipoColeta = tipoColeta,
            CriadoEm = DateTime.UtcNow,
        };

        _context.Sessoes.Add(sessao);

        if (req.CampoMaquinaIds is { Count: > 0 })
        {
            foreach (var campoId in req.CampoMaquinaIds)
            {
                _context.SessoesCampo.Add(new SessaoCampo
                {
                    Id = Guid.NewGuid(),
                    SessaoId = sessao.Id,
                    CampoMaquinaId = campoId,
                });
            }
        }

        await _context.SaveChangesAsync();

        return await ToDtoAsync(sessao);
    }

    public async Task<bool> FecharAsync(Guid sessaoId)
    {
        var sessao = await _context.Sessoes.FindAsync(sessaoId);
        if (sessao is null) return false;

        sessao.Fim = DateTime.UtcNow;
        sessao.Status = StatusSessao.Finalizada;
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<SessaoDto?> GetByIdAsync(Guid sessaoId)
    {
        var sessao = await _context.Sessoes.FindAsync(sessaoId);
        if (sessao is null) return null;
        return await ToDtoAsync(sessao);
    }

    public async Task<SessaoDto?> EstenderAsync(Guid sessaoId, DateTime novaPrevisaoTermino)
    {
        var sessao = await _context.Sessoes.FindAsync(sessaoId);
        if (sessao is null) return null;

        sessao.PrevisaoTermino = novaPrevisaoTermino;
        await _context.SaveChangesAsync();

        return await ToDtoAsync(sessao);
    }

    public async Task<bool> FinalizarComLeituraAsync(Guid sessaoId, FinalizarSessaoRequest req)
    {
        var sessao = await _context.Sessoes.FindAsync(sessaoId);
        if (sessao is null) return false;

        var agora = DateTime.UtcNow;

        // Salva a leitura final de produção
        _context.Producoes.Add(new Producao
        {
            Id = Guid.NewGuid(),
            SessaoId = sessaoId,
            Quantidade = req.ProducaoFinal,
            Refugo = req.RefugoFinal,
            Hora = agora,
        });

        // Salva os valores finais dos campos extras
        if (req.Extras is { Count: > 0 })
        {
            foreach (var extra in req.Extras)
            {
                _context.LeiturasExtra.Add(new LeituraExtra
                {
                    Id = Guid.NewGuid(),
                    SessaoId = sessaoId,
                    CampoMaquinaId = extra.CampoMaquinaId,
                    Valor = extra.Valor,
                    Hora = agora,
                });
            }
        }

        sessao.Fim = agora;
        sessao.Status = StatusSessao.Finalizada;

        await _context.SaveChangesAsync();
        return true;
    }

    // Busca a sessão ativa do usuário com tudo já preenchido — permite reconstruir o estado
    // da tela de Medição inteiramente a partir do banco (sobrevive a reiniciar o servidor ou
    // trocar de dispositivo, sem depender do localStorage do navegador).
    public async Task<SessaoAtivaDto?> GetSessaoAtivaDoUsuarioAsync(Guid usuarioId)
    {
        var sessao = await _context.Sessoes
            .Include(s => s.MaquinaLinha).ThenInclude(ml => ml.Maquina)
            .Include(s => s.MaquinaLinha).ThenInclude(ml => ml.Linha)
            .Include(s => s.Producoes)
            .Include(s => s.LeiturasExtra)
            .Include(s => s.Paradas).ThenInclude(p => p.Motivo)
            .Include(s => s.SessoesCampo)
            .FirstOrDefaultAsync(s => s.UsuarioId == usuarioId && s.Status == StatusSessao.EmAndamento);

        if (sessao is null) return null;

        // Status atual — deriva da parada em aberto (se houver) e do tipo de motivo dela.
        // Ordena por início como blindagem extra: se por algum motivo existir mais de uma
        // parada em aberto, sempre considera a mais antiga (a que realmente está em curso).
        var paradaAtiva = sessao.Paradas.Where(p => !p.Fim.HasValue).OrderBy(p => p.Inicio).FirstOrDefault();
        string status;
        if (paradaAtiva is null)
        {
            status = "Rodando";
        }
        else if (paradaAtiva.Motivo?.Tipo == TipoParada.Planejada)
        {
            status = "Pausada";
        }
        else
        {
            status = "Parada";
        }

        // Total parado — soma só paradas Interna/Externa (Planejada vira "Pausada", não conta aqui),
        // incluindo o tempo da parada em aberto até agora, se for o caso.
        var segundosTotalParadoMs = sessao.Paradas
            .Where(p => p.Fim.HasValue && p.Motivo?.Tipo != TipoParada.Planejada)
            .Sum(p => (p.Fim!.Value - p.Inicio).TotalMilliseconds);

        if (paradaAtiva is not null && status == "Parada")
        {
            segundosTotalParadoMs += (DateTime.UtcNow - paradaAtiva.Inicio).TotalMilliseconds;
        }

        // Reconstrói as linhas de leitura — Produção e campos extras são salvos em tabelas
        // separadas, ligados só pelo horário (registrados no mesmo instante); agrupa por
        // horário exato para remontar cada linha da tabela como ela apareceu originalmente.
        var todasHoras = new SortedSet<DateTime>();
        foreach (var p in sessao.Producoes) todasHoras.Add(p.Hora);
        foreach (var le in sessao.LeiturasExtra) todasHoras.Add(le.Hora);

        var leituras = new List<LeituraReconstruidaDto>();
        var primeira = true;
        foreach (var hora in todasHoras)
        {
            var producaoDaHora = sessao.Producoes.FirstOrDefault(p => p.Hora == hora);
            var extrasDaHora = sessao.LeiturasExtra
                .Where(le => le.Hora == hora)
                .ToDictionary(le => le.CampoMaquinaId.ToString(), le => le.Valor);

            leituras.Add(new LeituraReconstruidaDto(
                Hora: hora,
                Inicial: primeira,
                Producao: producaoDaHora?.Quantidade,
                Extras: extrasDaHora
            ));
            primeira = false;
        }

        var camposSelecionados = sessao.SessoesCampo
            .Select(sc => sc.CampoMaquinaId.ToString())
            .ToList();

        return new SessaoAtivaDto(
            SessaoId: sessao.Id.ToString(),
            MaquinaLinhaId: sessao.MaquinaLinhaId.ToString(),
            MaquinaId: sessao.MaquinaLinha.MaquinaId.ToString(),
            MaquinaNome: sessao.MaquinaLinha.Maquina.Nome,
            Critica: sessao.MaquinaLinha.Critica,
            MedeProducao: sessao.MaquinaLinha.MedeProducao,
            VelocidadeNominal: sessao.VelocidadeNominal,
            SobreVelocidade: sessao.SobreVelocidade,
            LinhaId: sessao.MaquinaLinha.LinhaId.ToString(),
            LinhaNome: sessao.MaquinaLinha.Linha.Nome,
            Inicio: sessao.Inicio,
            PrevisaoTermino: sessao.PrevisaoTermino,
            CamposSelecionados: camposSelecionados,
            Status: status,
            Leituras: leituras,
            ParadaAtivaId: paradaAtiva?.Id.ToString(),
            ParadaAtivaInicio: paradaAtiva?.Inicio,
            SegundosTotalParadoMs: segundosTotalParadoMs
        );
    }

    private async Task<SessaoDto> ToDtoAsync(Sessao s)
    {
        var camposSelecionados = await _context.SessoesCampo
            .Where(sc => sc.SessaoId == s.Id)
            .Select(sc => sc.CampoMaquinaId.ToString())
            .ToListAsync();

        return new SessaoDto(
            Id: s.Id.ToString(),
            MaquinaLinhaId: s.MaquinaLinhaId.ToString(),
            UsuarioId: s.UsuarioId.ToString(),
            Inicio: s.Inicio,
            Fim: s.Fim,
            PrevisaoTermino: s.PrevisaoTermino,
            Status: s.Status.ToString(),
            TipoColeta: s.TipoColeta.ToString(),
            VelocidadeNominal: s.VelocidadeNominal,
            SobreVelocidade: s.SobreVelocidade,
            CamposSelecionados: camposSelecionados
        );
    }
}