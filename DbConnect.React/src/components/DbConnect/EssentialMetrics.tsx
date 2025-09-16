import React, { useState, useEffect } from 'react';
import { Activity, Database, Table, BarChart3, AlertCircle, CheckCircle, Play, RefreshCw } from 'lucide-react';
import { PlaceholderSection } from './PlaceholderSection';
import { LoadingSpinner } from './LoadingSpinner';

interface EssentialMetricsProps {
  isConnected: boolean;
  activeProfileId: number | null;
}

interface TableMetrics {
  schema: string;
  tableName: string;
  collectedAt: string;
  totalRows: number;
  totalColumns: number;
  estimatedSizeBytes: number;
  completenessRate: number;
  duplicateRows: number;
  columnsWithNulls: number;
}

interface CollectionRequest {
  schema: string;
  tableName: string;
}

export function EssentialMetrics({ isConnected, activeProfileId }: EssentialMetricsProps) {
  const [loading, setLoading] = useState(false);
  const [collecting, setCollecting] = useState(false);
  const [tableMetrics, setTableMetrics] = useState<TableMetrics[]>([]);
  const [collectionForm, setCollectionForm] = useState<CollectionRequest>({
    schema: 'public',
    tableName: ''
  });
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (isConnected) {
      loadExistingMetrics();
    }
  }, [isConnected, activeProfileId]);

  async function loadExistingMetrics() {
    setLoading(true);
    setError(null);

    try {
      // TODO: Implementar endpoint para listar métricas existentes
      // const response = await fetch('/api/essential-metrics/tables');
      // const data = await response.json();
      // setTableMetrics(data.data || []);

      // Por enquanto, dados de exemplo
      setTableMetrics([]);
    } catch (err) {
      setError('Falha ao carregar métricas existentes');
      console.error('Error loading metrics:', err);
    } finally {
      setLoading(false);
    }
  }

  async function collectMetrics() {
    if (!collectionForm.tableName.trim()) {
      setError('Nome da tabela é obrigatório');
      return;
    }

    setCollecting(true);
    setError(null);

    try {
      const response = await fetch('/api/essential-metrics/collect-basic', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(collectionForm),
      });

      const result = await response.json();

      if (result.success) {
        // Atualizar lista de métricas
        await loadExistingMetrics();

        // Limpar formulário
        setCollectionForm(prev => ({ ...prev, tableName: '' }));

        // Mostrar sucesso (você pode implementar toast aqui)
        console.log('Métricas coletadas com sucesso:', result.data);
      } else {
        setError(result.message || 'Falha ao coletar métricas');
      }
    } catch (err) {
      setError('Erro de conexão ao coletar métricas');
      console.error('Error collecting metrics:', err);
    } finally {
      setCollecting(false);
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

  if (!isConnected) {
    return (
      <div className="space-y-8">
        <div className="text-center py-12">
          <div className="w-16 h-16 mx-auto bg-orange-100 rounded-full flex items-center justify-center mb-4">
            <AlertCircle className="w-8 h-8 text-orange-600" />
          </div>
          <h3 className="text-xl font-semibold text-gray-900 mb-2">
            Conexão Necessária
          </h3>
          <p className="text-gray-600 max-w-md mx-auto">
            Para coletar métricas essenciais, você precisa se conectar a um perfil de banco de dados primeiro.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-8">
      {/* Header */}
      <div className="text-center">
        <div className="w-16 h-16 mx-auto bg-gradient-primary rounded-2xl flex items-center justify-center mb-4">
          <Activity className="w-8 h-8 text-primary-foreground" />
        </div>
        <h2 className="text-3xl font-bold text-gray-900 mb-2">Métricas Essenciais</h2>
        <p className="text-gray-600 max-w-2xl mx-auto">
          Análise rápida de qualidade de dados sem IA - contagens, completude, cardinalidade e estatísticas básicas
        </p>
      </div>

      {/* Collection Form */}
      <div className="bg-white rounded-lg border border-gray-200 p-6">
        <h3 className="text-lg font-semibold text-gray-900 mb-4 flex items-center gap-2">
          <Play className="w-5 h-5 text-blue-600" />
          Coletar Métricas de Tabela
        </h3>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              Schema
            </label>
            <input
              type="text"
              value={collectionForm.schema}
              onChange={(e) => setCollectionForm(prev => ({ ...prev, schema: e.target.value }))}
              className="w-full p-3 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
              placeholder="public"
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              Nome da Tabela
            </label>
            <input
              type="text"
              value={collectionForm.tableName}
              onChange={(e) => setCollectionForm(prev => ({ ...prev, tableName: e.target.value }))}
              className="w-full p-3 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
              placeholder="users, orders, products..."
              disabled={collecting}
            />
          </div>

          <div className="flex items-end">
            <button
              onClick={collectMetrics}
              disabled={collecting || !collectionForm.tableName.trim()}
              className="w-full bg-blue-600 text-white px-4 py-3 rounded-lg hover:bg-blue-700 disabled:bg-gray-400 disabled:cursor-not-allowed flex items-center justify-center gap-2 transition-colors"
            >
              {collecting ? (
                <>
                  <LoadingSpinner size="sm" />
                  Coletando...
                </>
              ) : (
                <>
                  <Activity className="w-4 h-4" />
                  Coletar Métricas
                </>
              )}
            </button>
          </div>
        </div>

        {error && (
          <div className="mt-4 p-4 bg-red-50 border border-red-200 rounded-lg">
            <div className="flex items-center gap-2 text-red-800">
              <AlertCircle className="w-5 h-5" />
              <span className="font-medium">Erro:</span>
              <span>{error}</span>
            </div>
          </div>
        )}
      </div>

      {/* Metrics List */}
      <div className="bg-white rounded-lg border border-gray-200">
        <div className="p-6 border-b border-gray-200">
          <div className="flex items-center justify-between">
            <h3 className="text-lg font-semibold text-gray-900 flex items-center gap-2">
              <BarChart3 className="w-5 h-5 text-green-600" />
              Métricas Coletadas
            </h3>
            <button
              onClick={loadExistingMetrics}
              disabled={loading}
              className="text-blue-600 hover:text-blue-700 flex items-center gap-1 text-sm"
            >
              <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
              Atualizar
            </button>
          </div>
        </div>

        <div className="p-6">
          {loading ? (
            <div className="text-center py-8">
              <LoadingSpinner size="lg" className="mb-4" />
              <p className="text-gray-600">Carregando métricas...</p>
            </div>
          ) : tableMetrics.length === 0 ? (
            <div className="text-center py-8">
              <div className="w-12 h-12 mx-auto bg-gray-100 rounded-full flex items-center justify-center mb-4">
                <Table className="w-6 h-6 text-gray-400" />
              </div>
              <h4 className="text-lg font-medium text-gray-900 mb-2">
                Nenhuma métrica coletada
              </h4>
              <p className="text-gray-600">
                Colete métricas de uma tabela para visualizar os dados aqui.
              </p>
            </div>
          ) : (
            <div className="space-y-4">
              {tableMetrics.map((metrics, index) => (
                <div key={index} className="border border-gray-200 rounded-lg p-4">
                  <div className="flex items-start justify-between mb-3">
                    <div>
                      <h4 className="font-semibold text-gray-900">
                        {metrics.schema}.{metrics.tableName}
                      </h4>
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
                        {metrics.totalRows.toLocaleString()}
                      </div>
                      <div className="text-sm text-blue-700">Linhas</div>
                    </div>

                    <div className="text-center p-3 bg-green-50 rounded-lg">
                      <div className="text-2xl font-bold text-green-600">
                        {metrics.totalColumns}
                      </div>
                      <div className="text-sm text-green-700">Colunas</div>
                    </div>

                    <div className="text-center p-3 bg-purple-50 rounded-lg">
                      <div className="text-2xl font-bold text-purple-600">
                        {formatBytes(metrics.estimatedSizeBytes)}
                      </div>
                      <div className="text-sm text-purple-700">Tamanho</div>
                    </div>

                    <div className="text-center p-3 bg-orange-50 rounded-lg">
                      <div className="text-2xl font-bold text-orange-600">
                        {Math.round(metrics.completenessRate)}%
                      </div>
                      <div className="text-sm text-orange-700">Completude</div>
                    </div>
                  </div>

                  {metrics.duplicateRows > 0 && (
                    <div className="mt-3 p-3 bg-yellow-50 border border-yellow-200 rounded-lg">
                      <div className="flex items-center gap-2 text-yellow-800">
                        <AlertCircle className="w-4 h-4" />
                        <span className="text-sm">
                          {metrics.duplicateRows.toLocaleString()} linhas duplicadas encontradas
                        </span>
                      </div>
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}