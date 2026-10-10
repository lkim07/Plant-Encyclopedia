import { Component } from '@angular/core';
import { Shell } from './layout/shell/shell';

@Component({
  imports: [Shell],
  selector: 'app-root',
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {}
