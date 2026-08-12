'use client';

import { FormEvent, useCallback, useEffect, useState } from 'react';
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
    <div><h1 className="text-3xl font-bold text-gray-950">Registration form builder</h1><p className="mt-1 text-sm text-gray-600">Versioned tenant forms with dynamic web and Flutter-compatible schemas.</p></div>
    {error && <p role="alert" className="rounded-md bg-red-50 p-3 text-sm text-red-700">{error}</p>}{message && <p className="rounded-md bg-green-50 p-3 text-sm text-green-800">{message}</p>}
    {platform && <label className="block max-w-md text-sm font-semibold">Tenant<select value={tenantId} onChange={event => setTenantId(event.target.value)} className="mt-1 h-10 w-full rounded-md border bg-white px-3 font-normal"><option value="">Select tenant</option>{tenants.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>}
    <div className="grid gap-6 xl:grid-cols-[18rem_1fr_1fr]">
      <aside className="space-y-3 rounded-xl border bg-white p-4"><h2 className="font-bold">Tenant forms</h2><select value={selectedId} onChange={event => selectForm(event.target.value)} className="h-10 w-full rounded-md border bg-white px-3"><option value="">Create new form</option>{forms.map(item => <option key={item.id} value={item.id}>{item.name} · v{item.version}</option>)}</select><p className="text-xs text-gray-500">Each schema save creates a new version. Existing submissions retain their original schema.</p></aside>
      <form onSubmit={save} className="space-y-4 rounded-xl border bg-white p-5"><h2 className="font-bold">Form definition</h2><label className="text-sm font-semibold">Name<input value={name} onChange={event => setName(event.target.value)} required className="mt-1 h-10 w-full rounded-md border px-3 font-normal" /></label><label className="text-sm font-semibold">Description<textarea value={description} onChange={event => setDescription(event.target.value)} className="mt-1 min-h-20 w-full rounded-md border p-3 font-normal" /></label><label className="text-sm font-semibold">Competition<select value={competitionId} onChange={event => setCompetitionId(event.target.value)} className="mt-1 h-10 w-full rounded-md border bg-white px-3 font-normal"><option value="">Reusable tenant form</option>{competitions.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
        <div className="flex items-center justify-between"><h2 className="font-bold">Fields</h2><button type="button" onClick={() => setFields(current => [...current, blankField(current.length)])} className="rounded-md border px-3 py-2 text-xs font-semibold">Add field</button></div>
        {fields.map((field, index) => <div key={`${field.id}-${index}`} className="space-y-3 rounded-lg bg-gray-50 p-3"><div className="grid gap-2 sm:grid-cols-2"><label className="text-xs font-semibold">ID<input value={field.id} onChange={event => updateField(index, { id: event.target.value })} className="mt-1 h-9 w-full rounded border px-2 font-normal" /></label><label className="text-xs font-semibold">Type<select value={field.fieldType} onChange={event => updateField(index, { fieldType: event.target.value })} className="mt-1 h-9 w-full rounded border bg-white px-2 font-normal">{fieldTypes.map(type => <option key={type}>{type}</option>)}</select></label><label className="text-xs font-semibold">Label<input value={field.label} onChange={event => updateField(index, { label: event.target.value })} className="mt-1 h-9 w-full rounded border px-2 font-normal" /></label><label className="flex items-end gap-2 pb-2 text-xs font-semibold"><input type="checkbox" checked={field.isRequired} onChange={event => updateField(index, { isRequired: event.target.checked })} />Required</label></div>
          {(field.fieldType === 'Select' || field.fieldType === 'Checkbox') && <label className="text-xs font-semibold">Options (comma separated)<input value={field.options?.map(item => item.value).join(',') ?? ''} onChange={event => updateField(index, { options: event.target.value.split(',').map(value => value.trim()).filter(Boolean).map(value => ({ value, label: value })) })} className="mt-1 h-9 w-full rounded border px-2 font-normal" /></label>}
          {field.fieldType === 'Text' && <div className="grid grid-cols-3 gap-2"><input aria-label="Minimum length" type="number" placeholder="Min length" value={field.minLength ?? ''} onChange={event => updateField(index, { minLength: event.target.value ? Number(event.target.value) : null })} className="h-9 rounded border px-2" /><input aria-label="Maximum length" type="number" placeholder="Max length" value={field.maxLength ?? ''} onChange={event => updateField(index, { maxLength: event.target.value ? Number(event.target.value) : null })} className="h-9 rounded border px-2" /><input aria-label="Validation expression" placeholder="Validation regex" value={field.validationRegex ?? ''} onChange={event => updateField(index, { validationRegex: event.target.value || null })} className="h-9 rounded border px-2" /></div>}
          {field.fieldType === 'Number' && <div className="grid grid-cols-2 gap-2"><input aria-label="Minimum value" type="number" placeholder="Minimum" value={field.minValue ?? ''} onChange={event => updateField(index, { minValue: event.target.value ? Number(event.target.value) : null })} className="h-9 rounded border px-2" /><input aria-label="Maximum value" type="number" placeholder="Maximum" value={field.maxValue ?? ''} onChange={event => updateField(index, { maxValue: event.target.value ? Number(event.target.value) : null })} className="h-9 rounded border px-2" /></div>}
          {field.fieldType === 'Consent' && <textarea aria-label="Consent text" placeholder="Consent text" value={field.consentText ?? ''} onChange={event => updateField(index, { consentText: event.target.value })} className="min-h-16 w-full rounded border p-2 text-xs" />}
          {field.fieldType === 'File' && <input aria-label="Allowed content types" value={field.allowedContentTypes?.join(',') ?? 'application/pdf,image/jpeg,image/png'} onChange={event => updateField(index, { allowedContentTypes: event.target.value.split(',').map(value => value.trim()).filter(Boolean) })} className="h-9 w-full rounded border px-2 text-xs" />}
          <div className="grid grid-cols-2 gap-2"><select aria-label="Conditional field" value={field.conditionalFieldId ?? ''} onChange={event => updateField(index, { conditionalFieldId: event.target.value || null })} className="h-9 rounded border bg-white px-2 text-xs"><option value="">Always visible</option>{fields.filter((_, position) => position !== index).map(item => <option key={item.id} value={item.id}>When {item.label}</option>)}</select><input aria-label="Conditional value" placeholder="equals value" value={field.conditionalValue ?? ''} onChange={event => updateField(index, { conditionalValue: event.target.value || null })} className="h-9 rounded border px-2 text-xs" /></div><button type="button" onClick={() => setFields(current => current.filter((_, position) => position !== index))} className="text-xs font-semibold text-red-700">Remove field</button></div>)}
        <button disabled={busy || !tenantId || !name || fields.length === 0} className="rounded-md bg-primary px-4 py-2 text-sm font-semibold text-white disabled:opacity-50">Save versioned schema</button>
      </form>
      <section className="space-y-4 rounded-xl border bg-white p-5"><h2 className="font-bold">Dynamic web preview</h2>{fields.filter(visible).map(field => <DynamicField key={field.id} field={field} value={values[field.id]} onChange={value => setValues(current => ({ ...current, [field.id]: value }))} onUpload={file => void upload(field, file)} />)}{validation.length > 0 && <ul className="rounded bg-red-50 p-3 text-xs text-red-700">{validation.map(item => <li key={item}>{item}</li>)}</ul>}<button type="button" disabled={!selectedId} onClick={() => void validatePreview()} className="rounded-md border px-4 py-2 text-sm font-semibold disabled:opacity-50">Validate preview on server</button></section>
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
