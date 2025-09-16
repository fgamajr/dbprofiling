import React, { useState, useEffect } from 'react';
import { 
  Save, 
  X, 
  FileText, 
  AlertTriangle,
  CheckCircle,
  RefreshCw,
  Code
} from 'lucide-react';
import { Toast } from './Toast';

interface YamlEditorProps {
  isOpen: boolean;
  onClose: () => void;
  initialYaml?: string;
  tableName: string;
  onSave: (yaml: string) => Promise<void>;
}

export function YamlEditor({ isOpen, onClose, initialYaml = '', tableName, onSave }: YamlEditorProps) {
  const [yaml, setYaml] = useState(initialYaml);
  const [saving, setSaving] = useState(false);
  const [toast, setToast] = useState<{ type: 'success' | 'error' | 'warning'; message: string } | null>(null);

  useEffect(() => {
    if (isOpen) {
      setYaml(initialYaml);
    }
  }, [isOpen, initialYaml]);

  const defaultYamlTemplate = `# ==================================================================
# 📋 CONFIGURAÇÃO DE REGRAS DE DATA PROFILING PARA "${tableName}"
# ==================================================================
# 
# Este arquivo define regras customizadas de validação e profiling de dados.
# O sistema processa essas regras e as aplica durante a análise dos dados.
#
# 📖 FORMATO ESPERADO:
#   - Cada regra deve ter: name, description, column, type, severity
#   - Tipos suportados: regex, range, not_null, unique, length, custom_sql
#   - Severity: "error", "warning", "info"
#   - Use aspas duplas para strings, sem aspas para números/booleanos
# ==================================================================

# 🎯 REGRAS DE VALIDAÇÃO
rules:
  
  # ========================================
  # 📧 VALIDAÇÕES DE FORMATO (REGEX)
  # ========================================
  
  - name: "validacao_email"
    description: "Valida formato de email válido"
    column: "email"  # substitua pelo nome real da coluna
    type: "regex"
    pattern: "^[A-Za-z0-9._%-]+@[A-Za-z0-9.-]+\\.[A-Za-z]{2,4}$"
    severity: "error"
    expected_match_rate: 95  # % mínimo de registros que devem passar
    
  - name: "validacao_cpf"
    description: "Valida formato de CPF brasileiro (XXX.XXX.XXX-XX ou 11 dígitos)"
    column: "cpf"  # substitua pelo nome real da coluna
    type: "regex"
    pattern: "^([0-9]{3}\\.[0-9]{3}\\.[0-9]{3}-[0-9]{2})|([0-9]{11})$"
    severity: "error"
    expected_match_rate: 98
    
  - name: "validacao_cnpj"
    description: "Valida formato de CNPJ brasileiro (XX.XXX.XXX/XXXX-XX ou 14 dígitos)"
    column: "cnpj"  # substitua pelo nome real da coluna
    type: "regex"
    pattern: "^([0-9]{2}\\.[0-9]{3}\\.[0-9]{3}/[0-9]{4}-[0-9]{2})|([0-9]{14})$"
    severity: "warning"
    expected_match_rate: 95
    
  - name: "validacao_telefone"
    description: "Valida formato de telefone brasileiro"
    column: "telefone"  # substitua pelo nome real da coluna
    type: "regex"
    pattern: "^(\\([0-9]{2}\\)|[0-9]{2})[\\s\\-]?[9]?[0-9]{4}[\\s\\-]?[0-9]{4}$"
    severity: "warning"
    expected_match_rate: 90
  
  # ========================================
  # 📊 VALIDAÇÕES NUMÉRICAS (RANGE)
  # ========================================
  
  - name: "idade_valida"
    description: "Idade deve estar entre 0 e 120 anos"
    column: "idade"  # substitua pelo nome real da coluna
    type: "range"
    min: 0
    max: 120
    severity: "error"
    
  - name: "percentual_valido"
    description: "Valores percentuais entre 0 e 100"
    column: "percentual"  # substitua pelo nome real da coluna
    type: "range"
    min: 0.0
    max: 100.0
    severity: "warning"
  
  # ========================================
  # 📏 VALIDAÇÕES DE COMPRIMENTO (LENGTH)
  # ========================================
  
  - name: "nome_tamanho_minimo"
    description: "Nome deve ter pelo menos 2 caracteres"
    column: "nome"  # substitua pelo nome real da coluna
    type: "length"
    min_length: 2
    max_length: 100
    severity: "error"
    
  - name: "codigo_formato_fixo"
    description: "Código deve ter exatamente 10 caracteres"
    column: "codigo"  # substitua pelo nome real da coluna
    type: "length"
    exact_length: 10
    severity: "error"
  
  # ========================================
  # ✅ VALIDAÇÕES OBRIGATÓRIAS (NOT_NULL)
  # ========================================
  
  - name: "campo_obrigatorio_nome"
    description: "Nome é campo obrigatório"
    column: "nome"  # substitua pelo nome real da coluna
    type: "not_null"
    severity: "error"
    
  - name: "campo_obrigatorio_email"
    description: "Email é campo obrigatório"
    column: "email"  # substitua pelo nome real da coluna
    type: "not_null"
    severity: "error"
  
  # ========================================
  # 🔑 VALIDAÇÕES DE UNICIDADE (UNIQUE)
  # ========================================
  
  - name: "unicidade_email"
    description: "Email deve ser único na tabela"
    column: "email"  # substitua pelo nome real da coluna
    type: "unique"
    severity: "error"
    allow_duplicates_percent: 0  # 0% = totalmente único
    
  - name: "unicidade_cpf"
    description: "CPF deve ser único na tabela"
    column: "cpf"  # substitua pelo nome real da coluna
    type: "unique"
    severity: "error"
    allow_duplicates_percent: 1  # até 1% de duplicatas aceitas
  
  # ========================================
  # 🔍 VALIDAÇÕES PERSONALIZADAS (CUSTOM_SQL)
  # ========================================
  
  - name: "validacao_data_nascimento"
    description: "Data de nascimento não pode ser futura"
    column: "data_nascimento"  # substitua pelo nome real da coluna
    type: "custom_sql"
    sql_condition: "data_nascimento <= CURRENT_DATE"
    severity: "error"
    
  - name: "consistencia_idade_data"
    description: "Idade deve ser consistente com data de nascimento"
    columns: ["idade", "data_nascimento"]  # múltiplas colunas
    type: "custom_sql"
    sql_condition: "ABS(EXTRACT(YEAR FROM AGE(data_nascimento)) - idade) <= 1"
    severity: "warning"

# ========================================
# ⚙️  CONFIGURAÇÕES GLOBAIS
# ========================================
config:
  # Máximo de violações mostradas no relatório
  max_violations_shown: 100
  
  # Habilitar correção automática quando possível
  enable_auto_correction: false
  
  # Exportar resultados para arquivo
  export_results: true
  
  # Formato de exportação (json, csv, xlsx)
  export_format: "json"
  
  # Parar na primeira regra que falhar
  fail_fast: false
  
  # Nivel de log (error, warning, info, debug)
  log_level: "warning"
  
  # Limite de tempo em segundos para cada regra
  timeout_seconds: 300

# ========================================
# 📝 NOTAS IMPORTANTES:
# ========================================
# 
# 1. Substitua os nomes das colunas pelos nomes reais da sua tabela
# 2. Ajuste os patterns regex conforme seu padrão de dados
# 3. Configure expected_match_rate para definir tolerância
# 4. Use severity adequada: "error" para crítico, "warning" para alertas
# 5. Teste suas regras antes de aplicar em produção
# 6. Regras custom_sql devem retornar true/false para cada linha
#
# 🚀 Para começar: descomente e configure as regras relevantes!
# ========================================`;

  const handleSave = async () => {
    setSaving(true);
    try {
      await onSave(yaml);
      setToast({ type: 'success', message: 'Regras YAML salvas com sucesso!' });
      setTimeout(() => {
        onClose();
      }, 1500);
    } catch (error) {
      setToast({ type: 'error', message: `Erro ao salvar regras: ${error instanceof Error ? error.message : 'Erro desconhecido'}` });
    } finally {
      setSaving(false);
    }
  };

  const handleLoadTemplate = () => {
    setYaml(defaultYamlTemplate);
    setToast({ type: 'success', message: 'Template carregado! Modifique conforme necessário.' });
  };

  const validateYaml = (yamlText: string): boolean => {
    try {
      // Validação básica de sintaxe YAML
      const lines = yamlText.split('\n');
      let indentLevel = 0;
      
      for (const line of lines) {
        const trimmed = line.trim();
        if (trimmed.startsWith('#') || trimmed === '') continue;
        
        const spaces = line.length - line.trimLeft().length;
        if (spaces % 2 !== 0 && spaces > 0) {
          throw new Error('Indentação deve ser múltipla de 2 espaços');
        }
      }
      
      return true;
    } catch (error) {
      return false;
    }
  };

  const isValidYaml = validateYaml(yaml);

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-xl shadow-2xl max-w-4xl w-full max-h-[90vh] flex flex-col">
        {/* Header */}
        <div className="flex items-center justify-between p-6 border-b border-gray-200">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-purple-100 rounded-lg flex items-center justify-center">
              <Code className="w-5 h-5 text-purple-600" />
            </div>
            <div>
              <h2 className="text-xl font-semibold text-gray-900">Editor de Regras YAML</h2>
              <p className="text-sm text-gray-500">Configure regras customizadas para {tableName}</p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
          >
            <X className="w-5 h-5 text-gray-500" />
          </button>
        </div>

        {/* Toolbar */}
        <div className="flex items-center gap-3 p-4 bg-gray-50 border-b border-gray-200">
          <button
            onClick={handleLoadTemplate}
            className="flex items-center gap-2 px-3 py-2 bg-blue-100 text-blue-700 rounded-lg hover:bg-blue-200 transition-colors"
          >
            <FileText className="w-4 h-4" />
            <span className="text-sm font-medium">Carregar Template</span>
          </button>
          
          <div className="flex items-center gap-2">
            {isValidYaml ? (
              <>
                <CheckCircle className="w-4 h-4 text-green-600" />
                <span className="text-sm text-green-700 font-medium">Sintaxe válida</span>
              </>
            ) : (
              <>
                <AlertTriangle className="w-4 h-4 text-red-600" />
                <span className="text-sm text-red-700 font-medium">Erro de sintaxe</span>
              </>
            )}
          </div>
        </div>

        {/* Editor */}
        <div className="flex-1 p-6 overflow-hidden">
          <div className="h-full">
            <textarea
              value={yaml}
              onChange={(e) => setYaml(e.target.value)}
              placeholder="Digite suas regras YAML aqui..."
              className="w-full h-full resize-none border border-gray-300 rounded-lg p-4 font-mono text-sm leading-relaxed focus:ring-2 focus:ring-purple-500 focus:border-transparent"
              style={{ 
                minHeight: '400px',
                fontFamily: 'Consolas, "Courier New", monospace'
              }}
            />
          </div>
        </div>

        {/* Footer */}
        <div className="flex items-center justify-between p-6 border-t border-gray-200 bg-gray-50">
          <div className="text-sm text-gray-500">
            Use espaços (não tabs) para indentação. Veja o template para exemplos.
          </div>
          
          <div className="flex items-center gap-3">
            <button
              onClick={onClose}
              className="px-4 py-2 text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 transition-colors"
            >
              Cancelar
            </button>
            <button
              onClick={handleSave}
              disabled={!isValidYaml || saving}
              className="px-4 py-2 bg-purple-600 text-white rounded-lg hover:bg-purple-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors flex items-center gap-2"
            >
              {saving ? (
                <>
                  <RefreshCw className="w-4 h-4 animate-spin" />
                  Salvando...
                </>
              ) : (
                <>
                  <Save className="w-4 h-4" />
                  Salvar Regras
                </>
              )}
            </button>
          </div>
        </div>
      </div>

      {/* Toast notifications */}
      {toast && (
        <Toast
          type={toast.type}
          message={toast.message}
          onClose={() => setToast(null)}
        />
      )}
    </div>
  );
}