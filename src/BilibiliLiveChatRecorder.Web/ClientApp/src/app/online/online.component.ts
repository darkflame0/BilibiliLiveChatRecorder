import { Component, OnInit, Inject, OnDestroy, ElementRef, ViewChild } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { IRoomInfo } from './shared/Room.type';
import { retry, catchError, switchMap, filter, map } from 'rxjs/operators';
import { ActivatedRoute, Router } from '@angular/router';
import { timer, Observable, Subscription, Subject } from 'rxjs';
import { RoomDataService } from './room-data.service';
import { IPageInfo } from 'ngx-virtual-scroller';
import { HostListener } from '@angular/core';

@Component({
  selector: 'app-online',
  templateUrl: './online.component.html',
  styleUrls: ['./online.component.less']
})
export class OnlineComponent implements OnInit, OnDestroy {

  public get search(): string {
    return this._search;
  }

  public set search(v: string) {
    this._search = v;
    this.FilterBySearch();
  }
  constructor(
    private http: HttpClient,
    @Inject('BASE_URL') private baseUrl: string,
    private route: ActivatedRoute,
    private roomDataService: RoomDataService) { }
  rooms: IRoomInfo[] = [];
  data: IRoomInfo[] = [];
  loading = false;
  _search = "";
  lastFetch = 0;
  showKeyframe = false;

  selectedRooms: { roomId: number, roomInfo: IRoomInfo, index: number }[] = [];
  modalVisible: { [index: number]: boolean } = {};
  noCover = 'https://s1.hdslb.com/bfs/static/blive/live-assets/common/images/no-cover.png';
  timerSub: Subscription = new Subscription();
  area$: Observable<string>;
  area = '';
  loadRooms$: Subject<void> = new Subject<void>();
  @ViewChild('container') container: ElementRef;
  scrollHeight: number;

  ngOnInit() {
    this.area$ = this.route.queryParamMap.pipe(map(a => a.get('area')));
    this.loadRooms$.subscribe(() => {
      let time = new Date().getTime();
      if (!document.hidden && (time - this.lastFetch > 20000)) {
        this.lastFetch = time;
        this.loadRooms(this.area);
      }
    });
    this.area$.subscribe(a => {
      this.area = a;
      this.timerSub.unsubscribe();
      this.timerSub = timer(0, 1000).subscribe(() => {
        this.loadRooms$.next();
      });
    });
  }
  @HostListener('document:visibilitychange', ['$event'])
  visibilitychange() {
    if (!document.hidden) {
      this.loadRooms$.next();
    }

  }
  ngOnDestroy() {
    this.timerSub.unsubscribe();
  }
  FilterBySearch() {
    this.rooms = this.data.filter(a => [a.shortId.toString(), a.roomId.toString(), a.uid.toString(), a.uname.toLocaleLowerCase()].includes(this.search.trim()));
    this.rooms.push(...this.data.filter(a => `${a.uname}&${a.title}&${a.parentArea}&${a.area}&${a.roomId}&${a.uid}${a.shortId != 0 ? `&${a.shortId}` : ''}`.toLocaleLowerCase().includes(this.search.trim().toLocaleLowerCase()) && !this.rooms.includes(a)));
  }
  vsChange(e: IPageInfo) {
    this.scrollHeight = e.maxScrollPosition && e.scrollEndPosition - e.scrollStartPosition + e.maxScrollPosition || this.container.nativeElement.scrollHeight;
  }
  fectching = false;
  loadRooms = (area: string) => {
    if (this.fectching) {
      return;
    }
    if (this.rooms.length == 0) {
      this.loading = true;
    }
    this.fectching = true;
    this.http.get<IRoomList>(this.baseUrl + 'online' + (area ? `?area=${area}` : '')).subscribe(result => {
      this.fectching = false;
      this.loading = false;
      this.data = result.list;
      if (this.selectedRooms.length != 0) {
        this.data.forEach(a => {
          const room = this.selectedRooms.find(b => b.roomInfo.roomId == a.roomId);
          if (room) {
            room.roomInfo = a;
          }
        });
      }
      this.FilterBySearch();

    }, () => { this.fectching = false; this.loading = false; });
  }
  getUserCover(room: IRoomInfo, hover: boolean = false) {
    let hoverCover = !this.showKeyframe ? this.getKeyframe(room) : room.userCover || this.getKeyframe(room);
    let cover = this.showKeyframe ? this.getKeyframe(room) : room.userCover || this.getKeyframe(room);
    return hover ? hoverCover : cover;
  }
  getKeyframe(room: IRoomInfo) {
    if (room.keyframe) {
      return room.keyframe + '@1e_1c_100q.webp';
    }
    if (room.userCover) {
      return room.userCover;
    }
    return this.noCover;
  }
  getCover(room: IRoomInfo) {
    return room.cover ? room.cover : this.getUserCover(room);
  }
  trackByRooms(index: number, room: { roomId: number }): number { return room.roomId; }
  showModal(room: IRoomInfo) {
    this.modalVisible[room.roomId] = true;
    if (!this.selectedRooms.find(a => a.roomInfo.roomId == room.roomId)) {
      this.selectedRooms.push({ roomId: room.roomId, roomInfo: room, index: this.selectedRooms.length + 1 });
    }
  }
  onModalMouseDown(room: { roomId: number, index: number }) {
    this.selectedRooms.forEach(a => {
      if (a.index > room.index) {
        --a.index;
      }
    });
    room.index = this.selectedRooms.length;
  }
  getRoomData$(roomId: number) {
    return this.roomDataService.get(roomId) || this.roomDataService.create(roomId, () => this.modalVisible[roomId] = false);
  }
  onRoomShow(room: { roomId: number, index: number }) {
    this.onModalMouseDown(room);
    this.roomDataService.start(room.roomId);
  }
  onRoomHidden(roomId: number) {
    this.roomDataService.stop(roomId);
  }
  onRoomDestroy(roomId: number) {
    this.roomDataService.destroy(roomId);
  }
}
interface IRoomList {
  count: number;
  list: IRoomInfo[];
}
