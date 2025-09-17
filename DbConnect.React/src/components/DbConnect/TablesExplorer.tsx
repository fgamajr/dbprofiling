import React, { useState, useEffect } from 'react';
import { 
  Table, 
  ChevronDown, 
  ChevronRight, 
  Database, 
  Hash, 
  Type, 
  Key, 
  RefreshCw,
  Calendar,
  Search,
  Eye,
  ArrowUpDown,
  Activity,
  Brain,
  Settings,
  Zap,
  AlertTriangle,
  CheckCircle,
  XCircle,
  FileText
} from 'lucide-react';
import { LoadingSpinner } from './LoadingSpinner';
import { Toast } from './Toast';
import { YamlEditor } from './YamlEditor';
import { ApiKeySetup } from './ApiKeySetup';
import { TableEssentialMetrics } from './TableEssentialMetrics';
import { AdvancedMetricsCard } from './AdvancedMetricsCard';
import { apiService } from '../../services/api';
import type { DatabaseTablesResponse, TableDetailsResponse, DatabaseTable, TableProfilingResponse } from '../../services/api';

interface TablesExplorerProps {
  isConnected: boolean;
  activeProfileId: number | null;
}

export function TablesExplorer({ isConnected, activeProfileId }: TablesExplorerProps) {
  const [tablesData, setTablesData] = useState<DatabaseTablesResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [toast, setToast] = useState<{ type: 'success' | 'error' | 'warning'; message: string } | null>(null);
  const [expandedTables, setExpandedTables] = useState<Set<string>>(new Set());
  const [tableDetails, setTableDetails] = useState<Map<string, TableDetailsResponse>>(new Map());
  const [loadingDetails, setLoadingDetails] = useState<Set<string>>(new Set());
  const [expandedProfiling, setExpandedProfiling] = useState<Map<string, Set<string>>>(new Map());
  const [profilingData, setProfilingData] = useState<Map<string, TableProfilingResponse>>(new Map());
  const [loadingProfiling, setLoadingProfiling] = useState<Set<string>>(new Set());
  const [yamlEditorOpen, setYamlEditorOpen] = useState(false);
  const [currentEditingTable, setCurrentEditingTable] = useState<{ schema: string; name: string } | null>(null);
  const [yamlRules, setYamlRules] = useState<Map<string, string>>(new Map());
  const [aiQualityData, setAiQualityData] = useState<Map<string, any>>(new Map());
  const [apiKeySetupOpen, setApiKeySetupOpen] = useState(false);
  const [apiKeyStatus, setApiKeyStatus] = useState<{openai: boolean; claude: boolean; hasAnyKey: boolean} | null>(null);

  useEffect(() => {
    if (isConnected && activeProfileId) {
      loadTables();
      loadApiKeyStatus();
    } else {
      setTablesData(null);
      setExpandedTables(new Set());
      setTableDetails(new Map());
    }
  }, [isConnected, activeProfileId]);

  async function loadTables() {
    setLoading(true);
    try {
      const data = await apiService.getDatabaseTables();
      if (data) {
        setTablesData(data);
        setToast({ type: 'success', message: `${data.totalTables} tabelas carregadas com sucesso!` });
      } else {
        setToast({ type: 'error', message: 'Não foi possível carregar as tabelas' });
      }
    } catch (error) {
      setToast({ type: 'error', message: 'Erro ao carregar tabelas' });
    } finally {
      setLoading(false);
    }
  }

  async function toggleTableExpansion(table: DatabaseTable) {
    const tableKey = `${table.schema}.${table.name}`;
    const isCurrentlyExpanded = expandedTables.has(tableKey);
    
    if (isCurrentlyExpanded) {
      // Collapse
      const newExpanded = new Set(expandedTables);
      newExpanded.delete(tableKey);
      setExpandedTables(newExpanded);
    } else {
      // Expand and load details if not already loaded
      const newExpanded = new Set(expandedTables);
      newExpanded.add(tableKey);
      setExpandedTables(newExpanded);

      if (!tableDetails.has(tableKey)) {
        setLoadingDetails(prev => new Set([...prev, tableKey]));
        try {
          const details = await apiService.getTableDetails(table.schema, table.name);
          if (details) {
            const newDetails = new Map(tableDetails);
            newDetails.set(tableKey, details);
            setTableDetails(newDetails);
          }
        } catch (error) {
          setToast({ type: 'error', message: `Erro ao carregar detalhes da tabela ${table.name}` });
        } finally {
          setLoadingDetails(prev => {
            const newSet = new Set(prev);
            newSet.delete(tableKey);
            return newSet;
          });
        }
      }
    }
  }

  function toggleProfilingSection(tableKey: string, sectionType: string) {
    const currentSections = expandedProfiling.get(tableKey) || new Set();
    const newSections = new Set(currentSections);
    
    if (newSections.has(sectionType)) {
      newSections.delete(sectionType);
    } else {
      newSections.add(sectionType);
      // TODO: Load profiling data when section is expanded
      loadProfilingData(tableKey, sectionType);
    }
    
    const newExpandedProfiling = new Map(expandedProfiling);
    newExpandedProfiling.set(tableKey, newSections);
    setExpandedProfiling(newExpandedProfiling);
  }

  async function loadProfilingData(tableKey: string, sectionType: string) {
    if (sectionType !== 'auto') {
      console.log(`Loading ${sectionType} profiling for ${tableKey} (placeholder)`);
      return;
    }

    // Verificar se já temos dados de AI Quality para esta tabela
    if (aiQualityData.has(tableKey)) {
      return;
    }

    setLoadingProfiling(prev => new Set([...prev, tableKey]));

    try {
      const [schema, tableName] = tableKey.split('.');
      console.log(`🤖 Loading AI Data Quality assessment for ${schema}.${tableName}`);
      
      const response = await fetch(`/api/u/database/tables/${schema}/${tableName}/ai-quality`, {
        method: 'POST',
        credentials: 'include',
        headers: {
          'Content-Type': 'application/json'
        }
      });

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }

      const aiResult = await response.json();
      
      if (aiResult && aiResult.rules) {
        console.log(`✅ AI Data Quality assessment loaded for ${tableKey}:`, aiResult);
        const newAiQualityData = new Map(aiQualityData);
        newAiQualityData.set(tableKey, aiResult);
        setAiQualityData(newAiQualityData);
        setToast({ 
          type: 'success', 
          message: `🤖 Análise AI de qualidade concluída para ${tableName}` 
        });
      } else {
        setToast({ 
          type: 'warning', 
          message: `Não foi possível gerar análise AI para ${tableName}` 
        });
      }
    } catch (error) {
      console.error(`❌ Error loading AI quality data for ${tableKey}:`, error);
      setToast({ 
        type: 'error', 
        message: `Erro na análise AI de ${tableKey.split('.')[1]}: ${error instanceof Error ? error.message : 'Erro desconhecido'}` 
      });
    } finally {
      setLoadingProfiling(prev => {
        const newSet = new Set(prev);
        newSet.delete(tableKey);
        return newSet;
      });
    }
  }

  async function loadApiKeyStatus() {
    try {
      const response = await fetch('/api/u/api-keys/status', {
        credentials: 'include'
      });
      if (response.ok) {
        const status = await response.json();
        setApiKeyStatus(status);
      }
    } catch (error) {
      console.error('❌ Error loading API key status:', error);
    }
  }

  async function saveApiKey(provider: string, apiKey: string): Promise<boolean> {
    try {
      const response = await fetch('/api/u/api-keys/validate', {
        method: 'POST',
        credentials: 'include',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({ provider, apiKey })
      });

      const result = await response.json();
      if (result.valid) {
        setToast({ type: 'success', message: result.message });
        await loadApiKeyStatus(); // Recarregar status
        return true;
      } else {
        setToast({ type: 'error', message: result.message });
        return false;
      }
    } catch (error) {
      setToast({ type: 'error', message: 'Erro ao salvar API Key' });
      return false;
    }
  }

  function openYamlEditor(schema: string, tableName: string) {
    setCurrentEditingTable({ schema, name: tableName });
    setYamlEditorOpen(true);
  }

  function closeYamlEditor() {
    setYamlEditorOpen(false);
    setCurrentEditingTable(null);
  }

  async function saveYamlRules(yaml: string) {
    if (!currentEditingTable) return;
    
    const tableKey = `${currentEditingTable.schema}.${currentEditingTable.name}`;
    
    // Para agora, salvar localmente. TODO: Implementar API
    const newYamlRules = new Map(yamlRules);
    newYamlRules.set(tableKey, yaml);
    setYamlRules(newYamlRules);
    
    console.log(`💾 Saving YAML rules for ${tableKey}:`, yaml);
    
    // Simular delay da API
    await new Promise(resolve => setTimeout(resolve, 1000));
    
    setToast({ 
      type: 'success', 
      message: `Regras YAML salvas para ${currentEditingTable.name}!` 
    });
  }

  function formatNumber(num: number): string {
    return num.toLocaleString('pt-BR');
  }

  function formatDataType(column: any): string {
    let type = column.dataType;
    if (column.maxLength) {
      type += `(${column.maxLength})`;
    }
    return type;
  }

  if (!isConnected) {
    return (
      <div className="bg-gradient-card rounded-2xl shadow-md border border-border p-12 text-center">
        <div className="w-20 h-20 mx-auto bg-gradient-primary rounded-2xl flex items-center justify-center mb-6 animate-float">
          <Table className="w-10 h-10 text-primary-foreground" />
        </div>
        <h3 className="text-xl font-semibold text-card-foreground mb-2 font-heading">
          Nenhum banco conectado
        </h3>
        <p className="text-muted-foreground mb-6 max-w-md mx-auto">
          Conecte-se a um perfil na aba "Perfis" para explorar e analisar as tabelas do banco de dados
        </p>
        <div className="inline-flex items-center gap-2 px-4 py-2 bg-primary/10 text-primary rounded-lg text-sm font-medium">
          <span>🔌</span>
          <span>Conecte-se primeiro</span>
        </div>
      </div>
    );
  }

  if (loading) {
    return (
      <div className="bg-gradient-card rounded-2xl shadow-md border border-border p-12 text-center">
        <LoadingSpinner size="lg" className="mb-4 text-primary" />
        <p className="text-muted-foreground font-medium">Carregando tabelas do banco...</p>
      </div>
    );
  }

  if (!tablesData || tablesData.tables.length === 0) {
    return (
      <div className="bg-gradient-card rounded-2xl shadow-md border border-border p-12 text-center">
        <div className="w-20 h-20 mx-auto bg-yellow-100 rounded-2xl flex items-center justify-center mb-6">
          <Table className="w-10 h-10 text-yellow-600" />
        </div>
        <h3 className="text-xl font-semibold text-card-foreground mb-2 font-heading">
          Nenhuma tabela encontrada
        </h3>
        <p className="text-muted-foreground mb-6 max-w-md mx-auto">
          Não foram encontradas tabelas neste banco de dados
        </p>
        <button
          onClick={loadTables}
          className="inline-flex items-center gap-2 px-4 py-2 bg-primary text-primary-foreground rounded-lg hover:bg-primary/90 transition-colors"
        >
          <RefreshCw className="w-4 h-4" />
          <span>Tentar novamente</span>
        </button>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-bold text-card-foreground font-heading mb-2">
            Explorador de Tabelas
          </h2>
          <p className="text-muted-foreground">
            {tablesData.totalTables} {tablesData.totalTables === 1 ? 'tabela encontrada' : 'tabelas encontradas'}
          </p>
        </div>
        <button
          onClick={loadTables}
          disabled={loading}
          className="flex items-center gap-2 px-4 py-2 bg-primary text-primary-foreground rounded-lg hover:bg-primary/90 transition-colors disabled:opacity-50"
        >
          <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
          <span>Atualizar</span>
        </button>
      </div>

      {/* Tables List */}
      <div className="space-y-4">
        {tablesData.tables.map((table) => {
          const tableKey = `${table.schema}.${table.name}`;
          const isExpanded = expandedTables.has(tableKey);
          const details = tableDetails.get(tableKey);
          const isLoadingDetails = loadingDetails.has(tableKey);

          return (
            <div
              key={tableKey}
              className="bg-gradient-card rounded-xl shadow-sm border border-border overflow-hidden"
            >
              {/* Table Header */}
              <div
                className="p-6 cursor-pointer hover:bg-gray-50 transition-colors"
                onClick={() => toggleTableExpansion(table)}
              >
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-4">
                    <div className="flex items-center gap-2">
                      {isExpanded ? (
                        <ChevronDown className="w-5 h-5 text-gray-400" />
                      ) : (
                        <ChevronRight className="w-5 h-5 text-gray-400" />
                      )}
                      <div className="w-10 h-10 bg-blue-100 rounded-lg flex items-center justify-center">
                        <Table className="w-5 h-5 text-blue-600" />
                      </div>
                    </div>
                    
                    <div className="flex-1">
                      <h3 className="text-lg font-semibold text-card-foreground">
                        {table.name}
                      </h3>
                      <div className="flex items-center gap-4 mt-1">
                        <span className="text-sm text-muted-foreground">
                          Schema: {table.schema}
                        </span>
                        <span className="text-sm text-muted-foreground">
                          Registros: {formatNumber(table.estimatedRows)}
                        </span>
                        <span className="text-sm text-muted-foreground">
                          Tamanho: {table.size}
                        </span>
                      </div>
                    </div>
                  </div>

                  <div className="text-sm text-muted-foreground">
                    {isExpanded ? 'Clique para recolher' : 'Clique para expandir'}
                  </div>
                </div>
              </div>

              {/* Table Details (Expanded) */}
              {isExpanded && (
                <div className="border-t border-border bg-gray-50/50">
                  {isLoadingDetails ? (
                    <div className="p-6 text-center">
                      <LoadingSpinner size="sm" className="mb-2 text-primary" />
                      <p className="text-sm text-muted-foreground">Carregando detalhes...</p>
                    </div>
                  ) : details ? (
                    <div className="p-6 space-y-6">
                      {/* Summary Stats */}
                      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                        <div className="bg-white rounded-lg p-4 border border-border">
                          <div className="flex items-center gap-2 mb-2">
                            <Hash className="w-4 h-4 text-blue-600" />
                            <span className="text-sm font-medium text-card-foreground">Total de Registros</span>
                          </div>
                          <div className="text-2xl font-bold text-blue-600">
                            {formatNumber(details.statistics.totalRows)}
                          </div>
                        </div>

                        <div className="bg-white rounded-lg p-4 border border-border">
                          <div className="flex items-center gap-2 mb-2">
                            <Type className="w-4 h-4 text-green-600" />
                            <span className="text-sm font-medium text-card-foreground">Colunas</span>
                          </div>
                          <div className="text-2xl font-bold text-green-600">
                            {details.columns.length}
                          </div>
                        </div>

                        <div className="bg-white rounded-lg p-4 border border-border">
                          <div className="flex items-center gap-2 mb-2">
                            <Search className="w-4 h-4 text-yellow-600" />
                            <span className="text-sm font-medium text-card-foreground">Índices</span>
                          </div>
                          <div className="text-2xl font-bold text-yellow-600">
                            {details.indexes.length}
                          </div>
                        </div>

                        <div className="bg-white rounded-lg p-4 border border-border">
                          <div className="flex items-center gap-2 mb-2">
                            <Key className="w-4 h-4 text-purple-600" />
                            <span className="text-sm font-medium text-card-foreground">Chaves Primárias</span>
                          </div>
                          <div className="text-2xl font-bold text-purple-600">
                            {details.columns.filter(c => c.isPrimaryKey).length}
                          </div>
                        </div>
                      </div>

                      {/* Column Structure */}
                      <div>
                        <h4 className="text-lg font-semibold text-card-foreground mb-4 flex items-center gap-2">
                          <Type className="w-5 h-5 text-green-600" />
                          Estrutura das Colunas
                        </h4>
                        <div className="bg-white rounded-lg border border-border overflow-hidden">
                          <div className="overflow-x-auto">
                            <table className="min-w-full">
                              <thead className="bg-gray-50">
                                <tr>
                                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                                    Nome
                                  </th>
                                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                                    Tipo
                                  </th>
                                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                                    Nullable
                                  </th>
                                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                                    Padrão
                                  </th>
                                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                                    Chave
                                  </th>
                                </tr>
                              </thead>
                              <tbody className="divide-y divide-gray-200">
                                {details.columns.map((column, index) => (
                                  <tr key={index} className="hover:bg-gray-50">
                                    <td className="px-4 py-3 text-sm font-medium text-card-foreground">
                                      {column.name}
                                    </td>
                                    <td className="px-4 py-3 text-sm text-muted-foreground">
                                      {formatDataType(column)}
                                    </td>
                                    <td className="px-4 py-3 text-sm">
                                      <span className={`px-2 py-1 rounded-full text-xs font-medium ${
                                        column.nullable 
                                          ? 'bg-yellow-100 text-yellow-700' 
                                          : 'bg-green-100 text-green-700'
                                      }`}>
                                        {column.nullable ? 'Sim' : 'Não'}
                                      </span>
                                    </td>
                                    <td className="px-4 py-3 text-sm text-muted-foreground">
                                      {column.defaultValue || '-'}
                                    </td>
                                    <td className="px-4 py-3 text-sm">
                                      {column.isPrimaryKey && (
                                        <span className="px-2 py-1 rounded-full text-xs font-medium bg-purple-100 text-purple-700">
                                          PK
                                        </span>
                                      )}
                                    </td>
                                  </tr>
                                ))}
                              </tbody>
                            </table>
                          </div>
                        </div>
                      </div>

                      {/* Indexes */}
                      {details.indexes.length > 0 && (
                        <div>
                          <h4 className="text-lg font-semibold text-card-foreground mb-4 flex items-center gap-2">
                            <Search className="w-5 h-5 text-yellow-600" />
                            Índices
                          </h4>
                          <div className="space-y-2">
                            {details.indexes.map((index, i) => (
                              <div key={i} className="bg-white rounded-lg border border-border p-4">
                                <div className="flex items-center justify-between">
                                  <div>
                                    <h5 className="font-medium text-card-foreground">{index.name}</h5>
                                    <p className="text-sm text-muted-foreground">
                                      Colunas: {index.columns.join(', ')}
                                    </p>
                                  </div>
                                  <div className="flex gap-2">
                                    {index.isPrimary && (
                                      <span className="px-2 py-1 rounded-full text-xs font-medium bg-purple-100 text-purple-700">
                                        Primário
                                      </span>
                                    )}
                                    {index.isUnique && (
                                      <span className="px-2 py-1 rounded-full text-xs font-medium bg-blue-100 text-blue-700">
                                        Único
                                      </span>
                                    )}
                                  </div>
                                </div>
                              </div>
                            ))}
                          </div>
                        </div>
                      )}

                      {/* Sample Data */}
                      {details.sampleData.length > 0 && (
                        <div>
                          <h4 className="text-lg font-semibold text-card-foreground mb-4 flex items-center gap-2">
                            <Eye className="w-5 h-5 text-cyan-600" />
                            Primeiros 10 Registros
                          </h4>
                          <div className="bg-white rounded-lg border border-border overflow-hidden">
                            <div className="overflow-x-auto">
                              <table className="min-w-full">
                                <thead className="bg-gray-50">
                                  <tr>
                                    {details.columns.map((column) => (
                                      <th
                                        key={column.name}
                                        className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider whitespace-nowrap"
                                      >
                                        {column.name}
                                      </th>
                                    ))}
                                  </tr>
                                </thead>
                                <tbody className="divide-y divide-gray-200">
                                  {details.sampleData.map((row, index) => (
                                    <tr key={index} className="hover:bg-gray-50">
                                      {details.columns.map((column) => (
                                        <td
                                          key={column.name}
                                          className="px-4 py-3 text-sm text-muted-foreground whitespace-nowrap max-w-xs truncate"
                                          title={String(row[column.name] ?? 'NULL')}
                                        >
                                          {row[column.name] === null ? (
                                            <span className="text-gray-400 italic">NULL</span>
                                          ) : (
                                            String(row[column.name])
                                          )}
                                        </td>
                                      ))}
                                    </tr>
                                  ))}
                                </tbody>
                              </table>
                            </div>
                          </div>
                        </div>
                      )}

                      {/* Essential Metrics Section */}
                      <div className="border-t border-gray-200 pt-6">
                        <TableEssentialMetrics
                          schema={table.schema}
                          tableName={table.name}
                          onToast={setToast}
                        />
                      </div>

                      {/* Advanced Metrics Section */}
                      <div className="pt-6">
                        <AdvancedMetricsCard
                          schema={table.schema}
                          tableName={table.name}
                          onToast={setToast}
                        />
                      </div>

                      {/* Advanced Data Profiling Sections - DEBUG */}
                      {console.log('🚀 PROFILING SECTIONS SHOULD RENDER NOW!', tableKey, details)}
                      <div className="border-t border-gray-200 pt-6">
                        <h4 className="text-lg font-semibold text-card-foreground mb-4 flex items-center gap-2">
                          <Zap className="w-5 h-5 text-orange-600" />
                          Data Profiling Avançado ✨
                        </h4>
                        <div className="space-y-6">
                          {/* AI Data Quality Assessment - Expanded Interface */}
                          <div 
                            className={`bg-gradient-to-br from-orange-50 to-yellow-50 rounded-xl border-2 transition-all duration-300 ${
                              apiKeyStatus?.hasAnyKey 
                                ? 'border-orange-200 hover:border-orange-300 cursor-pointer' 
                                : 'border-gray-200 opacity-60'
                            }`}
                            onClick={() => apiKeyStatus?.hasAnyKey && toggleProfilingSection(tableKey, 'auto')}
                          >
                            <div className="p-6">
                              <div className="flex items-center justify-between mb-3">
                                <div className="flex items-center gap-3">
                                  <div className="w-12 h-12 bg-gradient-to-br from-orange-400 to-yellow-500 rounded-xl flex items-center justify-center">
                                    <Zap className="w-6 h-6 text-white" />
                                  </div>
                                  <div>
                                    <h5 className="text-lg font-semibold text-gray-900">🤖 Data Quality Assessment com IA</h5>
                                    <p className="text-sm text-gray-600">
                                      {apiKeyStatus?.hasAnyKey 
                                        ? `Powered by ${apiKeyStatus.openai ? 'OpenAI GPT-4' : 'Claude 3'} • 6 dimensões de qualidade`
                                        : 'Configure uma API Key para usar IA'
                                      }
                                    </p>
                                  </div>
                                </div>
                                <div className="flex items-center gap-2">
                                  {!apiKeyStatus?.hasAnyKey && (
                                    <button
                                      onClick={(e) => {
                                        e.stopPropagation();
                                        setApiKeySetupOpen(true);
                                      }}
                                      className="px-4 py-2 bg-blue-600 text-white text-sm rounded-lg hover:bg-blue-700 transition-colors"
                                    >
                                      Configurar API Key
                                    </button>
                                  )}
                                  {apiKeyStatus?.hasAnyKey && (
                                    <>
                                      {expandedProfiling.get(tableKey)?.has('auto') ? (
                                        <ChevronDown className="w-5 h-5 text-orange-600" />
                                      ) : (
                                        <ChevronRight className="w-5 h-5 text-orange-600" />
                                      )}
                                    </>
                                  )}
                                </div>
                              </div>
                              
                              {!apiKeyStatus?.hasAnyKey && (
                                <div className="bg-gray-100 rounded-lg p-4 border border-gray-200">
                                  <div className="flex items-center gap-3">
                                    <div className="w-8 h-8 bg-gray-300 rounded-full flex items-center justify-center">
                                      <Key className="w-4 h-4 text-gray-600" />
                                    </div>
                                    <div>
                                      <p className="text-sm font-medium text-gray-700">API Key necessária</p>
                                      <p className="text-xs text-gray-500">Configure OpenAI ou Claude para análise AI de qualidade</p>
                                    </div>
                                  </div>
                                </div>
                              )}
                            </div>
                            
                            {expandedProfiling.get(tableKey)?.has('auto') && apiKeyStatus?.hasAnyKey && (
                              <div className="border-t border-orange-200 bg-white rounded-b-xl p-6">
                                {loadingProfiling.has(tableKey) ? (
                                  <div className="space-y-4">
                                    {/* AI Analysis Progress */}
                                    <div className="bg-gradient-to-r from-blue-50 to-purple-50 rounded-lg p-4 border border-blue-200">
                                      <div className="flex items-center gap-3 mb-3">
                                        <div className="w-8 h-8 bg-blue-100 rounded-full flex items-center justify-center">
                                          <LoadingSpinner size="sm" className="text-blue-600" />
                                        </div>
                                        <div>
                                          <h6 className="font-medium text-blue-900">🤖 IA analisando dados...</h6>
                                          <p className="text-sm text-blue-700">Gerando regras de qualidade baseadas no schema e dados</p>
                                        </div>
                                      </div>
                                      <div className="w-full bg-blue-200 rounded-full h-2">
                                        <div className="bg-blue-600 h-2 rounded-full animate-pulse" style={{width: '60%'}}></div>
                                      </div>
                                    </div>

                                    {/* Streaming Steps */}
                                    <div className="space-y-2">
                                      <div className="flex items-center gap-2 text-sm">
                                        <CheckCircle className="w-4 h-4 text-green-600" />
                                        <span className="text-gray-700">✅ Schema da tabela analisado</span>
                                      </div>
                                      <div className="flex items-center gap-2 text-sm">
                                        <CheckCircle className="w-4 h-4 text-green-600" />
                                        <span className="text-gray-700">✅ Dados de amostra coletados (10 registros)</span>
                                      </div>
                                      <div className="flex items-center gap-2 text-sm">
                                        <LoadingSpinner size="sm" className="text-orange-600" />
                                        <span className="text-gray-700">🤖 {apiKeyStatus?.openai ? 'OpenAI GPT-4' : 'Claude 3'} gerando regras...</span>
                                      </div>
                                    </div>
                                  </div>
                                ) : aiQualityData.has(tableKey) ? (
                                  <div className="space-y-6">
                                    {/* Header with summary */}
                                    <div className="bg-gradient-to-r from-green-50 to-blue-50 p-4 rounded-lg border border-green-200">
                                      <div className="flex items-center justify-between mb-2">
                                        <h6 className="font-semibold text-green-800 flex items-center gap-2">
                                          <CheckCircle className="w-5 h-5" />
                                          📊 Análise AI Concluída
                                        </h6>
                                        <div className="text-xs text-green-600 bg-white px-3 py-1 rounded-full font-medium">
                                          {aiQualityData.get(tableKey)?.rules?.length || 0} regras geradas
                                        </div>
                                      </div>
                                      <div className="grid grid-cols-2 gap-4 text-sm">
                                        <div className="flex items-center gap-2">
                                          <span className="text-green-700">✅ Aprovadas:</span>
                                          <span className="font-bold text-green-800">
                                            {aiQualityData.get(tableKey)?.results?.filter((r: any) => r.status === 'PASS').length || 0}
                                          </span>
                                        </div>
                                        <div className="flex items-center gap-2">
                                          <span className="text-red-700">❌ Reprovadas:</span>
                                          <span className="font-bold text-red-800">
                                            {aiQualityData.get(tableKey)?.results?.filter((r: any) => r.status === 'FAIL').length || 0}
                                          </span>
                                        </div>
                                      </div>
                                      <div className="mt-2 text-xs text-gray-600">
                                        Powered by {aiQualityData.get(tableKey)?.provider || 'AI'} • 
                                        Gerado em {aiQualityData.get(tableKey)?.generatedAt ? new Date(aiQualityData.get(tableKey).generatedAt).toLocaleString('pt-BR') : 'agora'}
                                      </div>
                                    </div>

                                    {/* Data Quality Rules Results */}
                                    <div className="space-y-3">
                                      {aiQualityData.get(tableKey)?.rules?.map((rule: any, index: number) => {
                                        const result = aiQualityData.get(tableKey)?.results?.find((r: any) => r.ruleId === rule.id);
                                        const hasResult = !!result;
                                        
                                        return (
                                          <div key={rule.id} className="bg-white rounded-lg border border-gray-200 p-4 hover:shadow-sm transition-shadow">
                                            <div className="flex items-start justify-between mb-3">
                                              <div className="flex-1">
                                                <div className="flex items-center gap-2 mb-1">
                                                  <span className="text-sm font-medium text-gray-900">
                                                    {rule.name}
                                                  </span>
                                                  <span className={`px-2 py-1 rounded-full text-xs font-medium ${
                                                    rule.dimension === 'completeness' ? 'bg-blue-100 text-blue-700' :
                                                    rule.dimension === 'uniqueness' ? 'bg-purple-100 text-purple-700' :
                                                    rule.dimension === 'validity' ? 'bg-green-100 text-green-700' :
                                                    rule.dimension === 'consistency' ? 'bg-yellow-100 text-yellow-700' :
                                                    rule.dimension === 'accuracy' ? 'bg-red-100 text-red-700' :
                                                    rule.dimension === 'timeliness' ? 'bg-orange-100 text-orange-700' :
                                                    'bg-gray-100 text-gray-700'
                                                  }`}>
                                                    {rule.dimension}
                                                  </span>
                                                  <span className={`px-2 py-1 rounded-full text-xs font-medium ${
                                                    rule.severity === 'error' ? 'bg-red-100 text-red-700' :
                                                    rule.severity === 'warning' ? 'bg-yellow-100 text-yellow-700' :
                                                    'bg-blue-100 text-blue-700'
                                                  }`}>
                                                    {rule.severity}
                                                  </span>
                                                </div>
                                                <p className="text-sm text-gray-600 mb-2">{rule.description}</p>
                                                <div className="text-xs text-gray-500">
                                                  <span className="font-mono bg-gray-100 px-2 py-1 rounded">
                                                    {rule.column}
                                                  </span>
                                                  <span className="mx-2">•</span>
                                                  Esperado: {rule.expectedPassRate}% de aprovação
                                                </div>
                                              </div>
                                              
                                              {hasResult ? (
                                                <div className="flex items-center gap-2 ml-4">
                                                  {result.status === 'PASS' ? (
                                                    <div className="flex items-center gap-1 px-3 py-2 bg-green-50 rounded-lg border border-green-200">
                                                      <CheckCircle className="w-4 h-4 text-green-600" />
                                                      <span className="text-sm font-medium text-green-700">
                                                        {result.passRate.toFixed(1)}%
                                                      </span>
                                                    </div>
                                                  ) : (
                                                    <div className="flex items-center gap-1 px-3 py-2 bg-red-50 rounded-lg border border-red-200">
                                                      <XCircle className="w-4 h-4 text-red-600" />
                                                      <span className="text-sm font-medium text-red-700">
                                                        {result.passRate.toFixed(1)}%
                                                      </span>
                                                    </div>
                                                  )}
                                                </div>
                                              ) : (
                                                <div className="flex items-center gap-2 ml-4">
                                                  <div className="px-3 py-2 bg-gray-50 rounded-lg border border-gray-200">
                                                    <LoadingSpinner size="sm" className="text-gray-400" />
                                                  </div>
                                                </div>
                                              )}
                                            </div>
                                            
                                            {/* SQL Condition */}
                                            <div className="mt-3 pt-3 border-t border-gray-100">
                                              <div className="text-xs text-gray-500 mb-1">Condição SQL:</div>
                                              <code className="text-xs bg-gray-100 text-gray-800 px-2 py-1 rounded font-mono">
                                                {rule.sqlCondition}
                                              </code>
                                            </div>
                                            
                                            {/* Result Details */}
                                            {hasResult && (
                                              <div className="mt-3 pt-3 border-t border-gray-100">
                                                <div className="grid grid-cols-3 gap-3 text-xs">
                                                  <div>
                                                    <span className="text-gray-500">Registros Válidos:</span>
                                                    <div className="font-medium text-green-700">
                                                      {result.validRecords?.toLocaleString('pt-BR') || '0'}
                                                    </div>
                                                  </div>
                                                  <div>
                                                    <span className="text-gray-500">Registros Inválidos:</span>
                                                    <div className="font-medium text-red-700">
                                                      {result.invalidRecords?.toLocaleString('pt-BR') || '0'}
                                                    </div>
                                                  </div>
                                                  <div>
                                                    <span className="text-gray-500">Total Analisado:</span>
                                                    <div className="font-medium text-gray-700">
                                                      {result.totalRecords?.toLocaleString('pt-BR') || '0'}
                                                    </div>
                                                  </div>
                                                </div>
                                                {result.errorMessage && (
                                                  <div className="mt-2 p-2 bg-red-50 border border-red-200 rounded text-xs text-red-700">
                                                    <span className="font-medium">Erro:</span> {result.errorMessage}
                                                  </div>
                                                )}
                                              </div>
                                            )}
                                          </div>
                                        );
                                      })}
                                    </div>
                                    
                                    {/* Summary Footer */}
                                    <div className="mt-4 pt-4 border-t border-gray-200 text-center">
                                      <div className="text-sm text-gray-600">
                                        Total de {aiQualityData.get(tableKey)?.rules?.length || 0} regras aplicadas •
                                        Análise baseada em {details?.statistics?.totalRows?.toLocaleString('pt-BR') || '0'} registros
                                      </div>
                                    </div>
                                  </div>
                                ) : (
                                  <div className="text-center py-4 text-sm text-gray-500">
                                    Clique para expandir e carregar análise automática
                                  </div>
                                )}
                              </div>
                            )}
                          </div>

                        </div>
                      </div>
                    </div>
                  ) : (
                    <div className="p-6 text-center text-muted-foreground">
                      Erro ao carregar detalhes da tabela
                    </div>
                  )}
                </div>
              )}
            </div>
          );
        })}
      </div>

      {/* Footer */}
      <div className="text-center text-sm text-muted-foreground border-t border-border pt-4">
        <div className="flex items-center justify-center gap-2">
          <Calendar className="w-4 h-4" />
          <span>Última atualização: {new Date(tablesData.collectedAt).toLocaleString('pt-BR')}</span>
        </div>
      </div>

      {/* YAML Editor Modal */}
      <YamlEditor
        isOpen={yamlEditorOpen}
        onClose={closeYamlEditor}
        initialYaml={currentEditingTable ? yamlRules.get(`${currentEditingTable.schema}.${currentEditingTable.name}`) || '' : ''}
        tableName={currentEditingTable ? `${currentEditingTable.schema}.${currentEditingTable.name}` : ''}
        onSave={saveYamlRules}
      />

      {/* API Key Setup Modal */}
      <ApiKeySetup
        isOpen={apiKeySetupOpen}
        onClose={() => setApiKeySetupOpen(false)}
        onSave={saveApiKey}
        currentStatus={apiKeyStatus ? { openai: apiKeyStatus.openai, claude: apiKeyStatus.claude } : undefined}
      />

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