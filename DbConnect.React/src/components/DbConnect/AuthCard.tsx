import React, { useState } from 'react';
import { Database, User, Lock, UserPlus, LogIn, Eye, EyeOff } from 'lucide-react';
import { LoadingSpinner } from './LoadingSpinner';
import { Toast } from './Toast';
import { apiService } from '../../services/api';

interface AuthCardProps {
  onLogged: (user: any) => void;
}

export function AuthCard({ onLogged }: AuthCardProps) {
  const [user, setUser] = useState('');
  const [pass, setPass] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [toast, setToast] = useState<{ type: 'success' | 'error' | 'warning'; message: string } | null>(null);


  async function handleRegister() {
    if (!user.trim() || !pass.trim()) {
      setToast({ type: 'error', message: 'Preencha usuário e senha para continuar' });
      return;
    }

    setIsLoading(true);
    try {
      const { ok, data } = await apiService.register(user.trim(), pass);
      if (ok) {
        setToast({ type: 'success', message: 'Usuário registrado! Fazendo login...' });
        setTimeout(() => handleLogin(), 1000);
      } else {
        setToast({ type: 'error', message: data.message || 'Falha ao registrar usuário' });
      }
    } catch (e) {
      setToast({ type: 'error', message: 'Erro de conexão com o servidor' });
    } finally {
      setIsLoading(false);
    }
  }

  async function handleLogin() {
    if (!user.trim() || !pass.trim()) {
      setToast({ type: 'error', message: 'Preencha usuário e senha para continuar' });
      return;
    }

    setIsLoading(true);
    try {
      const ok = await apiService.login(user.trim(), pass);
      if (ok) {
        const userData = await apiService.getMe();
        onLogged(userData);
        setToast({ type: 'success', message: 'Login realizado com sucesso!' });
      } else {
        setToast({ type: 'error', message: 'Usuário ou senha inválidos' });
      }
    } catch (e) {
      setToast({ type: 'error', message: 'Erro de conexão com o servidor' });
    } finally {
      setIsLoading(false);
    }
  }

  const handleKeyPress = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !isLoading) {
      handleLogin();
    }
  };

  return (
    <div className="min-h-screen bg-gradient-surface flex items-center justify-center py-12 px-4 sm:px-6 lg:px-8">
      <div className="max-w-md w-full space-y-8">
        {/* Header */}
        <div className="text-center animate-fade-in">
          <div className="mx-auto w-16 h-16 bg-gradient-primary rounded-2xl flex items-center justify-center shadow-brand mb-6 animate-float">
            <Database className="w-8 h-8 text-primary-foreground" />
          </div>
          <h1 className="text-4xl font-bold bg-gradient-primary bg-clip-text text-transparent font-heading">
            DbConnect
          </h1>
          <p className="mt-2 text-muted-foreground text-lg">
            Gerencie suas conexões de banco de dados
          </p>
        </div>

        {/* Auth Form */}
        <div className="bg-gradient-card p-8 rounded-2xl shadow-lg border border-border animate-slide-up">
          <h2 className="text-2xl font-semibold text-card-foreground mb-8 text-center font-heading">
            Acesse sua conta
          </h2>
          
          <div className="space-y-6">
            <div>
              <label className="block text-sm font-medium text-card-foreground mb-2">
                Usuário
              </label>
              <div className="relative">
                <User className="absolute left-3 top-1/2 transform -translate-y-1/2 w-5 h-5 text-muted-foreground" />
                <input
                  className="w-full pl-10 pr-4 py-3 rounded-lg border border-border bg-input text-card-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-primary/50 focus:border-primary transition-all duration-200"
                  value={user}
                  onChange={(e) => setUser(e.target.value)}
                  placeholder="Seu nome de usuário"
                  disabled={isLoading}
                />
              </div>
            </div>

            <div>
              <label className="block text-sm font-medium text-card-foreground mb-2">
                Senha
              </label>
              <div className="relative">
                <Lock className="absolute left-3 top-1/2 transform -translate-y-1/2 w-5 h-5 text-muted-foreground" />
                <input
                  className="w-full pl-10 pr-12 py-3 rounded-lg border border-border bg-input text-card-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-primary/50 focus:border-primary transition-all duration-200"
                  type={showPassword ? 'text' : 'password'}
                  value={pass}
                  onChange={(e) => setPass(e.target.value)}
                  placeholder="Sua senha"
                  disabled={isLoading}
                  onKeyPress={handleKeyPress}
                />
                <button
                  type="button"
                  className="absolute right-3 top-1/2 transform -translate-y-1/2 text-muted-foreground hover:text-card-foreground transition-colors"
                  onClick={() => setShowPassword(!showPassword)}
                >
                  {showPassword ? <EyeOff className="w-5 h-5" /> : <Eye className="w-5 h-5" />}
                </button>
              </div>
            </div>
          </div>

          <div className="flex gap-3 mt-8">
            <button
              className="flex-1 px-6 py-3 rounded-lg border border-border bg-secondary text-secondary-foreground hover:bg-secondary-hover transition-all duration-200 font-medium flex items-center justify-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed"
              onClick={handleRegister}
              disabled={isLoading}
            >
              {isLoading ? (
                <LoadingSpinner size="sm" />
              ) : (
                <>
                  <UserPlus className="w-4 h-4" />
                  <span>Registrar</span>
                </>
              )}
            </button>
            
            <button
              className="flex-1 px-6 py-3 rounded-lg bg-gradient-primary text-primary-foreground hover:shadow-brand focus:outline-none focus:ring-2 focus:ring-primary/50 transition-all duration-200 font-medium flex items-center justify-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed shadow-md"
              onClick={handleLogin}
              disabled={isLoading}
            >
              {isLoading ? (
                <LoadingSpinner size="sm" />
              ) : (
                <>
                  <LogIn className="w-4 h-4" />
                  <span>Entrar</span>
                </>
              )}
            </button>
          </div>
        </div>
      </div>

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