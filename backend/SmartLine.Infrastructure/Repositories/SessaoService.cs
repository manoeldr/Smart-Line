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