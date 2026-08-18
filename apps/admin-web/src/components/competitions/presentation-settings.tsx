'use client';

import { useEffect, useState } from 'react';
import { Eye, Save, X } from 'lucide-react';
import { adminApi } from '@/lib/admin-api';

type PresentationSettings = {
  primaryContestRenderer?: string | null;
  visibleTabs: string[];
  defaultTab?: string | null;
  scoreVariant?: string | null;
  standingsVariant?: string | null;
  leaderboardVariant?: string | null;
  bracketEnabled?: boolean | null;
  seriesEnabled?: boolean | null;
  medalTableEnabled?: boolean | null;
};

const tabs = ['overview', 'fixtures', 'results', 'standings', 'bracket', 'leaderboard', 'participants', 'statistics'];
const empty: PresentationSettings = { visibleTabs: [] };

export function PresentationSettingsPanel({ competitionId, competitionName, onClose }: { competitionId: string; competitionName: string; onClose: () => void }) {
  const [form, setForm] = useState<PresentationSettings>(empty);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  useEffect(() => {
    setLoading(true);
    adminApi<PresentationSettings>(`/api/competitions/${competitionId}/presentation`)
      .then(value => setForm({ ...empty, ...value }))
      .catch(reason => setMessage(reason instanceof Error ? reason.message : 'Could not load presentation settings.'))
      .finally(() => setLoading(false));
  }, [competitionId]);

  async function save() {
    setSaving(true);
    setMessage(null);
    try {
      const saved = await adminApi<PresentationSettings>(`/api/competitions/${competitionId}/presentation`, { method: 'PUT', body: JSON.stringify(form) });
      setForm(saved);
      setMessage('Presentation settings saved.');
    } catch (reason) {
      setMessage(reason instanceof Error ? reason.message : 'Could not save presentation settings.');
    } finally {
      setSaving(false);
    }
  }

  const toggleTab = (tab: string) => setForm(current => ({
    ...current,
    visibleTabs: current.visibleTabs.includes(tab)
      ? current.visibleTabs.filter(item => item !== tab)
      : [...current.visibleTabs, tab],
  }));

  return (
    <section className="rounded-xl border border-gray-200 bg-white p-6" aria-labelledby="presentation-heading">
      <div className="flex items-start justify-between gap-4">
        <div><p className="text-sm font-semibold uppercase tracking-wider text-primary">Public presentation</p><h2 id="presentation-heading" className="text-2xl font-bold">{competitionName}</h2><p className="mt-1 text-sm text-gray-500">Leave fields on Sport default unless this competition needs a deliberate override.</p></div>
        <button onClick={onClose} className="rounded-lg p-2 hover:bg-gray-100" aria-label="Close presentation settings"><X className="h-5 w-5" /></button>
      </div>
      {loading ? <p className="mt-5 text-gray-500">Loading settings…</p> : (
        <div className="mt-6 space-y-6">
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            <SelectField label="Primary contest renderer" value={form.primaryContestRenderer ?? ''} onChange={value => setForm({ ...form, primaryContestRenderer: value || null })} options={['head-to-head', 'multi-participant-leaderboard', 'race-heat', 'fight-card-bout', 'cricket-scorecard']} />
            <TextField label="Score variant" value={form.scoreVariant ?? ''} placeholder="Sport default" onChange={value => setForm({ ...form, scoreVariant: value || null })} />
            <TextField label="Standings variant" value={form.standingsVariant ?? ''} placeholder="Sport default" onChange={value => setForm({ ...form, standingsVariant: value || null })} />
            <TextField label="Leaderboard variant" value={form.leaderboardVariant ?? ''} placeholder="Sport default" onChange={value => setForm({ ...form, leaderboardVariant: value || null })} />
            <SelectField label="Default tab" value={form.defaultTab ?? ''} onChange={value => setForm({ ...form, defaultTab: value || null })} options={tabs.filter(tab => form.visibleTabs.length === 0 || form.visibleTabs.includes(tab))} />
          </div>
          <fieldset><legend className="text-sm font-bold text-gray-900">Visible competition tabs</legend><p className="mt-1 text-xs text-gray-500">No selection uses every relevant output from the sport package.</p><div className="mt-3 flex flex-wrap gap-2">{tabs.map(tab => <label key={tab} className="flex items-center gap-2 rounded-full border px-3 py-2 text-sm"><input type="checkbox" checked={form.visibleTabs.includes(tab)} onChange={() => toggleTab(tab)} />{tab[0].toUpperCase() + tab.slice(1)}</label>)}</div></fieldset>
          <div className="grid gap-3 sm:grid-cols-3">
            <TriState label="Bracket" value={form.bracketEnabled} onChange={value => setForm({ ...form, bracketEnabled: value })} />
            <TriState label="Series" value={form.seriesEnabled} onChange={value => setForm({ ...form, seriesEnabled: value })} />
            <TriState label="Medal table" value={form.medalTableEnabled} onChange={value => setForm({ ...form, medalTableEnabled: value })} />
          </div>
          {message && <p role="status" className="rounded-lg bg-gray-50 p-3 text-sm">{message}</p>}
          <div className="flex flex-wrap gap-3"><button onClick={() => void save()} disabled={saving} className="inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2 font-semibold text-white disabled:opacity-50"><Save className="h-4 w-4" />{saving ? 'Saving…' : 'Save settings'}</button><a href={`/competition/${competitionId}`} target="_blank" rel="noreferrer" className="inline-flex items-center gap-2 rounded-lg border px-4 py-2 font-semibold"><Eye className="h-4 w-4" />Preview public view</a></div>
        </div>
      )}
    </section>
  );
}

function TextField({ label, value, placeholder, onChange }: { label: string; value: string; placeholder: string; onChange: (value: string) => void }) { return <label className="text-sm font-medium text-gray-700">{label}<input value={value} placeholder={placeholder} onChange={event => onChange(event.target.value)} className="mt-1 w-full rounded-lg border border-gray-200 px-3 py-2" /></label>; }
function SelectField({ label, value, options, onChange }: { label: string; value: string; options: string[]; onChange: (value: string) => void }) { return <label className="text-sm font-medium text-gray-700">{label}<select value={value} onChange={event => onChange(event.target.value)} className="mt-1 w-full rounded-lg border border-gray-200 px-3 py-2"><option value="">Sport default</option>{options.map(option => <option key={option} value={option}>{option}</option>)}</select></label>; }
function TriState({ label, value, onChange }: { label: string; value?: boolean | null; onChange: (value: boolean | null) => void }) { return <label className="text-sm font-medium text-gray-700">{label}<select value={value == null ? '' : String(value)} onChange={event => onChange(event.target.value === '' ? null : event.target.value === 'true')} className="mt-1 w-full rounded-lg border border-gray-200 px-3 py-2"><option value="">Sport default</option><option value="true">Enabled</option><option value="false">Disabled</option></select></label>; }
