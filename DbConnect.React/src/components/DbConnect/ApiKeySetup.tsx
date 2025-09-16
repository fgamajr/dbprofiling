import React, { useState } from 'react';
import { Key, Eye, EyeOff, CheckCircle, XCircle, Loader2 } from 'lucide-react';

interface ApiKeySetupProps {
  isOpen: boolean;
  onClose: () => void;
  onSave: (provider: string, apiKey: string) => Promise<boolean>;
  currentStatus?: {
    openai: boolean;
    claude: boolean;
  };
}

export function ApiKeySetup({ isOpen, onClose, onSave, currentStatus }: ApiKeySetupProps) {
  const [provider, setProvider] = useState('openai');
  const [apiKey, setApiKey] = useState('');
  const [showKey, setShowKey] = useState(false);
  const [validating, setValidating] = useState(false);
  const [error, setError] = useState('');

  const handleSave = async () => {
    if (!apiKey.trim()) {
      setError('API Key é obrigatória');
      return;
    }

    setValidating(true);
    setError('');

    try {
      const success = await onSave(provider, apiKey.trim());
      if (success) {
        setApiKey('');
        onClose();
      } else {
        setError('API Key inválida ou não funcionou');
      }
    } catch (err) {
      setError('Erro ao validar API Key');
    } finally {
      setValidating(false);
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-xl shadow-2xl max-w-md w-full">
        {/* Header */}
        <div className="flex items-center justify-between p-6 border-b border-gray-200">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-blue-100 rounded-lg flex items-center justify-center">
              <Key className="w-5 h-5 text-blue-600" />
            </div>
            <div>
              <h2 className="text-xl font-semibold text-gray-900">Configurar API Key</h2>
              <p className="text-sm text-gray-500">Para usar análise AI de qualidade de dados</p>
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
        <div className="p-6 space-y-4">
          {/* Provider Selection */}
          <div className="space-y-2">
            <label className="text-sm font-medium text-gray-700">Provedor LLM</label>
            <div className="grid grid-cols-2 gap-2">
              <button
                onClick={() => setProvider('openai')}
                className={`p-3 rounded-lg border transition-all ${
                  provider === 'openai'
                    ? 'border-blue-500 bg-blue-50 text-blue-700'
                    : 'border-gray-300 hover:border-gray-400'
                }`}
              >
                <div className="flex items-center justify-between">
                  <span className="font-medium">OpenAI</span>
                  {currentStatus?.openai && (
                    <CheckCircle className="w-4 h-4 text-green-600" />
                  )}
                </div>
                <div className="text-xs text-gray-500 mt-1">GPT-4 Turbo</div>
              </button>
              <button
                onClick={() => setProvider('claude')}
                className={`p-3 rounded-lg border transition-all ${
                  provider === 'claude'
                    ? 'border-blue-500 bg-blue-50 text-blue-700'
                    : 'border-gray-300 hover:border-gray-400'
                }`}
              >
                <div className="flex items-center justify-between">
                  <span className="font-medium">Claude</span>
                  {currentStatus?.claude && (
                    <CheckCircle className="w-4 h-4 text-green-600" />
                  )}
                </div>
                <div className="text-xs text-gray-500 mt-1">Claude 3 Haiku</div>
              </button>
            </div>
          </div>

          {/* API Key Input */}
          <div className="space-y-2">
            <label className="text-sm font-medium text-gray-700">
              API Key {provider === 'openai' ? 'OpenAI' : 'Anthropic'}
            </label>
            <div className="relative">
              <input
                type={showKey ? 'text' : 'password'}
                value={apiKey}
                onChange={(e) => setApiKey(e.target.value)}
                placeholder={provider === 'openai' ? 'sk-...' : 'sk-ant-...'}
                className="w-full px-3 py-2 pr-10 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
              />
              <button
                type="button"
                onClick={() => setShowKey(!showKey)}
                className="absolute right-3 top-2.5 text-gray-400 hover:text-gray-600"
              >
                {showKey ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
              </button>
            </div>
            <p className="text-xs text-gray-500">
              {provider === 'openai' 
                ? 'Obtenha em: https://platform.openai.com/api-keys'
                : 'Obtenha em: https://console.anthropic.com/account/keys'
              }
            </p>
          </div>

          {/* Error */}
          {error && (
            <div className="flex items-center gap-2 p-3 bg-red-50 border border-red-200 rounded-lg">
              <XCircle className="w-4 h-4 text-red-600" />
              <span className="text-sm text-red-700">{error}</span>
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="flex items-center justify-between p-6 border-t border-gray-200 bg-gray-50">
          <div className="text-xs text-gray-500">
            Sua API Key será criptografada e salva localmente
          </div>
          <div className="flex items-center gap-3">
            <button
              onClick={onClose}
              className="px-4 py-2 text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 transition-colors"
            >
              Cancelar
            </button>
            <button
              onClick={handleSave}
              disabled={validating || !apiKey.trim()}
              className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors flex items-center gap-2"
            >
              {validating ? (
                <>
                  <Loader2 className="w-4 h-4 animate-spin" />
                  Validando...
                </>
              ) : (
                'Salvar API Key'
              )}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}