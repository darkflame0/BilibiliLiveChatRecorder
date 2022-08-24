import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';

import { LiverRoutingModule } from './liver-routing.module';
import { LiverComponent } from './liver.component';

@NgModule({
  declarations: [LiverComponent],
  imports: [
    CommonModule,
    LiverRoutingModule
  ]
})
export class LiverModule { }
