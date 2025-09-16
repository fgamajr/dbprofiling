using DbConnect.Core.Models;
using DbConnect.Web.AI;

namespace DbConnect.Web.Services;

public class DataQualityTemplateService
{
    public List<DataQualityRuleTemplate> GetPreDefinedTemplates()
    {
        return new List<DataQualityRuleTemplate>
        {
            // VALIDAÇÕES BRASILEIRAS
            new DataQualityRuleTemplate
            {
                Id = "cpf_valido",
                Name = "CPF Válido",
                Description = "Valida formato de CPF (XXX.XXX.XXX-XX ou 11 dígitos)",
                Dimension = "validity",
                SqlCondition = "{column} ~* '^[0-9]{3}\\.[0-9]{3}\\.[0-9]{3}-[0-9]{2}$' OR {column} ~* '^[0-9]{11}$'",
                Severity = "error",
                ExpectedPassRate = 100.0,
                ApplicableDataTypes = new[] { "text", "varchar", "char" },
                Category = "Documentos Brasileiros"
            },
            
            new DataQualityRuleTemplate
            {
                Id = "cnpj_valido",
                Name = "CNPJ Válido",
                Description = "Valida formato de CNPJ (XX.XXX.XXX/XXXX-XX ou 14 dígitos)",
                Dimension = "validity",
                SqlCondition = "{column} ~* '^[0-9]{2}\\.[0-9]{3}\\.[0-9]{3}/[0-9]{4}-[0-9]{2}$' OR {column} ~* '^[0-9]{14}$'",
                Severity = "error",
                ExpectedPassRate = 100.0,
                ApplicableDataTypes = new[] { "text", "varchar", "char" },
                Category = "Documentos Brasileiros"
            },

            new DataQualityRuleTemplate
            {
                Id = "telefone_brasileiro",
                Name = "Telefone Brasileiro",
                Description = "Valida formato de telefone brasileiro (com ou sem DDD)",
                Dimension = "validity",
                SqlCondition = "{column} ~* '^(\\([0-9]{2}\\)|[0-9]{2})[\\s\\-]?[9]?[0-9]{4}[\\s\\-]?[0-9]{4}$'",
                Severity = "warning",
                ExpectedPassRate = 95.0,
                ApplicableDataTypes = new[] { "text", "varchar", "char" },
                Category = "Contato"
            },

            new DataQualityRuleTemplate
            {
                Id = "cep_brasileiro",
                Name = "CEP Brasileiro",
                Description = "Valida formato de CEP brasileiro (XXXXX-XXX ou 8 dígitos)",
                Dimension = "validity",
                SqlCondition = "{column} ~* '^[0-9]{5}-[0-9]{3}$' OR {column} ~* '^[0-9]{8}$'",
                Severity = "error",
                ExpectedPassRate = 100.0,
                ApplicableDataTypes = new[] { "text", "varchar", "char" },
                Category = "Endereço"
            },

            // VALIDAÇÕES GERAIS
            new DataQualityRuleTemplate
            {
                Id = "email_valido",
                Name = "Email Válido",
                Description = "Valida formato de endereço de email",
                Dimension = "validity",
                SqlCondition = "{column} ~* '^[A-Za-z0-9._%-]+@[A-Za-z0-9.-]+\\.[A-Za-z]{2,4}$'",
                Severity = "error",
                ExpectedPassRate = 100.0,
                ApplicableDataTypes = new[] { "text", "varchar", "char" },
                Category = "Contato"
            },

            new DataQualityRuleTemplate
            {
                Id = "data_nao_futura",
                Name = "Data Não Futura",
                Description = "Valida que a data não é no futuro",
                Dimension = "timeliness",
                SqlCondition = "{column} <= CURRENT_DATE",
                Severity = "error",
                ExpectedPassRate = 100.0,
                ApplicableDataTypes = new[] { "date", "timestamp", "timestamptz" },
                Category = "Temporal"
            },

            new DataQualityRuleTemplate
            {
                Id = "data_nascimento_valida",
                Name = "Data de Nascimento Válida",
                Description = "Valida que a data de nascimento é realista (entre 1900 e hoje)",
                Dimension = "accuracy",
                SqlCondition = "{column} >= '1900-01-01' AND {column} <= CURRENT_DATE",
                Severity = "error",
                ExpectedPassRate = 100.0,
                ApplicableDataTypes = new[] { "date", "timestamp", "timestamptz" },
                Category = "Temporal"
            },

            new DataQualityRuleTemplate
            {
                Id = "idade_valida",
                Name = "Idade Válida",
                Description = "Valida que a idade está entre 0 e 150 anos",
                Dimension = "accuracy",
                SqlCondition = "{column} >= 0 AND {column} <= 150",
                Severity = "error",
                ExpectedPassRate = 100.0,
                ApplicableDataTypes = new[] { "integer", "numeric", "smallint" },
                Category = "Demográfico"
            },

            new DataQualityRuleTemplate
            {
                Id = "valor_positivo",
                Name = "Valor Positivo",
                Description = "Valida que o valor numérico é positivo (>= 0)",
                Dimension = "accuracy",
                SqlCondition = "{column} >= 0",
                Severity = "warning",
                ExpectedPassRate = 95.0,
                ApplicableDataTypes = new[] { "integer", "numeric", "decimal", "money" },
                Category = "Financeiro"
            },

            new DataQualityRuleTemplate
            {
                Id = "texto_nao_vazio",
                Name = "Texto Não Vazio",
                Description = "Valida que campos de texto não são vazios após trim",
                Dimension = "completeness",
                SqlCondition = "LENGTH(TRIM({column}::text)) > 0",
                Severity = "error",
                ExpectedPassRate = 100.0,
                ApplicableDataTypes = new[] { "text", "varchar", "char" },
                Category = "Completude"
            },

            new DataQualityRuleTemplate
            {
                Id = "campo_obrigatorio",
                Name = "Campo Obrigatório",
                Description = "Valida que o campo não é nulo",
                Dimension = "completeness",
                SqlCondition = "{column} IS NOT NULL",
                Severity = "error",
                ExpectedPassRate = 100.0,
                ApplicableDataTypes = new[] { "text", "varchar", "char", "integer", "numeric", "date", "timestamp", "boolean" },
                Category = "Completude"
            },

            // REGRAS CONDICIONAIS PRÉ-DEFINIDAS
            new DataQualityRuleTemplate
            {
                Id = "data_fim_depois_inicio",
                Name = "Data Fim Após Data Início",
                Description = "Valida que data de fim é posterior à data de início (quando preenchida)",
                Dimension = "consistency",
                SqlCondition = "{column} > {reference_column} OR {column} IS NULL",
                Severity = "error",
                ExpectedPassRate = 100.0,
                ApplicableDataTypes = new[] { "date", "timestamp", "timestamptz" },
                Category = "Temporal",
                RequiresReferenceColumn = true,
                ConditionalRule = true
            },

            new DataQualityRuleTemplate
            {
                Id = "salario_minimo",
                Name = "Salário Mínimo",
                Description = "Valida que salário é >= salário mínimo (quando tipo_contrato = 'CLT')",
                Dimension = "accuracy",
                SqlCondition = "{column} >= 1412 OR {reference_column} != 'CLT'",
                Severity = "error",
                ExpectedPassRate = 100.0,
                ApplicableDataTypes = new[] { "numeric", "decimal", "money" },
                Category = "Financeiro",
                RequiresReferenceColumn = true,
                ConditionalRule = true
            }
        };
    }

    public List<DataQualityRuleTemplate> GetApplicableTemplates(ColumnSchema column, List<ColumnSchema> allColumns)
    {
        var templates = GetPreDefinedTemplates();
        var applicable = new List<DataQualityRuleTemplate>();

        foreach (var template in templates)
        {
            // Verificar se o tipo de dados é aplicável
            if (template.ApplicableDataTypes.Any(dt => 
                column.DataType.ToLower().Contains(dt.ToLower())))
            {
                applicable.Add(template);
            }

            // Para regras condicionais, verificar se existe coluna de referência adequada
            if (template.ConditionalRule && template.RequiresReferenceColumn)
            {
                var hasReferenceColumn = allColumns.Any(c => 
                    c.Name.ToLower().Contains("tipo") || 
                    c.Name.ToLower().Contains("status") ||
                    c.Name.ToLower().Contains("inicio") ||
                    c.Name.ToLower().Contains("start"));

                if (hasReferenceColumn)
                {
                    applicable.Add(template);
                }
            }
        }

        return applicable.Distinct().ToList();
    }

    public DataQualityRule ApplyTemplate(DataQualityRuleTemplate template, ColumnSchema column, string? referenceColumn = null)
    {
        var sqlCondition = template.SqlCondition.Replace("{column}", column.Name);
        
        if (template.RequiresReferenceColumn && !string.IsNullOrEmpty(referenceColumn))
        {
            sqlCondition = sqlCondition.Replace("{reference_column}", referenceColumn);
        }

        return new DataQualityRule
        {
            Id = $"{template.Id}_{column.Name}",
            Name = $"{template.Name} - {column.Name}",
            Description = template.Description,
            Dimension = template.Dimension,
            Column = column.Name,
            SqlCondition = sqlCondition,
            Severity = template.Severity,
            ExpectedPassRate = template.ExpectedPassRate
        };
    }
}

// Modelo para templates de regras
public class DataQualityRuleTemplate
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Dimension { get; set; } = "";
    public string SqlCondition { get; set; } = ""; // Usa {column} como placeholder
    public string Severity { get; set; } = "";
    public double ExpectedPassRate { get; set; } = 95.0;
    public string[] ApplicableDataTypes { get; set; } = Array.Empty<string>();
    public string Category { get; set; } = "";
    public bool RequiresReferenceColumn { get; set; } = false;
    public bool ConditionalRule { get; set; } = false;
}