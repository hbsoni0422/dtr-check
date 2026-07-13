import { Routes } from '@angular/router';
import { Dashboard } from './dashboard/dashboard';
import { Launch } from './launch/launch';

export const routes: Routes = [
  { path: '', component: Dashboard },
  { path: 'launch', component: Launch },
];
