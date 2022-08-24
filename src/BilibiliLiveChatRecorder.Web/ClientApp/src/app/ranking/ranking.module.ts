import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RankingRoutingModule } from './ranking-routing.module';
import { RankingComponent } from './ranking.component';
import { NzListModule } from 'ng-zorro-antd/list';
import { NzStatisticModule } from 'ng-zorro-antd/statistic';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { FormsModule } from '@angular/forms';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzPopoverModule } from 'ng-zorro-antd/popover';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzIconModule } from 'ng-zorro-antd/icon';

@NgModule({
  declarations: [RankingComponent],
  imports: [
    CommonModule,
    FormsModule,
    RankingRoutingModule,
    NzListModule,
    NzStatisticModule,
    NzGridModule,
    NzSpinModule,
    NzRadioModule,
    NzDividerModule,
    NzButtonModule,
    NzDropDownModule,
    NzDatePickerModule,
    NzPopoverModule,
    NzSwitchModule,
    NzSelectModule,
    NzIconModule,
    NzCheckboxModule
  ]
})
export class RankingModule { }
