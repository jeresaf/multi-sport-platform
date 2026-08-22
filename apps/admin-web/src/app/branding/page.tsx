'use client';

import { useEffect, useState } from 'react';
import Image from 'next/image';
import { ImageUp, Palette } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { adminApi, getEffectiveTenantId, getStoredUser, hasAnyPermission } from '@/lib/admin-api';

type Tenant = { id: string; name: string; shortName: string; slug: string };
type Branding = {
  tenantName: string;
  shortName?: string | null;
  logoUrl?: string | null;
  primaryColor?: string | null;
  secondaryColor?: string | null;
  accentColor?: string | null;
  publicSlug?: string | null;
  contactEmail?: string | null;
  contactPhone?: string | null;
  website?: string | null;
};

const defaults: Branding = {
  tenantName: '', shortName: '', logoUrl: '',
  primaryColor: '#0EA5E9', secondaryColor: '#7C3AED', accentColor: '#F59E0B',
  publicSlug: '', contactEmail: '', contactPhone: '', website: '',
};

export default function BrandingPage() {
  const [tenants, setTenants] = useState<Tenant[]>([]);
  const [tenantId, setTenantId] = useState('');
  const [form, setForm] = useState<Branding>(defaults);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const canManage = hasAnyPermission(getStoredUser(), ['branding.manage']);

  useEffect(() => {
    adminApi<Tenant[]>('/api/tenants').then(items => {
      setTenants(items);
      const ownTenant = getEffectiveTenantId();
      setTenantId(items.find(item => item.id === ownTenant)?.id ?? items[0]?.id ?? '');
    }).catch((reason: Error) => setError(reason.message));
  }, []);

  useEffect(() => {
    if (!tenantId) return;
    const tenant = tenants.find(item => item.id === tenantId);
    adminApi<Branding>(`/api/branding/tenant/${tenantId}`)
      .then(branding => setForm({ ...defaults, ...branding }))
      .catch(() => setForm({ ...defaults, tenantName: tenant?.name ?? '', shortName: tenant?.shortName ?? '', publicSlug: tenant?.slug ?? '' }));
  }, [tenantId, tenants]);

  const update = (key: keyof Branding, value: string) =>
    setForm(current => ({ ...current, [key]: value }));

  async function save() {
    setError(null); setMessage(null); setSaving(true);
    try {
      const saved = await adminApi<Branding>(`/api/branding/tenant/${tenantId}`, {
        method: 'PUT', body: JSON.stringify(form),
      });
      setForm({ ...defaults, ...saved });
      setMessage('Branding saved and available to public and mobile clients.');
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Could not save branding.');
    } finally { setSaving(false); }
  }

  async function uploadLogo(file?: File) {
    if (!file || !tenantId) return;
    setError(null); setMessage(null); setSaving(true);
    try {
      const body = new FormData(); body.append('file', file);
      const saved = await adminApi<Branding>(`/api/branding/tenant/${tenantId}/logo`, { method: 'POST', body });
      setForm({ ...defaults, ...saved });
      setMessage('Logo uploaded to tenant-owned object storage and published.');
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Could not upload the logo.');
    } finally { setSaving(false); }
  }

  return (
    <div className="space-y-6">
      <div>
        <p className="text-sm font-semibold uppercase tracking-wider text-primary">Tenant identity</p>
        <h1 className="text-3xl font-bold text-gray-950">Branding</h1>
        <p className="mt-1 text-gray-500">Manage the identity delivered to public web and mobile clients.</p>
      </div>
      <section className="grid gap-6 rounded-xl border bg-white p-6 lg:grid-cols-[1fr_320px]">
        <div className="space-y-4">
          <div><Label htmlFor="tenant">Tenant</Label><select id="tenant" value={tenantId} onChange={event => setTenantId(event.target.value)} className="mt-1 h-10 w-full rounded-md border px-3">{tenants.map(tenant => <option key={tenant.id} value={tenant.id}>{tenant.name}</option>)}</select></div>
          <Field label="Tenant name" value={form.tenantName} disabled={!canManage} onChange={value => update('tenantName', value)} />
          <Field label="Short name" value={form.shortName ?? ''} disabled={!canManage} onChange={value => update('shortName', value)} />
          <Field label="Public slug" value={form.publicSlug ?? ''} disabled onChange={() => undefined} />
          <Field label="Logo URL" value={form.logoUrl ?? ''} disabled={!canManage} onChange={value => update('logoUrl', value)} />
          {canManage && <label className="flex cursor-pointer items-center justify-center rounded-lg border border-dashed border-gray-300 p-4 text-sm font-semibold text-gray-700 hover:bg-gray-50"><ImageUp className="mr-2 h-5 w-5" />Upload PNG, JPEG, or WebP (max 2 MB)<input className="sr-only" type="file" accept="image/png,image/jpeg,image/webp" disabled={saving} onChange={event => void uploadLogo(event.target.files?.[0])} /></label>}
          <div className="grid gap-4 sm:grid-cols-3">
            <ColorField label="Primary" value={form.primaryColor ?? '#0EA5E9'} disabled={!canManage} onChange={value => update('primaryColor', value)} />
            <ColorField label="Secondary" value={form.secondaryColor ?? '#7C3AED'} disabled={!canManage} onChange={value => update('secondaryColor', value)} />
            <ColorField label="Accent" value={form.accentColor ?? '#F59E0B'} disabled={!canManage} onChange={value => update('accentColor', value)} />
          </div>
          <div className="grid gap-4 sm:grid-cols-2"><Field label="Contact email" type="email" value={form.contactEmail ?? ''} disabled={!canManage} onChange={value => update('contactEmail', value)} /><Field label="Contact phone" value={form.contactPhone ?? ''} disabled={!canManage} onChange={value => update('contactPhone', value)} /></div>
          <Field label="Website" type="url" value={form.website ?? ''} disabled={!canManage} onChange={value => update('website', value)} />
          {canManage && <Button onClick={save} disabled={saving || !tenantId || !form.tenantName.trim()}>{saving ? 'Saving…' : 'Save branding'}</Button>}
          {message && <p className="text-sm font-semibold text-green-700">{message}</p>}
          {error && <p className="text-sm font-semibold text-red-700">{error}</p>}
        </div>
        <div className="rounded-xl p-6 text-white" style={{ background: `linear-gradient(135deg, ${form.primaryColor}, ${form.secondaryColor})` }}>
          {isWebUrl(form.logoUrl) ? <Image unoptimized width={64} height={64} src={form.logoUrl!} alt="Tenant logo preview" className="h-16 w-16 rounded-lg bg-white object-contain p-1" /> : <Palette className="h-12 w-12" />}
          <h2 className="mt-8 text-2xl font-bold">{form.tenantName || 'Tenant name'}</h2>
          <p style={{ color: form.accentColor ?? '#FFFFFF' }}>{form.shortName || 'Short name'}</p>
        </div>
      </section>
    </div>
  );
}

function Field({ label, value, onChange, disabled = false, type = 'text' }: { label: string; value: string; onChange: (value: string) => void; disabled?: boolean; type?: string }) {
  const id = label.toLowerCase().replaceAll(' ', '-');
  return <div><Label htmlFor={id}>{label}</Label><Input id={id} type={type} disabled={disabled} value={value} onChange={event => onChange(event.target.value)} className="mt-1" /></div>;
}

function ColorField({ label, value, onChange, disabled }: { label: string; value: string; onChange: (value: string) => void; disabled: boolean }) {
  return <div><Label>{label}</Label><div className="mt-1 flex gap-2"><input aria-label={`${label} color`} disabled={disabled} type="color" value={value} onChange={event => onChange(event.target.value.toUpperCase())} className="h-10 w-12 rounded border disabled:opacity-50" /><Input aria-label={`${label} color hex`} disabled={disabled} value={value} pattern="#[0-9A-Fa-f]{6}" onChange={event => onChange(event.target.value)} /></div></div>;
}

function isWebUrl(value?: string | null) { return Boolean(value && /^https?:\/\//i.test(value)); }
