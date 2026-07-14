import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { PublishStatusService } from '@core/services/publish-status.service';
import { PublishStatus } from '@core/models/publish-status.model';

@Component({
  selector: 'app-publish-status-detail',
  imports: [ButtonModule, CardModule, TagModule, ToastModule],
  providers: [MessageService],
  templateUrl: './publish-status-detail.html',
  styleUrl: './publish-status-detail.scss',
})
export class PublishStatusDetail implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(PublishStatusService);
  private readonly messages = inject(MessageService);

  protected readonly status = signal<PublishStatus | null>(null);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.service.getById(id).subscribe({
      next: (status) => {
        this.status.set(status);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messages.add({ severity: 'error', summary: '載入失敗', detail: '找不到發布狀態' });
      },
    });
  }

  protected back(): void {
    this.router.navigate(['/publish-statuses']);
  }

  protected edit(): void {
    const status = this.status();
    if (status) {
      this.router.navigate(['/publish-statuses', status.pkid, 'edit']);
    }
  }
}
