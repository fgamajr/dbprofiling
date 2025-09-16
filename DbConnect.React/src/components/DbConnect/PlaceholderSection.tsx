import React from 'react';
import { Database, Table, BarChart3, Cog, Sparkles } from 'lucide-react';

interface PlaceholderSectionProps {
  section: string;
}

export function PlaceholderSection({ section }: PlaceholderSectionProps) {
  const sectionConfig = {
    databases: {
      icon: Database,
      title: 'Bancos de Dados',
      description: 'Explore e gerencie seus bancos conectados',
      features: [
        'Visualizar esquemas de banco',
        'Executar consultas SQL',
        'Monitorar performance',
        'Backup e restore'
      ],
      color: 'text-blue-600',
      bgColor: 'bg-blue-50',
      borderColor: 'border-blue-200'
    },
    tables: {
      icon: Table,
      title: 'Tabelas',
      description: 'Visualize e edite estruturas de dados',
      features: [
        'Visualizar estrutura das tabelas',
        'Editar registros inline',
        'Criar novos índices',
        'Relacionamentos e chaves'
      ],
      color: 'text-green-600',
      bgColor: 'bg-green-50',
      borderColor: 'border-green-200'
    },
    reports: {
      icon: BarChart3,
      title: 'Relatórios',
      description: 'Analytics e insights dos seus dados',
      features: [
        'Dashboards interativos',
        'Relatórios personalizados',
        'Exportação de dados',
        'Agendamento automático'
      ],
      color: 'text-purple-600',
      bgColor: 'bg-purple-50',
      borderColor: 'border-purple-200'
    }
  };

  const config = sectionConfig[section as keyof typeof sectionConfig] || sectionConfig.databases;
  const Icon = config.icon;

  return (
    <div className="bg-gradient-card rounded-2xl shadow-md border border-border p-12 text-center">
      <div className={`w-24 h-24 mx-auto rounded-2xl flex items-center justify-center mb-6 animate-float ${config.bgColor} border ${config.borderColor}`}>
        <Icon className={`w-12 h-12 ${config.color}`} />
      </div>
      
      <div className="mb-8">
        <h3 className="text-2xl font-bold text-card-foreground mb-3 font-heading">
          {config.title}
        </h3>
        <p className="text-muted-foreground text-lg max-w-md mx-auto">
          {config.description}
        </p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4 max-w-lg mx-auto mb-8">
        {config.features.map((feature, index) => (
          <div
            key={index}
            className="flex items-center gap-2 p-3 bg-background/50 rounded-lg border border-border/50"
          >
            <div className={`w-2 h-2 rounded-full ${config.bgColor}`} />
            <span className="text-sm text-card-foreground font-medium">{feature}</span>
          </div>
        ))}
      </div>

      <div className="flex items-center justify-center gap-2 px-6 py-3 bg-gradient-primary text-primary-foreground rounded-lg shadow-brand max-w-xs mx-auto">
        <Cog className="w-4 h-4 animate-spin" />
        <span className="font-medium">Em desenvolvimento...</span>
        <Sparkles className="w-4 h-4" />
      </div>

      <p className="text-muted-foreground text-sm mt-4">
        Esta funcionalidade estará disponível em breve
      </p>
    </div>
  );
}