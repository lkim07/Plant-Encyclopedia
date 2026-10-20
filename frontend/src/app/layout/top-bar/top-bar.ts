import { Component, output } from '@angular/core';
import { Icon } from '../../shared/icons/icon';

/** Mobile top application bar: menu, title, account (UX §4). */
@Component({
  selector: 'app-top-bar',
  imports: [Icon],
  templateUrl: './top-bar.html',
  styleUrl: './top-bar.css',
})
export class TopBar {
  readonly menuOpen = output<void>();
}
