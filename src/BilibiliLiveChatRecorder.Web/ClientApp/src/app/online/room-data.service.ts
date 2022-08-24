import { Injectable, Inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ILiveStatistic } from '../shared/LiveStatistic';
import { Observable, Subject } from 'rxjs';
import * as signalR from '@microsoft/signalr';
import { HttpTransportType } from '@microsoft/signalr';

@Injectable({
  providedIn: 'root'
})
export class RoomDataService {

  constructor(@Inject('BASE_URL') private baseUrl: string) { }
  map = new Map<number, { connection: signalR.HubConnection, roomSub: Subject<ILiveStatistic>, onDestroy: () => void }>();
  get(roomId: number) {
    return this.map.get(roomId)?.roomSub.asObservable();
  }
  create(roomId: number, onDestroy = () => { }) {
    let builder = new signalR.HubConnectionBuilder()
      .withAutomaticReconnect();
    if (typeof (EventSource) !== "undefined" && this.baseUrl.split(':')[0] === 'https') {
      builder = builder.withUrl(this.baseUrl + 'roomHub?roomId=' + roomId, { transport: HttpTransportType.ServerSentEvents })
    }
    else {
      builder = builder.withUrl(this.baseUrl + 'roomHub?roomId=' + roomId)
    }
    let connection = builder.build();
    let roomSub = new Subject<ILiveStatistic>();
    connection.on('ReceiveRoomData', (roomId: number, room: ILiveStatistic) => {
      roomSub.next(room);
    });
    connection.on('Disconnect', () => {
      this.destroy(roomId);
      onDestroy();
    });
    this.map.set(roomId, { connection, roomSub, onDestroy });
    return roomSub.asObservable();
  }
  destroy(roomId: number) {
    let val = this.map.get(roomId);
    val.connection.stop();
    val.roomSub.complete();
    val.onDestroy();
    return this.map.delete(roomId);
  }
  start(roomId: number) {
    return this.map.get(roomId).connection.start();
  }
  stop(roomId: number) {
    return this.map.get(roomId).connection.stop();
  }
}
