import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { LiverComponent } from './liver.component';

const routes: Routes = [
  // { path: '', component: LiverComponent },
  { path: ':uid', component: LiverComponent }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class LiverRoutingModule { }
