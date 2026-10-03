import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

/** Root component. Everything visible is in the shell (layout/shell) and its routed pages. */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.html',
})
export class App {}
