import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { Course } from '@core/models/course.model';
import { CourseQr } from '../course-qr/course-qr';
import { RowAuditBadge } from '@shared/row-audit-badge/row-audit-badge';

@Component({
  selector: 'app-course-detail',
  imports: [ButtonModule, CardModule, TagModule, ToastModule, CourseQr, RowAuditBadge],
  providers: [MessageService],
  templateUrl: './course-detail.html',
  styleUrl: './course-detail.scss',
})
export class CourseDetail implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(CourseService);
  private readonly lookups = inject(LookupService);
  private readonly messages = inject(MessageService);

  protected readonly course = signal<Course | null>(null);
  protected readonly certificationLabels = signal<string[]>([]);
  protected readonly jobCategoryLabels = signal<string[]>([]);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    forkJoin({
      course: this.service.getById(id),
      certifications: this.lookups.getCertifications(),
      jobCategories: this.lookups.getJobCategories(),
    }).subscribe({
      next: ({ course, certifications, jobCategories }) => {
        this.course.set(course);
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
}
