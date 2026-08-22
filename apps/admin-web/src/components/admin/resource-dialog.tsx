'use client';

import { X } from 'lucide-react';
import { useEffect, type ReactNode } from 'react';

export function ResourceDialog({
  title,
  description,
  onClose,
  children,
  width = 'max-w-3xl',
}: {
  title: string;
  description?: string;
  onClose: () => void;
  children: ReactNode;
  width?: string;
}) {
  useEffect(() => {
    const closeOnEscape = (event: KeyboardEvent) => event.key === 'Escape' && onClose();
    document.addEventListener('keydown', closeOnEscape);
    return () => document.removeEventListener('keydown', closeOnEscape);
  }, [onClose]);

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/45 p-4" role="presentation" onMouseDown={event => event.target === event.currentTarget && onClose()}>
      <section role="dialog" aria-modal="true" aria-labelledby="resource-dialog-title" className={`max-h-[90vh] w-full ${width} overflow-y-auto rounded-2xl bg-white p-6 shadow-2xl`}>
        <div className="flex items-start justify-between gap-4 border-b border-gray-100 pb-4">
          <div><h2 id="resource-dialog-title" className="text-xl font-bold text-gray-950">{title}</h2>{description && <p className="mt-1 text-sm text-gray-500">{description}</p>}</div>
          <button type="button" onClick={onClose} aria-label="Close dialog" className="rounded-lg p-2 text-gray-500 hover:bg-gray-100"><X className="h-5 w-5" /></button>
        </div>
        <div className="pt-5">{children}</div>
      </section>
    </div>
  );
}
