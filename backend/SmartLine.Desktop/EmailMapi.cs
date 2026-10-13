using System.Runtime.InteropServices;

namespace SmartLine.Desktop;

/// <summary>
/// Abre uma mensagem nova no programa de e-mail do Windows (Outlook ou o cliente MAPI padrão)
/// já com destinatário, assunto, texto e anexo, para a pessoa conferir e clicar em Enviar.
/// Usa o Simple MAPI (MAPISendMail): não precisa de conta configurada no SmartLine — vai pela
/// conta do próprio usuário. Não funciona com webmail no navegador (sem cliente MAPI).
/// </summary>
internal static class EmailMapi
{
    private const int MAPI_LOGON_UI = 0x00000001;
    private const int MAPI_DIALOG = 0x00000008;
    private const int MAPI_TO = 1;

    [DllImport("MAPI32.DLL", CharSet = CharSet.Ansi)]
    private static extern int MAPISendMail(IntPtr session, IntPtr uiParam, MapiMessage message, int flags, int reserved);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private class MapiMessage
    {
        public int Reserved;
        public string? Subject;
        public string? NoteText;
        public string? MessageType;
        public string? DateReceived;
        public string? ConversationId;
        public int Flags;
        public IntPtr Originator;
        public int RecipientCount;
        public IntPtr Recipients;
        public int FileCount;
        public IntPtr Files;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private class MapiRecipDesc
    {
        public int Reserved;
        public int RecipientClass;
        public string? Name;
        public string? Address;
        public int EntryIdSize;
        public IntPtr EntryId;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private class MapiFileDesc
    {
        public int Reserved;
        public int Flags;
        public int Position;
        public string? PathName;
        public string? FileName;
        public IntPtr FileType;
    }

    /// <summary>Roda <see cref="AbrirMensagem"/> numa thread STA própria (exigência do Outlook).</summary>
    public static Task<string?> AbrirMensagemEmThreadStaAsync(IntPtr janela, string para, string assunto, string texto, string caminhoAnexo)
    {
        var tcs = new TaskCompletionSource<string?>();
        var thread = new Thread(() =>
        {
            try { tcs.SetResult(AbrirMensagem(janela, para, assunto, texto, caminhoAnexo)); }
            catch (Exception e) { tcs.SetResult("Erro ao abrir o programa de e-mail: " + e.Message); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return tcs.Task;
    }

    /// <summary>Abre a janela de composição. Devolve null se abriu, ou a mensagem de erro.</summary>
    public static string? AbrirMensagem(IntPtr janela, string para, string assunto, string texto, string caminhoAnexo)
    {
        var destinatario = new MapiRecipDesc { RecipientClass = MAPI_TO, Name = para, Address = "SMTP:" + para };
        var anexo = new MapiFileDesc { Position = -1, PathName = caminhoAnexo, FileName = Path.GetFileName(caminhoAnexo) };

        var ptrDestinatario = Marshal.AllocHGlobal(Marshal.SizeOf<MapiRecipDesc>());
        var ptrAnexo = Marshal.AllocHGlobal(Marshal.SizeOf<MapiFileDesc>());
        try
        {
            Marshal.StructureToPtr(destinatario, ptrDestinatario, false);
            Marshal.StructureToPtr(anexo, ptrAnexo, false);

            var mensagem = new MapiMessage
            {
                Subject = assunto,
                NoteText = texto,
                RecipientCount = 1,
                Recipients = ptrDestinatario,
                FileCount = 1,
                Files = ptrAnexo,
            };

            var resultado = MAPISendMail(IntPtr.Zero, janela, mensagem, MAPI_LOGON_UI | MAPI_DIALOG, 0);
            return resultado switch
            {
                0 => null,          // SUCCESS_SUCCESS
                1 => null,          // MAPI_E_USER_ABORT: a pessoa fechou sem enviar — não é erro
                2 => "O programa de e-mail não respondeu.",
                26 => "Nenhum programa de e-mail configurado neste computador (ex.: Outlook).",
                _ => $"O programa de e-mail recusou a mensagem (código {resultado}).",
            };
        }
        catch (DllNotFoundException)
        {
            return "Nenhum programa de e-mail configurado neste computador (ex.: Outlook).";
        }
        finally
        {
            Marshal.DestroyStructure<MapiRecipDesc>(ptrDestinatario);
            Marshal.DestroyStructure<MapiFileDesc>(ptrAnexo);
            Marshal.FreeHGlobal(ptrDestinatario);
            Marshal.FreeHGlobal(ptrAnexo);
        }
    }
}
