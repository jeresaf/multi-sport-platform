'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { Archive, Download, FileBarChart, LoaderCircle, RefreshCw } from 'lucide-react';
import { adminApi, adminDownload, getStoredUser } from '@/lib/admin-api';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';

type CatalogItem = { value: string; label: string; available?: boolean };
type Catalog = { reportTypes: CatalogItem[]; formats: CatalogItem[] };
type Competition = { id: string; name: string };
type Contest = { id: string; name?: string | null; sortOrder: number; status: string };
type Report = {
  id: string;
  name: string;
  type: string;
  format: string;
  status: string;
  createdAt: string;
  generatedAt?: string | null;
  generatedFileName?: string | null;
  errorMessage?: string | null;
};
type ReportPage = { items: Report[]; total: number };
type ContestSheet = {
  id: string;
  contestId: string;
  contestName: string;
  sportName: string;
  template: string;
  name: string;
  status: string;
  createdAt: string;
  generatedFileName?: string | null;
  errorMessage?: string | null;
};
type ContestSheetPage = { items: ContestSheet[]; total: number };
type TenantExport = {
  id: string;
  name: string;
  status: string;
  createdAt: string;
  generatedAt?: string | null;
  generatedFileName?: string | null;
  errorMessage?: string | null;
};
type TenantExportPage = { items: TenantExport[]; total: number };

const selectClass = 'h-10 w-full rounded-md border border-gray-300 bg-white px-3 text-sm';

export default function ReportsPage() {
  const [tenantId, setTenantId] = useState('');
  const [catalog, setCatalog] = useState<Catalog | null>(null);
  const [competitions, setCompetitions] = useState<Competition[]>([]);
  const [contests, setContests] = useState<Contest[]>([]);
  const [reports, setReports] = useState<Report[]>([]);
  const [contestSheets, setContestSheets] = useState<ContestSheet[]>([]);
  const [tenantExports, setTenantExports] = useState<TenantExport[]>([]);
  const [name, setName] = useState('Fixture schedule');
  const [type, setType] = useState('FixtureSchedule');
  const [format, setFormat] = useState('Csv');
  const [competitionId, setCompetitionId] = useState('');
  const [fromUtc, setFromUtc] = useState('');
  const [toUtc, setToUtc] = useState('');
  const [sheetContestId, setSheetContestId] = useState('');
  const [sheetName, setSheetName] = useState('');
  const [exportName, setExportName] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  useEffect(() => setTenantId(getStoredUser()?.tenantId ?? ''), []);

  const load = useCallback(async () => {
    if (!tenantId) return;
    setError(null);
    try {
      const [nextCatalog, nextCompetitions, nextContests, nextReports, nextSheets, nextExports] = await Promise.all([
        adminApi<Catalog>('/api/reports/catalog'),
        adminApi<Competition[]>(`/api/competitions?tenantId=${tenantId}`),
        adminApi<Contest[]>(`/api/contests?tenantId=${tenantId}`),
        adminApi<ReportPage>(`/api/reports?tenantId=${tenantId}`),
        adminApi<ContestSheetPage>(`/api/contest-sheets?tenantId=${tenantId}`),
        adminApi<TenantExportPage>(`/api/tenant-exports?tenantId=${tenantId}`),
      ]);
      setCatalog(nextCatalog);
      setCompetitions(nextCompetitions);
      setContests(nextContests);
      setReports(nextReports.items);
      setContestSheets(nextSheets.items);
      setTenantExports(nextExports.items);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Could not load reports.');
    }
  }, [tenantId]);

  useEffect(() => { void load(); }, [load]);
  const hasActiveJobs = useMemo(
    () => [...reports, ...contestSheets, ...tenantExports].some(
      (item) => item.status === 'Pending' || item.status === 'Processing'),
    [reports, contestSheets, tenantExports],
  );
  useEffect(() => {
    if (!hasActiveJobs) return;
    const timer = window.setInterval(() => void load(), 2000);
    return () => window.clearInterval(timer);
  }, [hasActiveJobs, load]);

  async function generate(event: React.FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError(null);
    setMessage(null);
    try {
      await adminApi('/api/reports', {
        method: 'POST',
        body: JSON.stringify({
          tenantId,
          name,
          type,
          format,
          competitionId: competitionId || null,
          fromUtc: fromUtc ? new Date(`${fromUtc}T00:00:00Z`).toISOString() : null,
          toUtc: toUtc ? new Date(`${toUtc}T23:59:59Z`).toISOString() : null,
        }),
      });
      setMessage('Report queued. This page will update when the download is ready.');
      await load();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Could not generate the report.');
    } finally {
      setBusy(false);
    }
  }

  async function download(report: Report) {
    setError(null);
    try {
      await adminDownload(`/api/reports/${report.id}/download`);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Could not download the report.');
    }
  }

  async function archiveContestSheet(event: React.FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError(null);
    setMessage(null);
    try {
      await adminApi('/api/contest-sheets', {
        method: 'POST',
        body: JSON.stringify({
          tenantId,
          contestId: sheetContestId,
          name: sheetName.trim() || null,
        }),
      });
      setMessage('Printable contest sheet queued and archived. The download will appear when ready.');
      setSheetName('');
      await load();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Could not archive the contest sheet.');
    } finally {
      setBusy(false);
    }
  }

  async function downloadContestSheet(sheet: ContestSheet) {
    setError(null);
    try {
      await adminDownload(`/api/contest-sheets/${sheet.id}/download`);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Could not download the contest sheet.');
    }
  }

  async function generateTenantExport(event: React.FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError(null);
    setMessage(null);
    try {
      await adminApi('/api/tenant-exports', {
        method: 'POST',
        body: JSON.stringify({ tenantId, name: exportName.trim() || null }),
      });
      setMessage('Tenant data export queued. Request, completion, and download are audited.');
      setExportName('');
      await load();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Could not create the tenant export.');
    } finally {
      setBusy(false);
    }
  }

  async function downloadTenantExport(item: TenantExport) {
    setError(null);
    try {
      await adminDownload(`/api/tenant-exports/${item.id}/download`);
      setMessage('Tenant data export downloaded and audit event recorded.');
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Could not download the tenant export.');
    }
  }

  if (!tenantId) return <Card><CardHeader><CardTitle>Reports</CardTitle><CardDescription>Select a tenant-scoped account to generate reports.</CardDescription></CardHeader></Card>;

  return <div className="space-y-6" data-testid="reports-workspace">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <div><h1 className="text-2xl font-bold">Reports</h1><p className="text-sm text-gray-500">Generate tenant-isolated operational downloads.</p></div>
      <Button variant="outline" onClick={() => void load()}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button>
    </div>
    {error && <p role="alert" className="rounded-lg border border-red-200 bg-red-50 p-3 text-red-700">{error}</p>}
    {message && <p role="status" className="rounded-lg border border-green-200 bg-green-50 p-3 text-green-700">{message}</p>}

    <Card>
      <CardHeader><CardTitle>Generate report</CardTitle><CardDescription>All eleven report families support genuine PDF, CSV, Excel, and print-ready HTML through background generation.</CardDescription></CardHeader>
      <CardContent>
        <form className="grid gap-4 md:grid-cols-2 xl:grid-cols-3" onSubmit={generate}>
          <Field label="Report name" htmlFor="report-name"><Input id="report-name" required maxLength={200} value={name} onChange={(event) => setName(event.target.value)} /></Field>
          <Field label="Report type" htmlFor="report-type"><select id="report-type" className={selectClass} value={type} onChange={(event) => { setType(event.target.value); setName(event.target.options[event.target.selectedIndex].text); }}>{catalog?.reportTypes.filter((item) => item.available).map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}</select></Field>
          <Field label="Format" htmlFor="report-format"><select id="report-format" className={selectClass} value={format} onChange={(event) => setFormat(event.target.value)}>{catalog?.formats.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}</select></Field>
          <Field label="Competition" htmlFor="report-competition"><select id="report-competition" className={selectClass} value={competitionId} onChange={(event) => setCompetitionId(event.target.value)}><option value="">All competitions</option>{competitions.map((competition) => <option key={competition.id} value={competition.id}>{competition.name}</option>)}</select></Field>
          <Field label="From date (UTC)" htmlFor="report-from"><Input id="report-from" type="date" value={fromUtc} onChange={(event) => setFromUtc(event.target.value)} /></Field>
          <Field label="To date (UTC)" htmlFor="report-to"><Input id="report-to" type="date" value={toUtc} onChange={(event) => setToUtc(event.target.value)} /></Field>
          <div className="md:col-span-2 xl:col-span-3"><Button disabled={busy}>{busy ? <LoaderCircle className="mr-2 h-4 w-4 animate-spin" /> : <FileBarChart className="mr-2 h-4 w-4" />}Generate</Button></div>
        </form>
      </CardContent>
    </Card>

    <Card data-testid="contest-sheet-workspace">
      <CardHeader>
        <CardTitle>Printable contest sheets</CardTitle>
        <CardDescription>Create an immutable PDF snapshot using the contest&apos;s sport template, tenant branding, active sponsors, participants, officials, scoring structure, and results.</CardDescription>
      </CardHeader>
      <CardContent className="space-y-5">
        <form className="grid gap-4 md:grid-cols-2 xl:grid-cols-3" onSubmit={archiveContestSheet}>
          <Field label="Contest" htmlFor="sheet-contest"><select id="sheet-contest" className={selectClass} required value={sheetContestId} onChange={(event) => setSheetContestId(event.target.value)}><option value="">Select a contest</option>{contests.map((contest) => <option key={contest.id} value={contest.id}>{contest.name ?? `Contest ${contest.sortOrder}`} · {contest.status}</option>)}</select></Field>
          <Field label="Archive name (optional)" htmlFor="sheet-name"><Input id="sheet-name" maxLength={200} placeholder="Uses the contest name by default" value={sheetName} onChange={(event) => setSheetName(event.target.value)} /></Field>
          <div className="flex items-end"><Button disabled={busy || !sheetContestId}>{busy ? <LoaderCircle className="mr-2 h-4 w-4 animate-spin" /> : <Archive className="mr-2 h-4 w-4" />}Generate and archive</Button></div>
        </form>
        <div className="space-y-3">
          {contestSheets.map((sheet) => <div key={sheet.id} className="flex flex-wrap items-center justify-between gap-3 rounded-lg border p-3">
            <div><p className="font-semibold">{sheet.name}</p><p className="text-xs text-gray-500">{sheet.contestName} · {sheet.sportName} · {sheet.template} · {new Date(sheet.createdAt).toLocaleString()}</p>{sheet.errorMessage && <p className="mt-1 text-xs text-red-600">{sheet.errorMessage}</p>}</div>
            <div className="flex items-center gap-2"><span className={`rounded-full px-2 py-1 text-xs font-semibold ${sheet.status === 'Completed' ? 'bg-green-100 text-green-700' : sheet.status === 'Failed' ? 'bg-red-100 text-red-700' : 'bg-blue-100 text-blue-700'}`}>{sheet.status}</span><Button size="sm" variant="outline" disabled={sheet.status !== 'Completed'} onClick={() => void downloadContestSheet(sheet)}><Download className="mr-2 h-4 w-4" />PDF</Button></div>
          </div>)}
          {contestSheets.length === 0 && <p className="text-sm text-gray-500">No printable contest sheets have been archived for this tenant.</p>}
        </div>
      </CardContent>
    </Card>

    <Card data-testid="tenant-export-workspace">
      <CardHeader>
        <CardTitle>Tenant data exports</CardTitle>
        <CardDescription>Generate a tenant-isolated ZIP of JSON datasets. Credentials and session secrets are excluded; requests, completion, retries, and downloads are audited.</CardDescription>
      </CardHeader>
      <CardContent className="space-y-5">
        <form className="grid gap-4 md:grid-cols-2 xl:grid-cols-3" onSubmit={generateTenantExport}>
          <Field label="Export name (optional)" htmlFor="tenant-export-name"><Input id="tenant-export-name" maxLength={200} placeholder="Uses tenant and timestamp by default" value={exportName} onChange={(event) => setExportName(event.target.value)} /></Field>
          <div className="flex items-end"><Button disabled={busy}><Archive className="mr-2 h-4 w-4" />Generate tenant export</Button></div>
        </form>
        <div className="space-y-3">
          {tenantExports.map((item) => <div key={item.id} className="flex flex-wrap items-center justify-between gap-3 rounded-lg border p-3">
            <div><p className="font-semibold">{item.name}</p><p className="text-xs text-gray-500">ZIP/JSON · tenant-isolated · {new Date(item.createdAt).toLocaleString()}</p>{item.errorMessage && <p className="mt-1 text-xs text-red-600">{item.errorMessage}</p>}</div>
            <div className="flex items-center gap-2"><span className={`rounded-full px-2 py-1 text-xs font-semibold ${item.status === 'Completed' ? 'bg-green-100 text-green-700' : item.status === 'Failed' ? 'bg-red-100 text-red-700' : 'bg-blue-100 text-blue-700'}`}>{item.status}</span><Button size="sm" variant="outline" disabled={item.status !== 'Completed'} onClick={() => void downloadTenantExport(item)}><Download className="mr-2 h-4 w-4" />ZIP</Button></div>
          </div>)}
          {tenantExports.length === 0 && <p className="text-sm text-gray-500">No tenant data exports have been generated.</p>}
        </div>
      </CardContent>
    </Card>

    <Card>
      <CardHeader><CardTitle>Generated reports</CardTitle><CardDescription>{reports.length} of the latest {Math.max(reports.length, 20)} report records.</CardDescription></CardHeader>
      <CardContent className="space-y-3">
        {reports.map((report) => <div key={report.id} className="flex flex-wrap items-center justify-between gap-3 rounded-lg border p-3">
          <div><p className="font-semibold">{report.name}</p><p className="text-xs text-gray-500">{report.type} · {report.format} · {new Date(report.createdAt).toLocaleString()}</p>{report.errorMessage && <p className="mt-1 text-xs text-red-600">{report.errorMessage}</p>}</div>
          <div className="flex items-center gap-2"><span className={`rounded-full px-2 py-1 text-xs font-semibold ${report.status === 'Completed' ? 'bg-green-100 text-green-700' : report.status === 'Failed' ? 'bg-red-100 text-red-700' : 'bg-blue-100 text-blue-700'}`}>{report.status}</span><Button size="sm" variant="outline" disabled={report.status !== 'Completed'} onClick={() => void download(report)}><Download className="mr-2 h-4 w-4" />Download</Button></div>
        </div>)}
        {reports.length === 0 && <p className="text-sm text-gray-500">No reports have been generated for this tenant.</p>}
      </CardContent>
    </Card>
  </div>;
}

function Field({ label, htmlFor, children }: { label: string; htmlFor: string; children: React.ReactNode }) {
  return <div className="space-y-2"><Label htmlFor={htmlFor}>{label}</Label>{children}</div>;
}
