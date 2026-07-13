import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export type EvaluationStatus = 'answered' | 'gap' | 'not_applicable';

export interface EvaluationResult {
  linkId: string;
  text: string;
  status: EvaluationStatus;
  value?: string | null;
  evidence?: unknown[] | null;
  note?: string | null;
}

export interface SampleResponse {
  patient: unknown;
  questionnaire: { title?: string; id?: string; [key: string]: unknown };
  rules: unknown;
}

export interface EvaluateResponse {
  title: string;
  results: EvaluationResult[];
  gaps: EvaluationResult[];
  questionnaireResponse: unknown;
}

export interface EvaluateRequest {
  patient: unknown;
  questionnaire: unknown;
  rules: unknown;
}

@Injectable({ providedIn: 'root' })
export class DtrCheckApi {
  private readonly http = inject(HttpClient);

  getSample(): Observable<SampleResponse> {
    return this.http.get<SampleResponse>('/api/sample');
  }

  evaluate(request: EvaluateRequest): Observable<EvaluateResponse> {
    return this.http.post<EvaluateResponse>('/api/evaluate', request);
  }
}
