import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env';
import { RowAuditEntry } from '@core/models/row-audit.model';

@Injectable({ providedIn: 'root' })
export class RowAuditService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/rowaudit`;

  /** One record's audit trail (newest first). */
  getForRecord(tableName: string, pkid: number | string): Observable<RowAuditEntry[]> {
    const params = new HttpParams().set('tableName', tableName).set('pkid', String(pkid));
    return this.http.get<RowAuditEntry[]>(this.baseUrl, { params });
  }
}
