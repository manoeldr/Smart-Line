namespace SmartLine.Core.Enums;

/// <summary>Por que uma sessão foi fechada. Gravado como int: novos valores sempre no fim.</summary>
public enum MotivoFechamentoSessao
{
    /// <summary>Alguém finalizou.</summary>
    Manual,

    /// <summary>Virada da meia-noite na coleta contínua; outra sessão foi aberta em seguida.</summary>
    ViradaDoDia
}
