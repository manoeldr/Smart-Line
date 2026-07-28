using Microsoft.EntityFrameworkCore;
using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Interfaces;
using SmartLine.Infrastructure.Data;

namespace SmartLine.Infrastructure.Repositories;

public class ParadaRegistroService : IParadaRegistroService
{
    private readonly SmartLineDbContext _context;

    public ParadaRegistroService(SmartLineDbContext context)
    {
        _context = context;
    }

    public async Task<ParadaDto> AbrirAsync(Guid sessaoId, DateTime inicio)
    {
        var parada = new Parada
        {
            Id = Guid.NewGuid(),
            SessaoId = sessaoId,
            MotivoId = null,
            Inicio = inicio,
            Fim = null,
        };
        _context.Paradas.Add(parada);
        await _context.SaveChangesAsync();
        return ToDto(parada);
    }

    public async Task<ParadaDto?> FecharAsync(Guid paradaId, Guid motivoId, DateTime fim)
    {
        var parada = await _context.Paradas.FindAsync(paradaId);
        if (parada is null) return null;
        parada.MotivoId = motivoId;
        parada.Fim = fim;
        await _context.SaveChangesAsync();
        return ToDto(parada);
    }

    // Salva a foto da parada em ImagesStopReason/{Cliente}_{Estado}_{Linha}/{Cliente}_{Estado}_{Linha}_{Maquina}_{DataHora}.{ext}
    // e atualiza o campo FotoPath da parada com o caminho relativo.
    public async Task<ParadaDto?> SalvarFotoAsync(Guid paradaId, Stream conteudoFoto, string extensaoArquivo)
    {
        var parada = await _context.Paradas
            .Include(p => p.Sessao)
                .ThenInclude(s => s.MaquinaLinha)
                    .ThenInclude(ml => ml.Linha)
                        .ThenInclude(l => l.Cliente)
            .Include(p => p.Sessao)
                .ThenInclude(s => s.MaquinaLinha)
                    .ThenInclude(ml => ml.Maquina)
            .FirstOrDefaultAsync(p => p.Id == paradaId);

        if (parada is null) return null;

        var cliente = parada.Sessao.MaquinaLinha.Linha.Cliente;
        var linha = parada.Sessao.MaquinaLinha.Linha;
        var maquina = parada.Sessao.MaquinaLinha.Maquina;

        var nomePasta = Sanitizar($"{cliente.Nome}_{cliente.Estado}_{linha.Nome}");
        var nomeArquivo = Sanitizar($"{cliente.Nome}_{cliente.Estado}_{linha.Nome}_{maquina.Nome}_{DateTime.UtcNow:yyyyMMdd-HHmmss}") + extensaoArquivo;

        var raizFotos = Path.Combine(AppContext.BaseDirectory, "ImagesStopReason");
        var pastaCompleta = Path.Combine(raizFotos, nomePasta);
        Directory.CreateDirectory(pastaCompleta);

        var caminhoCompleto = Path.Combine(pastaCompleta, nomeArquivo);
        await using (var arquivoDestino = File.Create(caminhoCompleto))
        {
            await conteudoFoto.CopyToAsync(arquivoDestino);
        }

        parada.FotoPath = $"{nomePasta}/{nomeArquivo}";
        await _context.SaveChangesAsync();

        return ToDto(parada);
    }

    private static string Sanitizar(string texto)
    {
        var invalidos = Path.GetInvalidFileNameChars();
        var limpo = new string(texto.Where(c => !invalidos.Contains(c)).ToArray());
        return limpo.Replace(" ", "-");
    }

    private static ParadaDto ToDto(Parada p) => new(
        Id: p.Id.ToString(),
        SessaoId: p.SessaoId.ToString(),
        MotivoId: p.MotivoId == Guid.Empty ? null : p.MotivoId.ToString(),
        Inicio: p.Inicio,
        Fim: p.Fim,
        FotoPath: p.FotoPath
    );
}