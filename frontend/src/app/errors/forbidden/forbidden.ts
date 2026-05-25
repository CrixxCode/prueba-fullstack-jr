import { CommonModule, Location } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

@Component({
  selector: 'app-forbidden',
  imports: [CommonModule, RouterLink],
  templateUrl: './forbidden.html',
  styleUrl: './forbidden.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class Forbidden {
  private readonly location = inject(Location);
  private readonly router = inject(Router);

  goBack(): void {
    if (window.history.length <= 1) {
      void this.router.navigate(['/profile']);
      return;
    }

    const currentUrl = this.router.url;
    this.location.back();

    setTimeout(() => {
      if (this.router.url === currentUrl) {
        void this.router.navigate(['/profile']);
      }
    }, 150);
  }
}
