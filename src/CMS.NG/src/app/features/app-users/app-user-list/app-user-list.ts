import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { LookupService } from '@core/services/lookup.service';
import { AppUserLookup } from '@core/models/app-role.model';

@Component({
  selector: 'app-app-user-list',
  imports: [RouterLink, ButtonModule, TableModule],
  templateUrl: './app-user-list.html',
})
export class AppUserList implements OnInit {
  private readonly lookup = inject(LookupService);

  protected readonly users = signal<AppUserLookup[]>([]);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    this.lookup.getAppUsers().subscribe({
      next: (users) => {
        this.users.set(users);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
