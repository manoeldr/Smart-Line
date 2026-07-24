using Microsoft.EntityFrameworkCore;
using SmartLine.Core.Enums;
using SmartLine.Core.Interfaces;
using SmartLine.Infrastructure.Data;

namespace SmartLine.Infrastructure.Repositories;

public class ClienteService : IClienteService
{
    private readonly SmartLineDbContext _context;
    private readonly IOeeService _oeeService;

    public ClienteService(SmartLineDbContext context, IOeeService oeeService)
    {
        _context = context;
        _oeeService = oeeService;
    }

    public async Task<IList<ClienteDto>> GetAllAsync()
    {
        return await _context.Clientes
            .Where(c => c.Ativo)
            .OrderBy(c => c.Nome)
            .Select(c => new ClienteDto(
                c.Id.ToString(),
                c.Nome,
                c.Estado,
                c.Ativo
            ))
            .ToListAsync();
    }

    public async Task<ClienteDto?> GetByIdAsync(Guid id)
    {
        var c = await _context.Clientes.FindAsync(id);
        if (c is null) return null;
        return new ClienteDto(c.Id.ToString(), c.Nome, c.Estado, c.Ativo);
    }

    public async Task<IList<LinhaOverviewDto>> GetLinhasAsync(Guid clienteId)
    {
        var linhas = await _context.Linhas
            .Where(l => l.ClienteId == clienteId && l.Ativo)
            .Include(l => l.Maquinas)
                .ThenInclude(ml => ml.Maquina)
            .OrderBy(l => l.Nome)
            .ToListAsync();

        var resultado = new List<LinhaOverviewDto>();

        foreach (var linha in linhas)
        {
            var maquinasDto = new List<MaquinaLinhaOverviewDto>();

            foreach (var ml in linha.Maquinas.Where(m => m.Ativo).OrderBy(m => m.Ordem))
            {
                var sessaoAtiva = await _context.Sessoes
                    .Include(s => s.Producoes)
                    .Include(s => s.Paradas)
                        .ThenInclude(p => p.Motivo)
                    .FirstOrDefaultAsync(s => s.MaquinaLinhaId == ml.Id && s.Status == StatusSessao.EmAndamento);

                Core.Entities.Tenant.Sessao? ultimaSessaoFinalizada = null;

                if (sessaoAtiva is null)
                {
                    ultimaSessaoFinalizada = await _context.Sessoes
                        .Include(s => s.Producoes)
                        .Include(s => s.Paradas)
                            .ThenInclude(p => p.Motivo)
                        .Where(s => s.MaquinaLinhaId == ml.Id && s.Status == StatusSessao.Finalizada)
                        .OrderByDescending(s => s.Fim)
                        .FirstOrDefaultAsync();
                }

                var sessaoParaOee = sessaoAtiva ?? ultimaSessaoFinalizada;
                var status = ResolverStatus(sessaoAtiva);
                double? oee = null;

                if (sessaoParaOee is not null)
                {
                    var resultadoOee = _oeeService.Calcular(sessaoParaOee, ml.VelocidadeNominal);
                    oee = resultadoOee.Oee;
                }

                maquinasDto.Add(new MaquinaLinhaOverviewDto(
                    Id: ml.Id.ToString(),
                    LinhaId: ml.LinhaId.ToString(),
                    MaquinaId: ml.MaquinaId.ToString(),
                    MaquinaNome: ml.Maquina.Nome,
                    TipoColeta: ml.TipoColeta.ToString(),
                    VelocidadeNominal: ml.VelocidadeNominal,
                    Critica: ml.Critica,
                    Ordem: ml.Ordem,
                    Ativo: ml.Ativo,
                    Status: status,
                    Oee: oee.HasValue ? Math.Round(oee.Value, 1) : null,
                    SessaoAtiva: sessaoAtiva is not null,
                    UltimaSessaoFim: ultimaSessaoFinalizada?.Fim
                ));
            }

            resultado.Add(new LinhaOverviewDto(
                Id: linha.Id.ToString(),
                ClienteId: linha.ClienteId.ToString(),
                Nome: linha.Nome,
                Ativo: linha.Ativo,
                Maquinas: maquinasDto
            ));
        }

        return resultado;
    }

    private static string ResolverStatus(
        SmartLine.Core.Entities.Tenant.Sessao? sessaoAtiva)
    {
        if (sessaoAtiva is null) return "SemSessao";

        var paradaAtiva = sessaoAtiva.Paradas.FirstOrDefault(p => !p.Fim.HasValue);
        if (paradaAtiva is null) return "Rodando";

        if (paradaAtiva.Motivo is null) return "ParadaInterna";

        return paradaAtiva.Motivo.Tipo switch
        {
            TipoParada.Interna   => "ParadaInterna",
            TipoParada.Externa   => "ParadaExterna",
            TipoParada.Planejada => "ParadaPlanejada",
            _ => "Rodando"
        };
    }
}