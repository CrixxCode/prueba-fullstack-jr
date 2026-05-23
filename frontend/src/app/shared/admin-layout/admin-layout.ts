import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, HostListener, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-admin-layout',
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './admin-layout.html',
  styleUrl: './admin-layout.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AdminLayout {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private isDesktopViewport = false;
  sidebarOpen = false;

  constructor() {
    this.updateSidebarForViewport();
  }

  get user() {
    return this.authService.getCurrentUser();
  }

  logout(): void {
    this.authService.logout();
  }

  goToCreateUser(): void {
    this.closeSidebarOnMobile();
    this.router.navigate(['/users/new']);
  }

  toggleSidebar(): void {
    this.sidebarOpen = !this.sidebarOpen;
  }

  closeSidebar(): void {
    this.sidebarOpen = false;
  }

  closeSidebarOnMobile(): void {
    if (window.innerWidth < 1024) {
      this.sidebarOpen = false;
    }
  }

  @HostListener('window:resize')
  onResize(): void {
    this.updateSidebarForViewport();
  }

  private updateSidebarForViewport(): void {
    const nextIsDesktop = window.innerWidth >= 1024;

    if (this.isDesktopViewport !== nextIsDesktop) {
      this.sidebarOpen = nextIsDesktop;
    }

    this.isDesktopViewport = nextIsDesktop;
  }
}
