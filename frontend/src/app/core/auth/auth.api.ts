import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LoginResult } from '../models';

export type LogoutReason = 'manual' | 'pagehide';

@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/api/auth`;

  login(username: string, password: string): Observable<LoginResult> {
    return this.http.post<LoginResult>(`${this.baseUrl}/login`, { username, password });
  }

  logout(reason: LogoutReason): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/logout`, { reason });
  }

  /** The page came back after a reload: cancels the close that the page announced when it left. */
  resume(): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/resume`, null);
  }
}
