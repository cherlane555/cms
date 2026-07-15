import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env';
import { AppUserLookup } from '@core/models/app-role.model';
import { PublishStatusLookup } from '@core/models/publish-status.model';
import { PartnerLookup } from '@core/models/partner.model';
import { CourseGroupLookup } from '@core/models/course-group.model';
import {
  PromotionLookup,
  TrainingCenterLookup,
} from '@core/models/featured-promo-item.model';
import { CertificationLookup, JobCategoryLookup } from '@core/models/course.model';

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

  getPartners(): Observable<PartnerLookup[]> {
    return this.http.get<PartnerLookup[]>(`${this.baseUrl}/partners`);
  }

  getCourseGroups(): Observable<CourseGroupLookup[]> {
    return this.http.get<CourseGroupLookup[]>(`${this.baseUrl}/course-groups`);
  }

  getCertifications(): Observable<CertificationLookup[]> {
    return this.http.get<CertificationLookup[]>(`${this.baseUrl}/certifications`);
  }

  getJobCategories(): Observable<JobCategoryLookup[]> {
    return this.http.get<JobCategoryLookup[]>(`${this.baseUrl}/job-categories`);
  }

  getTrainingCenters(): Observable<TrainingCenterLookup[]> {
    return this.http.get<TrainingCenterLookup[]>(`${this.baseUrl}/training-centers`);
  }

  getPromotionByCode(code: string): Observable<PromotionLookup> {
    return this.http.get<PromotionLookup>(
      `${this.baseUrl}/promotions/by-code/${encodeURIComponent(code)}`,
    );
  }
}
