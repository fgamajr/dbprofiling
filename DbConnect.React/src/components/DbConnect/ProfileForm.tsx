import React, { useState, useEffect } from 'react';
import { Plus, TestTube, Save, Database } from 'lucide-react';
import { LoadingSpinner } from './LoadingSpinner';
import { apiService } from '../../services/api';
import type { CreateProfileRequest } from '../../services/api';

interface Profile {
  name: string;
  kind: number;
  hostOrFile: string;
  port: string;
  database: string;
  username: string;
  password: string;
}

interface ProfileFormProps {
  onSaved?: () => void;
  editingProfile?: Profile | null;
  onCancelEdit?: () => void;
  onToast?: (toast: { type: 'success' | 'error' | 'warning'; message: string }) => void;
}

const DbKind = { PostgreSql: 0, SqlServer: 1, MySql: 2, Sqlite: 3 };
const DbKindLabels = ['PostgreSQL', 'SQL Server', 'MySQL', 'SQLite'];
const DbKindColors = [
  'bg-blue-100 text-blue-700 border-blue-200',
  'bg-orange-100 text-orange-700 border-orange-200',
  'bg-cyan-100 text-cyan-700 border-cyan-200',
  'bg-gray-100 text-gray-700 border-gray-200',
];

export function ProfileForm({ onSaved, editingProfile, onCancelEdit, onToast }: ProfileFormProps) {
  const [form, setForm] = useState<Profile>({
    name: '',
    kind: 0,
    hostOrFile: '',
    port: '',
    database: '',
    username: '',
    password: '',
  });
  const [isLoading, setIsLoading] = useState(false);

  // Popula form quando estiver editando
  useEffect(() => {
    if (editingProfile) {
      setForm({
        name: editingProfile.name,
        kind: editingProfile.kind,
        hostOrFile: editingProfile.hostOrFile,
        port: editingProfile.port ? editingProfile.port.toString() : '',
        database: editingProfile.database,
        username: editingProfile.username,
        password: '', // Senha sempre vazia na edição
      });
    }
  }, [editingProfile]);

  function updateForm(key: keyof Profile, value: string | number) {
    setForm((f) => ({ ...f, [key]: value }));
  }


  async function handleSave() {
    if (!form.name.trim() || !form.hostOrFile.trim() || !form.database.trim() || !form.username.trim()) {
      onToast?.({ type: 'error', message: 'Preencha todos os campos obrigatórios' });
      return;
    }

    setIsLoading(true);
    try {
      const payload: CreateProfileRequest = {
        ...form,
        port: form.port ? parseInt(form.port) : undefined,
      };
      
      if (editingProfile) {
        // Modo edição
        await apiService.updateProfile(editingProfile.id, payload);
        onToast?.({ type: 'success', message: 'Perfil atualizado e testado com sucesso!' });
      } else {
        // Modo criação
        await apiService.createProfile(payload);
        onToast?.({ type: 'success', message: 'Perfil salvo e testado com sucesso!' });
      }

      // Limpa form apenas se não estiver editando
      if (!editingProfile) {
        setForm({
          name: '',
          kind: 0,
          hostOrFile: '',
          port: '',
          database: '',
          username: '',
          password: '',
        });
      }
      
      onSaved && onSaved();
    } catch (e: any) {
      onToast?.({ type: 'error', message: e.message || 'Erro ao salvar perfil' });
    } finally {
      setIsLoading(false);
    }
  }

  function handleCancel() {
    setForm({
      name: '',
      kind: 0,
      hostOrFile: '',
      port: '',
      database: '',
      username: '',
      password: '',
    });
    onCancelEdit && onCancelEdit();
  }

  const selectedDbKind = DbKindLabels[form.kind];
  const selectedDbColor = DbKindColors[form.kind];

  return (
    <div className="bg-gradient-card rounded-2xl shadow-md border border-border p-8">
      <div className="flex items-center gap-3 mb-8">
        <div className="w-10 h-10 bg-gradient-primary rounded-lg flex items-center justify-center">
          <Plus className="w-5 h-5 text-primary-foreground" />
        </div>
        <div>
          <h3 className="text-xl font-semibold text-card-foreground font-heading">
            {editingProfile ? 'Editar Conexão' : 'Nova Conexão'}
          </h3>
          <p className="text-muted-foreground text-sm">
            {editingProfile ? 'Modifique os dados da conexão existente' : 'Configure um novo perfil de banco de dados'}
          </p>
        </div>
      </div>
      
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Nome do Perfil */}
        <div className="lg:col-span-2">
          <label className="block text-sm font-medium text-card-foreground mb-2">
            Nome do Perfil *
          </label>
          <input
            className="w-full px-4 py-3 rounded-lg border border-border bg-input text-card-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-primary/50 focus:border-primary transition-all duration-200"
            value={form.name}
            onChange={(e) => updateForm('name', e.target.value)}
            placeholder="Ex: Produção PostgreSQL, Desenvolvimento MySQL..."
            disabled={isLoading}
          />
        </div>

        {/* Tipo de Banco */}
        <div>
          <label className="block text-sm font-medium text-card-foreground mb-2">
            Tipo de Banco *
          </label>
          <div className="relative">
            <select
              className="w-full px-4 py-3 rounded-lg border border-border bg-input text-card-foreground focus:outline-none focus:ring-2 focus:ring-primary/50 focus:border-primary transition-all duration-200 appearance-none"
              value={form.kind}
              onChange={(e) => updateForm('kind', parseInt(e.target.value))}
              disabled={isLoading}
            >
              {DbKindLabels.map((label, i) => (
                <option key={i} value={i}>
                  {label}
                </option>
              ))}
            </select>
            <div className="absolute right-3 top-1/2 transform -translate-y-1/2">
              <span className={`px-2 py-1 rounded text-xs font-medium border ${selectedDbColor}`}>
                {selectedDbKind}
              </span>
            </div>
          </div>
        </div>

        {/* Host/Arquivo */}
        <div>
          <label className="block text-sm font-medium text-card-foreground mb-2">
            Host/Arquivo *
          </label>
          <input
            className="w-full px-4 py-3 rounded-lg border border-border bg-input text-card-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-primary/50 focus:border-primary transition-all duration-200"
            value={form.hostOrFile}
            onChange={(e) => updateForm('hostOrFile', e.target.value)}
            placeholder={form.kind === 3 ? '/caminho/para/arquivo.db' : 'localhost ou IP do servidor'}
            disabled={isLoading}
          />
        </div>

        {/* Porta */}
        <div>
          <label className="block text-sm font-medium text-card-foreground mb-2">
            Porta
            <span className="text-muted-foreground text-xs ml-1">(deixe vazio para usar padrão)</span>
          </label>
          <input
            className="w-full px-4 py-3 rounded-lg border border-border bg-input text-card-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-primary/50 focus:border-primary transition-all duration-200"
            type="number"
            value={form.port}
            onChange={(e) => updateForm('port', e.target.value)}
            placeholder="5432, 3306, 1433..."
            disabled={isLoading || form.kind === 3}
          />
        </div>

        {/* Database */}
        <div>
          <label className="block text-sm font-medium text-card-foreground mb-2">
            Nome do Banco *
          </label>
          <input
            className="w-full px-4 py-3 rounded-lg border border-border bg-input text-card-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-primary/50 focus:border-primary transition-all duration-200"
            value={form.database}
            onChange={(e) => updateForm('database', e.target.value)}
            placeholder="Nome do banco de dados"
            disabled={isLoading}
          />
        </div>

        {/* Usuário */}
        <div>
          <label className="block text-sm font-medium text-card-foreground mb-2">
            Usuário *
          </label>
          <input
            className="w-full px-4 py-3 rounded-lg border border-border bg-input text-card-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-primary/50 focus:border-primary transition-all duration-200"
            value={form.username}
            onChange={(e) => updateForm('username', e.target.value)}
            placeholder="Nome de usuário do banco"
            disabled={isLoading}
          />
        </div>

        {/* Senha */}
        <div>
          <label className="block text-sm font-medium text-card-foreground mb-2">
            Senha
          </label>
          <input
            className="w-full px-4 py-3 rounded-lg border border-border bg-input text-card-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-primary/50 focus:border-primary transition-all duration-200"
            type="password"
            value={form.password}
            onChange={(e) => updateForm('password', e.target.value)}
            placeholder="Senha de acesso (opcional)"
            disabled={isLoading}
          />
        </div>
      </div>

      {/* Botões de Ação */}
      <div className="mt-8 flex justify-end gap-3">
        {editingProfile && (
          <button
            className="px-6 py-3 border border-border text-card-foreground rounded-lg hover:bg-secondary focus:outline-none focus:ring-2 focus:ring-primary/50 transition-all duration-200 font-medium"
            onClick={handleCancel}
            disabled={isLoading}
          >
            Cancelar
          </button>
        )}
        <button
          className="px-8 py-3 bg-gradient-primary text-primary-foreground rounded-lg hover:shadow-brand focus:outline-none focus:ring-2 focus:ring-primary/50 transition-all duration-200 font-medium flex items-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed shadow-md"
          onClick={handleSave}
          disabled={isLoading}
        >
          {isLoading ? (
            <>
              <LoadingSpinner size="sm" />
              <span>Testando conexão...</span>
            </>
          ) : (
            <>
              <Save className="w-4 h-4" />
              <span>{editingProfile ? 'Atualizar e Testar' : 'Salvar e Testar'}</span>
            </>
          )}
        </button>
      </div>

    </div>
  );
}