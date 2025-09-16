import React, { useState, useEffect } from 'react';
import { 
  Database, 
  Server, 
  Table, 
  Eye, 
  Search, 
  Zap, 
  Activity, 
  Clock, 
  Gauge, 
  Users, 
  Layers,
  RefreshCw,
  Calendar,
  HardDrive
} from 'lucide-react';
import { LoadingSpinner } from './LoadingSpinner';
import { Toast } from './Toast';
import { apiService } from '../../services/api';
import type { DatabaseInfo } from '../../services/api';

interface DatabaseDashboardProps {
  isConnected: boolean;
  activeProfileId: number | null;
}

export function DatabaseDashboard({ isConnected, activeProfileId }: DatabaseDashboardProps) {
  const [dbInfo, setDbInfo] = useState<DatabaseInfo | null>(null);
  const [loading, setLoading] = useState(false);
  const [toast, setToast] = useState<{ type: 'success' | 'error' | 'warning'; message: string } | null>(null);

  useEffect(() => {
    if (isConnected && activeProfileId) {
      loadDatabaseInfo();
    } else {
      setDbInfo(null);
    }
  }, [isConnected, activeProfileId]);

  async function loadDatabaseInfo() {
    setLoading(true);
    try {
      const info = await apiService.getDatabaseInfo();
      if (info) {
        setDbInfo(info);
        setToast({ type: 'success', message: 'Informações do banco carregadas com sucesso!' });
      } else {
        setToast({ type: 'error', message: 'Não foi possível carregar as informações do banco' });
      }
    } catch (error) {
      setToast({ type: 'error', message: 'Erro ao carregar informações do banco' });
    } finally {
      setLoading(false);
    }
  }

  function formatNumber(num: number): string {
    return num.toLocaleString('pt-BR');
  }

  if (!isConnected) {
    return (
      <div className="bg-gradient-card rounded-2xl shadow-md border border-border p-12 text-center">
        <div className="w-20 h-20 mx-auto bg-gradient-primary rounded-2xl flex items-center justify-center mb-6 animate-float">
          <Database className="w-10 h-10 text-primary-foreground" />
        </div>
        <h3 className="text-xl font-semibold text-card-foreground mb-2 font-heading">
          Nenhum banco conectado
        </h3>
        <p className="text-muted-foreground mb-6 max-w-md mx-auto">
          Conecte-se a um perfil na aba "Perfis" para visualizar informações detalhadas do banco de dados
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
        <p className="text-muted-foreground font-medium">Carregando informações do banco...</p>
      </div>
    );
  }

  if (!dbInfo) {
    return (
      <div className="bg-gradient-card rounded-2xl shadow-md border border-border p-12 text-center">
        <div className="w-20 h-20 mx-auto bg-red-100 rounded-2xl flex items-center justify-center mb-6">
          <Database className="w-10 h-10 text-red-600" />
        </div>
        <h3 className="text-xl font-semibold text-card-foreground mb-2 font-heading">
          Erro ao carregar informações
        </h3>
        <p className="text-muted-foreground mb-6 max-w-md mx-auto">
          Não foi possível obter as informações do banco de dados conectado
        </p>
        <button
          onClick={loadDatabaseInfo}
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
            Dashboard do Banco de Dados
          </h2>
          <p className="text-muted-foreground">
            Informações detalhadas sobre {dbInfo.basic.databaseName}
          </p>
        </div>
        <button
          onClick={loadDatabaseInfo}
          disabled={loading}
          className="flex items-center gap-2 px-4 py-2 bg-primary text-primary-foreground rounded-lg hover:bg-primary/90 transition-colors disabled:opacity-50"
        >
          <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
          <span>Atualizar</span>
        </button>
      </div>

      {/* Informações Básicas */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <div className="bg-gradient-card rounded-xl shadow-sm border border-border p-6">
          <div className="flex items-center gap-3 mb-4">
            <div className="w-10 h-10 bg-blue-100 rounded-lg flex items-center justify-center">
              <Database className="w-5 h-5 text-blue-600" />
            </div>
            <div>
              <h3 className="font-semibold text-card-foreground">Banco de Dados</h3>
              <p className="text-sm text-muted-foreground">Informações gerais</p>
            </div>
          </div>
          <div className="space-y-2">
            <div>
              <span className="text-sm font-medium text-card-foreground">Nome:</span>
              <span className="ml-2 text-sm text-muted-foreground">{dbInfo.basic.databaseName}</span>
            </div>
            <div>
              <span className="text-sm font-medium text-card-foreground">Tamanho:</span>
              <span className="ml-2 text-sm text-muted-foreground">{dbInfo.basic.databaseSize}</span>
            </div>
          </div>
        </div>

        <div className="bg-gradient-card rounded-xl shadow-sm border border-border p-6">
          <div className="flex items-center gap-3 mb-4">
            <div className="w-10 h-10 bg-green-100 rounded-lg flex items-center justify-center">
              <Server className="w-5 h-5 text-green-600" />
            </div>
            <div>
              <h3 className="font-semibold text-card-foreground">Servidor</h3>
              <p className="text-sm text-muted-foreground">Status e versão</p>
            </div>
          </div>
          <div className="space-y-2">
            <div>
              <span className="text-sm font-medium text-card-foreground">Versão:</span>
              <span className="ml-2 text-sm text-muted-foreground">
                {dbInfo.basic.serverVersion.split(' ')[1] || 'PostgreSQL'}
              </span>
            </div>
            <div>
              <span className="text-sm font-medium text-card-foreground">Uptime:</span>
              <span className="ml-2 text-sm text-muted-foreground">{dbInfo.performance.uptime}</span>
            </div>
          </div>
        </div>

        <div className="bg-gradient-card rounded-xl shadow-sm border border-border p-6">
          <div className="flex items-center gap-3 mb-4">
            <div className="w-10 h-10 bg-purple-100 rounded-lg flex items-center justify-center">
              <Activity className="w-5 h-5 text-purple-600" />
            </div>
            <div>
              <h3 className="font-semibold text-card-foreground">Performance</h3>
              <p className="text-sm text-muted-foreground">Métricas atuais</p>
            </div>
          </div>
          <div className="space-y-2">
            <div>
              <span className="text-sm font-medium text-card-foreground">Conexões:</span>
              <span className="ml-2 text-sm text-muted-foreground">{dbInfo.performance.activeConnections}</span>
            </div>
            <div>
              <span className="text-sm font-medium text-card-foreground">Cache Hit:</span>
              <span className="ml-2 text-sm text-muted-foreground">{dbInfo.performance.cacheHitRatio}</span>
            </div>
          </div>
        </div>
      </div>

      {/* Estatísticas de Objetos */}
      <div className="bg-gradient-card rounded-xl shadow-sm border border-border p-6">
        <div className="flex items-center gap-3 mb-6">
          <div className="w-10 h-10 bg-orange-100 rounded-lg flex items-center justify-center">
            <Layers className="w-5 h-5 text-orange-600" />
          </div>
          <div>
            <h3 className="text-lg font-semibold text-card-foreground">Objetos do Banco</h3>
            <p className="text-sm text-muted-foreground">Contagem de estruturas do banco de dados</p>
          </div>
        </div>

        <div className="grid grid-cols-2 md:grid-cols-5 gap-4">
          <div className="text-center p-4 bg-blue-50 rounded-lg border border-blue-100">
            <Table className="w-8 h-8 text-blue-600 mx-auto mb-2" />
            <div className="text-2xl font-bold text-blue-600">{formatNumber(dbInfo.objects.tablesCount)}</div>
            <div className="text-sm text-blue-700 font-medium">Tabelas</div>
          </div>

          <div className="text-center p-4 bg-green-50 rounded-lg border border-green-100">
            <Eye className="w-8 h-8 text-green-600 mx-auto mb-2" />
            <div className="text-2xl font-bold text-green-600">{formatNumber(dbInfo.objects.viewsCount)}</div>
            <div className="text-sm text-green-700 font-medium">Views</div>
          </div>

          <div className="text-center p-4 bg-yellow-50 rounded-lg border border-yellow-100">
            <Search className="w-8 h-8 text-yellow-600 mx-auto mb-2" />
            <div className="text-2xl font-bold text-yellow-600">{formatNumber(dbInfo.objects.indexesCount)}</div>
            <div className="text-sm text-yellow-700 font-medium">Índices</div>
          </div>

          <div className="text-center p-4 bg-purple-50 rounded-lg border border-purple-100">
            <Zap className="w-8 h-8 text-purple-600 mx-auto mb-2" />
            <div className="text-2xl font-bold text-purple-600">{formatNumber(dbInfo.objects.functionsCount)}</div>
            <div className="text-sm text-purple-700 font-medium">Funções</div>
          </div>

          <div className="text-center p-4 bg-red-50 rounded-lg border border-red-100">
            <Activity className="w-8 h-8 text-red-600 mx-auto mb-2" />
            <div className="text-2xl font-bold text-red-600">{formatNumber(dbInfo.objects.triggersCount)}</div>
            <div className="text-sm text-red-700 font-medium">Triggers</div>
          </div>
        </div>
      </div>

      {/* Top Tables e Schemas */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Top Tables */}
        {dbInfo.topTables.length > 0 && (
          <div className="bg-gradient-card rounded-xl shadow-sm border border-border p-6">
            <div className="flex items-center gap-3 mb-4">
              <div className="w-10 h-10 bg-cyan-100 rounded-lg flex items-center justify-center">
                <HardDrive className="w-5 h-5 text-cyan-600" />
              </div>
              <div>
                <h3 className="text-lg font-semibold text-card-foreground">Tabelas com Mais Dados</h3>
                <p className="text-sm text-muted-foreground">Top 5 tabelas por número de registros</p>
              </div>
            </div>

            <div className="space-y-3">
              {dbInfo.topTables.map((table, index) => (
                <div key={`${table.schema}.${table.table}`} className="flex items-center justify-between p-3 bg-gray-50 rounded-lg">
                  <div className="flex items-center gap-3">
                    <div className="w-6 h-6 bg-cyan-500 text-white rounded-full flex items-center justify-center text-xs font-bold">
                      {index + 1}
                    </div>
                    <div>
                      <div className="font-medium text-card-foreground">{table.table}</div>
                      <div className="text-sm text-muted-foreground">{table.schema}</div>
                    </div>
                  </div>
                  <div className="text-right">
                    <div className="font-semibold text-card-foreground">{formatNumber(table.estimatedRows)}</div>
                    <div className="text-sm text-muted-foreground">registros</div>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Schemas */}
        <div className="bg-gradient-card rounded-xl shadow-sm border border-border p-6">
          <div className="flex items-center gap-3 mb-4">
            <div className="w-10 h-10 bg-indigo-100 rounded-lg flex items-center justify-center">
              <Layers className="w-5 h-5 text-indigo-600" />
            </div>
            <div>
              <h3 className="text-lg font-semibold text-card-foreground">Schemas Disponíveis</h3>
              <p className="text-sm text-muted-foreground">{dbInfo.schemas.length} schemas encontrados</p>
            </div>
          </div>

          <div className="flex flex-wrap gap-2">
            {dbInfo.schemas.map((schema) => (
              <span
                key={schema}
                className="px-3 py-1 bg-indigo-50 text-indigo-700 rounded-full text-sm font-medium border border-indigo-200"
              >
                {schema}
              </span>
            ))}
          </div>
        </div>
      </div>

      {/* Footer */}
      <div className="text-center text-sm text-muted-foreground border-t border-border pt-4">
        <div className="flex items-center justify-center gap-2">
          <Calendar className="w-4 h-4" />
          <span>Última atualização: {new Date(dbInfo.collectedAt).toLocaleString('pt-BR')}</span>
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