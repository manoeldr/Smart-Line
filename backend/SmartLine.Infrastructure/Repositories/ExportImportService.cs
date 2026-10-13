using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartLine.Core.Entities.Global;
using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Interfaces;
using SmartLine.Core.Iot;
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
    public async Task<byte[]> ExportarAsync(ExportOpcoes opcoes, bool chamadorDesenvolvedor = false)
    {
        var pacote = new ExportPacoteDto(
            GeradoEm: DateTime.UtcNow,
            Clientes: opcoes.ClientesLinhas ? await MontarClientesAsync() : new List<ClienteExportDto>(),
            Maquinas: opcoes.Maquinas ? await MontarMaquinasAsync() : new List<MaquinaExportDto>(),
            Sessoes: opcoes.SessoesMedicoes ? await MontarSessoesAsync() : new List<SessaoExportDto>(),
            Usuarios: opcoes.Usuarios ? await MontarUsuariosAsync(chamadorDesenvolvedor) : new List<UsuarioExportDto>(),
            PeriodosSemComunicacao: opcoes.SessoesMedicoes ? await MontarPeriodosSemComunicacaoAsync() : new List<PeriodoSemComunicacaoExportDto>(),
            Wises: opcoes.ClientesLinhas ? await MontarWisesAsync() : new List<WiseExportDto>()
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

            // modelo-relatorio/ — a folha timbrada do relatório (imagem + margens), se houver.
            // Vai junto com clientes/linhas: é configuração da instalação, não dado de medição.
            if (opcoes.ClientesLinhas && Directory.Exists(ModeloRelatorioService.Pasta))
            {
                foreach (var caminho in Directory.GetFiles(ModeloRelatorioService.Pasta))
                    zip.CreateEntryFromFile(caminho, $"modelo-relatorio/{Path.GetFileName(caminho)}");
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

        // Regras personalizadas por máquina da linha (as do catálogo vão com as máquinas).
        var personalizadas = (await ConjuntosComRegras().Where(c => c.MaquinaLinhaId != null).ToListAsync())
            .ToDictionary(c => c.MaquinaLinhaId!.Value, c => MontarRegras(c));

        return clientes.Select(c => new ClienteExportDto(
            c.Id, c.Nome, c.Estado, c.Ativo,
            c.Linhas.Select(l => new LinhaExportDto(
                l.Id, l.Nome, l.Ativo,
                l.Maquinas.Select(ml => new MaquinaLinhaExportDto(
                    ml.Id, ml.MaquinaId, ml.TipoColeta.ToString(),
                    ml.VelocidadeNominal, ml.SobreVelocidade, ml.Critica, ml.Ordem, ml.Ativo,
                    ml.MedeProducao, ml.TempoDeteccaoParadaSegundos,
                    // Lista vazia = sem personalização (a importação apaga a que houver no destino).
                    personalizadas.GetValueOrDefault(ml.Id) ?? []
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

        var textos = (await _context.TextosEntradasWise.ToListAsync())
            .GroupBy(t => t.MaquinaId)
            .ToDictionary(g => g.Key, g => g.Select(t => new TextoEntradaExportDto(t.Id, t.Canal.ToString(), t.Nome, t.TextoAtivo, t.TextoNormal)).ToList());

        var regras = (await ConjuntosComRegras().Where(c => c.MaquinaId != null).ToListAsync())
            .ToDictionary(c => c.MaquinaId!.Value, c => MontarRegras(c));

        return maquinas.Select(m => new MaquinaExportDto(
            m.Id, m.Nome, m.Descricao, m.Ativo,
            m.CamposMaquina.Select(c => new CampoMaquinaExportDto(c.Id, c.Nome, c.Unidade, c.Ordem, c.Ativo)).ToList(),
            m.MotivosParada.Select(mp => new MotivoParadaExportDto(mp.Id, mp.Nome, mp.Tipo.ToString(), mp.Ativo)).ToList(),
            textos.GetValueOrDefault(m.Id),
            regras.GetValueOrDefault(m.Id)
        )).ToList();
    }

    private IQueryable<ConjuntoRegras> ConjuntosComRegras() =>
        _context.ConjuntosRegras.AsNoTracking()
            .Include(c => c.Regras).ThenInclude(r => r.Condicoes);

    private static List<RegraExportDto> MontarRegras(ConjuntoRegras conjunto) =>
        conjunto.Regras.OrderBy(r => r.Prioridade).Select(r => new RegraExportDto(
            r.Id, r.Prioridade, r.Nome, r.MotivoParadaId, r.Ativa,
            r.Condicoes.Select(c => new CondicaoRegraExportDto(c.Id, c.Tipo.ToString(), c.Canal?.ToString(), c.TempoMinimoSegundos)).ToList()
        )).ToList();

    private async Task<List<SessaoExportDto>> MontarSessoesAsync()
    {
        var sessoes = await _context.Sessoes
            .Include(s => s.Producoes)
            .Include(s => s.Paradas).ThenInclude(p => p.HistoricoClassificacao)
            .Include(s => s.LeiturasExtra)
            .Include(s => s.SessoesCampo)
            .ToListAsync();

        return sessoes.Select(s => new SessaoExportDto(
            s.Id, s.MaquinaLinhaId, s.UsuarioId, s.Inicio, s.Fim, s.PrevisaoTermino,
            s.Status.ToString(), s.TipoColeta.ToString(), s.VelocidadeNominal, s.SobreVelocidade,
            s.Producoes.Select(p => new ProducaoExportDto(p.Id, p.Quantidade, p.Refugo, p.Hora, p.SemComunicacao)).ToList(),
            s.Paradas.Select(p => new ParadaExportDto(p.Id, p.MotivoId, p.Inicio, p.Fim, p.FotoPath, p.RegraClassificacaoId,
                p.HistoricoClassificacao.Select(h => new HistoricoClassificacaoExportDto(h.Id, h.MotivoAnteriorId, h.MotivoNovoId, h.UsuarioId, h.AlteradoEm)).ToList())).ToList(),
            s.LeiturasExtra.Select(le => new LeituraExtraExportDto(le.Id, le.CampoMaquinaId, le.Valor, le.Hora)).ToList(),
            s.SessoesCampo.Select(sc => sc.CampoMaquinaId).ToList()
        )).ToList();
    }

    private async Task<List<PeriodoSemComunicacaoExportDto>> MontarPeriodosSemComunicacaoAsync()
    {
        var periodos = await _context.PeriodosSemComunicacao.AsNoTracking().ToListAsync();
        return periodos.Select(p => new PeriodoSemComunicacaoExportDto(p.Id, p.MaquinaLinhaId, p.Inicio, p.Fim, p.ProducaoNaoRecuperada)).ToList();
    }

    private async Task<List<WiseExportDto>> MontarWisesAsync()
    {
        var wises = await _context.Wises.AsNoTracking().OrderBy(w => w.CriadoEm).ToListAsync();
        return wises.Select(w => new WiseExportDto(w.Id, w.EnderecoIp, w.Nome, w.CriadoEm)).ToList();
    }

    private async Task<List<UsuarioExportDto>> MontarUsuariosAsync(bool chamadorDesenvolvedor)
    {
        // O perfil Desenvolvedor é reservado: só sai no pacote de um Desenvolvedor.
        var usuarios = await _context.Usuarios
            .Where(u => chamadorDesenvolvedor || u.Nivel != Core.Enums.NivelUsuario.Desenvolvedor)
            .ToListAsync();
        return usuarios.Select(u => new UsuarioExportDto(
            u.Id, u.ClienteId, u.Nome, u.Login, u.SenhaHash, u.Nivel.ToString(), u.Ativo, u.Email
        )).ToList();
    }

    // ── PRÉVIA (mostra contagem antes de confirmar importação) ────
    public async Task<ImportResumoDto> PreviaImportacaoAsync(byte[] arquivoZip)
    {
        var pacote = await LerPacoteAsync(arquivoZip);
        return ContarPacote(pacote);
    }

    // ── IMPORTAÇÃO ──────────────────────────────────────────────
    public async Task<ImportResumoDto> ImportarAsync(byte[] arquivoZip, bool chamadorDesenvolvedor = false)
    {
        var pacote = await LerPacoteAsync(arquivoZip);

        // 1) Clientes + Linhas + MaquinaLinha (upsert por Id)
        foreach (var clienteDto in pacote.Clientes)
        {
            var cliente = await _context.Clientes.FindAsync(clienteDto.Id);
            if (cliente is null)
            {
                cliente = new Cliente { Id = clienteDto.Id };
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
                    linha = new Linha { Id = linhaDto.Id, ClienteId = clienteDto.Id };
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
                        ml = new MaquinaLinha { Id = mlDto.Id, LinhaId = linhaDto.Id };
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
                    ml.MedeProducao = mlDto.MedeProducao;
                    ml.TempoDeteccaoParadaSegundos = mlDto.TempoDeteccaoParadaSegundos;
                }
            }
        }

        // 2) Máquinas + Campos + Motivos (upsert por Id) + textos das entradas
        foreach (var maquinaDto in pacote.Maquinas)
        {
            var maquina = await _context.Maquinas.FindAsync(maquinaDto.Id);
            if (maquina is null)
            {
                maquina = new Maquina { Id = maquinaDto.Id };
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
                    campo = new CampoMaquina { Id = campoDto.Id, MaquinaId = maquinaDto.Id };
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
                    motivo = new MotivoParada { Id = motivoDto.Id, MaquinaId = maquinaDto.Id };
                    _context.MotivosParada.Add(motivo);
                }
                motivo.Nome = motivoDto.Nome;
                motivo.Tipo = tipoParada;
                motivo.Ativo = motivoDto.Ativo;
            }

            // Textos das entradas: no máximo um por canal em cada máquina, então o par
            // (máquina, canal) é a chave — o Id pode ser outro no destino.
            if (maquinaDto.TextosEntradas is { Count: > 0 } textos)
            {
                var existentes = await _context.TextosEntradasWise.Where(t => t.MaquinaId == maquinaDto.Id).ToListAsync();
                foreach (var textoDto in textos)
                {
                    var canal = Enum.Parse<CanalWise>(textoDto.Canal);
                    var texto = existentes.FirstOrDefault(t => t.Canal == canal);
                    if (texto is null)
                    {
                        var idEmUso = await _context.TextosEntradasWise.AnyAsync(t => t.Id == textoDto.Id);
                        texto = new TextoEntradaWise { Id = idEmUso ? Guid.NewGuid() : textoDto.Id, MaquinaId = maquinaDto.Id, Canal = canal };
                        _context.TextosEntradasWise.Add(texto);
                    }
                    texto.Nome = textoDto.Nome;
                    texto.TextoAtivo = textoDto.TextoAtivo;
                    texto.TextoNormal = textoDto.TextoNormal;
                }
            }
        }

        // 2b) Cadastro de WISE: o IP é único, então casa pelo IP (o Id pode ser outro no destino).
        if (pacote.Wises is { Count: > 0 } wises)
        {
            var existentes = await _context.Wises.ToListAsync();
            foreach (var wiseDto in wises)
            {
                var wise = existentes.FirstOrDefault(w => w.EnderecoIp == wiseDto.EnderecoIp)
                    ?? existentes.FirstOrDefault(w => w.Id == wiseDto.Id);
                if (wise is null)
                {
                    wise = new Wise { Id = wiseDto.Id, CriadoEm = wiseDto.CriadoEm };
                    _context.Wises.Add(wise);
                    existentes.Add(wise);
                }
                wise.EnderecoIp = wiseDto.EnderecoIp;
                wise.Nome = wiseDto.Nome;
            }
        }

        // 3) Usuários (upsert por Id). Quem não é Desenvolvedor não traz um Desenvolvedor no pacote
        // nem muda um que já exista aqui (seria o jeito de trocar a senha dele por fora).
        foreach (var usuarioDto in pacote.Usuarios)
        {
            var usuario = await _context.Usuarios.FindAsync(usuarioDto.Id);
            var nivel = Enum.Parse<Core.Enums.NivelUsuario>(usuarioDto.Nivel);
            if (!chamadorDesenvolvedor && (nivel == Core.Enums.NivelUsuario.Desenvolvedor || usuario?.Nivel == Core.Enums.NivelUsuario.Desenvolvedor))
                continue;
            if (usuario is null)
            {
                usuario = new Usuario { Id = usuarioDto.Id };
                _context.Usuarios.Add(usuario);
            }
            usuario.ClienteId = usuarioDto.ClienteId;
            usuario.Nome = usuarioDto.Nome;
            usuario.Login = usuarioDto.Login;
            usuario.SenhaHash = usuarioDto.SenhaHash;
            usuario.Nivel = nivel;
            usuario.Ativo = usuarioDto.Ativo;
            usuario.Email = usuarioDto.Email;
        }

        // Salva config antes das sessões, para garantir que MaquinaLinha/CampoMaquina/Usuario já existam
        await _context.SaveChangesAsync();

        // 4) Regras de classificação: as do catálogo (por máquina) e as personalizadas (por máquina
        // da linha). Precisam dos motivos já gravados. Um conjunto só por dono: se o destino já tem
        // um, as regras entram nele; as que não vieram no pacote saem (o pacote espelha a origem).
        foreach (var maquinaDto in pacote.Maquinas)
            if (maquinaDto.Regras is { } regras)
                await ImportarRegrasAsync(regras, c => c.MaquinaId == maquinaDto.Id, maquinaId: maquinaDto.Id, maquinaLinhaId: null);

        foreach (var mlDto in pacote.Clientes.SelectMany(c => c.Linhas).SelectMany(l => l.Maquinas))
            if (mlDto.Regras is { } regras)
                await ImportarRegrasAsync(regras, c => c.MaquinaLinhaId == mlDto.Id, maquinaId: null, maquinaLinhaId: mlDto.Id);

        await _context.SaveChangesAsync();

        // 5) Sessões + Produções + Paradas (com histórico) + LeiturasExtra + SessaoCampo (upsert por Id)
        var regrasExistentes = await _context.RegrasClassificacao.Select(r => r.Id).ToHashSetAsync();
        var motivosExistentes = await _context.MotivosParada.Select(m => m.Id).ToHashSetAsync();
        var usuariosExistentes = await _context.Usuarios.Select(u => u.Id).ToHashSetAsync();

        foreach (var sessaoDto in pacote.Sessoes)
        {
            var sessao = await _context.Sessoes.FindAsync(sessaoDto.Id);
            var status = Enum.Parse<Core.Enums.StatusSessao>(sessaoDto.Status);
            var tipoColeta = Enum.Parse<Core.Enums.TipoColeta>(sessaoDto.TipoColeta);
            if (sessao is null)
            {
                sessao = new Sessao { Id = sessaoDto.Id };
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
                    producao = new Producao { Id = pDto.Id, SessaoId = sessaoDto.Id };
                    _context.Producoes.Add(producao);
                }
                producao.Quantidade = pDto.Quantidade;
                producao.Refugo = pDto.Refugo;
                producao.Hora = pDto.Hora;
                producao.SemComunicacao = pDto.SemComunicacao;
            }

            foreach (var pdDto in sessaoDto.Paradas)
            {
                var parada = await _context.Paradas.FindAsync(pdDto.Id);
                if (parada is null)
                {
                    parada = new Parada { Id = pdDto.Id, SessaoId = sessaoDto.Id };
                    _context.Paradas.Add(parada);
                }
                parada.MotivoId = pdDto.MotivoId;
                parada.Inicio = pdDto.Inicio;
                parada.Fim = pdDto.Fim;
                parada.FotoPath = pdDto.FotoPath;
                // A regra só é referenciada se existir aqui (as máquinas podem não ter vindo no pacote).
                parada.RegraClassificacaoId = pdDto.RegraClassificacaoId is { } regraId && regrasExistentes.Contains(regraId) ? regraId : null;

                foreach (var hDto in pdDto.Historico ?? [])
                {
                    var historico = await _context.HistoricosClassificacaoParada.FindAsync(hDto.Id);
                    if (historico is null)
                    {
                        historico = new HistoricoClassificacaoParada { Id = hDto.Id, ParadaId = pdDto.Id };
                        _context.HistoricosClassificacaoParada.Add(historico);
                    }
                    historico.MotivoAnteriorId = hDto.MotivoAnteriorId is { } ma && motivosExistentes.Contains(ma) ? ma : null;
                    historico.MotivoNovoId = hDto.MotivoNovoId is { } mn && motivosExistentes.Contains(mn) ? mn : null;
                    historico.UsuarioId = hDto.UsuarioId is { } u && usuariosExistentes.Contains(u) ? u : null;
                    historico.AlteradoEm = hDto.AlteradoEm;
                }
            }

            foreach (var leDto in sessaoDto.LeiturasExtra)
            {
                var leitura = await _context.LeiturasExtra.FindAsync(leDto.Id);
                if (leitura is null)
                {
                    leitura = new LeituraExtra { Id = leDto.Id, SessaoId = sessaoDto.Id };
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
                    _context.SessoesCampo.Add(new SessaoCampo
                    {
                        Id = Guid.NewGuid(),
                        SessaoId = sessaoDto.Id,
                        CampoMaquinaId = campoId,
                    });
                }
            }
        }

        // 6) Trechos sem comunicação (só de máquinas da linha que existem aqui)
        var maquinasLinhaExistentes = await _context.MaquinasLinha.Select(m => m.Id).ToHashSetAsync();
        foreach (var periodoDto in pacote.PeriodosSemComunicacao ?? [])
        {
            if (!maquinasLinhaExistentes.Contains(periodoDto.MaquinaLinhaId)) continue;
            var periodo = await _context.PeriodosSemComunicacao.FindAsync(periodoDto.Id);
            if (periodo is null)
            {
                periodo = new PeriodoSemComunicacao { Id = periodoDto.Id, MaquinaLinhaId = periodoDto.MaquinaLinhaId };
                _context.PeriodosSemComunicacao.Add(periodo);
            }
            periodo.Inicio = periodoDto.Inicio;
            periodo.Fim = periodoDto.Fim;
            periodo.ProducaoNaoRecuperada = periodoDto.ProducaoNaoRecuperada;
        }

        await _context.SaveChangesAsync();

        // 7) Extrai as fotos do zip para a pasta local (se houver)
        await ExtrairFotosAsync(arquivoZip);

        // 8) Folha timbrada do relatório (se vier no pacote, substitui a daqui)
        await ExtrairModeloRelatorioAsync(arquivoZip);

        return ContarPacote(pacote);
    }

    /// <summary>
    /// Põe as regras do pacote no conjunto do dono (criando o conjunto se não houver): regra com o
    /// mesmo Id é atualizada, nova é criada, e as que não vieram saem. As condições de cada regra
    /// são substituídas pelas do pacote.
    /// </summary>
    private async Task ImportarRegrasAsync(
        IList<RegraExportDto> regras,
        System.Linq.Expressions.Expression<Func<ConjuntoRegras, bool>> doDono,
        Guid? maquinaId, Guid? maquinaLinhaId)
    {
        var conjunto = await _context.ConjuntosRegras
            .Include(c => c.Regras).ThenInclude(r => r.Condicoes)
            .FirstOrDefaultAsync(doDono);
        if (conjunto is null)
        {
            // Máquina da linha sem personalização: nada a criar.
            if (maquinaLinhaId is not null && regras.Count == 0) return;
            conjunto = new ConjuntoRegras
            {
                Id = maquinaId is { } m ? RegrasPadraoIot.IdDeterministico(m, "conjunto") : Guid.NewGuid(),
                MaquinaId = maquinaId,
                MaquinaLinhaId = maquinaLinhaId,
                CriadoEm = DateTime.UtcNow
            };
            _context.ConjuntosRegras.Add(conjunto);
        }

        var existentes = conjunto.Regras.ToDictionary(r => r.Id);
        var mantidas = new HashSet<Guid>();
        foreach (var regraDto in regras)
        {
            RegraClassificacao regra;
            if (existentes.TryGetValue(regraDto.Id, out var existente))
            {
                regra = existente;
                mantidas.Add(regra.Id);
                _context.CondicoesRegra.RemoveRange(regra.Condicoes);
                regra.Condicoes.Clear();
            }
            else
            {
                // O mesmo Id em outro conjunto deste banco (não deveria acontecer): vira regra nova.
                var emOutroConjunto = await _context.RegrasClassificacao.AnyAsync(r => r.Id == regraDto.Id);
                regra = new RegraClassificacao { Id = emOutroConjunto ? Guid.NewGuid() : regraDto.Id, ConjuntoRegrasId = conjunto.Id };
                _context.RegrasClassificacao.Add(regra);
                conjunto.Regras.Add(regra);
            }

            regra.Prioridade = regraDto.Prioridade;
            regra.Nome = regraDto.Nome;
            regra.MotivoParadaId = regraDto.MotivoParadaId;
            regra.Ativa = regraDto.Ativa;

            foreach (var c in regraDto.Condicoes)
            {
                _context.CondicoesRegra.Add(new CondicaoRegra
                {
                    Id = Guid.NewGuid(),
                    RegraClassificacaoId = regra.Id,
                    Tipo = Enum.Parse<TipoCondicao>(c.Tipo),
                    Canal = c.Canal is null ? null : Enum.Parse<CanalWise>(c.Canal),
                    TempoMinimoSegundos = c.TempoMinimoSegundos
                });
            }
        }

        foreach (var removida in existentes.Values.Where(r => !mantidas.Contains(r.Id)))
            _context.RegrasClassificacao.Remove(removida);

        // Personalização da máquina da linha que ficou vazia na origem: some também aqui.
        if (maquinaLinhaId is not null && regras.Count == 0)
            _context.ConjuntosRegras.Remove(conjunto);
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

    private static async Task ExtrairModeloRelatorioAsync(byte[] arquivoZip)
    {
        using var memoryStream = new MemoryStream(arquivoZip);
        using var zip = new ZipArchive(memoryStream, ZipArchiveMode.Read);
        var entradas = zip.Entries.Where(e => e.FullName.StartsWith("modelo-relatorio/") && !string.IsNullOrEmpty(e.Name)).ToList();
        if (entradas.Count == 0) return;

        if (Directory.Exists(ModeloRelatorioService.Pasta)) Directory.Delete(ModeloRelatorioService.Pasta, recursive: true);
        Directory.CreateDirectory(ModeloRelatorioService.Pasta);
        foreach (var entry in entradas)
        {
            // Só o nome do arquivo (sem subpastas): a pasta é plana e evita caminho fora dela
            var destino = Path.Combine(ModeloRelatorioService.Pasta, Path.GetFileName(entry.Name));
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
        Usuarios: pacote.Usuarios.Count,
        Regras: pacote.Maquinas.Sum(m => m.Regras?.Count ?? 0)
            + pacote.Clientes.SelectMany(c => c.Linhas).SelectMany(l => l.Maquinas).Sum(ml => ml.Regras?.Count ?? 0),
        TextosEntradas: pacote.Maquinas.Sum(m => m.TextosEntradas?.Count ?? 0),
        Wises: pacote.Wises?.Count ?? 0
    );
}
