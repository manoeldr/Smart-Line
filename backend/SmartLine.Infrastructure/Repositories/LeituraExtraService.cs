using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Interfaces;
using SmartLine.Infrastructure.Data;

namespace SmartLine.Infrastructure.Repositories;

public class LeituraExtraService : ILeituraExtraService
{
    private readonly SmartLineDbContext _context;

    public LeituraExtraService(SmartLineDbContext context)
    {
        _context = context;
    }

    public async Task<LeituraExtraDto> RegistrarAsync(Guid sessaoId, Guid campoMaquinaId, decimal valor, DateTime hora)
    {
        var leitura = new LeituraExtra
        {
            Id = Guid.NewGuid(),
            SessaoId = sessaoId,
            CampoMaquinaId = campoMaquinaId,
            Valor = valor,
            Hora = hora,
        };

        _context.LeiturasExtra.Add(leitura);
        await _context.SaveChangesAsync();

        return new LeituraExtraDto(
            leitura.Id.ToString(),
            leitura.SessaoId.ToString(),
            leitura.CampoMaquinaId.ToString(),
            leitura.Valor,
            leitura.Hora
        );
    }
}
