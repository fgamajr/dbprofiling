using System.Text.RegularExpressions;
using DbConnect.Web.AI;

namespace DbConnect.Web.Services;

public class SqlParser
{
    private readonly List<string> _reservedWords = new()
    {
        "SELECT", "FROM", "WHERE", "AND", "OR", "NOT", "IS", "NULL", "IN", "LIKE", 
        "BETWEEN", "EXISTS", "CASE", "WHEN", "THEN", "ELSE", "END", "TRUE", "FALSE",
        "CURRENT_DATE", "CURRENT_TIME", "CURRENT_TIMESTAMP", "NOW", "LENGTH", "TRIM",
        "UPPER", "LOWER", "SUBSTRING", "REPLACE", "COALESCE", "CAST", "EXTRACT"
    };

    private readonly Dictionary<string, string> _postgresqlFunctions = new()
    {
        { "LEN", "LENGTH" },
        { "LTRIM", "TRIM" },
        { "RTRIM", "TRIM" },
        { "ISNULL", "COALESCE" },
        { "DATEDIFF", "EXTRACT" },
        { "GETDATE", "CURRENT_DATE" },
        { "SYSDATE", "CURRENT_DATE" }
    };

    private readonly List<string> _postgresqlOperators = new()
    {
        "~", "~*", "!~", "!~*", "~~", "~~*", "!~~", "!~~*", "SIMILAR TO", "NOT SIMILAR TO",
        "@@", "@>", "<@", "?", "?&", "?|", "#>", "#>>", "||", "->", "->>"
    };

    public SqlValidationResult ValidateCondition(string sqlCondition, List<ColumnSchema> availableColumns, string tableName = "")
    {
        var result = new SqlValidationResult { IsValid = true, Warnings = new(), Errors = new() };

        try
        {
            // 1. Verificar sintaxe básica
            ValidateBasicSyntax(sqlCondition, result);

            // 2. Verificar colunas existentes
            ValidateColumnReferences(sqlCondition, availableColumns, result);

            // 3. Verificar funções PostgreSQL
            ValidatePostgreSQLFunctions(sqlCondition, result);

            // 4. Verificar operadores válidos
            ValidateOperators(sqlCondition, result);

            // 5. Verificar casting de tipos
            ValidateTypeCasting(sqlCondition, result);

            // 6. Verificar NULL handling
            ValidateNullHandling(sqlCondition, result);

            // 7. Sugestões de otimização
            SuggestOptimizations(sqlCondition, result);

            result.IsValid = result.Errors.Count == 0;
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Erro durante validação: {ex.Message}");
        }

        return result;
    }

    private void ValidateBasicSyntax(string sql, SqlValidationResult result)
    {
        // Verificar parênteses balanceados
        int openParens = sql.Count(c => c == '(');
        int closeParens = sql.Count(c => c == ')');
        if (openParens != closeParens)
        {
            result.Errors.Add($"Parênteses não balanceados: {openParens} abertos, {closeParens} fechados");
        }

        // Verificar aspas balanceadas
        int singleQuotes = sql.Count(c => c == '\'');
        if (singleQuotes % 2 != 0)
        {
            result.Errors.Add("Aspas simples não balanceadas");
        }

        // Verificar se não termina com operadores
        var trimmed = sql.Trim();
        if (trimmed.EndsWith(" AND") || trimmed.EndsWith(" OR") || trimmed.EndsWith("=") || 
            trimmed.EndsWith(">") || trimmed.EndsWith("<") || trimmed.EndsWith("!="))
        {
            result.Errors.Add("Condição SQL não pode terminar com operador");
        }

        // Verificar se não começa com operadores lógicos
        if (trimmed.StartsWith("AND ") || trimmed.StartsWith("OR "))
        {
            result.Errors.Add("Condição SQL não pode começar com operador lógico");
        }
    }

    private void ValidateColumnReferences(string sql, List<ColumnSchema> availableColumns, SqlValidationResult result)
    {
        // Extrair todas as possíveis referências de colunas
        var columnPattern = @"\b([a-zA-Z_][a-zA-Z0-9_]*)\b";
        var matches = Regex.Matches(sql, columnPattern, RegexOptions.IgnoreCase);

        var availableColumnNames = availableColumns.Select(c => c.Name.ToLower()).ToHashSet();

        foreach (Match match in matches)
        {
            var word = match.Value.ToLower();
            
            // Pular palavras reservadas e funções
            if (_reservedWords.Contains(word.ToUpper()) || _postgresqlFunctions.ContainsKey(word.ToUpper()) ||
                word.All(char.IsDigit) || word == "true" || word == "false")
            {
                continue;
            }

            // Verificar se a coluna existe
            if (!availableColumnNames.Contains(word))
            {
                result.Errors.Add($"Coluna '{match.Value}' não encontrada no schema da tabela");
            }
        }
    }

    private void ValidatePostgreSQLFunctions(string sql, SqlValidationResult result)
    {
        foreach (var (wrongFunc, correctFunc) in _postgresqlFunctions)
        {
            var pattern = $@"\b{wrongFunc}\s*\(";
            if (Regex.IsMatch(sql, pattern, RegexOptions.IgnoreCase))
            {
                result.Warnings.Add($"Função '{wrongFunc}' não é PostgreSQL. Use '{correctFunc}' em vez disso");
            }
        }

        // Verificar uso correto de funções PostgreSQL
        var commonErrors = new Dictionary<string, string>
        {
            { @"LENGTH\s*\(\s*([^)]+)\s*\)", "Para texto, use LENGTH($1::text)" },
            { @"([a-zA-Z_][a-zA-Z0-9_]*)\s*LIKE\s*'%[^%]*%'", "Para busca case-insensitive, use $1 ~* 'pattern'" },
            { @"TRIM\s*\(\s*([^)]+)\s*\)", "TRIM em PostgreSQL: TRIM($1::text)" }
        };

        foreach (var (pattern, suggestion) in commonErrors)
        {
            if (Regex.IsMatch(sql, pattern, RegexOptions.IgnoreCase))
            {
                result.Warnings.Add(suggestion);
            }
        }
    }

    private void ValidateOperators(string sql, SqlValidationResult result)
    {
        // Verificar operadores de regex válidos
        if (sql.Contains(" REGEXP ") || sql.Contains(" RLIKE "))
        {
            result.Errors.Add("Use '~' para regex case-sensitive ou '~*' para case-insensitive no PostgreSQL");
        }

        // Verificar operadores de comparação com NULL
        if (Regex.IsMatch(sql, @"\w+\s*[!=<>]+\s*NULL", RegexOptions.IgnoreCase))
        {
            result.Errors.Add("Use 'IS NULL' ou 'IS NOT NULL' para comparar com NULL, não operadores de comparação");
        }

        // Verificar uso correto de BETWEEN
        var betweenPattern = @"BETWEEN\s+(.+?)\s+AND\s+(.+?)(?:\s|$)";
        var betweenMatches = Regex.Matches(sql, betweenPattern, RegexOptions.IgnoreCase);
        foreach (Match match in betweenMatches)
        {
            if (match.Groups[1].Value.Trim() == match.Groups[2].Value.Trim())
            {
                result.Warnings.Add("BETWEEN com valores iguais pode ser otimizado para '=' simples");
            }
        }
    }

    private void ValidateTypeCasting(string sql, SqlValidationResult result)
    {
        // Verificar casting explícito recomendado
        var needsCasting = new[]
        {
            (@"LENGTH\s*\(\s*([a-zA-Z_][a-zA-Z0-9_]*)\s*\)", "LENGTH($1::text)"),
            (@"TRIM\s*\(\s*([a-zA-Z_][a-zA-Z0-9_]*)\s*\)", "TRIM($1::text)"),
            (@"([a-zA-Z_][a-zA-Z0-9_]*)\s*~\*?\s*'", "$1::text ~* '")
        };

        foreach (var (pattern, suggestion) in needsCasting)
        {
            if (Regex.IsMatch(sql, pattern, RegexOptions.IgnoreCase) && !sql.Contains("::"))
            {
                result.Warnings.Add($"Considere usar casting explícito: {suggestion}");
            }
        }
    }

    private void ValidateNullHandling(string sql, SqlValidationResult result)
    {
        // Verificar condições que podem falhar com NULL
        var potentialNullIssues = new[]
        {
            (@"[a-zA-Z_][a-zA-Z0-9_]*\s*=\s*'[^']*'", "Condições de igualdade podem falhar se a coluna for NULL. Considere adicionar 'OR coluna IS NULL'"),
            (@"LENGTH\s*\([^)]+\)\s*>\s*0", "LENGTH pode retornar NULL. Use 'LENGTH(COALESCE(coluna, '')) > 0'"),
            (@"[a-zA-Z_][a-zA-Z0-9_]*\s*[><=]\s*[0-9]", "Comparações numéricas podem falhar com NULL")
        };

        foreach (var (pattern, warning) in potentialNullIssues)
        {
            if (Regex.IsMatch(sql, pattern, RegexOptions.IgnoreCase))
            {
                result.Warnings.Add(warning);
            }
        }
    }

    private void SuggestOptimizations(string sql, SqlValidationResult result)
    {
        // Sugestões de otimização
        if (sql.Contains("UPPER(") && sql.Contains("LIKE"))
        {
            result.Warnings.Add("Para busca case-insensitive, '~*' é mais eficiente que UPPER() + LIKE");
        }

        if (Regex.IsMatch(sql, @"[a-zA-Z_][a-zA-Z0-9_]*\s*LIKE\s*'%[^%]+%'", RegexOptions.IgnoreCase))
        {
            result.Warnings.Add("LIKE '%texto%' pode ser lento. Considere usar índice de texto completo ou regex");
        }

        if (sql.Count(c => c == '(') > 3)
        {
            result.Warnings.Add("Condição complexa. Considere quebrar em múltiplas regras para melhor legibilidade");
        }

        // Detectar condições sempre verdadeiras ou falsas
        if (Regex.IsMatch(sql, @"1\s*=\s*1|'true'\s*=\s*'true'", RegexOptions.IgnoreCase))
        {
            result.Warnings.Add("Condição sempre verdadeira detectada");
        }

        if (Regex.IsMatch(sql, @"1\s*=\s*0|'true'\s*=\s*'false'", RegexOptions.IgnoreCase))
        {
            result.Warnings.Add("Condição sempre falsa detectada");
        }
    }

    public string SuggestCorrections(string sqlCondition, List<ColumnSchema> availableColumns)
    {
        var corrected = sqlCondition;

        // Aplicar correções automáticas comuns
        foreach (var (wrong, correct) in _postgresqlFunctions)
        {
            corrected = Regex.Replace(corrected, $@"\b{wrong}\s*\(", $"{correct}(", RegexOptions.IgnoreCase);
        }

        // Corrigir operadores de regex
        corrected = Regex.Replace(corrected, @"\s+REGEXP\s+", " ~ ", RegexOptions.IgnoreCase);
        corrected = Regex.Replace(corrected, @"\s+RLIKE\s+", " ~* ", RegexOptions.IgnoreCase);

        // Corrigir comparações com NULL
        corrected = Regex.Replace(corrected, @"(\w+)\s*!=\s*NULL", "$1 IS NOT NULL", RegexOptions.IgnoreCase);
        corrected = Regex.Replace(corrected, @"(\w+)\s*=\s*NULL", "$1 IS NULL", RegexOptions.IgnoreCase);

        return corrected;
    }
}

public class SqlValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public double ComplexityScore { get; set; } = 0;
    public string CorrectedSql { get; set; } = "";
}