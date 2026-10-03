import { HttpClient, HttpEvent, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { CommerceRecord, ProcessResult, QuarantineRecord, UploadResult } from './models';

@Injectable({ providedIn: 'root' })
export class CommerceApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/api/commerce`;

  uploadFile(file: File): Observable<HttpEvent<UploadResult>> {
    const body = new FormData();
    body.append('file', file, file.name);

    return this.http.post<UploadResult>(`${this.baseUrl}/files`, body, {
      reportProgress: true,
      observe: 'events',
    });
  }

  processDate(processDate: string): Observable<ProcessResult> {
    return this.http.post<ProcessResult>(`${this.baseUrl}/process`, { processDate });
  }

  listCommerce(processDate?: string): Observable<CommerceRecord[]> {
    const params = processDate ? new HttpParams().set('processDate', processDate) : undefined;

    return this.http.get<CommerceRecord[]>(this.baseUrl, { params });
  }

  listQuarantine(processDate?: string): Observable<QuarantineRecord[]> {
    const params = processDate ? new HttpParams().set('processDate', processDate) : undefined;

    return this.http.get<QuarantineRecord[]>(`${this.baseUrl}/quarantine`, { params });
  }
}
