import { Component, OnInit, Input, Output, EventEmitter, OnChanges, SimpleChanges, OnDestroy, ElementRef, ChangeDetectorRef, HostListener, ViewChild, AfterViewInit, AfterViewChecked } from '@angular/core';
import { ILiveStatistic } from 'src/app/shared/LiveStatistic';
import { fromEvent, Observable, Subscription } from 'rxjs';
import { NzModalComponent } from 'ng-zorro-antd/modal';

@Component({
  selector: 'app-statistic-modal',
  templateUrl: './statistic-modal.component.html',
  styleUrls: ['./statistic-modal.component.less']
})
export class StatisticModalComponent implements OnInit, OnChanges, OnDestroy, AfterViewChecked, AfterViewInit {
  constructor(private el: ElementRef, private d: ChangeDetectorRef) { }
  @Input()
  index: number;
  @Input()
  isVisible = false;
  @Output()
  isVisibleChange = new EventEmitter<boolean>();
  @Output()
  onHidden = new EventEmitter<void>();
  @Output()
  onShow = new EventEmitter<void>();
  @Output()
  onDestroy = new EventEmitter<void>();
  @Input()
  room: { roomId: number, uname: string, title?: string, participantDuring10Min?: number };
  @Input()
  data$: Observable<ILiveStatistic>;
  dataSub: Subscription;
  _hidden = false;
  @ViewChild(NzModalComponent, { static: false })
  modalElement: NzModalComponent;
  public get hidden() {
    return this._hidden;
  }

  public set hidden(v: boolean) {
    if (v) {
      this.onHidden.emit();
    }
    else {
      this.onShow.emit();
    }
    this._hidden = v;
  }

  get roomId() {
    return this.room.roomId;
  }
  get title() {
    return this.data && this.data.title || this.room.title;
  }
  get uname() {
    return this.room.uname;
  }
  get participantDuring10Min() {
    return this.room.participantDuring10Min;
  }
  ngAfterViewInit() {
  }
  eventListened = false;
  ngAfterViewChecked() {
    if (!this.eventListened) {
      Object.keys(window).forEach(key => {
        if (/^on/.test(key)) {
          (this.modalElement.getElement() as HTMLElement).addEventListener(key.slice(2), event => {
            if (!event.cancelBubble && event instanceof MouseEvent) {
              let e = new MouseEvent(event.type, event);
              this.el.nativeElement.dispatchEvent(e);
            }
          });
        }
      });
      this.eventListened = true;
    }

  }
  ngOnChanges(changes: SimpleChanges): void {
    if (changes.isVisible) {
      this.hidden = !changes.isVisible.currentValue;
      this.eventListened = !changes.isVisible.currentValue;
    }
  }
  data?: ILiveStatistic;


  ngOnInit() {
    this.dataSub = this.data$.subscribe(a => { this.data = a; this.d.detectChanges(); });
  }
  ngOnDestroy(): void {
    this.dataSub.unsubscribe();
    this.onDestroy.emit();
  }
  handleCancel() {
    this.isVisibleChange.emit(false);
  }
  timerId?: number;
  @HostListener('document:visibilitychange', ['$event'])
  visibilitychange() {
    if (this.isVisible) {
      if (document.hidden) {
        this.timerId = window.setTimeout(() => {
          this.hidden = true;
        }, 10000);
      } else {
        if (this.timerId != null) {
          window.clearTimeout(this.timerId)
          this.timerId = null;
        }
        if (this.hidden) {
          this.hidden = false;
        }
      }
    }
  }
}
