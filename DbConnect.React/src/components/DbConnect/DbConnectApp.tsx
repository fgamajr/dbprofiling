import React, { useState, useEffect } from 'react';
import { Database, Menu, X } from 'lucide-react';
import { AuthCard } from './AuthCard';
import { Avatar } from './Avatar';
import { Sidebar } from './Sidebar';
import { ProfileForm } from './ProfileForm';
import { ProfilesList } from './ProfilesList';
import { PlaceholderSection } from './PlaceholderSection';
import { DatabaseDashboard } from './DatabaseDashboard';
import { TablesExplorer } from './TablesExplorer';
import { EssentialMetrics } from './EssentialMetrics';
import { AccountSettings } from './AccountSettings';
import { LoadingSpinner } from './LoadingSpinner';
import { Toast } from './Toast';
import { apiService } from '../../services/api';
import type { User, Profile } from '../../services/api';


export function DbConnectApp() {
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(true);
  const [section, setSection] = useState('profiles');
  const [profiles, setProfiles] = useState<Profile[]>([]);
  const [toast, setToast] = useState<{ type: 'success' | 'error' | 'warning'; message: string } | null>(null);
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [accountSettingsOpen, setAccountSettingsOpen] = useState(false);
  const [editingProfile, setEditingProfile] = useState<Profile | null>(null);
  const [activeProfileId, setActiveProfileId] = useState<number | null>(null);


  useEffect(() => {
    checkAuth();
  }, []);

  async function checkAuth() {
    console.log('🔍 Starting auth check...');
    try {
      const userData = await apiService.getMe();
      console.log('👤 User data received:', userData);
      setUser(userData);
      if (userData) {
        console.log('✅ User found, loading profiles...');
        await loadProfiles();
        await loadActiveProfile();
      } else {
        console.log('❌ No user found');
      }
    } catch (e) {
      console.error('Auth check failed:', e);
    } finally {
      setLoading(false);
    }
  }

  async function loadProfiles() {
    console.log('📋 Loading profiles...');
    try {
      const data = await apiService.listProfiles();
      console.log('📋 Profiles loaded:', data.length, 'profiles');
      console.log('📋 Profiles data:', data);
      setProfiles(data);
      // Se estiver editando, sair do modo de edição após salvar
      if (editingProfile) {
        setEditingProfile(null);
      }
    } catch (e) {
      console.error('Failed to load profiles:', e);
      setToast({ type: 'error', message: 'Falha ao carregar perfis' });
    }
  }

  async function loadActiveProfile() {
    console.log('🔌 Loading active profile...');
    try {
      const { activeProfileId } = await apiService.getActiveProfile();
      console.log('🔌 Active profile ID:', activeProfileId);
      setActiveProfileId(activeProfileId);
    } catch (e) {
      console.error('Failed to load active profile:', e);
      setActiveProfileId(null);
    }
  }

  async function handleUserAction(action: 'account' | 'logout') {
    if (action === 'logout') {
      await apiService.logout();
      setUser(null);
      setProfiles([]);
      setActiveProfileId(null);
      setSection('profiles');
      setToast({ type: 'success', message: 'Logout realizado com sucesso' });
    } else if (action === 'account') {
      setAccountSettingsOpen(true);
    }
  }

  async function handleConnect(profile: Profile) {
    const isCurrentlyActive = activeProfileId === profile.id;
    
    if (isCurrentlyActive) {
      // Desconectar
      console.log('🔌 Disconnecting from profile:', profile);
      setToast({ 
        type: 'success', 
        message: `Desconectando do perfil "${profile.name}"...` 
      });

      try {
        const result = await apiService.disconnectProfile();
        if (result.success) {
          setActiveProfileId(null);
          setToast({ 
            type: 'success', 
            message: result.message
          });
        } else {
          setToast({ 
            type: 'error', 
            message: result.message 
          });
        }
      } catch (error) {
        setToast({ 
          type: 'error', 
          message: 'Erro ao desconectar do perfil' 
        });
      }
    } else {
      // Conectar
      console.log('🔌 Connecting to profile:', profile);
      setToast({ 
        type: 'success', 
        message: `Conectando ao perfil "${profile.name}" (${profile.database})...` 
      });

      try {
        const result = await apiService.connectProfile(profile.id);
        if (result.success) {
          setActiveProfileId(profile.id);
          setToast({ 
            type: 'success', 
            message: result.message
          });
        } else {
          setToast({ 
            type: 'error', 
            message: result.message 
          });
        }
      } catch (error) {
        setToast({ 
          type: 'error', 
          message: 'Erro ao conectar ao perfil' 
        });
      }
    }
  }

  function handleEdit(profile: Profile) {
    setEditingProfile(profile);
    setToast({ 
      type: 'success', 
      message: `Editando perfil "${profile.name}". Modifique os campos acima.` 
    });
  }

  function handleCancelEdit() {
    setEditingProfile(null);
    setToast({ 
      type: 'success', 
      message: 'Edição cancelada.' 
    });
  }

  async function handleDelete(profile: Profile) {
    if (!confirm(`Tem certeza que deseja excluir o perfil "${profile.name}"?`)) {
      return;
    }
    
    try {
      await apiService.deleteProfile(profile.id);
      setToast({ 
        type: 'success', 
        message: `Perfil "${profile.name}" excluído com sucesso!` 
      });
      await loadProfiles(); // Recarrega a lista
    } catch (e: any) {
      setToast({ 
        type: 'error', 
        message: e.message || 'Erro ao excluir perfil' 
      });
    }
  }

  if (loading) {
    return (
      <div className="min-h-screen bg-gradient-surface flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 mx-auto bg-gradient-primary rounded-2xl flex items-center justify-center mb-4 animate-glow">
            <Database className="w-8 h-8 text-primary-foreground" />
          </div>
          <LoadingSpinner size="lg" className="mb-4 text-primary" />
          <p className="text-muted-foreground font-medium">Carregando DbConnect...</p>
        </div>
      </div>
    );
  }

  if (!user) {
    return <AuthCard onLogged={setUser} />;
  }

  return (
    <div className="min-h-screen bg-gradient-surface">
      {/* Header */}
      <header className="bg-card border-b border-border sticky top-0 z-30 shadow-sm">
        <div className="flex items-center justify-between px-4 lg:px-6 py-4">
          <div className="flex items-center gap-4">
            {/* Mobile menu button */}
            <button
              className="lg:hidden p-2 hover:bg-secondary rounded-lg transition-colors"
              onClick={() => setSidebarOpen(!sidebarOpen)}
            >
              {sidebarOpen ? <X className="w-5 h-5" /> : <Menu className="w-5 h-5" />}
            </button>
            
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 bg-gradient-primary rounded-xl flex items-center justify-center">
                <Database className="w-5 h-5 text-primary-foreground" />
              </div>
              <div>
                <h1 className="text-xl font-bold bg-gradient-primary bg-clip-text text-transparent font-heading">
                  DbConnect
                </h1>
                <p className="text-xs text-muted-foreground hidden sm:block">
                  Database Management Platform
                </p>
              </div>
            </div>
          </div>
          
          <Avatar user={user} onAction={handleUserAction} />
        </div>
      </header>

      <div className="flex">
        {/* Sidebar */}
        <div className={`fixed inset-y-0 left-0 z-20 transform ${
          sidebarOpen ? 'translate-x-0' : '-translate-x-full'
        } lg:translate-x-0 lg:static lg:inset-0 transition-transform duration-200 ease-in-out`}>
          <Sidebar section={section} setSection={setSection} />
        </div>

        {/* Mobile sidebar overlay */}
        {sidebarOpen && (
          <div
            className="fixed inset-0 bg-black bg-opacity-50 z-10 lg:hidden"
            onClick={() => setSidebarOpen(false)}
          />
        )}

        {/* Main Content */}
        <main className="flex-1 lg:ml-0">
          <div className="p-4 lg:p-8">
            <div className="max-w-7xl mx-auto space-y-8">
              {section === 'profiles' ? (
                <>
                  <ProfileForm 
                    onSaved={loadProfiles} 
                    editingProfile={editingProfile}
                    onCancelEdit={handleCancelEdit}
                    onToast={setToast}
                  />
                  <ProfilesList 
                    items={profiles} 
                    onConnect={handleConnect} 
                    onEdit={handleEdit} 
                    onDelete={handleDelete}
                    activeProfileId={activeProfileId}
                  />
                  {/* Debug: Show profiles state */}
                  <div style={{fontSize: '10px', color: 'red', marginTop: '10px'}}>
                    DEBUG: profiles.length = {profiles?.length || 0}
                  </div>
                </>
              ) : section === 'databases' ? (
                <DatabaseDashboard 
                  isConnected={activeProfileId !== null} 
                  activeProfileId={activeProfileId}
                />
              ) : section === 'tables' ? (
                <TablesExplorer
                  isConnected={activeProfileId !== null}
                  activeProfileId={activeProfileId}
                />
              ) : (
                <PlaceholderSection section={section} />
              )}
            </div>
          </div>
        </main>
      </div>

      {/* Toast notifications */}
      {toast && (
        <Toast
          type={toast.type}
          message={toast.message}
          onClose={() => setToast(null)}
        />
      )}

      {/* Account Settings Modal */}
      {accountSettingsOpen && user && (
        <AccountSettings
          user={user}
          onClose={() => setAccountSettingsOpen(false)}
        />
      )}
    </div>
  );
}