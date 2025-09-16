import React, { useEffect } from 'react';
import { createPortal } from 'react-dom';
import { CheckCircle, XCircle, AlertTriangle, X } from 'lucide-react';

interface ToastProps {
  type: 'success' | 'error' | 'warning';
  message: string;
  onClose: () => void;
}

export function Toast({ type, message, onClose }: ToastProps) {
  const icons = {
    success: CheckCircle,
    error: XCircle,
    warning: AlertTriangle,
  };

  const styles = {
    success: 'bg-success-bg border-success text-success-foreground shadow-lg',
    error: 'bg-error-bg border-error text-error-foreground shadow-lg',
    warning: 'bg-warning-bg border-warning text-warning-foreground shadow-lg',
  };

  const Icon = icons[type];

  useEffect(() => {
    const timer = setTimeout(onClose, 4000);
    return () => clearTimeout(timer);
  }, [onClose]);

  return createPortal(
    <div className={`fixed top-6 right-6 p-4 rounded-lg border-l-4 ${styles[type]} z-[9999] animate-slide-up max-w-md shadow-brand pointer-events-auto`}>
      <div className="flex items-start gap-3">
        <Icon className="w-5 h-5 flex-shrink-0 mt-0.5" />
        <div className="flex-1">
          <p className="font-medium text-sm">{message}</p>
        </div>
        <button
          onClick={onClose}
          className="p-1 hover:bg-white/10 rounded-md transition-colors"
          aria-label="Fechar notificação"
        >
          <X className="w-4 h-4" />
        </button>
      </div>
    </div>,
    document.body
  );
}