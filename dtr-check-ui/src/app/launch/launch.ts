import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

declare const FHIR: any;

@Component({
  selector: 'app-launch',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './launch.html',
  styleUrl: './launch.css',
})
export class Launch {
  iss = signal('https://launch.smarthealthit.org/v/r4/fhir');
  clientId = signal('dtr-check-demo');
  scope = signal('patient/*.read launch/patient');
  status = signal('');

  constructor() {
    const params = new URLSearchParams(window.location.search);
    if (params.get('iss')) this.iss.set(params.get('iss')!);
    if (params.get('client_id')) this.clientId.set(params.get('client_id')!);
    if (params.get('scope')) this.scope.set(params.get('scope')!);
  }

  launch(): void {
    if (typeof FHIR === 'undefined') {
      this.status.set('fhirclient failed to load (public/fhir-client.js missing?).');
      return;
    }
    this.status.set('Redirecting to authorization...');
    FHIR.oauth2
      .authorize({
        clientId: this.clientId(),
        scope: this.scope(),
        iss: this.iss(),
        redirectUri: window.location.origin + '/',
      })
      .catch((err: Error) => {
        this.status.set('Launch failed: ' + err.message);
      });
  }
}
