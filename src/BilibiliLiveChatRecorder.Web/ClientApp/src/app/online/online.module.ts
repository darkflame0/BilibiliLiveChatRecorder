import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';

import { OnlineRoutingModule } from './online-routing.module';
import { OnlineComponent } from './online.component';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzListModule } from 'ng-zorro-antd/list';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { StatisticModalModule } from '../shared/statistic-modal/statistic-modal.module';
import { DragDropModule } from '@angular/cdk/drag-drop';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzInputModule } from 'ng-zorro-antd/input';
import { VirtualScrollerModule } from 'ngx-virtual-scroller';
import { FormsModule } from '@angular/forms';
import { NzBackTopModule } from 'ng-zorro-antd/back-top';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { NzTypographyModule } from 'ng-zorro-antd/typography';
import { NzSwitchModule } from 'ng-zorro-antd/switch';

@NgModule({
  declarations: [OnlineComponent],
  imports: [
    CommonModule,
    FormsModule,
    NzIconModule,
    NzListModule,
    NzCardModule,
    NzSpinModule,
    DragDropModule,
    OnlineRoutingModule,
    StatisticModalModule,
    VirtualScrollerModule,
    NzGridModule,
    NzInputModule,
    NzBackTopModule,
    NzToolTipModule,
    NzTypographyModule,
    NzSwitchModule,
  ]
})
export class OnlineModule { }
