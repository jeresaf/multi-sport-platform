'use client';

import { FormEvent, useCallback, useEffect, useMemo, useState } from 'react';
import { adminApi, getStoredUser, isPlatformOperator } from '@/lib/admin-api';

type Tenant = { id: string; name: string };
type Competition = { id: string; name: string };
type Option = { value: string; label: string };
type Field = {
  id: string; fieldType: string; label: string; placeholder?: string | null;
  isRequired: boolean; validationRegex?: string | null; validationMessage?: string | null;
  minLength?: number | null; maxLength?: number | null; minValue?: number | null; maxValue?: number | null;
  options?: Option[] | null; conditionalFieldId?: string | null; conditionalValue?: string | null;
  order: number; showInList: boolean; helpText?: string | null; consentText?: string | null;
  allowedContentTypes?: string[] | null; maxFileSizeBytes?: number | null;
};
type RegistrationForm = { id: string; tenantId: string; competitionId?: string | null; name: string; description?: string | null; formSchema: string; isActive: boolean; version: number };
type FileValue = { url: string; fileName: string; contentType: string; size: number };

const fieldTypes = ['Text', 'Number', 'Date', 'Select', 'Checkbox', 'File', 'Consent'];
const blankField = (order: number): Field => ({ id: `field_${order + 1}`, fieldType: 'Text', label: `Field ${order + 1}`, isRequired: false, order, showInList: true });

export default function RegistrationFormsPage() {
  const user = getStoredUser();
  const platform = isPlatformOperator(user);
  const [tenants, setTenants] = useState<Tenant[]>([]);
  const [tenantId, setTenantId] = useState(user?.tenantId ?? '');
  const [competitions, setCompetitions] = useState<Competition[]>([]);
  const [forms, setForms] = useState<RegistrationForm[]>([]);
  const [selectedId, setSelectedId] = useState('');
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [competitionId, setCompetitionId] = useState('');
  const [fields, setFields] = useState<Field[]>([blankField(0)]);
  const [values, setValues] = useState<Record<string, unknown>>({});
  const [validation, setValidation] = useState<string[]>([]);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const metrics = useMemo(() => ({
    forms: forms.length,
    fields: fields.length,
    required: fields.filter(field => field.isRequired).length,
    conditional: fields.filter(field => field.conditionalFieldId).length,
  }), [fields, forms]);

  const load = useCallback(async (scope = tenantId) => {
    if (!scope) return;
    const [formItems, competitionItems] = await Promise.all([
      adminApi<RegistrationForm[]>(`/api/registrationforms/tenant/${scope}`),
      adminApi<Competition[]>(`/api/competitions?tenantId=${scope}`),
    ]);
    setForms(formItems.sort((a, b) => a.name.localeCompare(b.name)));
    setCompetitions(competitionItems);
  }, [tenantId]);

  useEffect(() => {
    if (platform) adminApi<Tenant[]>('/api/tenants').then(items => { setTenants(items); setTenantId(current => current || items[0]?.id || ''); }).catch(reason => setError(reason instanceof Error ? reason.message : 'Unable to load tenants.'));
  }, [platform]);
  useEffect(() => { setSelectedId(''); setName(''); setDescription(''); setCompetitionId(''); setFields([blankField(0)]); setValues({}); void load(tenantId).catch(reason => setError(reason instanceof Error ? reason.message : 'Unable to load forms.')); }, [load, tenantId]);

  function selectForm(id: string) {
    setSelectedId(id); setValues({}); setValidation([]); setError(null); setMessage(null);
    const form = forms.find(item => item.id === id);
    if (!form) { setName(''); setDescription(''); setCompetitionId(''); setFields([blankField(0)]); return; }
    setName(form.name); setDescription(form.description ?? ''); setCompetitionId(form.competitionId ?? '');
    try { const parsed = JSON.parse(form.formSchema) as Field[]; setFields(parsed.length ? parsed : [blankField(0)]); }
    catch { setFields([blankField(0)]); setError('The stored schema could not be parsed.'); }
  }

  async function save(event: FormEvent) {
    event.preventDefault(); setBusy(true); setError(null); setMessage(null);
    try {
      let id = selectedId;
      if (!id) {
        const created = await adminApi<RegistrationForm>('/api/registrationforms', { method: 'POST', body: JSON.stringify({ tenantId, name, description: description || null, competitionId: competitionId || null, createdBy: user?.id ?? 'admin-web' }) });
        id = created.id;
      } else {
        await adminApi(`/api/registrationforms/${id}`, { method: 'PUT', body: JSON.stringify({ name, description: description || null }) });
      }
      await adminApi(`/api/registrationforms/${id}/schema`, { method: 'PUT', body: JSON.stringify({ fields: fields.map((field, index) => ({ ...field, order: index })) }) });
      await load(); setSelectedId(id); setMessage('Registration form schema saved as a new version and audited.');
      const schema = await adminApi<{ fields: Field[] }>(`/api/registrationforms/${id}/schema`); setFields(schema.fields);
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to save the form.'); }
    finally { setBusy(false); }
  }

  function updateField(index: number, change: Partial<Field>) { setFields(current => current.map((item, position) => position === index ? { ...item, ...change } : item)); }
  function visible(field: Field) { return !field.conditionalFieldId || String(values[field.conditionalFieldId] ?? '').toLowerCase() === String(field.conditionalValue ?? '').toLowerCase(); }
  async function validatePreview() {
    if (!selectedId) return;
    const result = await adminApi<{ isValid: boolean; errors: string[] }>(`/api/registrationforms/${selectedId}/validate`, { method: 'POST', body: JSON.stringify({ formData: values }) });
    setValidation(result.errors); setMessage(result.isValid ? 'Preview data passes the server validation rules.' : null);
  }
  async function upload(field: Field, file: File) {
    if (!selectedId) return setError('Save the form before uploading a file.');
    const body = new FormData(); body.append('file', file);
    const uploaded = await adminApi<FileValue>(`/api/registrationforms/${selectedId}/fields/${field.id}/files`, { method: 'POST', body });
    setValues(current => ({ ...current, [field.id]: uploaded }));
  }

  return <div className="space-y-6">
    <section className="overflow-hidden rounded-3xl bg-gradient-to-br from-slate-950 via-slate-900 to-slate-800 p-6 text-white shadow-sm">
      <div className="flex flex-col gap-5 xl:flex-row xl:items-end xl:justify-between">
        <div className="max-w-3xl">
          <p className="text-xs font-bold uppercase tracking-[0.2em] text-slate-400">Registration architecture</p>
          <h1 className="mt-2 text-3xl font-black tracking-tight">Registration form builder</h1>
          <p className="mt-2 max-w-2xl text-sm leading-6 text-slate-300">
            Design versioned tenant forms with reusable competition scope, conditional fields, file inputs, consent, validation rules, and server-validated previews.
          </p>
        </div>
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          <FormBuilderMetric label="Forms" value={metrics.forms} />
          <FormBuilderMetric label="Fields" value={metrics.fields} />
          <FormBuilderMetric label="Required" value={metrics.required} />
          <FormBuilderMetric label="Conditional" value={metrics.conditional} />
        </div>
      </div>
    </section>
    {error && <p role="alert" className="rounded-md bg-red-50 p-3 text-sm text-red-700">{error}</p>}{message && <p className="rounded-md bg-green-50 p-3 text-sm text-green-800">{message}</p>}
    {platform && <label className="block max-w-md text-sm font-bold text-slate-800">Tenant<select value={tenantId} onChange={event => setTenantId(event.target.value)} className="mt-1 h-10 w-full rounded-md border bg-white px-3 font-normal"><option value="">Select tenant</option>{tenants.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>}
    <div className="grid gap-6 xl:grid-cols-[18rem_minmax(0,1.15fr)_minmax(0,0.85fr)]">
      <aside className="space-y-4 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm xl:sticky xl:top-20 xl:self-start">
        <div>
          <p className="text-xs font-bold uppercase tracking-[0.16em] text-slate-400">Form library</p>
          <h2 className="mt-1 text-lg font-extrabold text-slate-950">Tenant forms</h2>
          <p className="mt-1 text-xs text-slate-500">{forms.length} saved form{forms.length === 1 ? '' : 's'}</p>
        </div>
        <select value={selectedId} onChange={event => selectForm(event.target.value)} className="h-10 w-full rounded-lg border border-slate-300 bg-white px-3">
          <option value="">Create new form</option>
          {forms.map(item => <option key={item.id} value={item.id}>{item.name} · v{item.version}</option>)}
        </select>
        <p className="rounded-xl border border-blue-100 bg-blue-50 p-3 text-xs leading-5 text-blue-800">
          Each schema save creates a new version. Existing submissions retain the schema version they were created against.
        </p>
      </aside>
      <form onSubmit={save} className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm"><div><p className="text-xs font-bold uppercase tracking-[0.16em] text-slate-400">Schema editor</p><h2 className="mt-1 text-lg font-extrabold text-slate-950">Form definition</h2><p className="mt-1 text-sm text-slate-500">Edit the form metadata and field schema, then save a new audited version.</p></div><label className="text-sm font-semibold">Name<input value={name} onChange={event => setName(event.target.value)} required className="mt-1 h-10 w-full rounded-md border px-3 font-normal" /></label><label className="text-sm font-semibold">Description<textarea value={description} onChange={event => setDescription(event.target.value)} className="mt-1 min-h-20 w-full rounded-md border p-3 font-normal" /></label><label className="text-sm font-semibold">Competition<select value={competitionId} onChange={event => setCompetitionId(event.target.value)} className="mt-1 h-10 w-full rounded-md border bg-white px-3 font-normal"><option value="">Reusable tenant form</option>{competitions.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
        <div className="flex items-center justify-between"><div><p className="text-xs font-bold uppercase tracking-[0.16em] text-slate-400">Structure</p><h2 className="mt-1 font-extrabold text-slate-950">Fields</h2></div><button type="button" onClick={() => setFields(current => [...current, blankField(current.length)])} className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs font-bold text-slate-800 shadow-sm transition hover:bg-slate-50">Add field</button></div>
        {fields.map((field, index) => <div key={`${field.id}-${index}`} className="space-y-3 rounded-2xl border border-slate-200 bg-slate-50 p-4"><div className="grid gap-2 sm:grid-cols-2"><label className="text-xs font-semibold">ID<input value={field.id} onChange={event => updateField(index, { id: event.target.value })} className="mt-1 h-9 w-full rounded border px-2 font-normal" /></label><label className="text-xs font-semibold">Type<select value={field.fieldType} onChange={event => updateField(index, { fieldType: event.target.value })} className="mt-1 h-9 w-full rounded border bg-white px-2 font-normal">{fieldTypes.map(type => <option key={type}>{type}</option>)}</select></label><label className="text-xs font-semibold">Label<input value={field.label} onChange={event => updateField(index, { label: event.target.value })} className="mt-1 h-9 w-full rounded border px-2 font-normal" /></label><label className="flex items-end gap-2 pb-2 text-xs font-semibold"><input type="checkbox" checked={field.isRequired} onChange={event => updateField(index, { isRequired: event.target.checked })} />Required</label></div>
          {(field.fieldType === 'Select' || field.fieldType === 'Checkbox') && <label className="text-xs font-semibold">Options (comma separated)<input value={field.options?.map(item => item.value).join(',') ?? ''} onChange={event => updateField(index, { options: event.target.value.split(',').map(value => value.trim()).filter(Boolean).map(value => ({ value, label: value })) })} className="mt-1 h-9 w-full rounded border px-2 font-normal" /></label>}
          {field.fieldType === 'Text' && <div className="grid grid-cols-3 gap-2"><input aria-label="Minimum length" type="number" placeholder="Min length" value={field.minLength ?? ''} onChange={event => updateField(index, { minLength: event.target.value ? Number(event.target.value) : null })} className="h-9 rounded border px-2" /><input aria-label="Maximum length" type="number" placeholder="Max length" value={field.maxLength ?? ''} onChange={event => updateField(index, { maxLength: event.target.value ? Number(event.target.value) : null })} className="h-9 rounded border px-2" /><input aria-label="Validation expression" placeholder="Validation regex" value={field.validationRegex ?? ''} onChange={event => updateField(index, { validationRegex: event.target.value || null })} className="h-9 rounded border px-2" /></div>}
          {field.fieldType === 'Number' && <div className="grid grid-cols-2 gap-2"><input aria-label="Minimum value" type="number" placeholder="Minimum" value={field.minValue ?? ''} onChange={event => updateField(index, { minValue: event.target.value ? Number(event.target.value) : null })} className="h-9 rounded border px-2" /><input aria-label="Maximum value" type="number" placeholder="Maximum" value={field.maxValue ?? ''} onChange={event => updateField(index, { maxValue: event.target.value ? Number(event.target.value) : null })} className="h-9 rounded border px-2" /></div>}
          {field.fieldType === 'Consent' && <textarea aria-label="Consent text" placeholder="Consent text" value={field.consentText ?? ''} onChange={event => updateField(index, { consentText: event.target.value })} className="min-h-16 w-full rounded border p-2 text-xs" />}
          {field.fieldType === 'File' && <input aria-label="Allowed content types" value={field.allowedContentTypes?.join(',') ?? 'application/pdf,image/jpeg,image/png'} onChange={event => updateField(index, { allowedContentTypes: event.target.value.split(',').map(value => value.trim()).filter(Boolean) })} className="h-9 w-full rounded border px-2 text-xs" />}
          <div className="grid grid-cols-2 gap-2"><select aria-label="Conditional field" value={field.conditionalFieldId ?? ''} onChange={event => updateField(index, { conditionalFieldId: event.target.value || null })} className="h-9 rounded border bg-white px-2 text-xs"><option value="">Always visible</option>{fields.filter((_, position) => position !== index).map(item => <option key={item.id} value={item.id}>When {item.label}</option>)}</select><input aria-label="Conditional value" placeholder="equals value" value={field.conditionalValue ?? ''} onChange={event => updateField(index, { conditionalValue: event.target.value || null })} className="h-9 rounded border px-2 text-xs" /></div><button type="button" onClick={() => setFields(current => current.filter((_, position) => position !== index))} className="text-xs font-semibold text-red-700">Remove field</button></div>)}
        <button disabled={busy || !tenantId || !name || fields.length === 0} className="rounded-lg bg-primary px-4 py-2 text-sm font-bold text-white shadow-sm transition hover:brightness-95 disabled:opacity-50">Save versioned schema</button>
      </form>
      <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm xl:sticky xl:top-20 xl:self-start"><div><p className="text-xs font-bold uppercase tracking-[0.16em] text-slate-400">Preview</p><h2 className="mt-1 text-lg font-extrabold text-slate-950">Dynamic web preview</h2><p className="mt-1 text-sm text-slate-500">Preview visible fields using the same schema and validate the sample data against the server.</p></div>{fields.filter(visible).map(field => <DynamicField key={field.id} field={field} value={values[field.id]} onChange={value => setValues(current => ({ ...current, [field.id]: value }))} onUpload={file => void upload(field, file)} />)}{validation.length > 0 && <ul className="rounded-xl border border-red-200 bg-red-50 p-3 text-xs text-red-700">{validation.map(item => <li key={item}>{item}</li>)}</ul>}<button type="button" disabled={!selectedId} onClick={() => void validatePreview()} className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-bold text-slate-800 shadow-sm transition hover:bg-slate-50 disabled:opacity-50">Validate preview on server</button></section>
    </div>
  </div>;
}

function DynamicField({ field, value, onChange, onUpload }: { field: Field; value: unknown; onChange: (value: unknown) => void; onUpload: (file: File) => void }) {
  const label = <span>{field.label}{field.isRequired && <span className="text-red-600"> *</span>}</span>;
  if (field.fieldType === 'Consent') return <label className="flex gap-2 text-sm"><input type="checkbox" checked={value === true} onChange={event => onChange(event.target.checked)} /><span>{field.consentText || field.label}</span></label>;
  if (field.fieldType === 'Checkbox') return <fieldset><legend className="text-sm font-semibold">{label}</legend>{field.options?.map(option => <label key={option.value} className="mr-3 inline-flex gap-1 text-sm"><input type="checkbox" checked={Array.isArray(value) && value.includes(option.value)} onChange={event => { const current = Array.isArray(value) ? value as string[] : []; onChange(event.target.checked ? [...current, option.value] : current.filter(item => item !== option.value)); }} />{option.label}</label>)}</fieldset>;
  if (field.fieldType === 'Select') return <label className="block text-sm font-semibold">{label}<select value={String(value ?? '')} onChange={event => onChange(event.target.value)} className="mt-1 h-10 w-full rounded border bg-white px-3 font-normal"><option value="">Select</option>{field.options?.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}</select></label>;
  if (field.fieldType === 'File') return <label className="block text-sm font-semibold">{label}<input type="file" accept={field.allowedContentTypes?.join(',')} onChange={event => event.target.files?.[0] && onUpload(event.target.files[0])} className="mt-1 block w-full text-sm font-normal" />{typeof value === 'object' && value && <span className="text-xs text-green-700">Uploaded {(value as FileValue).fileName}</span>}</label>;
  return <label className="block text-sm font-semibold">{label}<input type={field.fieldType === 'Number' ? 'number' : field.fieldType === 'Date' ? 'date' : 'text'} value={String(value ?? '')} onChange={event => onChange(field.fieldType === 'Number' && event.target.value ? Number(event.target.value) : event.target.value)} placeholder={field.placeholder ?? ''} className="mt-1 h-10 w-full rounded border px-3 font-normal" />{field.helpText && <span className="text-xs text-gray-500">{field.helpText}</span>}</label>;
}

function FormBuilderMetric({ label, value }: { label: string; value: number }) {
  return <div className="min-w-24 rounded-2xl border border-white/10 bg-white/5 px-4 py-3 backdrop-blur">
    <div className="text-[10px] font-bold uppercase tracking-[0.16em] text-slate-400">{label}</div>
    <div className="mt-1 text-2xl font-black text-white">{value}</div>
  </div>;
}

