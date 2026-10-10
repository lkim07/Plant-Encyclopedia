import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

/** Shown for unknown URLs, with a way back into the app. */
@Component({
  selector: 'app-not-found-page',
  imports: [RouterLink],
  template: `
    <h1>Page not found</h1>
    <a routerLink="/identify">Go to Identify</a>
  `,
})
export class NotFoundPage {}
