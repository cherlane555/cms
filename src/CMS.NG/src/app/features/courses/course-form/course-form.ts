import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { forkJoin, of } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { CheckboxModule } from 'primeng/checkbox';
import { DatePickerModule } from 'primeng/datepicker';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import {
  CertificationLookup,
  CourseRequest,
  JobCategoryLookup,
} from '@core/models/course.model';
import { PartnerLookup } from '@core/models/partner.model';
import { CourseGroupLookup } from '@core/models/course-group.model';
import { PublishStatusLookup } from '@core/models/publish-status.model';
import { RowAuditBadge } from '@shared/row-audit-badge/row-audit-badge';

@Component({
  selector: 'app-course-form',
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    CardModule,
    CheckboxModule,
    DatePickerModule,
    InputNumberModule,
    InputTextModule,
    MultiSelectModule,
    SelectModule,
    TextareaModule,
    ToastModule,
    RowAuditBadge,
  ],
  providers: [MessageService],
  templateUrl: './course-form.html',
  styleUrl: './course-form.scss',
})
export class CourseForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(CourseService);
  private readonly lookups = inject(LookupService);
  private readonly messages = inject(MessageService);

  protected readonly isEdit = signal(false);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly partners = signal<PartnerLookup[]>([]);
  protected readonly courseGroups = signal<CourseGroupLookup[]>([]);
  protected readonly publishStatuses = signal<PublishStatusLookup[]>([]);
  protected readonly certifications = signal<CertificationLookup[]>([]);
  protected readonly jobCategories = signal<JobCategoryLookup[]>([]);

  // pkid is IDENTITY (DB-assigned) — not an editable form field; exposed for the audit badge.
  protected readonly pkid = signal(0);

  protected readonly form = this.fb.group({
    title: this.fb.nonNullable.control('', [Validators.required]),
    officialTitle: this.fb.control<string | null>(null),
    courseId: this.fb.nonNullable.control('', [Validators.required]),
    prodCourseId: this.fb.nonNullable.control('', [Validators.required]),
    friendlyUrl: this.fb.nonNullable.control('', [Validators.required]),
    displayOrder: this.fb.nonNullable.control(0, [Validators.required]),
    partnerPkid: this.fb.control<number | null>(null, [Validators.required]),
    courseGroupPkid: this.fb.control<number | null>(null),
    publishStatusPkid: this.fb.control<number | null>(null, [Validators.required]),
    scheduleOn: this.fb.control<Date | null>(null, [Validators.required]),
    scheduleOff: this.fb.control<Date | null>(null, [Validators.required]),
    hour: this.fb.nonNullable.control(0),
    listPrice: this.fb.nonNullable.control(0),
    learningCredit: this.fb.nonNullable.control(0),
    material: this.fb.control<string | null>(null),
    objective: this.fb.control<string | null>(null),
    target: this.fb.control<string | null>(null),
    prerequisites: this.fb.control<string | null>(null),
    outline: this.fb.control<string | null>(null),
    towardCertOrExam: this.fb.control<string | null>(null),
    note: this.fb.control<string | null>(null),
    otherInfo: this.fb.control<string | null>(null),
    canRepeat: this.fb.nonNullable.control(false),
    certificationPkids: this.fb.nonNullable.control<number[]>([]),
    jobCategoryPkids: this.fb.nonNullable.control<number[]>([]),
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    this.isEdit.set(!!id);

    forkJoin({
      partners: this.lookups.getPartners(),
      courseGroups: this.lookups.getCourseGroups(),
      publishStatuses: this.lookups.getPublishStatuses(),
      certifications: this.lookups.getCertifications(),
      jobCategories: this.lookups.getJobCategories(),
      course: id ? this.service.getById(Number(id)) : of(null),
    }).subscribe({
      next: ({ partners, courseGroups, publishStatuses, certifications, jobCategories, course }) => {
        this.partners.set(partners);
        this.courseGroups.set(courseGroups);
        this.publishStatuses.set(publishStatuses);
        this.certifications.set(certifications);
        this.jobCategories.set(jobCategories);
        if (course) {
          this.pkid.set(course.pkid);
          this.form.patchValue({
            ...course,
            scheduleOn: CourseForm.parseDate(course.scheduleOn),
            scheduleOff: CourseForm.parseDate(course.scheduleOff),
          });
        }
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法載入資料' });
      },
    });
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);

    const raw = this.form.getRawValue();
    const request: CourseRequest = {
      pkid: this.pkid(),
      title: raw.title,
      officialTitle: raw.officialTitle,
      courseId: raw.courseId,
      prodCourseId: raw.prodCourseId,
      friendlyUrl: raw.friendlyUrl,
      displayOrder: raw.displayOrder,
      partnerPkid: raw.partnerPkid!,
      courseGroupPkid: raw.courseGroupPkid,
      publishStatusPkid: raw.publishStatusPkid!,
      scheduleOn: CourseForm.toDateStr(raw.scheduleOn!),
      scheduleOff: CourseForm.toDateStr(raw.scheduleOff!),
      hour: raw.hour,
      listPrice: raw.listPrice,
      learningCredit: raw.learningCredit,
      material: raw.material,
      objective: raw.objective,
      target: raw.target,
      prerequisites: raw.prerequisites,
      outline: raw.outline,
      towardCertOrExam: raw.towardCertOrExam,
      note: raw.note,
      otherInfo: raw.otherInfo,
      canRepeat: raw.canRepeat,
      certificationPkids: raw.certificationPkids,
      jobCategoryPkids: raw.jobCategoryPkids,
    };

    const onOk = () => {
      this.messages.add({ severity: 'success', summary: '儲存成功', detail: request.title });
      this.router.navigate(['/courses']);
    };

    const onError = () => {
      this.saving.set(false);
      this.messages.add({ severity: 'error', summary: '儲存失敗', detail: '儲存失敗，請稍後再試' });
    };

    if (this.isEdit()) {
      this.service.update(request).subscribe({ next: () => onOk(), error: onError });
    } else {
      this.service.create(request).subscribe({ next: () => onOk(), error: onError });
    }
  }

  protected cancel(): void {
    this.router.navigate(['/courses']);
  }

  // ISO string <-> Date, in local time (avoid toISOString's UTC shift).
  static parseDate(iso: string): Date {
    const [y, m, d] = iso.slice(0, 10).split('-').map(Number);
    return new Date(y, m - 1, d);
  }

  static toDateStr(date: Date): string {
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
  }
}
