import React, { useState, useEffect, useRef } from 'react';
import { Activity, BarChart3, AlertCircle, CheckCircle, Play, RefreshCw, TrendingUp, Eye, ChevronDown, ChevronRight, AlertTriangle, Info } from 'lucide-react';
import { LoadingSpinner } from './LoadingSpinner';
import { DataQualityCharts } from './DataQualityCharts';

interface TableEssentialMetricsProps {
  schema: string;
  tableName: string;
  onToast?: (toast: { type: 'success' | 'error' | 'warning'; message: string }) => void;
}

interface TableMetrics {
  schema: string;
  tableName: string;
  collectedAt: string;
  general: {
    totalRows: number;
    totalColumns: number;
    estimatedSizeBytes: number;
    overallCompleteness: number;
    duplicateRows: number;
    duplicationRate: number;
    columnsWithNulls: number;
    duplicateDetails?: {
      totalDuplicates: number;
      duplicateGroups: any[];
      sampleDuplicateRows: string[];
    };
    columnsWithNullsSample: string[];
  };
  columns: ColumnMetrics[];
}

interface ColumnMetrics {
  columnName: string;
  dataType: string;
  isNullable: boolean;
  typeClassification: 'UniqueId' | 'Numeric' | 'DateTime' | 'Boolean' | 'Categorical' | 'Text' | 'Geographic' | 'Other';
  isUniqueIdentifier: boolean;
  totalValues: number;
  nullValues: number;
  completenessRate: number;
  uniqueValues: number;
  cardinalityRate: number;
  topValues: { value: string; count: number; percentage: number }[];
  sampleNullRows: string[];
  qualityAnomalies: QualityAnomaly[];
  distribution: {
    hasSuspiciousFrequency: boolean;
    hasPatternViolations: boolean;
    hasOutliers: boolean;
    uniformityScore: number;
    recommendedAction: string;
  };
  // Visualizações específicas por tipo
  timeline?: { period: string; count: number; percentage: number; label: string }[];
  geographicPoints?: { latitude: number; longitude: number; count: number; percentage: number; label: string }[];
  numeric?: {
    min: number;
    max: number;
    avg: number;
    median: number;
    stdDev: number;
    distribution?: HistogramBucket[];
  };
  date?: {
    min: string;
    max: string;
  };
  text?: {
    minLength: number;
    maxLength: number;
    avgLength: number;
  };
  boolean?: {
    trueCount: number;
    falseCount: number;
    nullCount: number;
    truePercentage: number;
    falsePercentage: number;
    nullPercentage: number;
  };
}

interface HistogramBucket {
  rangeStart: number;
  rangeEnd: number;
  count: number;
  percentage: number;
}

interface QualityAnomaly {
  type: string;
  description: string;
  value: string;
  count: number;
  severity: number;
  sampleRows: string[];
}

export function TableEssentialMetrics({ schema, tableName, onToast }: TableEssentialMetricsProps) {
  const [loading, setLoading] = useState(false);
  const [collecting, setCollecting] = useState(false);
  const [metrics, setMetrics] = useState<TableMetrics | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [showDetails, setShowDetails] = useState(false);
  const [expandedColumns, setExpandedColumns] = useState<Set<string>>(new Set());
  const abortControllerRef = useRef<AbortController | null>(null);

  useEffect(() => {
    loadExistingMetrics();
  }, [schema, tableName]);

  async function loadExistingMetrics() {
    setLoading(true);
    setError(null);

    try {
      // TODO: Implementar endpoint para buscar métricas existentes
      // const response = await fetch(`/api/essential-metrics/table/${schema}/${tableName}`);
      // if (response.ok) {
      //   const data = await response.json();
      //   setMetrics(data.data);
      // }

      // Por enquanto, não há métricas existentes
      setMetrics(null);
    } catch (err) {
      console.error('Error loading existing metrics:', err);
      setError('Erro ao carregar métricas existentes');
    } finally {
      setLoading(false);
    }
  }

  async function collectMetrics() {
    // Cancel previous request if still running
    abortControllerRef.current?.abort();
    const controller = new AbortController();
    abortControllerRef.current = controller;

    setCollecting(true);
    setError(null);

    try {
      console.log('🔍 Coletando métricas para:', { schema, tableName });

      const response = await fetch('/api/essential-metrics/collect-basic', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ schema, tableName }),
        signal: controller.signal,
      });

      console.log('📡 Response status:', response.status);

      if (!response.ok) {
        throw new Error(`Erro na API: ${response.status} - ${response.statusText || 'Erro de conexão'}`);
      }

      const result = await response.json();
      console.log('📋 Result:', result);

      if (result.success) {
        // Processar dados reais do backend
        const processedMetrics: TableMetrics = {
          schema,
          tableName,
          collectedAt: result.data?.collectedAt || new Date().toISOString(),
          general: result.data?.general || {
            totalRows: 0,
            totalColumns: 0,
            estimatedSizeBytes: 0,
            overallCompleteness: 0,
            duplicateRows: 0,
            duplicationRate: 0,
            columnsWithNulls: 0,
            columnsWithNullsSample: []
          },
          columns: result.data?.columns || []
        };

        setMetrics(processedMetrics);
        onToast?.({ type: 'success', message: 'Métricas essenciais coletadas com sucesso!' });
      } else {
        setError(result.message || 'Falha ao coletar métricas');
        onToast?.({ type: 'error', message: result.message || 'Falha ao coletar métricas' });
      }
    } catch (err: any) {
      if (err.name === 'AbortError') {
        console.log('Request was aborted');
        return;
      }
      console.error('Error collecting metrics:', err);
      const errorMessage = err.name === 'TypeError' && err.message.includes('Failed to fetch')
        ? 'Erro de conexão - Backend não está rodando na porta 5000'
        : err.message || 'Erro ao coletar métricas';
      setError(errorMessage);
      onToast?.({ type: 'error', message: errorMessage });
    } finally {
      if (!controller.signal.aborted) {
        setCollecting(false);
      }
    }
  }

  function formatBytes(bytes: number): string {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }

  function formatDate(dateString: string): string {
    return new Date(dateString).toLocaleString('pt-BR');
  }

  return (
    <div className="bg-gradient-to-br from-blue-50 to-indigo-50 rounded-xl border-2 border-blue-200 mb-6">
      <div className="p-6">
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center gap-3">
            <div className="w-12 h-12 bg-gradient-to-br from-blue-500 to-indigo-600 rounded-xl flex items-center justify-center">
              <Activity className="w-6 h-6 text-white" />
            </div>
            <div>
              <h5 className="text-lg font-semibold text-gray-900 flex items-center gap-2">
                📊 Métricas Essenciais
                {metrics && (
                  <span className="text-xs bg-green-100 text-green-700 px-2 py-1 rounded-full font-medium">
                    Coletadas
                  </span>
                )}
              </h5>
              <p className="text-sm text-gray-600">
                Análise básica sem IA • Contagens, completude e estatísticas
              </p>
            </div>
          </div>

          <div className="flex items-center gap-2">
            {metrics && (
              <button
                onClick={loadExistingMetrics}
                disabled={loading}
                className="p-2 text-blue-600 hover:bg-blue-100 rounded-lg transition-colors"
                title="Atualizar métricas"
              >
                <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
              </button>
            )}
            <button
              onClick={collectMetrics}
              disabled={collecting}
              className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:bg-gray-400 disabled:cursor-not-allowed flex items-center gap-2 transition-colors text-sm font-medium"
            >
              {collecting ? (
                <>
                  <LoadingSpinner size="sm" />
                  Coletando...
                </>
              ) : (
                <>
                  <Play className="w-4 h-4" />
                  {metrics ? 'Recoletar' : 'Coletar Métricas'}
                </>
              )}
            </button>
          </div>
        </div>

        {error && (
          <div className="mb-4 p-4 bg-red-50 border border-red-200 rounded-lg">
            <div className="flex items-center gap-2 text-red-800">
              <AlertCircle className="w-5 h-5" />
              <span className="font-medium">Erro:</span>
              <span>{error}</span>
            </div>
          </div>
        )}

        {loading ? (
          <div className="text-center py-8">
            <LoadingSpinner size="lg" className="mb-4 text-blue-600" />
            <p className="text-gray-600">Carregando métricas...</p>
          </div>
        ) : metrics ? (
          <div className="space-y-4">
            <div className="bg-white rounded-lg p-4 border border-blue-200">
              <div className="flex items-start justify-between mb-3">
                <div>
                  <h6 className="font-semibold text-gray-900">
                    {metrics.schema}.{metrics.tableName}
                  </h6>
                  <p className="text-sm text-gray-600">
                    Coletado em: {formatDate(metrics.collectedAt)}
                  </p>
                </div>
                <div className="flex items-center gap-1 text-green-600">
                  <CheckCircle className="w-4 h-4" />
                  <span className="text-sm font-medium">Completo</span>
                </div>
              </div>

              <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                <div className="text-center p-3 bg-blue-50 rounded-lg">
                  <div className="text-2xl font-bold text-blue-600">
                    {metrics.general.totalRows.toLocaleString()}
                  </div>
                  <div className="text-sm text-blue-700">Linhas</div>
                </div>

                <div className="text-center p-3 bg-green-50 rounded-lg">
                  <div className="text-2xl font-bold text-green-600">
                    {metrics.general.totalColumns}
                  </div>
                  <div className="text-sm text-green-700">Colunas</div>
                </div>

                <div className="text-center p-3 bg-purple-50 rounded-lg">
                  <div className="text-2xl font-bold text-purple-600">
                    {formatBytes(metrics.general.estimatedSizeBytes)}
                  </div>
                  <div className="text-sm text-purple-700">Tamanho</div>
                </div>

                <div className="text-center p-3 bg-orange-50 rounded-lg">
                  <div className="text-2xl font-bold text-orange-600">
                    {metrics.general.overallCompleteness.toFixed(1)}%
                  </div>
                  <div className="text-sm text-orange-700">Completude</div>
                </div>
              </div>

              {/* Problemas encontrados */}
              {(metrics.general.duplicateRows > 0 || metrics.general.columnsWithNulls > 0) && (
                <div className="mt-4 space-y-3">
                  {metrics.general.duplicateRows > 0 && (
                    <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-4">
                      <div className="flex items-center justify-between mb-2">
                        <div className="flex items-center gap-2 text-yellow-800">
                          <AlertCircle className="w-5 h-5" />
                          <span className="font-medium">
                            {metrics.general.duplicateRows.toLocaleString()} linhas duplicadas encontradas
                          </span>
                        </div>
                        <button
                          onClick={() => setShowDetails(!showDetails)}
                          className="text-yellow-700 hover:text-yellow-900 flex items-center gap-1"
                        >
                          {showDetails ? <ChevronDown className="w-4 h-4" /> : <ChevronRight className="w-4 h-4" />}
                          <span className="text-sm">Detalhes</span>
                        </button>
                      </div>
                      {showDetails && metrics.general.duplicateDetails && (
                        <div className="text-sm text-yellow-700 space-y-2">
                          <div>Taxa de duplicação: {metrics.general.duplicationRate.toFixed(2)}%</div>
                          {metrics.general.duplicateDetails.sampleDuplicateRows.length > 0 && (
                            <div>
                              <span className="font-medium">Exemplos de linhas duplicadas:</span>
                              <ul className="list-disc list-inside ml-4 mt-1">
                                {metrics.general.duplicateDetails.sampleDuplicateRows.slice(0, 3).map((row, i) => (
                                  <li key={i}>{row}</li>
                                ))}
                              </ul>
                            </div>
                          )}
                        </div>
                      )}
                    </div>
                  )}

                  {metrics.general.columnsWithNulls > 0 && (
                    <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
                      <div className="flex items-center gap-2 text-blue-800 mb-2">
                        <Info className="w-5 h-5" />
                        <span className="font-medium">
                          {metrics.general.columnsWithNulls} colunas com valores nulos
                        </span>
                      </div>
                      <div className="text-sm text-blue-700">
                        <span className="font-medium">Colunas afetadas: </span>
                        {metrics.general.columnsWithNullsSample.slice(0, 5).join(', ')}
                        {metrics.general.columnsWithNullsSample.length > 5 && ' ...'}
                      </div>
                    </div>
                  )}
                </div>
              )}

              {/* Análise por Colunas */}
              {metrics.columns && metrics.columns.length > 0 && (
                <div className="mt-6 bg-white rounded-lg border border-blue-200 p-4">
                  <h6 className="text-lg font-semibold text-gray-900 mb-4 flex items-center gap-2">
                    <BarChart3 className="w-5 h-5 text-blue-600" />
                    Análise Detalhada por Colunas
                  </h6>

                  <div className="space-y-3">
                    {metrics.columns.map((column, index) => {
                      const isExpanded = expandedColumns.has(column.columnName);
                      const hasAnomalies = column.qualityAnomalies && column.qualityAnomalies.length > 0;
                      const hasNulls = column.nullValues > 0;

                      return (
                        <div key={index} className="border border-gray-200 rounded-lg p-3">
                          <div
                            className="flex items-center justify-between cursor-pointer"
                            onClick={() => {
                              const newExpanded = new Set(expandedColumns);
                              if (isExpanded) {
                                newExpanded.delete(column.columnName);
                              } else {
                                newExpanded.add(column.columnName);
                              }
                              setExpandedColumns(newExpanded);
                            }}
                          >
                            <div className="flex items-center gap-3">
                              <div className="flex items-center gap-1">
                                {isExpanded ? <ChevronDown className="w-4 h-4" /> : <ChevronRight className="w-4 h-4" />}
                                <span className="font-medium text-gray-900">{column.columnName}</span>
                                <span className="text-xs bg-gray-100 text-gray-600 px-2 py-1 rounded">{column.dataType}</span>
                                {/* Indicador do tipo de coluna */}
                                {column.typeClassification === 'UniqueId' && (
                                  <span className="text-xs bg-green-100 text-green-700 px-2 py-1 rounded font-medium">🆔 ID</span>
                                )}
                                {column.typeClassification === 'Numeric' && (
                                  <span className="text-xs bg-blue-100 text-blue-700 px-2 py-1 rounded font-medium">🔢 Numérica</span>
                                )}
                                {column.typeClassification === 'DateTime' && (
                                  <span className="text-xs bg-purple-100 text-purple-700 px-2 py-1 rounded font-medium">📅 Data</span>
                                )}
                                {column.typeClassification === 'Boolean' && (
                                  <span className="text-xs bg-green-100 text-green-700 px-2 py-1 rounded font-medium">🔘 Booleana</span>
                                )}
                                {column.typeClassification === 'Categorical' && (
                                  <span className="text-xs bg-orange-100 text-orange-700 px-2 py-1 rounded font-medium">🏷️ Categórica</span>
                                )}
                                {column.typeClassification === 'Text' && (
                                  <span className="text-xs bg-yellow-100 text-yellow-700 px-2 py-1 rounded font-medium">📝 Texto</span>
                                )}
                                {column.typeClassification === 'Geographic' && (
                                  <span className="text-xs bg-green-100 text-green-700 px-2 py-1 rounded font-medium">🌍 Geográfica</span>
                                )}
                              </div>
                              <div className="flex items-center gap-2">
                                {hasAnomalies && (
                                  <span className="text-xs bg-red-100 text-red-700 px-2 py-1 rounded font-medium">
                                    {column.qualityAnomalies.length} anomalias
                                  </span>
                                )}
                                {hasNulls && (
                                  <span className="text-xs bg-yellow-100 text-yellow-700 px-2 py-1 rounded">
                                    {column.nullValues} nulos
                                  </span>
                                )}
                              </div>
                            </div>
                            <div className="text-sm text-gray-600">
                              {column.completenessRate.toFixed(1)}% completo
                            </div>
                          </div>

                          {isExpanded && (
                            <div className="mt-3 space-y-4 border-t border-gray-100 pt-3">
                              {/* Estatísticas básicas */}
                              <div className="grid grid-cols-2 md:grid-cols-4 gap-3 text-sm">
                                <div className="bg-gray-50 p-2 rounded">
                                  <div className="text-gray-600">Valores únicos</div>
                                  <div className="font-medium">{column.uniqueValues.toLocaleString()}</div>
                                </div>
                                <div className="bg-gray-50 p-2 rounded">
                                  <div className="text-gray-600">Cardinalidade</div>
                                  <div className="font-medium">{column.cardinalityRate.toFixed(1)}%</div>
                                </div>
                                <div className="bg-gray-50 p-2 rounded">
                                  <div className="text-gray-600">Nulos</div>
                                  <div className="font-medium">{column.nullValues.toLocaleString()}</div>
                                </div>
                                <div className="bg-gray-50 p-2 rounded">
                                  <div className="text-gray-600">Completude</div>
                                  <div className="font-medium">{column.completenessRate.toFixed(1)}%</div>
                                </div>
                              </div>

                              {/* Anomalias de qualidade */}
                              {hasAnomalies && (
                                <div className="bg-red-50 border border-red-200 rounded-lg p-3">
                                  <h7 className="font-medium text-red-800 mb-2 flex items-center gap-1">
                                    <AlertTriangle className="w-4 h-4" />
                                    Anomalias Detectadas
                                  </h7>
                                  <div className="space-y-2">
                                    {column.qualityAnomalies.map((anomaly, aIndex) => (
                                      <div key={aIndex} className="text-sm">
                                        <div className="font-medium text-red-700">{anomaly.description}</div>
                                        <div className="text-red-600 text-xs">
                                          Valor: "{anomaly.value}" • Frequência: {anomaly.count} •
                                          Severidade: {(anomaly.severity * 100).toFixed(0)}%
                                        </div>
                                      </div>
                                    ))}
                                  </div>
                                </div>
                              )}

                              {/* Estatísticas específicas por tipo de coluna */}
                              {column.typeClassification === 'UniqueId' && column.isUniqueIdentifier && (
                                <div className="bg-green-50 border border-green-200 rounded-lg p-3">
                                  <div className="flex items-center gap-2">
                                    <div className="w-2 h-2 bg-green-500 rounded-full"></div>
                                    <span className="font-medium text-green-800">✅ Campo 100% único</span>
                                  </div>
                                </div>
                              )}

                              {/* Estatísticas numéricas */}
                              {column.typeClassification === 'Numeric' && column.numeric && (
                                <div className="bg-blue-50 rounded-lg p-3">
                                  <h7 className="font-medium text-blue-800 mb-2">Estatísticas Numéricas</h7>
                                  <div className="grid grid-cols-2 gap-2 text-sm">
                                    <div><span className="font-medium">Mínimo:</span> {column.numeric.min.toLocaleString()}</div>
                                    <div><span className="font-medium">Máximo:</span> {column.numeric.max.toLocaleString()}</div>
                                    <div><span className="font-medium">Média:</span> {column.numeric.avg.toLocaleString()}</div>
                                    <div><span className="font-medium">Mediana:</span> {column.numeric.median.toLocaleString()}</div>
                                    <div className="col-span-2"><span className="font-medium">Desvio Padrão:</span> {column.numeric.stdDev.toFixed(2)}</div>
                                  </div>
                                </div>
                              )}

                              {/* Estatísticas de data */}
                              {column.typeClassification === 'DateTime' && column.date && (
                                <div className="bg-purple-50 rounded-lg p-3">
                                  <h7 className="font-medium text-purple-800 mb-2">Intervalo Temporal</h7>
                                  <div className="space-y-1 text-sm">
                                    <div><span className="font-medium">Data mais antiga:</span> {new Date(column.date.min).toLocaleDateString()}</div>
                                    <div><span className="font-medium">Data mais recente:</span> {new Date(column.date.max).toLocaleDateString()}</div>
                                  </div>
                                </div>
                              )}

                              {/* Estatísticas de texto */}
                              {column.typeClassification === 'Text' && column.text && (
                                <div className="bg-yellow-50 rounded-lg p-3">
                                  <h7 className="font-medium text-yellow-800 mb-2">Estatísticas de Texto</h7>
                                  <div className="grid grid-cols-3 gap-2 text-sm">
                                    <div><span className="font-medium">Min:</span> {column.text.minLength}</div>
                                    <div><span className="font-medium">Max:</span> {column.text.maxLength}</div>
                                    <div><span className="font-medium">Médio:</span> {column.text.avgLength.toFixed(1)}</div>
                                  </div>
                                </div>
                              )}

                              {/* Estatísticas booleanas */}
                              {column.typeClassification === 'Boolean' && column.boolean && (
                                <div className="bg-green-50 rounded-lg p-3">
                                  <h7 className="font-medium text-green-800 mb-3">Distribuição Booleana</h7>
                                  <div className="grid grid-cols-3 gap-3 text-sm">
                                    <div className="text-center p-2 bg-white rounded border border-green-200">
                                      <div className="text-lg font-bold text-green-600">{column.boolean.trueCount.toLocaleString()}</div>
                                      <div className="text-xs text-green-700">True ({column.boolean.truePercentage.toFixed(1)}%)</div>
                                    </div>
                                    <div className="text-center p-2 bg-white rounded border border-red-200">
                                      <div className="text-lg font-bold text-red-600">{column.boolean.falseCount.toLocaleString()}</div>
                                      <div className="text-xs text-red-700">False ({column.boolean.falsePercentage.toFixed(1)}%)</div>
                                    </div>
                                    {column.boolean.nullCount > 0 && (
                                      <div className="text-center p-2 bg-white rounded border border-gray-200">
                                        <div className="text-lg font-bold text-gray-600">{column.boolean.nullCount.toLocaleString()}</div>
                                        <div className="text-xs text-gray-700">Null ({column.boolean.nullPercentage.toFixed(1)}%)</div>
                                      </div>
                                    )}
                                  </div>
                                  {Math.abs(column.boolean.truePercentage - column.boolean.falsePercentage) > 70 && (
                                    <div className="mt-2 text-xs text-orange-600 bg-orange-50 p-2 rounded">
                                      ⚠️ Distribuição muito desbalanceada - {column.boolean.truePercentage > column.boolean.falsePercentage ? 'muitos True' : 'muitos False'}
                                    </div>
                                  )}
                                </div>
                              )}

                              {/* Top valores (RESTAURADO: agora mostra para categóricas, numéricas e texto) */}
                              {column.topValues && column.topValues.length > 0 &&
                               ['Categorical', 'Numeric', 'Text'].includes(column.typeClassification) && (
                                <div className="bg-gray-50 rounded-lg p-3">
                                  <h7 className="font-medium text-gray-800 mb-2">🏆 Top 5 Valores Mais Frequentes</h7>
                                  <div className="space-y-2">
                                    {column.topValues.slice(0, 5).map((value, vIndex) => (
                                      <div key={vIndex} className="flex items-center justify-between text-sm bg-white p-2 rounded border">
                                        <div className="flex items-center gap-2">
                                          <span className="w-5 h-5 bg-blue-100 text-blue-700 rounded-full text-xs flex items-center justify-center font-medium">
                                            {vIndex + 1}
                                          </span>
                                          <span className="font-mono text-gray-700 truncate max-w-xs">
                                            "{value.value}"
                                          </span>
                                        </div>
                                        <div className="text-right">
                                          <div className="font-medium text-gray-900">{value.count.toLocaleString()}</div>
                                          <div className="text-xs text-gray-600">{value.percentage.toFixed(1)}%</div>
                                        </div>
                                      </div>
                                    ))}
                                  </div>
                                  {column.topValues.length > 5 && (
                                    <div className="text-xs text-gray-500 mt-2">
                                      ... e mais {column.topValues.length - 5} valores diferentes
                                    </div>
                                  )}
                                </div>
                              )}

                              {/* Recomendações */}
                              {column.distribution?.recommendedAction && (
                                <div className="bg-blue-50 border border-blue-200 rounded-lg p-3">
                                  <h7 className="font-medium text-blue-800 mb-1">Recomendação</h7>
                                  <div className="text-sm text-blue-700">
                                    {column.distribution.recommendedAction}
                                  </div>
                                </div>
                              )}

                              {/* NOVO: Gráficos visuais de distribuição */}
                              <DataQualityCharts
                                columnName={column.columnName}
                                typeClassification={column.typeClassification}
                                histogram={column.numeric?.distribution}
                                booleanStats={column.boolean}
                                topValues={column.topValues}
                                timeline={column.timeline}
                                geographicPoints={column.geographicPoints}
                              />
                            </div>
                          )}
                        </div>
                      );
                    })}
                  </div>
                </div>
              )}
            </div>
          </div>
        ) : (
          <div className="text-center py-8 bg-white rounded-lg border border-blue-200">
            <div className="w-12 h-12 mx-auto bg-blue-100 rounded-full flex items-center justify-center mb-4">
              <BarChart3 className="w-6 h-6 text-blue-600" />
            </div>
            <h6 className="text-lg font-medium text-gray-900 mb-2">
              Métricas não coletadas
            </h6>
            <p className="text-gray-600">
              Clique em "Coletar Métricas" para analisar esta tabela.
            </p>
          </div>
        )}
      </div>
    </div>
  );
}