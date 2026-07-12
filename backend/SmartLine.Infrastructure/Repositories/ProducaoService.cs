using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Interfaces;
using SmartLine.Infrastructure.Data;

namespace SmartLine.Infrastructure.Repositories;

public class ProducaoService : IProducaoService
{
    private readonly SmartLineDbContext _context;

    public ProducaoService(SmartLineDbContext context)
    {
        _context = context;
    }

    public async Task<ProducaoDto> RegistrarAsync(Guid sessaoId, int quantidade, int refugo, DateTime hora)
    {
        var producao = new Producao
        {
            Id = Guid.NewGuid(),
            SessaoId = sessaoId,
            Quantidade = quantidade,
            Refugo = refugo,
            Hora = hora,
        };

        _context.Producoes.Add(producao);
        await _context.SaveChangesAsync();

        return new ProducaoDto(
            producao.Id.ToString(),
            producao.SessaoId.ToString(),
            producao.Quantidade,
            producao.Refugo,
            producao.Hora
        );
    }
}
