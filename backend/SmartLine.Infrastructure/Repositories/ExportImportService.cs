using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartLine.Core.Interfaces;
using SmartLine.Infrastructure.Data;

namespace SmartLine.Infrastructure.Repositories;

public class ExportImportService : IExportImportService
{
    private readonly SmartLineDbContext _context;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public ExportImportService(SmartLineDbContext context)
    {
        _context = context;
    }

    // ── EXPORTAÇÃO ──────────────────────────────────────────────
    public async Task<byte[]> ExportarAsync(ExportOpcoes opcoes)
    {
        var pacote = new ExportPacoteDto(
            GeradoEm: DateTime.UtcNow,
            Clientes: opcoes.ClientesLinhas ? await MontarClientesAsync() : new List<ClienteExportDto>(),
            Maquinas: opcoes.Maquinas ? await MontarMaquinasAsync() : new List<MaquinaExportDto>(),
            Sessoes: opcoes.SessoesMedicoes ? await MontarSessoesAsync() : new List<SessaoExportDto>(),
            Usuarios: opcoes.Usuarios ? await MontarUsuariosAsync() : new List<UsuarioExportDto>()
        );

        using var memoryStream = new MemoryStream();
        using (var zip = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            // dados.json
            var jsonEntry = zip.CreateEntry("dados.json");
            await using (var entryStream = jsonEntry.Open())
            {
                var json = JsonSerializer.Serialize(pacote, JsonOptions);
                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                await entryStream.WriteAsync(bytes);
            }

            // fotos/ — copia arquivos físicos referenciados pelas paradas exportadas, se existirem
            if (opcoes.SessoesMedicoes)
            {
                var fotosPaths = pacote.Sessoes
                    .SelectMany(s => s.Paradas)
                    .Where(p => !string.IsNullOrEmpty(p.FotoPath))
                    .Select(p => p.FotoPath!)
                    .Distinct();

                var raizFotos = Path.Combine(AppContext.BaseDirectory, "ImagesStopReason");

                foreach (var fotoPath in fotosPaths)
                {
                    var caminhoCompleto = Path.Combine(raizFotos, fotoPath);
                    if (File.Exists(caminhoCompleto))
                    {
                        zip.CreateEntryFromFile(caminhoCompleto, $"fotos/{fotoPath}");
                    }
                }
            }
        }

        return memoryStream.ToArray();
    }

    private async Task<List<ClienteExportDto>> MontarClientesAsync()
    {
        var clientes = await _context.Clientes
            .Include(c => c.Linhas)
                .ThenInclude(l => l.Maquinas)
            .ToListAsync();

        return clientes.Select(c => new ClienteExportDto(
            c.Id, c.Nome, c.Estado, c.Ativo,
            c.Linhas.Select(l => new LinhaExportDto(
                l.Id, l.Nome, l.Ativo,
                l.Maquinas.Select(ml => new MaquinaLinhaExportDto(
                    ml.Id, ml.MaquinaId, ml.TipoColeta.ToString(),
                    ml.VelocidadeNominal, ml.SobreVelocidade, ml.Critica, ml.Ordem, ml.Ativo
                )).ToList()
            )).ToList()
        )).ToList();
    }

    private async Task<List<MaquinaExportDto>> MontarMaquinasAsync()
    {
        var maquinas = await _context.Maquinas
            .Include(m => m.CamposMaquina)
            .Include(m => m.MotivosParada)
            .ToListAsync();

        return maquinas.Select(m => new MaquinaExportDto(
            m.Id, m.Nome, m.Descricao, m.Ativo,
            m.CamposMaquina.Select(c => new CampoMaquinaExportDto(c.Id, c.Nome, c.Unidade, c.Ordem, c.Ativo)).ToList(),
            m.MotivosParada.Select(mp => new MotivoParadaExportDto(mp.Id, mp.Nome, mp.Tipo.ToString(), mp.Ativo)).ToList()
        )).ToList();
    }

    private async Task<List<SessaoExportDto>> MontarSessoesAsync()
    {
        var sessoes = await _context.Sessoes
            .Include(s => s.Producoes)
            .Include(s => s.Paradas)
            .Include(s => s.LeiturasExtra)
            .Include(s => s.SessoesCampo)
            .ToListAsync();

        return sessoes.Select(s => new SessaoExportDto(
            s.Id, s.MaquinaLinhaId, s.UsuarioId, s.Inicio, s.Fim, s.PrevisaoTermino,
            s.Status.ToString(), s.TipoColeta.ToString(), s.VelocidadeNominal, s.SobreVelocidade,
            s.Producoes.Select(p => new ProducaoExportDto(p.Id, p.Quantidade, p.Refugo, p.Hora)).ToList(),
            s.Paradas.Select(p => new ParadaExportDto(p.Id, p.MotivoId, p.Inicio, p.Fim, p.FotoPath)).ToList(),
            s.LeiturasExtra.Select(le => new LeituraExtraExportDto(le.Id, le.CampoMaquinaId, le.Valor, le.Hora)).ToList(),
            s.SessoesCampo.Select(sc => sc.CampoMaquinaId).ToList()
        )).ToList();
    }

    private async Task<List<UsuarioExportDto>> MontarUsuariosAsync()
    {
        var usuarios = await _context.Usuarios.ToListAsync();
        return usuarios.Select(u => new UsuarioExportDto(
            u.Id, u.ClienteId, u.Nome, u.Login, u.SenhaHash, u.Nivel.ToString(), u.Ativo
        )).ToList();
    }

    // ── PRÉVIA (mostra contagem antes de confirmar importação) ────
    public async Task<ImportResumoDto> PreviaImportacaoAsync(byte[] arquivoZip)
    {
        var pacote = await LerPacoteAsync(arquivoZip);
        return ContarPacote(pacote);
    }

    // ── IMPORTAÇÃO ──────────────────────────────────────────────
    public async Task<ImportResumoDto> ImportarAsync(byte[] arquivoZip)
    {
        var pacote = await LerPacoteAsync(arquivoZip);

        // 1) Clientes + Linhas + MaquinaLinha (upsert por Id)
        foreach (var clienteDto in pacote.Clientes)
        {
            var cliente = await _context.Clientes.FindAsync(clienteDto.Id);
            if (cliente is null)
            {
                cliente = new Core.Entities.Tenant.Cliente { Id = clienteDto.Id };
                _context.Clientes.Add(cliente);
            }
            cliente.Nome = clienteDto.Nome;
            cliente.Estado = clienteDto.Estado;
            cliente.Ativo = clienteDto.Ativo;

            foreach (var linhaDto in clienteDto.Linhas)
            {
                var linha = await _context.Linhas.FindAsync(linhaDto.Id);
                if (linha is null)
                {
                    linha = new Core.Entities.Tenant.Linha { Id = linhaDto.Id, ClienteId = clienteDto.Id };
                    _context.Linhas.Add(linha);
                }
                linha.Nome = linhaDto.Nome;
                linha.Ativo = linhaDto.Ativo;
                linha.ClienteId = clienteDto.Id;

                foreach (var mlDto in linhaDto.Maquinas)
                {
                    var ml = await _context.MaquinasLinha.FindAsync(mlDto.Id);
                    var tipoColeta = Enum.Parse<Core.Enums.TipoColeta>(mlDto.TipoColeta);
                    if (ml is null)
                    {
                        ml = new Core.Entities.Tenant.MaquinaLinha { Id = mlDto.Id, LinhaId = linhaDto.Id };
                        _context.MaquinasLinha.Add(ml);
                    }
                    ml.MaquinaId = mlDto.MaquinaId;
                    ml.LinhaId = linhaDto.Id;
                    ml.TipoColeta = tipoColeta;
                    ml.VelocidadeNominal = mlDto.VelocidadeNominal;
                    ml.SobreVelocidade = mlDto.SobreVelocidade;
                    ml.Critica = mlDto.Critica;
                    ml.Ordem = mlDto.Ordem;
                    ml.Ativo = mlDto.Ativo;
                }
            }
        }

        // 2) Máquinas + Campos + Motivos (upsert por Id)
        foreach (var maquinaDto in pacote.Maquinas)
        {
            var maquina = await _context.Maquinas.FindAsync(maquinaDto.Id);
            if (maquina is null)
            {
                maquina = new Core.Entities.Global.Maquina { Id = maquinaDto.Id };
                _context.Maquinas.Add(maquina);
            }
            maquina.Nome = maquinaDto.Nome;
            maquina.Descricao = maquinaDto.Descricao;
            maquina.Ativo = maquinaDto.Ativo;

            foreach (var campoDto in maquinaDto.Campos)
            {
                var campo = await _context.CamposMaquina.FindAsync(campoDto.Id);
                if (campo is null)
                {
                    campo = new Core.Entities.Global.CampoMaquina { Id = campoDto.Id, MaquinaId = maquinaDto.Id };
                    _context.CamposMaquina.Add(campo);
                }
                campo.Nome = campoDto.Nome;
                campo.Unidade = campoDto.Unidade;
                campo.Ordem = campoDto.Ordem;
                campo.Ativo = campoDto.Ativo;
            }

            foreach (var motivoDto in maquinaDto.Motivos)
            {
                var motivo = await _context.MotivosParada.FindAsync(motivoDto.Id);
                var tipoParada = Enum.Parse<Core.Enums.TipoParada>(motivoDto.Tipo);
                if (motivo is null)
                {
                    motivo = new Core.Entities.Global.MotivoParada { Id = motivoDto.Id, MaquinaId = maquinaDto.Id };
                    _context.MotivosParada.Add(motivo);
                }
                motivo.Nome = motivoDto.Nome;
                motivo.Tipo = tipoParada;
                motivo.Ativo = motivoDto.Ativo;
            }
        }

        // 3) Usuários (upsert por Id)
        foreach (var usuarioDto in pacote.Usuarios)
        {
            var usuario = await _context.Usuarios.FindAsync(usuarioDto.Id);
            var nivel = Enum.Parse<Core.Enums.NivelUsuario>(usuarioDto.Nivel);
            if (usuario is null)
            {
                usuario = new Core.Entities.Tenant.Usuario { Id = usuarioDto.Id };
                _context.Usuarios.Add(usuario);
            }
            usuario.ClienteId = usuarioDto.ClienteId;
            usuario.Nome = usuarioDto.Nome;
            usuario.Login = usuarioDto.Login;
            usuario.SenhaHash = usuarioDto.SenhaHash;
            usuario.Nivel = nivel;
            usuario.Ativo = usuarioDto.Ativo;
        }

        // Salva config antes das sessões, para garantir que MaquinaLinha/CampoMaquina/Usuario já existam
        await _context.SaveChangesAsync();

        // 4) Sessões + Produções + Paradas + LeiturasExtra + SessaoCampo (upsert por Id)
        foreach (var sessaoDto in pacote.Sessoes)
        {
            var sessao = await _context.Sessoes.FindAsync(sessaoDto.Id);
            var status = Enum.Parse<Core.Enums.StatusSessao>(sessaoDto.Status);
            var tipoColeta = Enum.Parse<Core.Enums.TipoColeta>(sessaoDto.TipoColeta);
            if (sessao is null)
            {
                sessao = new Core.Entities.Tenant.Sessao { Id = sessaoDto.Id };
                _context.Sessoes.Add(sessao);
            }
            sessao.MaquinaLinhaId = sessaoDto.MaquinaLinhaId;
            sessao.UsuarioId = sessaoDto.UsuarioId;
            sessao.Inicio = sessaoDto.Inicio;
            sessao.Fim = sessaoDto.Fim;
            sessao.PrevisaoTermino = sessaoDto.PrevisaoTermino;
            sessao.Status = status;
            sessao.TipoColeta = tipoColeta;
            sessao.VelocidadeNominal = sessaoDto.VelocidadeNominal;
            sessao.SobreVelocidade = sessaoDto.SobreVelocidade;

            foreach (var pDto in sessaoDto.Producoes)
            {
                var producao = await _context.Producoes.FindAsync(pDto.Id);
                if (producao is null)
                {
                    producao = new Core.Entities.Tenant.Producao { Id = pDto.Id, SessaoId = sessaoDto.Id };
                    _context.Producoes.Add(producao);
                }
                producao.Quantidade = pDto.Quantidade;
                producao.Refugo = pDto.Refugo;
                producao.Hora = pDto.Hora;
            }

            foreach (var pdDto in sessaoDto.Paradas)
            {
                var parada = await _context.Paradas.FindAsync(pdDto.Id);
                if (parada is null)
                {
                    parada = new Core.Entities.Tenant.Parada { Id = pdDto.Id, SessaoId = sessaoDto.Id };
                    _context.Paradas.Add(parada);
                }
                parada.MotivoId = pdDto.MotivoId;
                parada.Inicio = pdDto.Inicio;
                parada.Fim = pdDto.Fim;
                parada.FotoPath = pdDto.FotoPath;
            }

            foreach (var leDto in sessaoDto.LeiturasExtra)
            {
                var leitura = await _context.LeiturasExtra.FindAsync(leDto.Id);
                if (leitura is null)
                {
                    leitura = new Core.Entities.Tenant.LeituraExtra { Id = leDto.Id, SessaoId = sessaoDto.Id };
                    _context.LeiturasExtra.Add(leitura);
                }
                leitura.CampoMaquinaId = leDto.CampoMaquinaId;
                leitura.Valor = leDto.Valor;
                leitura.Hora = leDto.Hora;
            }

            foreach (var campoId in sessaoDto.CamposSelecionados)
            {
                var jaExiste = await _context.SessoesCampo
                    .AnyAsync(sc => sc.SessaoId == sessaoDto.Id && sc.CampoMaquinaId == campoId);
                if (!jaExiste)
                {
                    _context.SessoesCampo.Add(new Core.Entities.Tenant.SessaoCampo
                    {
                        Id = Guid.NewGuid(),
                        SessaoId = sessaoDto.Id,
                        CampoMaquinaId = campoId,
                    });
                }
            }
        }

        await _context.SaveChangesAsync();

        // 5) Extrai as fotos do zip para a pasta local (se houver)
        await ExtrairFotosAsync(arquivoZip);

        return ContarPacote(pacote);
    }

    private static async Task<ExportPacoteDto> LerPacoteAsync(byte[] arquivoZip)
    {
        using var memoryStream = new MemoryStream(arquivoZip);
        using var zip = new ZipArchive(memoryStream, ZipArchiveMode.Read);
        var entry = zip.GetEntry("dados.json") ?? throw new Exception("Arquivo dados.json não encontrado no pacote.");
        await using var stream = entry.Open();
        var pacote = await JsonSerializer.DeserializeAsync<ExportPacoteDto>(stream, JsonOptions);
        return pacote ?? throw new Exception("Não foi possível ler o pacote de importação.");
    }

    private static async Task ExtrairFotosAsync(byte[] arquivoZip)
    {
        using var memoryStream = new MemoryStream(arquivoZip);
        using var zip = new ZipArchive(memoryStream, ZipArchiveMode.Read);
        var raizFotos = Path.Combine(AppContext.BaseDirectory, "ImagesStopReason");

        foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith("fotos/") && !string.IsNullOrEmpty(e.Name)))
        {
            var caminhoRelativo = entry.FullName.Substring("fotos/".Length);
            var destino = Path.Combine(raizFotos, caminhoRelativo);
            Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
            await using var origemStream = entry.Open();
            await using var destinoStream = File.Create(destino);
            await origemStream.CopyToAsync(destinoStream);
        }
    }

    private static ImportResumoDto ContarPacote(ExportPacoteDto pacote) => new(
        Clientes: pacote.Clientes.Count,
        Linhas: pacote.Clientes.Sum(c => c.Linhas.Count),
        Maquinas: pacote.Maquinas.Count,
        Sessoes: pacote.Sessoes.Count,
        Usuarios: pacote.Usuarios.Count
    );
}