import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { RankingComponent } from './ranking.component';


const routes: Routes = [
  { path: '', redirectTo: '/ranking/day', pathMatch: 'full' },
  { path: 'day', component: RankingComponent, data: { title: '日榜' } },
  { path: 'month', component: RankingComponent, data: { title: '月榜' } },
  { path: 'month/:year/:month', component: RankingComponent, data: { title: '月榜' } },
  { path: 'day/:year/:month/:day', component: RankingComponent, data: { title: '日榜' } }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class RankingRoutingModule { }
