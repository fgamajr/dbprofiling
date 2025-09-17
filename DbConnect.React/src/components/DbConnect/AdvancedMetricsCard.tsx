import React, { useState, useEffect, useRef } from 'react';
import {
  Search, Play, RefreshCw, CheckCircle, AlertCircle, TrendingUp, TrendingDown,
  ChevronDown, ChevronRight, Target, BarChart3, Zap, AlertTriangle, Info,
  Eye, ExternalLink, Fingerprint, Activity
} from 'lucide-react';
import { LoadingSpinner } from './LoadingSpinner';
import {
  Pagination,
  PaginationContent,
  PaginationEllipsis,
  PaginationItem,
  PaginationLink,
  PaginationNext,
  PaginationPrevious,
} from "@/components/ui/pagination";

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
  items: OutlierRowData[];
  totalCount: number;
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
  const [collecting, setCollecting] = useState(false);
  const [metrics, setMetrics] = useState<AdvancedTableMetrics | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [expandedColumns, setExpandedColumns] = useState<Set<string>>(new Set());
  const [outlierData, setOutlierData] = useState<Record<string, OutlierAnalysis>>({});
  const [loadingOutliers, setLoadingOutliers] = useState<Record<string, boolean>>({});
  const abortControllerRef = useRef<AbortController | null>(null);

  // Auto-load first page when metrics are available
  useEffect(() => {
    if (metrics?.columnMetrics) {
      metrics.columnMetrics.forEach(column => {
        if (column.outlierAnalysis && column.outlierAnalysis.outlierCount > 0) {
          const columnKey = column.columnName;
          const hasData = outlierData[columnKey];

          if (!hasData) {
            // Load page 1 from API
            loadOutlierPage(column.columnName, 1);
          }
        }
      });
    }
  }, [metrics?.columnMetrics]);

  // Navigate to a specific page
  const navigateToPage = async (columnName: string, page: number) => {
    const columnKey = columnName;
    const isLoading = loadingOutliers[columnKey];

    if (!isLoading) {
      await loadOutlierPage(columnName, page);
    }
  };

  async function loadOutlierPage(columnName: string, page: number, pageSize: number = 20) {
    const columnKey = columnName;
    setLoadingOutliers(prev => ({ ...prev, [columnKey]: true }));

    try {
      console.log(`🔍 Carregando outliers: ${schema}.${tableName}.${columnName}, página ${page}`);

      const apiUrl = `/api/data-quality/outliers?tableName=${tableName}&schemaName=${schema}&columnName=${columnName}&page=${page - 1}&pageSize=${pageSize}`;

      const response = await fetch(apiUrl, {
        signal: abortControllerRef.current?.signal
      });

      if (!response.ok) {
        throw new Error(`Erro na API: ${response.status} - ${response.statusText}`);
      }

      const outlierAnalysis: OutlierAnalysis = await response.json();

      // Ajustar para 1-based page para display
      outlierAnalysis.currentPage = page;

      console.log(`✅ Outliers carregados: ${outlierAnalysis.items.length} itens da página ${page}/${outlierAnalysis.totalPages}`);

      setOutlierData(prev => ({ ...prev, [columnKey]: outlierAnalysis }));

    } catch (error) {
      console.error('Error loading outlier page:', error);
      onToast?.({ type: 'error', message: `Erro ao carregar outliers: ${error instanceof Error ? error.message : 'Erro desconhecido'}` });
    } finally {
      setLoadingOutliers(prev => ({ ...prev, [columnKey]: false }));
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
                disabled={collecting}
                className="p-2 text-purple-600 hover:bg-purple-100 rounded-lg transition-colors"
                title="Limpar análise"
              >
                <RefreshCw className={`w-4 h-4 ${collecting ? 'animate-spin' : ''}`} />
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

        {collecting ? (
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
                                <div className="font-medium text-purple-800 mb-2 flex items-center gap-2">
                                  <Target className="w-4 h-4" />
                                  Padrões Detectados
                                </div>
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
                                <div className="font-medium text-orange-800 mb-2 flex items-center gap-2">
                                  <AlertTriangle className="w-4 h-4" />
                                  Outliers Estatísticos (Regra 3σ)
                                </div>

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

                                <div className="text-sm text-orange-700 mb-4">
                                  <div><strong>Limites (3σ):</strong></div>
                                  <div className="font-mono text-xs">
                                    Inferior: {column.outlierAnalysis.lowerBound.toFixed(2)} |
                                    Superior: {column.outlierAnalysis.upperBound.toFixed(2)}
                                  </div>
                                </div>

                                {/* Tabela detalhada dos outliers com paginação adequada */}
                                {column.outlierAnalysis.outlierCount > 0 && (
                                  <div className="mt-4">
                                    {(() => {
                                      const columnKey = column.columnName;
                                      const currentPageData = outlierData[columnKey];
                                      const displayData = currentPageData?.items || [];
                                      const currentPage = currentPageData?.currentPage || 1;
                                      const totalPages = currentPageData?.totalPages || Math.ceil(column.outlierAnalysis.outlierCount / 20);
                                      const isLoading = loadingOutliers[columnKey];

                                      return (
                                        <div>
                                          <div className="text-sm font-medium mb-3 flex items-center gap-2">
                                            🔍 <span>Outliers ordenados (maiores primeiro) - Página {currentPage} de {totalPages}</span>
                                            <span className="text-xs bg-orange-100 text-orange-700 px-2 py-1 rounded">
                                              {currentPageData?.totalCount || column.outlierAnalysis.outlierCount.toLocaleString()} total
                                            </span>
                                          </div>

                                          {/* Paginação usando componente padronizado */}
                                          {totalPages > 1 && (
                                            <div className="mb-4">
                                              <Pagination>
                                                <PaginationContent>
                                                  <PaginationItem>
                                                    <PaginationPrevious
                                                      onClick={() => currentPage > 1 && navigateToPage(column.columnName, currentPage - 1)}
                                                      className={currentPage <= 1 ? 'pointer-events-none opacity-50' : 'cursor-pointer'}
                                                    />
                                                  </PaginationItem>

                                                  {(() => {
                                                    const pages = [];
                                                    const maxVisible = 5;
                                                    const halfVisible = Math.floor(maxVisible / 2);
                                                    let startPage = Math.max(1, currentPage - halfVisible);
                                                    let endPage = Math.min(totalPages, startPage + maxVisible - 1);

                                                    if (endPage - startPage < maxVisible - 1) {
                                                      startPage = Math.max(1, endPage - maxVisible + 1);
                                                    }

                                                    // First page
                                                    if (startPage > 1) {
                                                      pages.push(
                                                        <PaginationItem key={1}>
                                                          <PaginationLink
                                                            onClick={() => navigateToPage(column.columnName, 1)}
                                                            className="cursor-pointer"
                                                          >
                                                            1
                                                          </PaginationLink>
                                                        </PaginationItem>
                                                      );

                                                      if (startPage > 2) {
                                                        pages.push(
                                                          <PaginationItem key="ellipsis1">
                                                            <PaginationEllipsis />
                                                          </PaginationItem>
                                                        );
                                                      }
                                                    }

                                                    // Visible pages
                                                    for (let i = startPage; i <= endPage; i++) {
                                                      pages.push(
                                                        <PaginationItem key={i}>
                                                          <PaginationLink
                                                            onClick={() => navigateToPage(column.columnName, i)}
                                                            isActive={i === currentPage}
                                                            className="cursor-pointer"
                                                          >
                                                            {i}
                                                          </PaginationLink>
                                                        </PaginationItem>
                                                      );
                                                    }

                                                    // Last page
                                                    if (endPage < totalPages) {
                                                      if (endPage < totalPages - 1) {
                                                        pages.push(
                                                          <PaginationItem key="ellipsis2">
                                                            <PaginationEllipsis />
                                                          </PaginationItem>
                                                        );
                                                      }

                                                      pages.push(
                                                        <PaginationItem key={totalPages}>
                                                          <PaginationLink
                                                            onClick={() => navigateToPage(column.columnName, totalPages)}
                                                            className="cursor-pointer"
                                                          >
                                                            {totalPages}
                                                          </PaginationLink>
                                                        </PaginationItem>
                                                      );
                                                    }

                                                    return pages;
                                                  })()}

                                                  <PaginationItem>
                                                    <PaginationNext
                                                      onClick={() => currentPage < totalPages && navigateToPage(column.columnName, currentPage + 1)}
                                                      className={currentPage >= totalPages ? 'pointer-events-none opacity-50' : 'cursor-pointer'}
                                                    />
                                                  </PaginationItem>
                                                </PaginationContent>
                                              </Pagination>
                                            </div>
                                          )}

                                          <div className="max-h-96 overflow-auto border border-gray-200 rounded">
                                            {(() => {
                                              if (isLoading) {
                                                return (
                                                  <div className="p-8 text-center text-gray-500">
                                                    <div className="flex items-center justify-center gap-2">
                                                      <LoadingSpinner size="sm" />
                                                      <span>Carregando outliers da página {currentPage}...</span>
                                                    </div>
                                                  </div>
                                                );
                                              }

                                              if (displayData.length === 0) {
                                                return (
                                                  <div className="p-8 text-center text-gray-500">
                                                    <div className="flex flex-col items-center gap-2">
                                                      <span>📄 Página {currentPage} não carregada</span>
                                                      <span className="text-xs">Clique para carregar os outliers desta página</span>
                                                      <button
                                                        onClick={() => navigateToPage(column.columnName, currentPage)}
                                                        className="px-3 py-1 text-sm bg-orange-500 text-white rounded hover:bg-orange-600 mt-1"
                                                      >
                                                        🔄 Carregar outliers
                                                      </button>
                                                    </div>
                                                  </div>
                                                );
                                              }

                                              return (
                                                <table className="min-w-full text-xs">
                                                  <thead className="bg-gray-50 sticky top-0">
                                                    <tr>
                                                      <th className="px-2 py-1 text-center font-medium border-b text-gray-700 bg-blue-50 border-r">
                                                        #
                                                      </th>
                                                      {Object.keys(displayData[0]?.rowData || {}).map((colName) => (
                                                        <th
                                                          key={colName}
                                                          className={`px-2 py-1 text-left font-medium border-b ${
                                                            colName === column.columnName
                                                              ? 'text-orange-700 bg-orange-50'
                                                              : 'text-gray-700'
                                                          }`}
                                                        >
                                                          {colName}
                                                          {colName === column.columnName && (
                                                            <span className="ml-1 text-xs text-orange-600">(outlier)</span>
                                                          )}
                                                        </th>
                                                      ))}
                                                    </tr>
                                                  </thead>
                                                  <tbody>
                                                    {displayData.map((row: any, rowIndex: number) => {
                                                      // Calcular o número global do outlier considerando a paginação
                                                      const globalRowNumber = (currentPage - 1) * (currentPageData?.pageSize || 20) + rowIndex + 1;

                                                      return (
                                                        <tr key={rowIndex} className="hover:bg-gray-50 border-b">
                                                          <td className="px-2 py-1 text-center font-mono text-blue-700 bg-blue-50 border-r font-semibold">
                                                            {globalRowNumber}
                                                          </td>
                                                          {Object.entries(row.rowData).map(([colName, value]) => (
                                                            <td
                                                              key={colName}
                                                              className={`px-2 py-1 font-mono ${
                                                                colName === column.columnName
                                                                  ? 'bg-orange-100 text-orange-900 font-bold'
                                                                  : 'text-gray-700'
                                                              }`}
                                                            >
                                                              {typeof value === 'number'
                                                                ? value.toLocaleString()
                                                                : (value?.toString() || 'NULL')}
                                                            </td>
                                                          ))}
                                                        </tr>
                                                      );
                                                    })}
                                                  </tbody>
                                                </table>
                                              );
                                            })()}
                                          </div>

                                          {/* Nota explicativa */}
                                          <div className="mt-2 text-xs text-gray-600 bg-orange-50 p-2 rounded">
                                            💡 <strong>Nota:</strong> Os outliers são ordenados do maior para o menor valor.
                                            A coluna <span className="bg-blue-100 text-blue-800 px-1 rounded font-mono">#</span> mostra o ranking/posição de cada outlier.
                                            A coluna <strong>{column.columnName}</strong> destacada em <span className="bg-orange-100 text-orange-800 px-1 rounded">laranja</span> contém os valores outliers.
                                            Use o scroll horizontal para visualizar todas as colunas da tabela.
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
                      <div className="font-medium text-blue-800 mb-3 flex items-center gap-2">
                        🔗 Relações Status ↔ Data Detectadas
                      </div>
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
                      <div className="font-medium text-green-800 mb-3 flex items-center gap-2">
                        📈 Correlações Numéricas Fortes
                      </div>
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