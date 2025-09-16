namespace DbConnect.Core.Models;

public class PatternValidationRule
{
    public string PatternName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Regex { get; set; } = string.Empty;
    public List<string> ColumnKeywords { get; set; } = new();
    public string Culture { get; set; } = string.Empty;
}

public class PatternValidationSettings
{
    public List<PatternValidationRule> PatternValidationRules { get; set; } = new();
}