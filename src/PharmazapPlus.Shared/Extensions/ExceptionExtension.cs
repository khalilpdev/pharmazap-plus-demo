namespace PharmazapPlus.Shared.Extensions;

public static class ExceptionExtension
{
    /// <summary>
    /// Retorna a mensagem completa da exceção, incluindo a inner exception quando presente.
    /// Exemplo: "Erro ao salvar | Inner: Violation of PRIMARY KEY constraint".
    /// </summary>
    public static string ToFullMessage(this Exception ex)
    {
        var inner = ex.InnerException?.Message;
        return inner != null ? $"{ex.Message} | Inner: {inner}" : ex.Message;
    }

    /// <summary>
    /// Retorna as primeiras linhas do stack trace da exceção como um array de strings.
    /// </summary>
    public static string[] ToStackTraceLines(this Exception ex, int maxLines = 5)
    {
        return ex.StackTrace?
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Take(maxLines)
            .ToArray() ?? [];
    }
}
