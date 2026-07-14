import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env';
import { AppUserLookup } from '@core/models/app-role.model';
import { PublishStatusLookup } from '@core/models/publish-status.model';

@Injectable({ providedIn: 'root' })
export class LookupService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/lookups`;

  getAppUsers(): Observable<AppUserLookup[]> {
    return this.http.get<AppUserLookup[]>(`${this.baseUrl}/app-users`);
  }

  getPublishStatuses(): Observable<PublishStatusLookup[]> {
    return this.http.get<PublishStatusLookup[]>(`${this.baseUrl}/publish-statuses`);
  }
}
