import React from 'react';
import { Database, Server, Table, BarChart3, Plug, Activity } from 'lucide-react';

interface SidebarProps {
  section: string;
  setSection: (section: string) => void;
  className?: string;
}

export function Sidebar({ section, setSection, className = '' }: SidebarProps) {
  const items = [
    { id: 'profiles', label: 'Perfis de Conexão', icon: Plug, description: 'Gerencie suas conexões' },
    { id: 'databases', label: 'Bancos de Dados', icon: Database, description: 'Explore seus bancos' },
    { id: 'tables', label: 'Tabelas', icon: Table, description: 'Visualize estruturas' },
    { id: 'reports', label: 'Relatórios', icon: BarChart3, description: 'Analytics e dados' },
  ];

  return (
    <aside className={`w-72 h-screen bg-sidebar border-r border-sidebar-border ${className}`}>
      <div className="p-6">
        <div className="flex items-center gap-3 mb-2">
          <div className="w-8 h-8 bg-gradient-primary rounded-lg flex items-center justify-center">
            <Server className="w-4 h-4 text-primary-foreground" />
          </div>
          <h2 className="text-lg font-semibold text-sidebar-foreground font-heading">
            Menu Principal
          </h2>
        </div>
        <p className="text-sm text-muted-foreground">
          Navegue pelas funcionalidades
        </p>
      </div>
      
      <nav className="px-4 space-y-2">
        {items.map((item) => {
          const Icon = item.icon;
          const isActive = section === item.id;
          
          return (
            <button
              key={item.id}
              onClick={() => setSection(item.id)}
              className={`w-full flex items-start p-4 rounded-lg transition-all duration-200 text-left group ${
                isActive
                  ? 'bg-gradient-primary text-primary-foreground shadow-brand'
                  : 'text-sidebar-foreground hover:bg-sidebar-hover hover:shadow-sm'
              }`}
            >
              <Icon className={`w-5 h-5 mt-0.5 flex-shrink-0 ${
                isActive ? 'text-primary-foreground' : 'text-muted-foreground group-hover:text-sidebar-foreground'
              }`} />
              <div className="ml-3 flex-1">
                <div className={`font-medium text-sm ${
                  isActive ? 'text-primary-foreground' : 'text-sidebar-foreground'
                }`}>
                  {item.label}
                </div>
                <div className={`text-xs mt-0.5 ${
                  isActive ? 'text-primary-foreground/80' : 'text-muted-foreground'
                }`}>
                  {item.description}
                </div>
              </div>
            </button>
          );
        })}
      </nav>

      {/* Status Footer */}
      <div className="absolute bottom-0 left-0 right-0 p-4 border-t border-sidebar-border">
        <div className="flex items-center gap-3 text-sm text-muted-foreground">
          <div className="w-2 h-2 bg-success rounded-full animate-pulse"></div>
          <span>Sistema online</span>
        </div>
      </div>
    </aside>
  );
}