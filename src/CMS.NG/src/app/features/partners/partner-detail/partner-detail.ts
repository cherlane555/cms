import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { PartnerService } from '@core/services/partner.service';
import { Partner } from '@core/models/partner.model';
import { RowAuditBadge } from '@shared/row-audit-badge/row-audit-badge';

@Component({
  selector: 'app-partner-detail',
  imports: [ButtonModule, CardModule, ToastModule, RowAuditBadge],
  providers: [MessageService],
  templateUrl: './partner-detail.html',
  styleUrl: './partner-detail.scss',
})
export class PartnerDetail implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(PartnerService);
  private readonly messages = inject(MessageService);

  protected readonly partner = signal<Partner | null>(null);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.service.getById(id).subscribe({
      next: (partner) => {
        this.partner.set(partner);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messages.add({ severity: 'error', summary: '載入失敗', detail: '找不到廠商' });
      },
    });
  }

  protected back(): void {
    this.router.navigate(['/partners']);
  }

  protected edit(): void {
    const partner = this.partner();
    if (partner) {
      this.router.navigate(['/partners', partner.pkid, 'edit']);
    }
  }
}
