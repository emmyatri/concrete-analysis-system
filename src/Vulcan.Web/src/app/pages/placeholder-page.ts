import { Component, input } from '@angular/core';

/**
 * Empty page used for each menu item until its real screen is built.
 * The heading comes from the route's data (see app.routes.ts).
 */
@Component({
  selector: 'app-placeholder-page',
  template: `<h1 class="page-heading">{{ heading() }}</h1>`,
  styles: `
    .page-heading {
      margin: 0;
      font-size: 1.5rem;
      font-weight: 600;
      color: var(--p-text-color);
    }
  `,
})
export class PlaceholderPage {
  readonly heading = input.required<string>();
}
