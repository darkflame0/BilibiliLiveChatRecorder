import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';

const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: '/online' },
  { path: 'online', loadChildren: () => import('./online/online.module').then(m => m.OnlineModule) },
  { path: 'ranking', loadChildren: () => import('./ranking/ranking.module').then(m => m.RankingModule) },
  { path: 'liver', loadChildren: () => import('./liver/liver.module').then(m => m.LiverModule) }
];

@NgModule({
  declarations: [
  ],
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
