import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { StatisticModalComponent } from './statistic-modal.component';
import { NzStatisticModule } from 'ng-zorro-antd/statistic';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { DragDropModule } from '@angular/cdk/drag-drop';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { ScrollStrategyOptions } from '@angular/cdk/overlay';
import { NonBlockScrollStrategyOptions } from 'src/app/shared/statistic-modal/non-block-scroll-strategy-options';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';

@NgModule({
  declarations: [StatisticModalComponent],
  providers: [
    {
      provide: ScrollStrategyOptions,
      useClass: NonBlockScrollStrategyOptions
    }
  ],
  imports: [
    CommonModule,
    NzIconModule,
    NzModalModule,
    NzGridModule,
    NzSpinModule,
    NzStatisticModule,
    NzButtonModule,
    DragDropModule,
    NzToolTipModule
  ],
  exports: [StatisticModalComponent]
})
export class StatisticModalModule { }
