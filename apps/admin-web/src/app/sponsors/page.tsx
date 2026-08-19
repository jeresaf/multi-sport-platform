'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { Handshake, Plus, RefreshCw } from 'lucide-react';
import { adminApi, getStoredUser } from '@/lib/admin-api';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';

type Tenant = { id: string; name: string };
type Association = { id: string; name?: string | null; contestShape?: string };
type Sponsorship = {
  id: string; name: string; type: string; value: number; currency: string;
  description?: string | null; benefits?: string | null; contractUrl?: string | null;
  status: string; startDate?: string | null; endDate?: string | null;
  competitionId?: string | null; contestId?: string | null;
  placement: string; displayOrder: number; isCurrentlyVisible: boolean;
};
type Sponsor = {
  id: string; name: string; description?: string | null; logoUrl?: string | null;
  website?: string | null; contactEmail?: string | null; contactPhone?: string | null;
  tier: string; isActive: boolean; sponsorships: Sponsorship[];
};
type SponsorList = { tenantId: string; placements: string[]; items: Sponsor[] };

const tiers = ['Standard', 'Silver', 'Gold', 'Platinum', 'Diamond'];
const sponsorshipTypes = ['Title', 'Presenting', 'Gold', 'Silver', 'Bronze', 'Media', 'Technical', 'InKind'];
const tierValue = (tier: string) => Math.max(0, tiers.indexOf(tier));
const typeValue = (type: string) => Math.max(0, sponsorshipTypes.indexOf(type));
const toUtc = (value: string) => value ? new Date(value).toISOString() : null;
const toLocalInput = (value?: string | null) => {
  if (!value) return '';
  const date = new Date(value);
  return new Date(date.getTime() - date.getTimezoneOffset() * 60_000).toISOString().slice(0, 16);
};
const emptyProfile = { name: '', description: '', logoUrl: '', website: '', contactEmail: '', contactPhone: '', tier: 'Standard' };
const emptyDeal = {
  name: '', type: 'Title', value: '0', currency: 'USD', startDate: '', endDate: '',
  placement: 'PublicHome', displayOrder: '0', associationType: 'competition', associationId: '',
  description: '', benefits: '', contractUrl: '',
};

export default function SponsorsPage() {
  const [tenants, setTenants] = useState<Tenant[]>([]);
  const [tenantId, setTenantId] = useState('');
  const [data, setData] = useState<SponsorList | null>(null);
  const [competitions, setCompetitions] = useState<Association[]>([]);
  const [contests, setContests] = useState<Association[]>([]);
  const [selectedId, setSelectedId] = useState('');
  const [profile, setProfile] = useState(emptyProfile);
  const [deal, setDeal] = useState(emptyDeal);
  const [editingDealId, setEditingDealId] = useState('');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const selected = useMemo(() => data?.items.find(item => item.id === selectedId) ?? null, [data, selectedId]);

  const load = useCallback(async (id: string) => {
    if (!id) return;
    setError(null);
    try {
      const [sponsors, competitionRows, contestRows] = await Promise.all([
        adminApi<SponsorList>(`/api/sponsors?tenantId=${id}`),
        adminApi<Association[]>(`/api/competitions?tenantId=${id}`),
        adminApi<Association[]>(`/api/contests?tenantId=${id}`),
      ]);
      setData(sponsors); setCompetitions(competitionRows); setContests(contestRows);
      setSelectedId(current => sponsors.items.some(item => item.id === current) ? current : sponsors.items[0]?.id ?? '');
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Could not load sponsors.'); }
  }, []);

  useEffect(() => {
    adminApi<Tenant[]>('/api/tenants').then(items => {
      setTenants(items);
      const own = getStoredUser()?.tenantId;
      setTenantId(items.find(item => item.id === own)?.id ?? items[0]?.id ?? '');
    }).catch((reason: Error) => setError(reason.message));
  }, []);
  useEffect(() => { void load(tenantId); }, [tenantId, load]);
  useEffect(() => {
    if (!selected) return;
    setProfile({
      name: selected.name, description: selected.description ?? '', logoUrl: selected.logoUrl ?? '',
      website: selected.website ?? '', contactEmail: selected.contactEmail ?? '',
      contactPhone: selected.contactPhone ?? '', tier: selected.tier,
    });
  }, [selected]);

  async function createSponsor() {
    if (!tenantId || !profile.name.trim()) return;
    await perform(async () => {
      const created = await adminApi<Sponsor>('/api/sponsors', { method: 'POST', body: JSON.stringify({
        tenantId, ...profile, tier: tierValue(profile.tier),
      }) });
      await load(tenantId);
      setSelectedId(created.id);
      setMessage('Sponsor profile created and audited.');
    });
  }

  async function saveSponsor() {
    if (!selected || !profile.name.trim()) return;
    await perform(async () => {
      await adminApi(`/api/sponsors/${selected.id}`, { method: 'PUT', body: JSON.stringify({
        ...profile, tier: tierValue(profile.tier), isActive: selected.isActive,
      }) });
      setMessage('Sponsor profile updated and audited.'); await load(tenantId);
    });
  }

  async function toggleSponsor() {
    if (!selected) return;
    await perform(async () => {
      await adminApi(`/api/sponsors/${selected.id}`, { method: 'PUT', body: JSON.stringify({
        ...profile, tier: tierValue(profile.tier), isActive: !selected.isActive,
      }) });
      setMessage(`Sponsor ${selected.isActive ? 'deactivated' : 'activated'}.`); await load(tenantId);
    });
  }

  async function saveSponsorship() {
    if (!selected || !deal.name.trim() || !deal.associationId) return;
    await perform(async () => {
      const path = editingDealId
        ? `/api/sponsors/sponsorships/${editingDealId}`
        : `/api/sponsors/${selected.id}/sponsorships`;
      await adminApi(path, { method: editingDealId ? 'PUT' : 'POST', body: JSON.stringify({
        type: typeValue(deal.type), name: deal.name, description: deal.description || null,
        value: Number(deal.value), currency: deal.currency, startDate: toUtc(deal.startDate),
        endDate: toUtc(deal.endDate), benefits: deal.benefits || null, contractUrl: deal.contractUrl || null,
        competitionId: deal.associationType === 'competition' ? deal.associationId : null,
        contestId: deal.associationType === 'contest' ? deal.associationId : null,
        placement: deal.placement, displayOrder: Number(deal.displayOrder),
      }) });
      setDeal(current => ({ ...emptyDeal, placement: data?.placements[0] ?? current.placement }));
      setEditingDealId('');
      setMessage(editingDealId
        ? 'Sponsorship placement updated and audited.'
        : 'Sponsorship placement created. Activate it when it is ready to publish.');
      await load(tenantId);
    });
  }

  async function changeStatus(item: Sponsorship, action: 'activate' | 'terminate') {
    await perform(async () => {
      await adminApi(`/api/sponsors/sponsorships/${item.id}/${action}`, { method: 'POST' });
      setMessage(`Sponsorship ${action === 'activate' ? 'activated' : 'terminated'} and audited.`); await load(tenantId);
    });
  }

  function editSponsorship(item: Sponsorship) {
    setEditingDealId(item.id);
    setDeal({
      name: item.name, type: item.type, value: String(item.value), currency: item.currency,
      startDate: toLocalInput(item.startDate), endDate: toLocalInput(item.endDate),
      placement: item.placement, displayOrder: String(item.displayOrder),
      associationType: item.contestId ? 'contest' : 'competition',
      associationId: item.contestId ?? item.competitionId ?? '',
      description: item.description ?? '', benefits: item.benefits ?? '', contractUrl: item.contractUrl ?? '',
    });
  }

  async function perform(action: () => Promise<void>) {
    setBusy(true); setError(null); setMessage(null);
    try { await action(); } catch (reason) { setError(reason instanceof Error ? reason.message : 'Sponsor operation failed.'); }
    finally { setBusy(false); }
  }

  const associations = deal.associationType === 'competition' ? competitions : contests;
  return <div className="space-y-6" data-testid="sponsor-management-workspace">
    <div className="flex flex-wrap items-end justify-between gap-4">
      <div><p className="text-sm font-semibold uppercase tracking-wider text-primary">Commercial</p><h1 className="text-3xl font-bold text-gray-950">Sponsors</h1><p className="mt-1 text-gray-500">Manage tenant-owned profiles and date-controlled placements.</p></div>
      <Button variant="outline" onClick={() => void load(tenantId)} disabled={!tenantId || busy}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button>
    </div>
    <div><Label htmlFor="sponsor-tenant">Tenant</Label><select id="sponsor-tenant" value={tenantId} onChange={event => setTenantId(event.target.value)} className="mt-1 h-10 w-full rounded-md border bg-white px-3 md:max-w-md">{tenants.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></div>
    {message && <p className="rounded-lg border border-green-200 bg-green-50 p-3 text-sm font-semibold text-green-700">{message}</p>}
    {error && <p role="alert" className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm font-semibold text-red-700">{error}</p>}

    <div className="grid gap-6 lg:grid-cols-[280px_1fr]">
      <Card><CardHeader><CardTitle>Profiles</CardTitle><CardDescription>{data?.items.length ?? 0} tenant sponsor(s)</CardDescription></CardHeader><CardContent className="space-y-2">
        {data?.items.map(item => <button key={item.id} onClick={() => setSelectedId(item.id)} className={`w-full rounded-lg border p-3 text-left ${selectedId === item.id ? 'border-primary bg-primary/5' : ''}`}><span className="font-semibold">{item.name}</span><span className="block text-xs text-gray-500">{item.tier} · {item.isActive ? 'Active' : 'Inactive'} · {item.sponsorships.length} placement(s)</span></button>)}
        {!data?.items.length && <p className="text-sm text-gray-500">Create the first sponsor profile.</p>}
      </CardContent></Card>

      <div className="space-y-6">
        <Card><CardHeader><CardTitle>{selected ? 'Sponsor profile' : 'New sponsor profile'}</CardTitle><CardDescription>Logo and website assets remain owned by the selected tenant.</CardDescription></CardHeader><CardContent className="grid gap-4 md:grid-cols-2">
          <Field label="Name" value={profile.name} onChange={value => setProfile(current => ({ ...current, name: value }))} />
          <SelectField label="Tier" value={profile.tier} values={tiers} onChange={value => setProfile(current => ({ ...current, tier: value }))} />
          <Field label="Logo URL" value={profile.logoUrl} onChange={value => setProfile(current => ({ ...current, logoUrl: value }))} />
          <Field label="Website" value={profile.website} onChange={value => setProfile(current => ({ ...current, website: value }))} />
          <Field label="Contact email" value={profile.contactEmail} onChange={value => setProfile(current => ({ ...current, contactEmail: value }))} />
          <Field label="Contact phone" value={profile.contactPhone} onChange={value => setProfile(current => ({ ...current, contactPhone: value }))} />
          <div className="md:col-span-2"><Field label="Description" value={profile.description} onChange={value => setProfile(current => ({ ...current, description: value }))} /></div>
          <div className="flex flex-wrap gap-2 md:col-span-2">{selected ? <><Button onClick={saveSponsor} disabled={busy || !profile.name.trim()}>Save profile</Button><Button variant="outline" onClick={toggleSponsor} disabled={busy}>{selected.isActive ? 'Deactivate' : 'Activate'}</Button><Button variant="ghost" onClick={() => { setSelectedId(''); setProfile(emptyProfile); }}>New profile</Button></> : <Button onClick={createSponsor} disabled={busy || !profile.name.trim()}><Plus className="mr-2 h-4 w-4" />Create sponsor</Button>}</div>
        </CardContent></Card>

        {selected && <Card><CardHeader><CardTitle>{editingDealId ? 'Edit sponsorship placement' : 'New sponsorship placement'}</CardTitle><CardDescription>Associate exactly one competition or contest and control when and where the sponsor appears.</CardDescription></CardHeader><CardContent className="grid gap-4 md:grid-cols-2">
          <Field label="Campaign name" value={deal.name} onChange={value => setDeal(current => ({ ...current, name: value }))} />
          <SelectField label="Level" value={deal.type} values={sponsorshipTypes} onChange={value => setDeal(current => ({ ...current, type: value }))} />
          <Field label="Value" type="number" value={deal.value} onChange={value => setDeal(current => ({ ...current, value }))} />
          <Field label="Currency" value={deal.currency} onChange={value => setDeal(current => ({ ...current, currency: value.toUpperCase().slice(0, 3) }))} />
          <Field label="Start date" type="datetime-local" value={deal.startDate} onChange={value => setDeal(current => ({ ...current, startDate: value }))} />
          <Field label="End date" type="datetime-local" value={deal.endDate} onChange={value => setDeal(current => ({ ...current, endDate: value }))} />
          <SelectField label="Placement" value={deal.placement} values={data?.placements ?? ['PublicHome']} onChange={value => setDeal(current => ({ ...current, placement: value }))} />
          <Field label="Display order" type="number" value={deal.displayOrder} onChange={value => setDeal(current => ({ ...current, displayOrder: value }))} />
          <SelectField label="Association type" value={deal.associationType} values={['competition', 'contest']} onChange={value => setDeal(current => ({ ...current, associationType: value, associationId: '' }))} />
          <div><Label htmlFor="association">{deal.associationType === 'competition' ? 'Competition' : 'Contest'}</Label><select id="association" value={deal.associationId} onChange={event => setDeal(current => ({ ...current, associationId: event.target.value }))} className="mt-1 h-10 w-full rounded-md border px-3"><option value="">Select…</option>{associations.map(item => <option key={item.id} value={item.id}>{item.name || item.contestShape || item.id}</option>)}</select></div>
          <Field label="Contract URL" value={deal.contractUrl} onChange={value => setDeal(current => ({ ...current, contractUrl: value }))} />
          <Field label="Benefits" value={deal.benefits} onChange={value => setDeal(current => ({ ...current, benefits: value }))} />
          <div className="flex gap-2 md:col-span-2"><Button onClick={saveSponsorship} disabled={busy || !deal.name.trim() || !deal.associationId}><Handshake className="mr-2 h-4 w-4" />{editingDealId ? 'Save placement' : 'Create placement'}</Button>{editingDealId && <Button variant="ghost" onClick={() => { setEditingDealId(''); setDeal(emptyDeal); }}>Cancel</Button>}</div>
        </CardContent></Card>}

        {selected && <Card><CardHeader><CardTitle>Placements</CardTitle><CardDescription>Only active placements inside their date window are publicly visible.</CardDescription></CardHeader><CardContent className="space-y-3">{selected.sponsorships.map(item => <div key={item.id} className="flex flex-wrap items-center justify-between gap-3 rounded-lg border p-3"><div><p className="font-semibold">{item.name}</p><p className="text-sm text-gray-500">{item.type} · {item.placement} · {item.currency} {item.value.toLocaleString()} · order {item.displayOrder}</p><p className={`text-xs font-semibold ${item.isCurrentlyVisible ? 'text-green-700' : 'text-gray-500'}`}>{item.status} · {item.isCurrentlyVisible ? 'Visible now' : 'Not currently visible'}</p></div><div className="flex gap-2"><Button size="sm" variant="ghost" onClick={() => editSponsorship(item)} disabled={busy}>Edit</Button>{item.status !== 'Active' && item.status !== 'Terminated' && <Button size="sm" onClick={() => void changeStatus(item, 'activate')} disabled={busy}>Activate</Button>}{item.status === 'Active' && <Button size="sm" variant="outline" onClick={() => void changeStatus(item, 'terminate')} disabled={busy}>Terminate</Button>}</div></div>)}{selected.sponsorships.length === 0 && <p className="text-sm text-gray-500">No placements configured.</p>}</CardContent></Card>}
      </div>
    </div>
  </div>;
}

function Field({ label, value, onChange, type = 'text' }: { label: string; value: string; onChange: (value: string) => void; type?: string }) {
  const id = `sponsor-${label.toLowerCase().replaceAll(' ', '-')}`;
  return <div><Label htmlFor={id}>{label}</Label><Input id={id} type={type} value={value} onChange={event => onChange(event.target.value)} className="mt-1" /></div>;
}
function SelectField({ label, value, values, onChange }: { label: string; value: string; values: string[]; onChange: (value: string) => void }) {
  const id = `sponsor-${label.toLowerCase().replaceAll(' ', '-')}`;
  return <div><Label htmlFor={id}>{label}</Label><select id={id} value={value} onChange={event => onChange(event.target.value)} className="mt-1 h-10 w-full rounded-md border px-3">{values.map(item => <option key={item} value={item}>{item}</option>)}</select></div>;
}
