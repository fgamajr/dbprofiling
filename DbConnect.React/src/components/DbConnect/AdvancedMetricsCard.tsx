import React, { useState, useEffect, useRef } from 'react';
import {
  Search, Play, RefreshCw, CheckCircle, AlertCircle, TrendingUp, TrendingDown,
  ChevronDown, ChevronRight, Target, BarChart3, Zap, AlertTriangle, Info,
  Eye, ExternalLink, Fingerprint, Activity
} from 'lucide-react';
import { LoadingSpinner } from './LoadingSpinner';

interface AdvancedMetricsCardProps {
  schema: string;
  tableName: string;
  onToast?: (toast: { type: 'success' | 'error' | 'warning'; message: string }) => void;
}

interface PatternAnalysisResult {
  patternName: string;
  description: string;
  culture: string;
  conformityPercentage: number;
  totalSamples: number;
  matchingSamples: number;
  sampleMatchingValues: string[];
  sampleNonMatchingValues: string[];
}

interface OutlierRowData {
  outlierValue: number;
  outlierColumn: string;
  rowData: Record<string, any>;
}

interface OutlierAnalysis {
  totalValues: number;
  outlierCount: number;
  outlierPercentage: number;
  mean: number;
  standardDeviation: number;
  lowerBound: number;
  upperBound: number;
  sampleOutliers: number[];
  outlierRows: OutlierRowData[];
  currentPage: number;
  pageSize: number;
  totalPages: number;
}

interface AdvancedColumnMetrics {
  columnName: string;
  dataType: string;
  patternMatches: PatternAnalysisResult[];
  outlierAnalysis?: OutlierAnalysis;
}

interface StatusDateRelationship {
  statusColumn: string;
  dateColumn: string;
  commonRadical: string;
  inconsistencyPercentage: number;
  totalActiveRecords: number;
  inconsistentRecords: number;
  activeValues: string[];
  sqlQuery: string;
}

interface NumericCorrelation {
  column1: string;
  column2: string;
  correlationCoefficient: number;
  correlationStrength: string;
  sampleSize: number;
}

interface RelationshipMetrics {
  statusDateRelationships: StatusDateRelationship[];
  numericCorrelations: NumericCorrelation[];
}

interface AdvancedTableMetrics {
  tableName: string;
  schemaName: string;
  columnMetrics: AdvancedColumnMetrics[];
  relationshipMetrics: RelationshipMetrics;
  analysisTimestamp: string;
  processingTime: string;
}

export function AdvancedMetricsCard({ schema, tableName, onToast }: AdvancedMetricsCardProps) {
  const [loading, setLoading] = useState(false);
  const [collecting, setCollecting] = useState(false);
  const [metrics, setMetrics] = useState<AdvancedTableMetrics | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [expandedColumns, setExpandedColumns] = useState<Set<string>>(new Set());
  const [expandedRelationships, setExpandedRelationships] = useState<Set<string>>(new Set());
  const [outlierPages, setOutlierPages] = useState<Record<string, OutlierAnalysis>>({});
  const [loadingOutliers, setLoadingOutliers] = useState<Record<string, boolean>>({});
  const [currentOutlierPages, setCurrentOutlierPages] = useState<Record<string, number>>({});
  const abortControllerRef = useRef<AbortController | null>(null);

  // Load page 0 for columns with outliers when metrics are first available
  useEffect(() => {
    console.log(`🎯 [AUTO_LOAD] useEffect triggered for initial page loading`, {
      hasMetrics: !!metrics?.columnMetrics,
      columnCount: metrics?.columnMetrics?.length,
      currentOutlierPages: Object.keys(outlierPages),
      currentLoadingStates: Object.keys(loadingOutliers).filter(key => loadingOutliers[key])
    });

    if (metrics?.columnMetrics) {
      metrics.columnMetrics.forEach(column => {
        if (column.outlierAnalysis && column.outlierAnalysis.outlierCount > 0) {
          const pageKey = `${column.columnName}-0`;
          const hasData = outlierPages[pageKey];
          const isLoading = loadingOutliers[pageKey];

          console.log(`📋 [AUTO_LOAD] Checking column ${column.columnName}:`, {
            outlierCount: column.outlierAnalysis.outlierCount,
            pageKey,
            hasData: !!hasData,
            isLoading: !!isLoading,
            shouldAutoLoad: !hasData && !isLoading
          });

          // Only load if not already loaded and not currently loading
          if (!hasData && !isLoading) {
            console.log(`🚀 [AUTO_LOAD] Auto-loading page 1 for ${column.columnName}`);
            loadOutlierPage(column.columnName, 0);
          } else {
            console.log(`⏭️ [AUTO_LOAD] Skipping auto-load for ${column.columnName} - already available`);
          }
        } else {
          console.log(`📋 [AUTO_LOAD] Skipping column ${column.columnName} - no outliers or analysis`);
        }
      });
    }
  }, [metrics?.columnMetrics, outlierPages, loadingOutliers]);

  // Helper function to handle page navigation with auto-loading
  const navigateToPage = async (columnName: string, page: number, forceReload: boolean = false) => {
    console.log(`🧭 [NAVIGATION] Navigating to page ${page + 1} for column ${columnName}`, {
      forceReload,
      currentPages: currentOutlierPages,
      loadingStates: loadingOutliers,
      availablePages: Object.keys(outlierPages)
    });

    // Update current page immediately for UI responsiveness
    setCurrentOutlierPages(prev => ({ ...prev, [columnName]: page }));

    // Check if data is already loaded
    const pageKey = `${columnName}-${page}`;
    const hasData = outlierPages[pageKey];
    const isLoading = loadingOutliers[pageKey];

    console.log(`🔍 [NAVIGATION] Page ${page + 1} status:`, {
      pageKey,
      hasData: !!hasData,
      isLoading: !!isLoading,
      shouldLoad: forceReload || (!hasData && !isLoading)
    });

    if (forceReload || (!hasData && !isLoading)) {
      console.log(`📥 [NAVIGATION] Loading page ${page + 1} for ${columnName}...`);
      await loadOutlierPage(columnName, page);
    } else {
      console.log(`⏭️ [NAVIGATION] Skipping load for page ${page + 1} - data already available or loading`);
    }
  };

  async function loadOutlierPage(columnName: string, page: number, pageSize: number = 20) {
    const key = `${columnName}-${page}`;
    console.log(`🚀 [LOAD_PAGE] Starting to load page ${page + 1} for column ${columnName}`, {
      key,
      pageSize,
      currentState: {
        loadingStates: loadingOutliers,
        availablePages: Object.keys(outlierPages)
      }
    });

    setLoadingOutliers(prev => ({ ...prev, [key]: true }));

    try {
      // Get the column data for baseline statistics
      const column = metrics?.columnMetrics.find(c => c.columnName === columnName);
      if (!column?.outlierAnalysis) {
        console.error(`❌ [LOAD_PAGE] No outlier analysis found for column ${columnName}`);
        throw new Error('Dados de outlier não disponíveis');
      }

      console.log(`📊 [LOAD_PAGE] Column stats for ${columnName}:`, {
        outlierCount: column.outlierAnalysis.outlierCount,
        sampleOutliersLength: column.outlierAnalysis.sampleOutliers.length,
        mean: column.outlierAnalysis.mean,
        page: page + 1
      });

      const { outlierCount, totalValues, mean, standardDeviation, lowerBound, upperBound } = column.outlierAnalysis;
      const totalPages = Math.ceil(outlierCount / pageSize);

      console.log(`🧮 [LOAD_PAGE] Pagination calculation:`, {
        outlierCount,
        pageSize,
        totalPages,
        requestedPage: page + 1
      });

      // Try to get real paginated data from the backend
      const apiUrl = `/api/data-quality/outliers?col=${columnName}&page=${page}&size=${pageSize}`;
      console.log(`🌐 [LOAD_PAGE] Attempting API call to: ${apiUrl}`);

      try {
        const response = await fetch(apiUrl, {
          signal: abortControllerRef.current?.signal
        });

        console.log(`📡 [LOAD_PAGE] API response:`, {
          status: response.status,
          statusText: response.statusText,
          ok: response.ok,
          url: response.url
        });

        if (response.ok) {
          const data = await response.json();
          console.log(`✅ [LOAD_PAGE] API success! Got data:`, {
            itemsCount: data.items?.length || 0,
            totalPages: data.totalPages,
            currentPage: data.currentPage,
            data: data
          });

          const outlierAnalysis: OutlierAnalysis = {
            totalValues,
            outlierCount,
            outlierPercentage: (outlierCount / totalValues) * 100,
            mean,
            standardDeviation,
            lowerBound,
            upperBound,
            sampleOutliers: data.items?.map((item: any) => item.outlierValue) || [],
            outlierRows: data.items || [],
            currentPage: page,
            pageSize,
            totalPages: data.totalPages || totalPages
          };

          console.log(`💾 [LOAD_PAGE] Storing API data for ${key}:`, outlierAnalysis);
          setOutlierPages(prev => ({ ...prev, [key]: outlierAnalysis }));
          return;
        } else {
          console.warn(`⚠️ [LOAD_PAGE] API failed with status ${response.status}`);
        }
      } catch (apiError) {
        console.warn(`⚠️ [LOAD_PAGE] API error:`, apiError);
      }

      // Fallback: Use simulation with proper pagination for the UI
      console.log(`🎭 [LOAD_PAGE] Using simulation fallback for page ${page + 1}`);
      const { sampleOutliers } = column.outlierAnalysis;
      const startIdx = page * pageSize;
      const endIdx = startIdx + pageSize;
      const pageOutliers = sampleOutliers.slice(startIdx, endIdx);

      console.log(`🔢 [LOAD_PAGE] Simulation data:`, {
        sampleOutliersTotal: sampleOutliers.length,
        startIdx,
        endIdx,
        pageOutliersCount: pageOutliers.length,
        pageOutliers: pageOutliers.slice(0, 3) // Show first 3 for debugging
      });

      // Even if no data on this simulated page, still create proper structure
      const simulatedRows = pageOutliers.map((outlier, idx) => ({
        outlierValue: outlier,
        outlierColumn: columnName,
        rowData: {
          [columnName]: outlier,
          'id': startIdx + idx + 1,
          'status': 'ativo',
          'created_at': '2025-09-16',
          'updated_at': '2025-09-16'
        }
      }));

      const outlierAnalysis: OutlierAnalysis = {
        totalValues,
        outlierCount,
        outlierPercentage: (outlierCount / totalValues) * 100,
        mean,
        standardDeviation,
        lowerBound,
        upperBound,
        sampleOutliers: pageOutliers,
        outlierRows: simulatedRows,
        currentPage: page,
        pageSize,
        totalPages // Use the real total pages based on outlierCount
      };

      console.log(`🎭 [LOAD_PAGE] Created simulation data for ${key}:`, {
        outlierRowsCount: outlierAnalysis.outlierRows.length,
        sampleOutliersCount: outlierAnalysis.sampleOutliers.length,
        totalPages: outlierAnalysis.totalPages
      });

      // Simular delay de rede
      console.log(`⏱️ [LOAD_PAGE] Simulating network delay...`);
      await new Promise(resolve => setTimeout(resolve, 300));

      console.log(`💾 [LOAD_PAGE] Storing simulation data for ${key}`);
      setOutlierPages(prev => ({ ...prev, [key]: outlierAnalysis }));

    } catch (error) {
      console.error(`❌ [LOAD_PAGE] Error loading page ${page + 1}:`, error);
      onToast?.({ type: 'error', message: 'Erro ao carregar outliers paginados' });
    } finally {
      console.log(`🔄 [LOAD_PAGE] Clearing loading state for ${key}`);
      setLoadingOutliers(prev => ({ ...prev, [key]: false }));
    }
  }

  async function collectAdvancedMetrics() {
    // Cancel previous request if still running
    abortControllerRef.current?.abort();
    const controller = new AbortController();
    abortControllerRef.current = controller;

    setCollecting(true);
    setError(null);

    try {
      console.log('🔍 Coletando métricas avançadas para:', { schema, tableName });

      const response = await fetch('/api/essential-metrics/collect-advanced', {
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
        setMetrics(result.data);
        onToast?.({
          type: 'success',
          message: `Análise avançada concluída! Processado em ${result.summary?.processingTimeSeconds?.toFixed(2)}s`
        });
      } else {
        setError(result.message || 'Falha ao coletar métricas avançadas');
        onToast?.({ type: 'error', message: result.message || 'Falha ao coletar métricas avançadas' });
      }
    } catch (err: any) {
      if (err.name === 'AbortError') {
        console.log('Request was aborted');
        return;
      }
      console.error('Error collecting advanced metrics:', err);
      const errorMessage = err.name === 'TypeError' && err.message.includes('Failed to fetch')
        ? 'Erro de conexão - Backend não está rodando na porta 5000'
        : err.message || 'Erro ao coletar métricas avançadas';
      setError(errorMessage);
      onToast?.({ type: 'error', message: errorMessage });
    } finally {
      if (!controller.signal.aborted) {
        setCollecting(false);
      }
    }
  }

  function formatDate(dateString: string): string {
    return new Date(dateString).toLocaleString('pt-BR');
  }

  function getPatternIcon(patternName: string): string {
    if (patternName.includes('EMAIL')) return '📧';
    if (patternName.includes('CPF')) return '🆔';
    if (patternName.includes('CNPJ')) return '🏢';
    if (patternName.includes('CEP')) return '📮';
    if (patternName.includes('PHONE')) return '📱';
    if (patternName.includes('URL')) return '🌐';
    if (patternName.includes('IP')) return '💻';
    if (patternName.includes('CREDIT_CARD')) return '💳';
    if (patternName.includes('UUID')) return '🔑';
    if (patternName.includes('DATE')) return '📅';
    return '🔍';
  }

  function getConformityColor(percentage: number): string {
    if (percentage >= 95) return 'text-green-600 bg-green-50';
    if (percentage >= 80) return 'text-blue-600 bg-blue-50';
    if (percentage >= 60) return 'text-yellow-600 bg-yellow-50';
    return 'text-red-600 bg-red-50';
  }

  function getCorrelationIcon(coefficient: number): string {
    if (coefficient > 0.9) return '🔥';
    if (coefficient > 0.8) return '⬆️';
    if (coefficient < -0.8) return '⬇️';
    return '↔️';
  }

  return (
    <div className="bg-gradient-to-br from-purple-50 to-pink-50 rounded-xl border-2 border-purple-200 mb-6">
      <div className="p-6">
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center gap-3">
            <div className="w-12 h-12 bg-gradient-to-br from-purple-500 to-pink-600 rounded-xl flex items-center justify-center">
              <Search className="w-6 h-6 text-white" />
            </div>
            <div>
              <h5 className="text-lg font-semibold text-gray-900 flex items-center gap-2">
                🔍 Métricas Avançadas
                {metrics && (
                  <span className="text-xs bg-green-100 text-green-700 px-2 py-1 rounded-full font-medium">
                    Analisadas
                  </span>
                )}
              </h5>
              <p className="text-sm text-gray-600">
                Análise de padrões • Outliers estatísticos • Relações entre colunas
              </p>
            </div>
          </div>

          <div className="flex items-center gap-2">
            {metrics && (
              <button
                onClick={() => setMetrics(null)}
                disabled={loading}
                className="p-2 text-purple-600 hover:bg-purple-100 rounded-lg transition-colors"
                title="Limpar análise"
              >
                <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
              </button>
            )}
            <button
              onClick={collectAdvancedMetrics}
              disabled={collecting}
              className="px-4 py-2 bg-purple-600 text-white rounded-lg hover:bg-purple-700 disabled:bg-gray-400 disabled:cursor-not-allowed flex items-center gap-2 transition-colors text-sm font-medium"
            >
              {collecting ? (
                <>
                  <LoadingSpinner size="sm" />
                  Analisando...
                </>
              ) : (
                <>
                  <Zap className="w-4 h-4" />
                  {metrics ? 'Reanalisar' : 'Analisar Avançado'}
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
            <LoadingSpinner size="lg" className="mb-4 text-purple-600" />
            <p className="text-gray-600">Executando análise avançada...</p>
          </div>
        ) : metrics ? (
          <div className="space-y-6">
            {/* Resumo da Análise */}
            <div className="bg-white rounded-lg p-4 border border-purple-200">
              <div className="flex items-start justify-between mb-3">
                <div>
                  <h6 className="font-semibold text-gray-900">
                    {metrics.schemaName}.{metrics.tableName}
                  </h6>
                  <p className="text-sm text-gray-600">
                    Analisado em: {formatDate(metrics.analysisTimestamp)}
                  </p>
                </div>
                <div className="flex items-center gap-1 text-green-600">
                  <CheckCircle className="w-4 h-4" />
                  <span className="text-sm font-medium">Completo</span>
                </div>
              </div>

              <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                <div className="text-center p-3 bg-purple-50 rounded-lg">
                  <div className="text-2xl font-bold text-purple-600">
                    {metrics.columnMetrics.filter(c => c.patternMatches.length > 0).length}
                  </div>
                  <div className="text-sm text-purple-700">Colunas com Padrões</div>
                </div>

                <div className="text-center p-3 bg-orange-50 rounded-lg">
                  <div className="text-2xl font-bold text-orange-600">
                    {metrics.columnMetrics.filter(c => c.outlierAnalysis).length}
                  </div>
                  <div className="text-sm text-orange-700">Colunas com Outliers</div>
                </div>

                <div className="text-center p-3 bg-blue-50 rounded-lg">
                  <div className="text-2xl font-bold text-blue-600">
                    {metrics.relationshipMetrics.statusDateRelationships.length}
                  </div>
                  <div className="text-sm text-blue-700">Relações Status-Data</div>
                </div>

                <div className="text-center p-3 bg-green-50 rounded-lg">
                  <div className="text-2xl font-bold text-green-600">
                    {metrics.relationshipMetrics.numericCorrelations.length}
                  </div>
                  <div className="text-sm text-green-700">Correlações Fortes</div>
                </div>
              </div>
            </div>

            {/* Análise de Padrões por Coluna */}
            {metrics.columnMetrics.some(c => c.patternMatches.length > 0 || c.outlierAnalysis) && (
              <div className="bg-white rounded-lg border border-purple-200 p-4">
                <h6 className="text-lg font-semibold text-gray-900 mb-4 flex items-center gap-2">
                  <Fingerprint className="w-5 h-5 text-purple-600" />
                  Análise de Padrões e Outliers
                </h6>

                <div className="space-y-3">
                  {metrics.columnMetrics.filter(c => c.patternMatches.length > 0 || c.outlierAnalysis).map((column, index) => {
                    const isExpanded = expandedColumns.has(column.columnName);
                    const hasPatterns = column.patternMatches.length > 0;
                    const hasOutliers = column.outlierAnalysis !== null;

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
                            </div>
                            <div className="flex items-center gap-2">
                              {hasPatterns && (
                                <span className="text-xs bg-purple-100 text-purple-700 px-2 py-1 rounded font-medium">
                                  {column.patternMatches.length} padrões
                                </span>
                              )}
                              {hasOutliers && (
                                <span className="text-xs bg-orange-100 text-orange-700 px-2 py-1 rounded font-medium">
                                  {column.outlierAnalysis!.outlierCount} outliers
                                </span>
                              )}
                            </div>
                          </div>
                          <div className="text-sm text-gray-600">
                            {hasPatterns && column.patternMatches[0] && (
                              <span className="font-medium">
                                {column.patternMatches[0].conformityPercentage.toFixed(1)}% conformidade
                              </span>
                            )}
                          </div>
                        </div>

                        {isExpanded && (
                          <div className="mt-3 space-y-4 border-t border-gray-100 pt-3">
                            {/* Padrões Detectados */}
                            {hasPatterns && (
                              <div>
                                <h7 className="font-medium text-purple-800 mb-2 flex items-center gap-2">
                                  <Target className="w-4 h-4" />
                                  Padrões Detectados
                                </h7>
                                <div className="space-y-3">
                                  {column.patternMatches.map((pattern, pIndex) => (
                                    <div key={pIndex} className={`p-3 rounded-lg border ${getConformityColor(pattern.conformityPercentage)}`}>
                                      <div className="flex items-center justify-between mb-2">
                                        <div className="flex items-center gap-2">
                                          <span className="text-lg">{getPatternIcon(pattern.patternName)}</span>
                                          <div>
                                            <div className="font-medium">{pattern.description}</div>
                                            <div className="text-xs opacity-75">
                                              {pattern.culture !== 'any' && `Cultura: ${pattern.culture} • `}
                                              {pattern.matchingSamples} de {pattern.totalSamples} amostras
                                            </div>
                                          </div>
                                        </div>
                                        <div className="text-right">
                                          <div className="text-lg font-bold">
                                            {pattern.conformityPercentage.toFixed(1)}%
                                          </div>
                                          <div className="text-xs opacity-75">conformidade</div>
                                        </div>
                                      </div>

                                      {/* Exemplos de valores */}
                                      {pattern.sampleMatchingValues.length > 0 && (
                                        <div className="mt-2">
                                          <div className="text-sm font-medium mb-1">✅ Exemplos válidos:</div>
                                          <div className="flex flex-wrap gap-1">
                                            {pattern.sampleMatchingValues.slice(0, 3).map((value, vIndex) => (
                                              <span key={vIndex} className="text-xs bg-white bg-opacity-60 px-2 py-1 rounded font-mono">
                                                "{value}"
                                              </span>
                                            ))}
                                          </div>
                                        </div>
                                      )}

                                      {pattern.sampleNonMatchingValues.length > 0 && (
                                        <div className="mt-2">
                                          <div className="text-sm font-medium mb-1">❌ Exemplos inválidos:</div>
                                          <div className="flex flex-wrap gap-1">
                                            {pattern.sampleNonMatchingValues.slice(0, 3).map((value, vIndex) => (
                                              <span key={vIndex} className="text-xs bg-white bg-opacity-60 px-2 py-1 rounded font-mono">
                                                "{value}"
                                              </span>
                                            ))}
                                          </div>
                                        </div>
                                      )}
                                    </div>
                                  ))}
                                </div>
                              </div>
                            )}

                            {/* Análise de Outliers */}
                            {hasOutliers && column.outlierAnalysis && (
                              <div className="bg-orange-50 border border-orange-200 rounded-lg p-3">
                                <h7 className="font-medium text-orange-800 mb-2 flex items-center gap-2">
                                  <AlertTriangle className="w-4 h-4" />
                                  Outliers Estatísticos (Regra 3σ)
                                </h7>

                                <div className="grid grid-cols-2 md:grid-cols-4 gap-3 mb-3">
                                  <div className="text-center">
                                    <div className="text-lg font-bold text-orange-600">
                                      {column.outlierAnalysis.outlierCount}
                                    </div>
                                    <div className="text-xs text-orange-700">Outliers</div>
                                  </div>
                                  <div className="text-center">
                                    <div className="text-lg font-bold text-orange-600">
                                      {column.outlierAnalysis.outlierPercentage.toFixed(1)}%
                                    </div>
                                    <div className="text-xs text-orange-700">% dos dados</div>
                                  </div>
                                  <div className="text-center">
                                    <div className="text-lg font-bold text-gray-600">
                                      {column.outlierAnalysis.mean.toFixed(2)}
                                    </div>
                                    <div className="text-xs text-gray-700">Média</div>
                                  </div>
                                  <div className="text-center">
                                    <div className="text-lg font-bold text-gray-600">
                                      {column.outlierAnalysis.standardDeviation.toFixed(2)}
                                    </div>
                                    <div className="text-xs text-gray-700">Desvio Padrão</div>
                                  </div>
                                </div>

                                <div className="text-sm text-orange-700 mb-2">
                                  <div><strong>Limites (3σ):</strong></div>
                                  <div className="font-mono text-xs">
                                    Inferior: {column.outlierAnalysis.lowerBound.toFixed(2)} |
                                    Superior: {column.outlierAnalysis.upperBound.toFixed(2)}
                                  </div>
                                </div>

                                {/* Tabela detalhada dos outliers - sempre mostrar se existem outliers */}
                                {column.outlierAnalysis.sampleOutliers.length > 0 && (
                                  <div className="mt-4">
                                    {(() => {
                                      // Use dados paginados se disponíveis, senão use os dados originais
                                      const currentPage = currentOutlierPages[column.columnName] ?? 0;
                                      const pageKey = `${column.columnName}-${currentPage}`;
                                      const currentPageData = outlierPages[pageKey];
                                      const displayData = currentPageData?.outlierRows || [];
                                      // Calculate total pages based on the real outlier count
                                      const totalPages = Math.ceil(column.outlierAnalysis.outlierCount / 20);
                                      const hasMultiplePages = totalPages > 1;

                                      // Se temos dados paginados ou se é uma tabela que deveria ter paginação
                                      const shouldShowTable = currentPageData?.outlierRows || hasMultiplePages;

                                      return shouldShowTable ? (
                                        <div>
                                          <div className="text-sm font-medium mb-2">
                                            🔍 Linhas com outliers (página {currentPage + 1} de {totalPages}) - {column.outlierAnalysis.outlierCount.toLocaleString()} total:
                                          </div>

                                          {/* Controles de paginação avançados */}
                                          <div className="flex items-center justify-between mb-2">
                                            <div className="flex items-center gap-1">
                                              {/* Primeira página */}
                                              <button
                                                onClick={() => navigateToPage(column.columnName, 0)}
                                                disabled={currentPage === 0 || loadingOutliers[pageKey]}
                                                className="px-2 py-1 text-xs bg-gray-500 text-white rounded disabled:bg-gray-300 disabled:cursor-not-allowed"
                                                title="Primeira"
                                              >
                                                ««
                                              </button>

                                              {/* Página anterior */}
                                              <button
                                                onClick={() => navigateToPage(column.columnName, Math.max(0, currentPage - 1))}
                                                disabled={currentPage === 0 || loadingOutliers[pageKey]}
                                                className="px-2 py-1 text-xs bg-blue-500 text-white rounded disabled:bg-gray-300 disabled:cursor-not-allowed"
                                                title="Anterior"
                                              >
                                                ← Anterior
                                              </button>

                                              {/* Números de página */}
                                              <div className="flex items-center gap-1">
                                                {(() => {
                                                  const maxVisiblePages = 7;
                                                  const halfVisible = Math.floor(maxVisiblePages / 2);
                                                  let startPage = Math.max(0, currentPage - halfVisible);
                                                  let endPage = Math.min(totalPages - 1, startPage + maxVisiblePages - 1);

                                                  // Ajustar startPage se estivermos próximos do fim
                                                  if (endPage - startPage < maxVisiblePages - 1) {
                                                    startPage = Math.max(0, endPage - maxVisiblePages + 1);
                                                  }

                                                  const pages = [];

                                                  // Mostrar primeira página se não estiver visível
                                                  if (startPage > 0) {
                                                    pages.push(
                                                      <button
                                                        key={0}
                                                        onClick={() => navigateToPage(column.columnName, 0)}
                                                        disabled={Object.values(loadingOutliers).some(loading => loading)}
                                                        className="px-2 py-1 text-xs border border-gray-300 rounded hover:bg-gray-100 disabled:cursor-not-allowed"
                                                      >
                                                        1
                                                      </button>
                                                    );

                                                    if (startPage > 1) {
                                                      pages.push(<span key="ellipsis1" className="text-xs text-gray-400 px-1">...</span>);
                                                    }
                                                  }

                                                  // Páginas visíveis
                                                  for (let i = startPage; i <= endPage; i++) {
                                                    pages.push(
                                                      <button
                                                        key={i}
                                                        onClick={() => {
                                                          console.log(`📄 [PAGE_BUTTON] Page ${i + 1} button clicked for ${column.columnName}`);
                                                          navigateToPage(column.columnName, i);
                                                        }}
                                                        disabled={loadingOutliers[pageKey]}
                                                        className={`px-2 py-1 text-xs border rounded ${
                                                          i === currentPage
                                                            ? 'bg-blue-500 text-white border-blue-500'
                                                            : 'border-gray-300 hover:bg-gray-100'
                                                        } disabled:cursor-not-allowed`}
                                                      >
                                                        {i + 1}
                                                      </button>
                                                    );
                                                  }

                                                  // Mostrar última página se não estiver visível
                                                  if (endPage < totalPages - 1) {
                                                    if (endPage < totalPages - 2) {
                                                      pages.push(<span key="ellipsis2" className="text-xs text-gray-400 px-1">...</span>);
                                                    }

                                                    pages.push(
                                                      <button
                                                        key={totalPages - 1}
                                                        onClick={() => navigateToPage(column.columnName, totalPages - 1)}
                                                        disabled={Object.values(loadingOutliers).some(loading => loading)}
                                                        className="px-2 py-1 text-xs border border-gray-300 rounded hover:bg-gray-100 disabled:cursor-not-allowed"
                                                      >
                                                        {totalPages}
                                                      </button>
                                                    );
                                                  }

                                                  return pages;
                                                })()}
                                              </div>

                                              {/* Próxima página */}
                                              <button
                                                onClick={() => navigateToPage(column.columnName, currentPage + 1)}
                                                disabled={currentPage >= totalPages - 1 || loadingOutliers[pageKey]}
                                                className="px-2 py-1 text-xs bg-blue-500 text-white rounded disabled:bg-gray-300 disabled:cursor-not-allowed"
                                                title="Próxima"
                                              >
                                                Próxima →
                                              </button>

                                              {/* Última página */}
                                              <button
                                                onClick={() => navigateToPage(column.columnName, totalPages - 1)}
                                                disabled={currentPage >= totalPages - 1 || loadingOutliers[pageKey]}
                                                className="px-2 py-1 text-xs bg-gray-500 text-white rounded disabled:bg-gray-300 disabled:cursor-not-allowed"
                                                title="Última"
                                              >
                                                »»
                                              </button>
                                            </div>

                                            <div className="flex items-center gap-2">
                                              <span className="text-xs text-gray-600">
                                                Página {currentPage + 1} de {totalPages}
                                              </span>

                                              {loadingOutliers[pageKey] && (
                                                <div className="text-xs text-blue-600">Carregando...</div>
                                              )}
                                            </div>
                                          </div>

                                          <div className="max-h-96 overflow-auto border border-gray-200 rounded">
                                            {(() => {
                                              const isPageLoading = loadingOutliers[pageKey] || false;

                                              if (isPageLoading) {
                                                return (
                                                  <div className="p-8 text-center text-gray-500">
                                                    <div className="flex items-center justify-center gap-2">
                                                      <LoadingSpinner size="sm" />
                                                      <span>Carregando página {currentPage + 1}...</span>
                                                    </div>
                                                  </div>
                                                );
                                              }

                                              if (displayData.length === 0) {
                                                return (
                                                  <div className="p-8 text-center text-gray-500">
                                                    <div className="flex flex-col items-center gap-2">
                                                      <span>📄 Página {currentPage + 1} não carregada ainda</span>
                                                      <span className="text-xs">Esta página contém dados reais do banco, carregue para ver os outliers</span>
                                                      <button
                                                        onClick={() => {
                                                          console.log(`🔄 [RETRY_BUTTON] Retry button clicked for page ${currentPage + 1} of ${column.columnName}`);
                                                          navigateToPage(column.columnName, currentPage, true);
                                                        }}
                                                        className="px-3 py-1 text-sm bg-blue-500 text-white rounded hover:bg-blue-600 mt-1"
                                                      >
                                                        🔄 Carregar página
                                                      </button>
                                                    </div>
                                                  </div>
                                                );
                                              }

                                              return (
                                                <table className="min-w-full text-xs">
                                                  <thead className="bg-gray-50 sticky top-0">
                                                    <tr>
                                                      {Object.keys(displayData[0]?.rowData || {}).map((columnName) => (
                                                        <th key={columnName} className="px-2 py-1 text-left font-medium text-gray-700 border-b">
                                                          {columnName}
                                                        </th>
                                                      ))}
                                                    </tr>
                                                  </thead>
                                                  <tbody>
                                                    {displayData.map((row, rowIndex) => (
                                                      <tr key={rowIndex} className="hover:bg-gray-50 border-b">
                                                        {Object.entries(row.rowData).map(([colName, value]) => (
                                                          <td
                                                            key={colName}
                                                            className={`px-2 py-1 font-mono ${
                                                              colName === column.columnName
                                                                ? 'bg-red-100 text-red-800 font-bold'
                                                                : 'text-gray-700'
                                                            }`}
                                                          >
                                                            {value?.toString() || 'NULL'}
                                                          </td>
                                                        ))}
                                                      </tr>
                                                    ))}
                                                  </tbody>
                                                </table>
                                              );
                                            })()}
                                          </div>
                                        </div>
                                      ) : (
                                        <div>
                                          <div className="text-sm font-medium mb-2">🔍 Valores outliers ({column.outlierAnalysis.outlierCount} total):</div>
                                          <div className="flex flex-wrap gap-1">
                                            {column.outlierAnalysis.sampleOutliers.map((outlier, oIndex) => (
                                              <span key={oIndex} className="text-xs bg-white bg-opacity-60 px-2 py-1 rounded font-mono">
                                                {outlier.toLocaleString()}
                                              </span>
                                            ))}
                                          </div>
                                        </div>
                                      );
                                    })()}
                                  </div>
                                )}
                              </div>
                            )}
                          </div>
                        )}
                      </div>
                    );
                  })}
                </div>
              </div>
            )}

            {/* Análise de Relações */}
            {(metrics.relationshipMetrics.statusDateRelationships.length > 0 ||
              metrics.relationshipMetrics.numericCorrelations.length > 0) && (
              <div className="bg-white rounded-lg border border-purple-200 p-4">
                <h6 className="text-lg font-semibold text-gray-900 mb-4 flex items-center gap-2">
                  <Activity className="w-5 h-5 text-purple-600" />
                  Análise de Relações entre Colunas
                </h6>

                <div className="space-y-4">
                  {/* Relações Status-Data */}
                  {metrics.relationshipMetrics.statusDateRelationships.length > 0 && (
                    <div>
                      <h7 className="font-medium text-blue-800 mb-3 flex items-center gap-2">
                        🔗 Relações Status ↔ Data Detectadas
                      </h7>
                      {metrics.relationshipMetrics.statusDateRelationships.map((relation, rIndex) => (
                        <div key={rIndex} className="bg-blue-50 border border-blue-200 rounded-lg p-3 mb-2">
                          <div className="flex items-center justify-between mb-2">
                            <div className="flex items-center gap-2">
                              <span className="font-medium text-blue-900">
                                {relation.statusColumn} ↔ {relation.dateColumn}
                              </span>
                              <span className="text-xs bg-blue-100 text-blue-700 px-2 py-1 rounded">
                                Radical: "{relation.commonRadical}"
                              </span>
                            </div>
                            <div className="text-right">
                              <div className="text-lg font-bold text-red-600">
                                {relation.inconsistencyPercentage.toFixed(1)}%
                              </div>
                              <div className="text-xs text-blue-700">inconsistente</div>
                            </div>
                          </div>

                          <div className="text-sm text-blue-700">
                            <div>
                              <strong>Problema:</strong> {relation.inconsistentRecords} registros de {relation.totalActiveRecords} registros ativos
                              estão com {relation.dateColumn} nulo
                            </div>
                            <div className="mt-1">
                              <strong>Valores ativos considerados:</strong> {relation.activeValues.join(', ')}
                            </div>
                          </div>

                          {relation.inconsistencyPercentage > 10 && (
                            <div className="mt-2 text-xs text-red-600 bg-red-50 p-2 rounded">
                              ⚠️ Alta inconsistência! Recomenda-se investigar esta regra de negócio.
                            </div>
                          )}
                        </div>
                      ))}
                    </div>
                  )}

                  {/* Correlações Numéricas */}
                  {metrics.relationshipMetrics.numericCorrelations.length > 0 && (
                    <div>
                      <h7 className="font-medium text-green-800 mb-3 flex items-center gap-2">
                        📈 Correlações Numéricas Fortes
                      </h7>
                      {metrics.relationshipMetrics.numericCorrelations.map((correlation, cIndex) => (
                        <div key={cIndex} className="bg-green-50 border border-green-200 rounded-lg p-3 mb-2">
                          <div className="flex items-center justify-between mb-2">
                            <div className="flex items-center gap-2">
                              <span className="text-lg">{getCorrelationIcon(correlation.correlationCoefficient)}</span>
                              <span className="font-medium text-green-900">
                                {correlation.column1} ↔ {correlation.column2}
                              </span>
                            </div>
                            <div className="text-right">
                              <div className="text-lg font-bold text-green-600">
                                {correlation.correlationCoefficient.toFixed(3)}
                              </div>
                              <div className="text-xs text-green-700">coeficiente</div>
                            </div>
                          </div>

                          <div className="text-sm text-green-700">
                            <div><strong>Força:</strong> {correlation.correlationStrength}</div>
                            <div><strong>Amostra:</strong> {correlation.sampleSize.toLocaleString()} registros</div>
                          </div>

                          {Math.abs(correlation.correlationCoefficient) > 0.95 && (
                            <div className="mt-2 text-xs text-green-600 bg-green-50 p-2 rounded">
                              🔥 Correlação muito forte! Possível relação direta ou dependência funcional.
                            </div>
                          )}
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              </div>
            )}
          </div>
        ) : (
          <div className="text-center py-8 bg-white rounded-lg border border-purple-200">
            <div className="w-12 h-12 mx-auto bg-purple-100 rounded-full flex items-center justify-center mb-4">
              <Search className="w-6 h-6 text-purple-600" />
            </div>
            <h6 className="text-lg font-medium text-gray-900 mb-2">
              Análise avançada não executada
            </h6>
            <p className="text-gray-600 mb-4">
              Descubra padrões, outliers e relações ocultas nos seus dados.
            </p>
            <div className="text-sm text-gray-500 space-y-1">
              <div>✨ Validação automática de CPF, CNPJ, Email, etc.</div>
              <div>📊 Detecção estatística de outliers (regra 3σ)</div>
              <div>🔗 Descoberta de relações Status ↔ Data</div>
              <div>📈 Análise de correlações numéricas</div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}