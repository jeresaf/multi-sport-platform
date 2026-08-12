'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Check, ChevronLeft, ChevronRight, Save, Trash2 } from 'lucide-react';
import { adminApi, getStoredUser } from '@/lib/admin-api';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';

const RUGBY_ID = '00000000-0000-0000-0000-000000000106';

const steps = [
  'Basic information', 'Sport', 'Ruleset version', 'Format', 'Participants',
  'Stages', 'Scheduling', 'Officials', 'Publication', 'Review',
];
const sports = [
  { id: '00000000-0000-0000-0000-000000000100', name: 'Football', icon: '⚽', formats: ['ROUND_ROBIN', 'KNOCKOUT', 'GROUP_PLUS_KNOCKOUT'] },
  { id: '00000000-0000-0000-0000-000000000101', name: 'Basketball', icon: '🏀', formats: ['ROUND_ROBIN', 'KNOCKOUT', 'BEST_OF_SERIES'] },
  { id: '00000000-0000-0000-0000-000000000102', name: 'Golf', icon: '⛳', formats: ['MULTI_ROUND_LEADERBOARD'] },
  { id: '00000000-0000-0000-0000-000000000103', name: 'Athletics', icon: '🏃', formats: ['HEAT_QUALIFICATION'] },
  { id: '00000000-0000-0000-0000-000000000104', name: 'Cricket', icon: '🏏', formats: ['ROUND_ROBIN', 'KNOCKOUT', 'GROUP_PLUS_KNOCKOUT'] },
  { id: '00000000-0000-0000-0000-000000000105', name: 'MMA / Boxing', icon: '🥊', formats: ['FIGHT_CARD'] },
  { id: '00000000-0000-0000-0000-000000000106', name: 'Rugby Union (7s & 15s)', icon: '🏉', formats: ['ROUND_ROBIN', 'KNOCKOUT', 'GROUP_PLUS_KNOCKOUT'] },
];
const formatNames: Record<string, string> = {
  ROUND_ROBIN: 'Round robin', KNOCKOUT: 'Knockout', GROUP_PLUS_KNOCKOUT: 'Groups + knockout',
  BEST_OF_SERIES: 'Best-of series', MULTI_ROUND_LEADERBOARD: 'Multi-round leaderboard',
  HEAT_QUALIFICATION: 'Heat qualification', FIGHT_CARD: 'Fight card',
};
const participantTypes = ['Team', 'Athlete', 'Pair', 'RelayTeam', 'Crew', 'Club', 'School', 'Country', 'CompositeTeam'];
const registrationType: Record<string, number> = {
  Athlete: 0, Team: 1, Pair: 2, RelayTeam: 3, Crew: 4, Club: 4,
  School: 4, Country: 4, CompositeTeam: 4,
};

type Ruleset = {
  id: string;
  name: string;
  versionNumber: number;
  isPublished: boolean;
  validationConfiguration?: string | null;
};
type SportDetail = { versions: Ruleset[] };
type Created = { id: string };
type Fixtures = { fixtureCount: number; drawSeed: number };
type PublicationValidation = { isValid: boolean; errors: { field: string; message: string }[]; conflicts: unknown[] };
type WizardData = {
  name: string; edition: string; description: string; timezone: string;
  sportId: string; rulesetVersionId: string; format: string;
  participantType: string; participants: string[]; stages: string[];
  startDate: string; endDate: string; venue: string; homeAndAway: boolean;
  drawSeed: string;
  officials: string[]; publication: 'draft' | 'publish';
};

const initialData: WizardData = {
  name: '', edition: '', description: '', timezone: 'Africa/Nairobi', sportId: '',
  rulesetVersionId: '', format: '', participantType: 'Team', participants: Array.from({ length: 8 }, (_, i) => `Team ${i + 1}`),
  stages: ['League'], startDate: '', endDate: '', venue: 'Main Stadium', homeAndAway: true, drawSeed: '',
  officials: [], publication: 'draft',
};

function draftKey() { return `sports-platform.competition-draft.${getStoredUser()?.tenantId ?? 'unknown'}`; }

function rugbyVariant(ruleset: Ruleset): 'sevens' | 'fifteens' | null {
  if (!ruleset.validationConfiguration) {
    if (/sevens/i.test(ruleset.name)) return 'sevens';
    if (/fifteens/i.test(ruleset.name)) return 'fifteens';
    return null;
  }
  try {
    const parsed = JSON.parse(ruleset.validationConfiguration) as { variant?: string };
    return parsed.variant === 'sevens' || parsed.variant === 'fifteens'
      ? parsed.variant
      : null;
  } catch {
    return null;
  }
}

export default function CreateCompetitionPage() {
  const router = useRouter();
  const [step, setStep] = useState(0);
  const [data, setData] = useState<WizardData>(initialData);
  const [rulesets, setRulesets] = useState<Ruleset[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const selectedSport = sports.find((sport) => sport.id === data.sportId);
  const selectedRuleset = rulesets.find((ruleset) => ruleset.id === data.rulesetVersionId);

  useEffect(() => {
    const saved = localStorage.getItem(draftKey());
    if (!saved) return;
    try {
      const parsed = JSON.parse(saved) as { step: number; data: WizardData };
      setData({ ...initialData, ...parsed.data });
      setStep(Math.max(0, Math.min(9, parsed.step)));
      setNotice('Saved draft restored.');
    } catch { localStorage.removeItem(draftKey()); }
  }, []);

  useEffect(() => {
    if (!data.sportId) { setRulesets([]); return; }
    adminApi<SportDetail>(`/api/sports/${data.sportId}`)
      .then((sport) => setRulesets(sport.versions.filter((version) =>
        version.isPublished && (data.sportId !== RUGBY_ID || rugbyVariant(version) !== null))))
      .catch((reason: Error) => setError(reason.message));
  }, [data.sportId]);

  const update = <K extends keyof WizardData>(key: K, value: WizardData[K]) =>
    setData((current) => ({ ...current, [key]: value }));

  function validate(index: number) {
    const messages: Record<number, string | null> = {
      0: data.name.trim().length < 3 || !data.edition.trim() ? 'Enter a competition name and edition.' : null,
      1: !data.sportId ? 'Select a sport.' : null,
      2: !data.rulesetVersionId ? 'Select a published ruleset version.' : null,
      3: !data.format ? 'Select a competition format.' : null,
      4: data.participants.length < 2 || data.participants.some((name) => !name.trim()) || new Set(data.participants.map((name) => name.trim().toLowerCase())).size !== data.participants.length ? 'Enter at least two uniquely named participants.' : null,
      5: data.stages.length === 0 || data.stages.some((name) => !name.trim()) ? 'Enter at least one named stage.' : null,
      6: !data.startDate || !data.venue.trim() || Boolean(data.endDate && data.endDate < data.startDate) ? 'Enter a valid date range and venue.' : null,
      7: data.officials.some((name) => !name.trim()) ? 'Remove or complete blank official assignments.' : null,
      8: data.publication === 'publish' && data.format !== 'ROUND_ROBIN' ? 'This format must be saved as a draft and completed in its sport workspace before publication.' : null,
      9: null,
    };
    setError(messages[index]);
    return !messages[index];
  }

  function next() { if (validate(step)) setStep((value) => Math.min(9, value + 1)); }
  function previous() { setError(null); setStep((value) => Math.max(0, value - 1)); }
  function saveDraft() {
    localStorage.setItem(draftKey(), JSON.stringify({ step, data, savedAtUtc: new Date().toISOString() }));
    setNotice('Draft progress saved on this device.');
    setError(null);
  }
  function discardDraft() {
    localStorage.removeItem(draftKey()); setData(initialData); setStep(0); setNotice('Saved draft discarded.'); setError(null);
  }

  async function finish() {
    for (let index = 0; index < 9; index++) if (!validate(index)) { setStep(index); return; }
    const tenantId = getStoredUser()?.tenantId;
    if (!tenantId) { setError('A tenant account is required.'); return; }
    setSubmitting(true); setError(null);
    try {
      const competition = await adminApi<Created>('/api/competitions', {
        method: 'POST', body: JSON.stringify({ tenantId, name: data.name.trim(), sportId: data.sportId }),
      });
      await adminApi(`/api/competitions/${competition.id}/ruleset/${data.rulesetVersionId}`, { method: 'PUT' });
      const participants = await Promise.all(data.participants.map((name, index) => adminApi<Created>('/api/participants', {
        method: 'POST', body: JSON.stringify({ tenantId, name: name.trim(), shortName: `P${index + 1}`, type: data.participantType }),
      })));
      const registrations = await Promise.all(participants.map((participant) => adminApi<Created>(`/api/registrations/competition/${competition.id}`, {
        method: 'POST', body: JSON.stringify({ participantId: participant.id, type: registrationType[data.participantType] }),
      })));
      await Promise.all(registrations.map((registration) => adminApi(`/api/registrations/${registration.id}/submit`, { method: 'POST' })));
      await Promise.all(registrations.map((registration) => adminApi(`/api/registrations/${registration.id}/approve`, {
        method: 'POST', body: JSON.stringify({ notes: 'Approved during competition setup' }),
      })));
      let fixtures: Fixtures | null = null;
      if (data.format === 'ROUND_ROBIN') fixtures = await adminApi<Fixtures>(`/api/competitions/${competition.id}/fixtures/round-robin`, {
        method: 'POST', body: JSON.stringify({
          editionName: data.edition.trim(), stageName: data.stages[0],
          startTimeUtc: new Date(`${data.startDate}T15:00:00Z`).toISOString(),
          endDateUtc: data.endDate ? new Date(`${data.endDate}T23:59:59Z`).toISOString() : null,
          venue: data.venue.trim(), homeAndAway: data.homeAndAway, daysBetweenRounds: 7,
          drawSeed: data.drawSeed ? Number(data.drawSeed) : null,
        }),
      });
      if (data.publication === 'publish') {
        const validation = await adminApi<PublicationValidation>(`/api/competitions/${competition.id}/publication-validation`);
        if (!validation.isValid) throw new Error(validation.errors.map((item) => item.message).join(' ') || 'Resolve scheduling conflicts before publishing.');
        await adminApi(`/api/competitions/${competition.id}/publish`, { method: 'POST' });
      }
      localStorage.removeItem(draftKey());
      sessionStorage.setItem('sports-platform.competition-created', JSON.stringify({ id: competition.id, fixtures: fixtures?.fixtureCount ?? 0, published: data.publication === 'publish' }));
      router.push('/competitions');
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Competition setup failed.'); }
    finally { setSubmitting(false); }
  }

  return <div className="mx-auto max-w-6xl space-y-5">
    <div className="flex flex-wrap items-start justify-between gap-3">
      <div><p className="text-sm font-semibold uppercase tracking-wider text-primary">Competition setup</p><h1 className="text-3xl font-bold">Create competition</h1><p className="text-gray-500">A validated ten-step setup with resumable progress.</p></div>
      <div className="flex gap-2"><Button variant="outline" onClick={saveDraft}><Save className="mr-2 h-4 w-4" />Save draft</Button><Button variant="outline" onClick={discardDraft}><Trash2 className="mr-2 h-4 w-4" />Discard</Button></div>
    </div>
    <ol className="grid grid-cols-5 gap-2 lg:grid-cols-10" aria-label="Competition creation progress">
      {steps.map((name, index) => <li key={name}><button onClick={() => { if (index <= step || validate(step)) setStep(index); }} className={`w-full rounded-lg border p-2 text-left text-xs ${index === step ? 'border-primary bg-primary/10' : index < step ? 'border-green-300 bg-green-50' : 'bg-white'}`}><span className="mb-1 flex h-6 w-6 items-center justify-center rounded-full bg-gray-100 font-bold">{index < step ? <Check className="h-4 w-4 text-green-700" /> : index + 1}</span><span className="hidden lg:block">{name}</span></button></li>)}
    </ol>
    {notice && <p role="status" className="rounded-lg border border-green-200 bg-green-50 p-3 text-sm text-green-700">{notice}</p>}
    {error && <p role="alert" className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700">{error}</p>}
    <Card><CardHeader><CardTitle>{steps[step]}</CardTitle><CardDescription>Step {step + 1} of 10</CardDescription></CardHeader><CardContent className="min-h-80">{renderStep()}</CardContent></Card>
    <div className="flex justify-between"><Button variant="outline" onClick={previous} disabled={step === 0}><ChevronLeft className="mr-2 h-4 w-4" />Previous</Button>{step < 9 ? <Button onClick={next}>Validate and continue<ChevronRight className="ml-2 h-4 w-4" /></Button> : <Button onClick={() => void finish()} disabled={submitting}>{submitting ? 'Creating…' : data.publication === 'publish' ? 'Create and publish' : 'Create draft competition'}</Button>}</div>
  </div>;

  function renderStep() {
    if (step === 0) return <div className="grid gap-4 md:grid-cols-2"><Field label="Competition name" value={data.name} onChange={(value) => update('name', value)} /><Field label="Edition / season" value={data.edition} onChange={(value) => update('edition', value)} /><Field label="Timezone" value={data.timezone} onChange={(value) => update('timezone', value)} /><Field label="Description" value={data.description} onChange={(value) => update('description', value)} /></div>;
    if (step === 1) return <ChoiceGrid>{sports.map((sport) => <Choice key={sport.id} selected={data.sportId === sport.id} onClick={() => setData((current) => ({ ...current, sportId: sport.id, rulesetVersionId: '', format: '', participantType: ['00000000-0000-0000-0000-000000000102', '00000000-0000-0000-0000-000000000103', '00000000-0000-0000-0000-000000000105'].includes(sport.id) ? 'Athlete' : 'Team' }))} title={`${sport.icon} ${sport.name}`} detail="Select sport" />)}</ChoiceGrid>;
    if (step === 2) return <ChoiceGrid>{rulesets.map((ruleset) => <Choice key={ruleset.id} selected={data.rulesetVersionId === ruleset.id} onClick={() => update('rulesetVersionId', ruleset.id)} title={ruleset.name} detail={`${rugbyVariant(ruleset) === 'sevens' ? '7 players · 7-minute halves · 3/2/1 pool points' : rugbyVariant(ruleset) === 'fifteens' ? '15 players · 40-minute halves · bonus points' : 'Published'} · version ${ruleset.versionNumber}`} />)}{rulesets.length === 0 && <p className="text-gray-500">No published rulesets are available for {selectedSport?.name ?? 'this sport'}.</p>}</ChoiceGrid>;
    if (step === 3) return <ChoiceGrid>{selectedSport?.formats.map((format) => <Choice key={format} selected={data.format === format} onClick={() => { update('format', format); update('stages', defaultStages(format)); }} title={formatNames[format]} detail={format === 'ROUND_ROBIN' ? 'Connected automatic fixture generation' : 'Creates a draft for completion in the sport workspace'} />)}</ChoiceGrid>;
    if (step === 4) return <div className="space-y-4"><div><Label htmlFor="participant-type">Participant type</Label><select id="participant-type" className="mt-1 w-full rounded-md border bg-white px-3 py-2" value={data.participantType} onChange={(event) => update('participantType', event.target.value)}>{participantTypes.map((type) => <option key={type} value={type}>{type.replace(/([a-z])([A-Z])/g, '$1 $2')}</option>)}</select><p className="mt-1 text-sm text-gray-500">Teams and individuals use the same participant workflow.</p></div><EditableList label={data.participantType} values={data.participants} setValues={(values) => update('participants', values)} minimum={2} /></div>;
    if (step === 5) return <EditableList label="Stage" values={data.stages} setValues={(values) => update('stages', values)} minimum={1} />;
    if (step === 6) return <div className="grid gap-4 md:grid-cols-2"><Field label="Start date" type="date" value={data.startDate} onChange={(value) => update('startDate', value)} /><Field label="End date" type="date" value={data.endDate} onChange={(value) => update('endDate', value)} /><Field label="Primary venue" value={data.venue} onChange={(value) => update('venue', value)} /><Field label="Draw seed (optional)" type="number" value={data.drawSeed} onChange={(value) => update('drawSeed', value)} /><label className="flex items-center gap-2 pt-7"><input type="checkbox" checked={data.homeAndAway} onChange={(event) => update('homeAndAway', event.target.checked)} />Home and away fixtures</label></div>;
    if (step === 7) return <div><p className="mb-4 text-sm text-gray-500">Record the officiating plan now; assignments can be completed after fixtures exist.</p><EditableList label="Official / role" values={data.officials} setValues={(values) => update('officials', values)} minimum={0} /></div>;
    if (step === 8) return <ChoiceGrid><Choice selected={data.publication === 'draft'} onClick={() => update('publication', 'draft')} title="Create as draft" detail="Persist the setup without public visibility" /><Choice selected={data.publication === 'publish'} onClick={() => update('publication', 'publish')} title="Publish after creation" detail="Run server structure and schedule validation first" /></ChoiceGrid>;
    return <Review data={data} sport={selectedSport?.name} ruleset={selectedRuleset?.name} />;
  }
}

function defaultStages(format: string) { return format === 'GROUP_PLUS_KNOCKOUT' ? ['Group stage', 'Knockout stage'] : [formatNames[format] ?? 'Main stage']; }
function Field({ label, value, onChange, type = 'text' }: { label: string; value: string; onChange: (value: string) => void; type?: string }) { const id = label.toLowerCase().replaceAll(' ', '-').replace('/', ''); return <div><Label htmlFor={id}>{label}</Label><Input id={id} type={type} value={value} onChange={(event) => onChange(event.target.value)} className="mt-1" /></div>; }
function ChoiceGrid({ children }: { children: React.ReactNode }) { return <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">{children}</div>; }
function Choice({ selected, onClick, title, detail }: { selected: boolean; onClick: () => void; title?: string; detail?: string }) { return <button type="button" onClick={onClick} className={`rounded-xl border p-4 text-left ${selected ? 'border-primary bg-primary/10 ring-1 ring-primary' : 'hover:border-gray-400'}`}><span className="font-semibold">{title}</span><span className="mt-1 block text-sm text-gray-500">{detail}</span></button>; }
function EditableList({ label, values, setValues, minimum }: { label: string; values: string[]; setValues: (values: string[]) => void; minimum: number }) { return <div className="space-y-3">{values.map((value, index) => <div key={index} className="flex gap-2"><Input aria-label={`${label} ${index + 1}`} value={value} onChange={(event) => setValues(values.map((item, itemIndex) => itemIndex === index ? event.target.value : item))} /><Button type="button" variant="outline" disabled={values.length <= minimum} onClick={() => setValues(values.filter((_, itemIndex) => itemIndex !== index))}><Trash2 className="h-4 w-4" /></Button></div>)}<Button type="button" variant="outline" onClick={() => setValues([...values, ''])}>Add {label.toLowerCase()}</Button></div>; }
function Review({ data, sport, ruleset }: { data: WizardData; sport?: string; ruleset?: string }) { const rows = [['Competition', `${data.name} · ${data.edition}`], ['Sport / ruleset', `${sport} · ${ruleset}`], ['Format', formatNames[data.format]], ['Participants', `${data.participants.length} × ${data.participantType}`], ['Stages', data.stages.join(', ')], ['Schedule', `${data.startDate} to ${data.endDate || 'open-ended'} · ${data.venue}`], ['Officials plan', data.officials.join(', ') || 'Assign later'], ['Publication', data.publication]]; return <div className="grid gap-3 md:grid-cols-2">{rows.map(([label, value]) => <div key={label} className="rounded-lg bg-gray-50 p-3"><p className="text-xs font-semibold uppercase text-gray-400">{label}</p><p className="font-medium">{value}</p></div>)}</div>; }
