using Microsoft.EntityFrameworkCore;
using SmartLine.Core.Entities.Global;
using SmartLine.Core.Interfaces;
using SmartLine.Core.Iot;
using SmartLine.Infrastructure.Data;

namespace SmartLine.Infrastructure.Repositories;

public class EntradasWiseService : IEntradasWiseService
{
    private readonly SmartLineDbContext _context;

    public EntradasWiseService(SmartLineDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<EntradaWiseDto>?> ObterAsync(Guid maquinaId, CancellationToken cancellationToken = default)
    {
        if (!await _context.Maquinas.AnyAsync(m => m.Id == maquinaId, cancellationToken))
            return null;

        var personalizados = await _context.TextosEntradasWise.AsNoTracking()
            .Where(t => t.MaquinaId == maquinaId)
            .ToDictionaryAsync(t => t.Canal, cancellationToken);

        return MapaWise.Canais
            .Select(d =>
            {
                var padrao = TextosEntradasWise.DoPadrao(d.Canal);
                var texto = personalizados.GetValueOrDefault(d.Canal);
                return new EntradaWiseDto(
                    d.Canal, d.Entrada, d.Tipo, d.Funcao,
                    texto?.Nome ?? padrao.Nome,
                    d.Tipo == TipoCanal.Estado ? texto?.TextoAtivo ?? padrao.TextoAtivo : null,
                    d.Tipo == TipoCanal.Estado ? texto?.TextoNormal ?? padrao.TextoNormal : null,
                    texto is not null);
            })
            .ToList();
    }

    public async Task<ResultadoCadastro<IReadOnlyList<EntradaWiseDto>>> SalvarAsync(
        Guid maquinaId, IList<SalvarEntradaWiseRequest> entradas, CancellationToken cancellationToken = default)
    {
        if (!await _context.Maquinas.AnyAsync(m => m.Id == maquinaId, cancellationToken))
            return ResultadoCadastro<IReadOnlyList<EntradaWiseDto>>.Inexistente();

        entradas ??= [];
        var vistos = new HashSet<CanalWise>();
        var normalizadas = new List<TextoEntrada>();
        foreach (var e in entradas)
        {
            if (!Enum.IsDefined(e.Canal))
                return ResultadoCadastro<IReadOnlyList<EntradaWiseDto>>.Falha($"Entrada inexistente: {e.Canal}.");
            if (!vistos.Add(e.Canal))
                return ResultadoCadastro<IReadOnlyList<EntradaWiseDto>>.Falha($"{e.Canal} enviada mais de uma vez.");

            var ehSensor = MapaWise.Definicao(e.Canal).Tipo == TipoCanal.Estado;
            var nome = e.Nome?.Trim() ?? "";
            var ativo = ehSensor ? e.TextoAtivo?.Trim() ?? "" : null;
            var normal = ehSensor ? e.TextoNormal?.Trim() ?? "" : null;

            if (nome.Length == 0)
                return ResultadoCadastro<IReadOnlyList<EntradaWiseDto>>.Falha($"{e.Canal}: informe o nome.");
            if (ehSensor && (ativo!.Length == 0 || normal!.Length == 0))
                return ResultadoCadastro<IReadOnlyList<EntradaWiseDto>>.Falha($"{e.Canal}: informe o texto de ativo e o de normal.");
            if (new[] { nome, ativo, normal }.Any(t => t is { Length: > TextosEntradasWise.TamanhoMaximo }))
                return ResultadoCadastro<IReadOnlyList<EntradaWiseDto>>.Falha(
                    $"{e.Canal}: cada texto pode ter até {TextosEntradasWise.TamanhoMaximo} caracteres.");

            normalizadas.Add(new TextoEntrada(e.Canal, nome, ativo, normal));
        }

        var existentes = await _context.TextosEntradasWise
            .Where(t => t.MaquinaId == maquinaId)
            .ToDictionaryAsync(t => t.Canal, cancellationToken);

        foreach (var texto in normalizadas)
        {
            var existente = existentes.GetValueOrDefault(texto.Canal);
            if (texto == TextosEntradasWise.DoPadrao(texto.Canal))
            {
                // Igual ao padrão: não guarda, para a entrada acompanhar o padrão se ele mudar.
                if (existente is not null)
                    _context.TextosEntradasWise.Remove(existente);
                continue;
            }

            if (existente is null)
            {
                existente = new TextoEntradaWise { Id = Guid.NewGuid(), MaquinaId = maquinaId, Canal = texto.Canal };
                _context.TextosEntradasWise.Add(existente);
            }
            existente.Nome = texto.Nome;
            existente.TextoAtivo = texto.TextoAtivo;
            existente.TextoNormal = texto.TextoNormal;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return ResultadoCadastro<IReadOnlyList<EntradaWiseDto>>.Ok((await ObterAsync(maquinaId, cancellationToken))!);
    }

    public async Task<IReadOnlyList<EntradaWiseDto>?> RestaurarPadraoAsync(Guid maquinaId, CancellationToken cancellationToken = default)
    {
        if (!await _context.Maquinas.AnyAsync(m => m.Id == maquinaId, cancellationToken))
            return null;

        var existentes = await _context.TextosEntradasWise.Where(t => t.MaquinaId == maquinaId).ToListAsync(cancellationToken);
        _context.TextosEntradasWise.RemoveRange(existentes);
        await _context.SaveChangesAsync(cancellationToken);
        return await ObterAsync(maquinaId, cancellationToken);
    }
}
