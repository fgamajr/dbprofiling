import React, { useState, useRef, useEffect } from 'react';
import { User, LogOut, Settings, ChevronDown } from 'lucide-react';

interface AvatarProps {
  user: { username: string } | null;
  onAction: (action: 'account' | 'logout') => void;
}

export function Avatar({ user, onAction }: AvatarProps) {
  const [isOpen, setIsOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);

  const initials = (user?.username || '?').slice(0, 2).toUpperCase();

  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    }
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  return (
    <div className="relative" ref={dropdownRef}>
      <button
        className="flex items-center gap-2 px-3 py-2 bg-gradient-primary text-primary-foreground rounded-lg shadow-brand hover:shadow-lg transition-all duration-200 focus:outline-none focus:ring-2 focus:ring-primary/50"
        onClick={() => setIsOpen(!isOpen)}
        title={user?.username}
      >
        <div className="w-8 h-8 rounded-md bg-white/20 flex items-center justify-center font-semibold text-sm">
          {initials}
        </div>
        <span className="text-sm font-medium hidden sm:block">{user?.username}</span>
        <ChevronDown className={`w-4 h-4 transition-transform ${isOpen ? 'rotate-180' : ''}`} />
      </button>

      {isOpen && (
        <div className="absolute right-0 mt-2 w-64 bg-card rounded-xl shadow-lg border border-border py-2 z-20 animate-scale-in">
          <div className="px-4 py-3 border-b border-border">
            <p className="text-sm font-medium text-card-foreground">{user?.username || 'Usuário'}</p>
            <p className="text-xs text-muted-foreground">Conectado ao DbConnect</p>
          </div>
          
          <button
            className="w-full text-left px-4 py-3 hover:bg-card-hover flex items-center gap-3 transition-colors text-card-foreground"
            onClick={() => { setIsOpen(false); onAction('account'); }}
          >
            <User className="w-4 h-4" />
            <span className="text-sm">Minha conta</span>
          </button>
          
          <button
            className="w-full text-left px-4 py-3 hover:bg-card-hover flex items-center gap-3 transition-colors text-error"
            onClick={() => { setIsOpen(false); onAction('logout'); }}
          >
            <LogOut className="w-4 h-4" />
            <span className="text-sm">Sair</span>
          </button>
        </div>
      )}
    </div>
  );
}