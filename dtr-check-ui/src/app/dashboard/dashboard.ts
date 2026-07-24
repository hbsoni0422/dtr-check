import { JsonPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { DtrCheckApi, EvaluateRequest, EvaluateResponse } from '../services/dtr-check-api';

declare const FHIR: any;

type StatusKind = '' | 'loading' | 'error' | 'success';
type FileKind = 'patient' | 'questionnaire' | 'rules';

const FILE_LABELS: Record<FileKind, string> = {
  patient: 'Patient bundle',
  questionnaire: 'Questionnaire',
  rules: 'Rules',
};

const STORAGE_KEY = 'dtr-check:docs';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [RouterLink, JsonPipe],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
})
export class Dashboard {
  private readonly api = inject(DtrCheckApi);

  readonly fileKinds: FileKind[] = ['patient', 'questionnaire', 'rules'];

  readonly statusMessage = signal('');
  readonly statusKind = signal<StatusKind>('');
  readonly result = signal<EvaluateResponse | null>(null);
  readonly dragOver = signal<FileKind | null>(null);

  readonly selectedFiles = signal<Partial<Record<FileKind, File>>>({});
  private loadedDocs: EvaluateRequest | null = null;

  readonly canRun = computed(() => {
    const files = this.selectedFiles();
    const hasAllFiles = !!(files.patient && files.questionnaire && files.rules);
    return hasAllFiles || this.loadedDocs !== null;
  });

  readonly answeredCount = computed(() => this.result()?.results.filter((r) => r.status === 'answered').length ?? 0);
  readonly naCount = computed(() => this.result()?.results.filter((r) => r.status === 'not_applicable').length ?? 0);
  readonly missingCount = computed(() => this.result()?.gaps.length ?? 0);
  readonly totalCount = computed(() => this.result()?.results.length ?? 0);
  readonly applicableCount = computed(() => this.totalCount() - this.naCount());
  readonly completenessPct = computed(() => {
    const applicable = this.applicableCount();
    return applicable > 0 ? Math.round((this.answeredCount() / applicable) * 100) : 100;
  });
  readonly completenessLevel = computed<'low' | 'mid' | 'high'>(() => {
    const pct = this.completenessPct();
    return pct >= 90 ? 'high' : pct >= 50 ? 'mid' : 'low';
  });

  constructor() {
    const restored = this.restoreDocs();
    if (restored) {
      this.loadedDocs = restored;
      this.setStatus('Restored previously loaded data from browser storage. Click "Run check".', 'success');
    }
    this.trySmartLaunch();
  }

  private persistDocs(docs: EvaluateRequest): void {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(docs));
    } catch {
      // localStorage unavailable (private browsing, quota exceeded) -- caching is best-effort.
    }
  }

  private restoreDocs(): EvaluateRequest | null {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      return raw ? (JSON.parse(raw) as EvaluateRequest) : null;
    } catch {
      return null;
    }
  }

  private setStatus(message: string, kind: StatusKind = ''): void {
    this.statusMessage.set(message);
    this.statusKind.set(kind);
  }

  async loadSample(): Promise<void> {
    this.setStatus('Loading sample data...', 'loading');
    try {
      const sample = await firstValueFrom(this.api.getSample());
      this.loadedDocs = sample;
      this.persistDocs(sample);
      this.selectedFiles.set({});
      this.setStatus('Sample data loaded (synthetic OSA patient). Click "Run check".', 'success');
    } catch (err) {
      this.setStatus(this.errorMessage(err), 'error');
    }
  }

  onFileSelected(kind: FileKind, event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.selectedFiles.update((files) => ({ ...files, [kind]: file }));
    this.loadedDocs = null;
  }

  onDragOver(kind: FileKind, event: DragEvent): void {
    event.preventDefault();
    this.dragOver.set(kind);
  }

  onDragLeave(): void {
    this.dragOver.set(null);
  }

  onDrop(kind: FileKind, event: DragEvent): void {
    event.preventDefault();
    this.dragOver.set(null);
    const file = event.dataTransfer?.files?.[0];
    if (!file) return;
    this.selectedFiles.update((files) => ({ ...files, [kind]: file }));
    this.loadedDocs = null;
  }

  fileNameFor(kind: FileKind): string | null {
    return this.selectedFiles()[kind]?.name ?? null;
  }

  labelFor(kind: FileKind): string {
    return FILE_LABELS[kind];
  }

  private readFileAsJson(file: File): Promise<unknown> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => {
        try {
          resolve(JSON.parse(reader.result as string));
        } catch (err) {
          reject(new Error(`${file.name}: invalid JSON (${(err as Error).message})`));
        }
      };
      reader.onerror = () => reject(new Error(`${file.name}: could not be read`));
      reader.readAsText(file);
    });
  }

  async runCheck(): Promise<void> {
    this.setStatus('Running check...', 'loading');
    this.result.set(null);
    try {
      let docs = this.loadedDocs;
      const files = this.selectedFiles();
      if (files.patient || files.questionnaire || files.rules) {
        if (!files.patient || !files.questionnaire || !files.rules) {
          throw new Error('Select all three files (patient, questionnaire, rules) or load the sample data.');
        }
        docs = {
          patient: await this.readFileAsJson(files.patient),
          questionnaire: await this.readFileAsJson(files.questionnaire),
          rules: await this.readFileAsJson(files.rules),
        };
        this.loadedDocs = null;
      }
      if (!docs) throw new Error('No data loaded. Choose files or click "Load sample data".');
      this.persistDocs(docs);

      const response = await firstValueFrom(this.api.evaluate(docs));
      this.result.set(response);
      this.setStatus('Check complete.', 'success');
    } catch (err) {
      this.setStatus(this.errorMessage(err), 'error');
    }
  }

  downloadQuestionnaireResponse(): void {
    const r = this.result();
    if (!r) return;
    const blob = new Blob([JSON.stringify(r.questionnaireResponse, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'questionnaire-response.json';
    a.click();
    URL.revokeObjectURL(url);
  }

  private async trySmartLaunch(): Promise<void> {
    if (typeof FHIR === 'undefined') return;
    let client;
    try {
      client = await FHIR.oauth2.ready();
    } catch {
      return; // Not arriving from a SMART launch redirect -- normal direct page visit.
    }

    this.setStatus('Connected via SMART — fetching patient chart...', 'loading');
    try {
      const patient = await client.patient.read();
      const fetches = await Promise.allSettled([
        client.patient.request('Condition', { flat: true }),
        client.patient.request('Observation', { flat: true }),
        client.patient.request('DocumentReference', { flat: true }),
      ]);
      const entries: unknown[] = [{ resource: patient }];
      for (const settled of fetches) {
        if (settled.status === 'fulfilled') {
          for (const resource of settled.value as unknown[]) entries.push({ resource });
        }
      }
      const bundle = { resourceType: 'Bundle', type: 'collection', entry: entries };

      const sample = await firstValueFrom(this.api.getSample());
      this.loadedDocs = { ...sample, patient: bundle };
      this.persistDocs(this.loadedDocs);
      this.selectedFiles.set({});
      this.setStatus(`Connected via SMART — loaded patient ${patient.id}. Click "Run check".`, 'success');
    } catch (err) {
      this.setStatus('Connected via SMART, but failed to fetch chart data: ' + this.errorMessage(err), 'error');
    }
  }

  private errorMessage(err: unknown): string {
    if (err instanceof Error) return err.message;
    if (typeof err === 'object' && err && 'error' in err) {
      const inner = (err as { error?: unknown }).error;
      if (typeof inner === 'object' && inner && 'error' in inner) return String((inner as { error: unknown }).error);
    }
    return String(err);
  }
}
