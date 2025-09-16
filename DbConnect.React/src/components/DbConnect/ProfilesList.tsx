import React from 'react';
import { Database, Server, Calendar, User, Play, Settings, Trash2, Unplug } from 'lucide-react';

interface Profile {
  id: number;
  name: string;
  kind: number;
  hostOrFile: string;
  port?: number;
  database: string;
  username: string;
  createdAtUtc?: string;
}

interface ProfilesListProps {
  items: Profile[];
  onConnect: (profile: Profile) => void;
  onEdit?: (profile: Profile) => void;
  onDelete?: (profile: Profile) => void;
  activeProfileId?: number | null;
}

const DbKindLabels = ['PostgreSQL', 'SQL Server', 'MySQL', 'SQLite'];
const DbKindColors = [
  'bg-blue-50 text-blue-700 border-blue-200',
  'bg-orange-50 text-orange-700 border-orange-200', 
  'bg-cyan-50 text-cyan-700 border-cyan-200',
  'bg-gray-50 text-gray-700 border-gray-200',
];
const DbKindIcons = [Database, Server, Database, Database];

export function ProfilesList({ items, onConnect, onEdit, onDelete, activeProfileId }: ProfilesListProps) {
  console.log('📋 ProfilesList rendered with:', items?.length || 0, 'items');
  console.log('📋 ProfilesList items data:', items);
  
  if (!items || items.length === 0) {
    console.log('📋 No items found, showing placeholder');
    return (
      <div className="bg-gradient-card rounded-2xl shadow-md border border-border p-12 text-center">
        <div className="w-20 h-20 mx-auto bg-gradient-primary rounded-2xl flex items-center justify-center mb-6 animate-float">
          <Database className="w-10 h-10 text-primary-foreground" />
        </div>
        <h3 className="text-xl font-semibold text-card-foreground mb-2 font-heading">
          Nenhum perfil encontrado
        </h3>
        <p className="text-muted-foreground mb-6 max-w-md mx-auto">
          Crie seu primeiro perfil de conexão para começar a gerenciar seus bancos de dados
        </p>
        <div className="inline-flex items-center gap-2 px-4 py-2 bg-primary/10 text-primary rounded-lg text-sm font-medium">
          <span>👆</span>
          <span>Use o formulário acima</span>
        </div>
      </div>
    );
  }

  const formatDate = (dateString?: string) => {
    if (!dateString) return 'Data não informada';
    return new Date(dateString).toLocaleDateString('pt-BR');
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between mb-6">
        <div>
          <h2 className="text-xl font-semibold text-card-foreground font-heading">
            Meus Perfis de Conexão
          </h2>
          <p className="text-muted-foreground text-sm">
            {items.length} {items.length === 1 ? 'perfil configurado' : 'perfis configurados'}
          </p>
        </div>
      </div>

      {items.map((profile) => {
        const DbIcon = DbKindIcons[profile.kind] || Database;
        const dbColor = DbKindColors[profile.kind] || 'bg-gray-50 text-gray-700 border-gray-200';
        const dbLabel = DbKindLabels[profile.kind] || 'Unknown';
        const isActive = activeProfileId === profile.id;
        
        return (
          <div
            key={profile.id}
            className={`bg-gradient-card rounded-xl shadow-sm border p-6 hover:shadow-md transition-all duration-200 group ${
              isActive 
                ? 'border-green-400 bg-green-50/50 shadow-green-100' 
                : 'border-border hover:border-primary/20'
            }`}
          >
            <div className="flex items-start justify-between">
              <div className="flex-1">
                <div className="flex items-start gap-4">
                  <div className={`w-12 h-12 rounded-lg flex items-center justify-center border ${dbColor}`}>
                    <DbIcon className="w-6 h-6" />
                  </div>
                  
                  <div className="flex-1">
                    <div className="flex items-start justify-between">
                      <div>
                        <div className="flex items-center gap-2 mb-1">
                          <h4 className="text-lg font-semibold text-card-foreground group-hover:text-primary transition-colors">
                            {profile.name}
                          </h4>
                          {isActive && (
                            <div className="flex items-center gap-1">
                              <div className="w-2 h-2 bg-green-500 rounded-full animate-pulse"></div>
                              <span className="text-xs text-green-600 font-medium">Conectado</span>
                            </div>
                          )}
                        </div>
                        <div className="flex items-center gap-2 mb-3">
                          <span className={`px-2 py-1 rounded-lg text-xs font-medium border ${dbColor}`}>
                            {dbLabel}
                          </span>
                        </div>
                      </div>
                    </div>

                    <div className="space-y-2 text-sm text-muted-foreground">
                      <div className="flex items-center gap-2">
                        <Server className="w-4 h-4" />
                        <span>
                          {profile.hostOrFile}
                          {profile.port && `:${profile.port}`}
                        </span>
                      </div>
                      <div className="flex items-center gap-2">
                        <Database className="w-4 h-4" />
                        <span>Database: {profile.database}</span>
                      </div>
                      <div className="flex items-center gap-2">
                        <User className="w-4 h-4" />
                        <span>Usuário: {profile.username}</span>
                      </div>
                      {profile.createdAtUtc && (
                        <div className="flex items-center gap-2">
                          <Calendar className="w-4 h-4" />
                          <span>Criado em: {formatDate(profile.createdAtUtc)}</span>
                        </div>
                      )}
                    </div>
                  </div>
                </div>
              </div>

              <div className="flex items-center gap-2 ml-4">
                <button
                  className="p-2 text-muted-foreground hover:text-card-foreground hover:bg-secondary rounded-lg transition-all duration-200"
                  title="Configurações"
                  onClick={() => onEdit?.(profile)}
                >
                  <Settings className="w-4 h-4" />
                </button>
                <button
                  className="p-2 text-muted-foreground hover:text-error hover:bg-error-bg rounded-lg transition-all duration-200"
                  title="Excluir perfil"
                  onClick={() => onDelete?.(profile)}
                >
                  <Trash2 className="w-4 h-4" />
                </button>
                <button
                  className={`px-4 py-2 rounded-lg focus:outline-none focus:ring-2 transition-all duration-200 font-medium flex items-center gap-2 shadow-sm ${
                    isActive 
                      ? 'bg-red-100 text-red-700 hover:bg-red-200 focus:ring-red-300' 
                      : 'bg-gradient-primary text-primary-foreground hover:shadow-brand focus:ring-primary/50'
                  }`}
                  onClick={() => onConnect(profile)}
                >
                  {isActive ? (
                    <>
                      <Unplug className="w-4 h-4" />
                      <span>Desconectar</span>
                    </>
                  ) : (
                    <>
                      <Play className="w-4 h-4" />
                      <span>Conectar</span>
                    </>
                  )}
                </button>
              </div>
            </div>
          </div>
        );
      })}
    </div>
  );
}