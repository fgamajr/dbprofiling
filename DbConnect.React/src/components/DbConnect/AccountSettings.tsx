import React, { useState, useEffect } from 'react';
import { User, Lock, Key, Eye, EyeOff, Save, CheckCircle, XCircle } from 'lucide-react';
import { LoadingSpinner } from './LoadingSpinner';
import { Toast } from './Toast';
import { ApiKeySetup } from './ApiKeySetup';

interface User {
  id: number;
  username: string;
}

interface AccountSettingsProps {
  user: User;
  onClose: () => void;
}

interface ApiKeyStatus {
  openai: boolean;
  claude: boolean;
  hasAnyKey: boolean;
}

export function AccountSettings({ user, onClose }: AccountSettingsProps) {
  const [showPasswordChange, setShowPasswordChange] = useState(false);
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [showPasswords, setShowPasswords] = useState({ current: false, new: false, confirm: false });
  const [loading, setLoading] = useState(false);
  const [toast, setToast] = useState<{ type: 'success' | 'error' | 'warning'; message: string } | null>(null);
  const [apiKeySetupOpen, setApiKeySetupOpen] = useState(false);
  const [apiKeyStatus, setApiKeyStatus] = useState<ApiKeyStatus | null>(null);

  useEffect(() => {
    loadApiKeyStatus();
  }, []);

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

  async function handlePasswordChange() {
    if (!currentPassword.trim() || !newPassword.trim()) {
      setToast({ type: 'error', message: 'Preencha a senha atual e nova senha' });
      return;
    }

    if (newPassword !== confirmPassword) {
      setToast({ type: 'error', message: 'Nova senha e confirmação não coincidem' });
      return;
    }

    if (newPassword.length < 6) {
      setToast({ type: 'error', message: 'Nova senha deve ter pelo menos 6 caracteres' });
      return;
    }

    setLoading(true);
    try {
      const response = await fetch('/api/u/account/change-password', {
        method: 'POST',
        credentials: 'include',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          currentPassword,
          newPassword
        })
      });

      const result = await response.json();
      
      if (response.ok && result.success) {
        setToast({ type: 'success', message: 'Senha alterada com sucesso!' });
        setShowPasswordChange(false);
        setCurrentPassword('');
        setNewPassword('');
        setConfirmPassword('');
      } else {
        setToast({ type: 'error', message: result.message || 'Erro ao alterar senha' });
      }
    } catch (error) {
      setToast({ type: 'error', message: 'Erro ao alterar senha' });
    } finally {
      setLoading(false);
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

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-xl shadow-2xl max-w-2xl w-full max-h-[90vh] overflow-y-auto">
        {/* Header */}
        <div className="flex items-center justify-between p-6 border-b border-gray-200">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-blue-100 rounded-lg flex items-center justify-center">
              <User className="w-5 h-5 text-blue-600" />
            </div>
            <div>
              <h2 className="text-xl font-semibold text-gray-900">Minha Conta</h2>
              <p className="text-sm text-gray-500">Gerencie suas configurações e API Keys</p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
          >
            ×
          </button>
        </div>

        {/* Content */}
        <div className="p-6 space-y-6">
          {/* User Info */}
          <div className="bg-gray-50 rounded-lg p-4">
            <h3 className="text-lg font-semibold text-gray-900 mb-3 flex items-center gap-2">
              <User className="w-5 h-5 text-gray-600" />
              Informações da Conta
            </h3>
            <div className="space-y-2">
              <div>
                <label className="text-sm font-medium text-gray-700">Usuário</label>
                <div className="mt-1 px-3 py-2 bg-white border border-gray-300 rounded-lg text-gray-900 font-medium">
                  {user.username}
                </div>
              </div>
            </div>
          </div>

          {/* Password Change */}
          <div className="bg-gray-50 rounded-lg p-4">
            <h3 className="text-lg font-semibold text-gray-900 mb-3 flex items-center gap-2">
              <Lock className="w-5 h-5 text-gray-600" />
              Alterar Senha
            </h3>
            
            {!showPasswordChange ? (
              <button
                onClick={() => setShowPasswordChange(true)}
                className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors"
              >
                Alterar Senha
              </button>
            ) : (
              <div className="space-y-4">
                <div>
                  <label className="text-sm font-medium text-gray-700">Senha Atual</label>
                  <div className="relative mt-1">
                    <input
                      type={showPasswords.current ? 'text' : 'password'}
                      value={currentPassword}
                      onChange={(e) => setCurrentPassword(e.target.value)}
                      className="w-full px-3 py-2 pr-10 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                    />
                    <button
                      type="button"
                      onClick={() => setShowPasswords(prev => ({...prev, current: !prev.current}))}
                      className="absolute right-3 top-2.5 text-gray-400 hover:text-gray-600"
                    >
                      {showPasswords.current ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                    </button>
                  </div>
                </div>

                <div>
                  <label className="text-sm font-medium text-gray-700">Nova Senha</label>
                  <div className="relative mt-1">
                    <input
                      type={showPasswords.new ? 'text' : 'password'}
                      value={newPassword}
                      onChange={(e) => setNewPassword(e.target.value)}
                      className="w-full px-3 py-2 pr-10 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                    />
                    <button
                      type="button"
                      onClick={() => setShowPasswords(prev => ({...prev, new: !prev.new}))}
                      className="absolute right-3 top-2.5 text-gray-400 hover:text-gray-600"
                    >
                      {showPasswords.new ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                    </button>
                  </div>
                </div>

                <div>
                  <label className="text-sm font-medium text-gray-700">Confirmar Nova Senha</label>
                  <div className="relative mt-1">
                    <input
                      type={showPasswords.confirm ? 'text' : 'password'}
                      value={confirmPassword}
                      onChange={(e) => setConfirmPassword(e.target.value)}
                      className="w-full px-3 py-2 pr-10 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                    />
                    <button
                      type="button"
                      onClick={() => setShowPasswords(prev => ({...prev, confirm: !prev.confirm}))}
                      className="absolute right-3 top-2.5 text-gray-400 hover:text-gray-600"
                    >
                      {showPasswords.confirm ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                    </button>
                  </div>
                </div>

                <div className="flex items-center gap-3">
                  <button
                    onClick={handlePasswordChange}
                    disabled={loading}
                    className="px-4 py-2 bg-green-600 text-white rounded-lg hover:bg-green-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors flex items-center gap-2"
                  >
                    {loading ? (
                      <>
                        <LoadingSpinner size="sm" className="text-white" />
                        Alterando...
                      </>
                    ) : (
                      <>
                        <Save className="w-4 h-4" />
                        Salvar
                      </>
                    )}
                  </button>
                  <button
                    onClick={() => {
                      setShowPasswordChange(false);
                      setCurrentPassword('');
                      setNewPassword('');
                      setConfirmPassword('');
                    }}
                    className="px-4 py-2 bg-gray-300 text-gray-700 rounded-lg hover:bg-gray-400 transition-colors"
                  >
                    Cancelar
                  </button>
                </div>
              </div>
            )}
          </div>

          {/* API Keys Management */}
          <div className="bg-gray-50 rounded-lg p-4">
            <h3 className="text-lg font-semibold text-gray-900 mb-3 flex items-center gap-2">
              <Key className="w-5 h-5 text-gray-600" />
              API Keys para IA
            </h3>
            <p className="text-sm text-gray-600 mb-4">
              Configure suas API Keys para usar análise AI de qualidade de dados
            </p>

            <div className="space-y-3">
              {/* OpenAI Status */}
              <div className="flex items-center justify-between p-3 bg-white border border-gray-200 rounded-lg">
                <div className="flex items-center gap-3">
                  <div className="w-8 h-8 bg-green-100 rounded-full flex items-center justify-center">
                    <span className="text-green-600 font-bold text-sm">AI</span>
                  </div>
                  <div>
                    <div className="font-medium text-gray-900">OpenAI (GPT-4)</div>
                    <div className="text-sm text-gray-500">Para análise avançada de dados</div>
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  {apiKeyStatus?.openai ? (
                    <div className="flex items-center gap-1 text-green-600">
                      <CheckCircle className="w-4 h-4" />
                      <span className="text-sm font-medium">Configurada</span>
                    </div>
                  ) : (
                    <div className="flex items-center gap-1 text-gray-500">
                      <XCircle className="w-4 h-4" />
                      <span className="text-sm">Não configurada</span>
                    </div>
                  )}
                </div>
              </div>

              {/* Claude Status */}
              <div className="flex items-center justify-between p-3 bg-white border border-gray-200 rounded-lg">
                <div className="flex items-center gap-3">
                  <div className="w-8 h-8 bg-purple-100 rounded-full flex items-center justify-center">
                    <span className="text-purple-600 font-bold text-sm">C</span>
                  </div>
                  <div>
                    <div className="font-medium text-gray-900">Claude (Anthropic)</div>
                    <div className="text-sm text-gray-500">Análise alternativa de qualidade</div>
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  {apiKeyStatus?.claude ? (
                    <div className="flex items-center gap-1 text-green-600">
                      <CheckCircle className="w-4 h-4" />
                      <span className="text-sm font-medium">Configurada</span>
                    </div>
                  ) : (
                    <div className="flex items-center gap-1 text-gray-500">
                      <XCircle className="w-4 h-4" />
                      <span className="text-sm">Não configurada</span>
                    </div>
                  )}
                </div>
              </div>

              <button
                onClick={() => setApiKeySetupOpen(true)}
                className="w-full px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors flex items-center justify-center gap-2"
              >
                <Key className="w-4 h-4" />
                {apiKeyStatus?.hasAnyKey ? 'Gerenciar API Keys' : 'Configurar API Keys'}
              </button>
            </div>
          </div>
        </div>
      </div>

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