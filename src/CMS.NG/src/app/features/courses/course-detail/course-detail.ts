import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { PublishStatusService } from '@core/services/publish-status.service';
import { Course } from '@core/models/course.model';
import { PublishStatus } from '@core/models/publish-status.model';
import { CourseQr } from '../course-qr/course-qr';
import { RowAuditBadge } from '@shared/row-audit-badge/row-audit-badge';

@Component({
  selector: 'app-course-detail',
  imports: [
    ButtonModule,
    CardModule,
    TagModule,
    ToastModule,
    TooltipModule,
    CourseQr,
    RowAuditBadge,
  ],
  providers: [MessageService],
  templateUrl: './course-detail.html',
  styleUrl: './course-detail.scss',
})
export class CourseDetail implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(CourseService);
  private readonly lookups = inject(LookupService);
  private readonly publishStatuses = inject(PublishStatusService);
  private readonly messages = inject(MessageService);

  protected readonly course = signal<Course | null>(null);
  protected readonly certificationLabels = signal<string[]>([]);
  protected readonly jobCategoryLabels = signal<string[]>([]);
  protected readonly statuses = signal<PublishStatus[]>([]);
  protected readonly loading = signal(true);

  /**
   * 簡章 is offered only for a published course. A brochure for a 已下架 course promises a
   * student a course we no longer sell, and its QR points at a public page that correctly 404s
   * (verified: 87.6% of 上架中 courses resolve; 0.3% of 已下架 do).
   *
   * Asks the status for its `isPublished` flag rather than testing `pkid === 2`. PublishStatus
   * pkids are user-assigned and non-IDENTITY, and 發布狀態 is a live admin screen with no
   * Validate on the controller: an admin can repoint pkid 2, delete it, or add a pkid 4 that is
   * published. Against a literal, this button then changes behaviour with nothing to connect it
   * back to the screen that changed it. Note this is a PRODUCT rule, not authorization — the
   * global RequireAuthenticatedUser() fallback is what guards the data.
   */
  protected readonly canExportBrochure = computed(
    () =>
      this.statuses().find((s) => s.pkid === this.course()?.publishStatusPkid)?.isPublished ??
      false,
  );

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    forkJoin({
      course: this.service.getById(id),
      certifications: this.lookups.getCertifications(),
      jobCategories: this.lookups.getJobCategories(),
      // NOT LookupService.getPublishStatuses() — that returns PublishStatusLookup, which is
      // {pkid, description} and carries no flags. This is the one that has isPublished.
      //
      // Fails safe: this lookup only gates the brochure button, so a failure here must not sink
      // the whole page (forkJoin errors if any source does). On error we fall back to [], which
      // makes canExportBrochure() return false — the button disables, the course still renders.
      statuses: this.publishStatuses.getAll().pipe(catchError(() => of([] as PublishStatus[]))),
    }).subscribe({
      next: ({ course, certifications, jobCategories, statuses }) => {
        this.course.set(course);
        this.statuses.set(statuses);
        const certMap = new Map(certifications.map((c) => [c.pkid, c.title]));
        this.certificationLabels.set(
          course.certificationPkids.map((id) => certMap.get(id) ?? String(id)),
        );
        const jobMap = new Map(jobCategories.map((j) => [j.pkid, j.description]));
        this.jobCategoryLabels.set(
          course.jobCategoryPkids.map((id) => jobMap.get(id) ?? String(id)),
        );
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messages.add({ severity: 'error', summary: '載入失敗', detail: '找不到課程' });
      },
    });
  }

  protected dateOf(iso: string): string {
    return iso.slice(0, 10);
  }

  protected back(): void {
    this.router.navigate(['/courses']);
  }

  protected edit(): void {
    const course = this.course();
    if (course) {
      this.router.navigate(['/courses', course.pkid, 'edit']);
    }
  }

  protected brochure(): void {
    const course = this.course();
    if (course) {
      this.router.navigate(['/courses', course.pkid, 'brochure']);
    }
  }
}
