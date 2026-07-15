import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { CourseGroupService } from '@core/services/course-group.service';
import { CourseGroup } from '@core/models/course-group.model';
import { RowAuditBadge } from '@shared/row-audit-badge/row-audit-badge';

@Component({
  selector: 'app-course-group-detail',
  imports: [ButtonModule, CardModule, ToastModule, RowAuditBadge],
  providers: [MessageService],
  templateUrl: './course-group-detail.html',
  styleUrl: './course-group-detail.scss',
})
export class CourseGroupDetail implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(CourseGroupService);
  private readonly messages = inject(MessageService);

  protected readonly group = signal<CourseGroup | null>(null);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.service.getById(id).subscribe({
      next: (group) => {
        this.group.set(group);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messages.add({ severity: 'error', summary: '載入失敗', detail: '找不到課程群組' });
      },
    });
  }

  protected back(): void {
    this.router.navigate(['/course-groups']);
  }

  protected edit(): void {
    const group = this.group();
    if (group) {
      this.router.navigate(['/course-groups', group.pkid, 'edit']);
    }
  }
}
