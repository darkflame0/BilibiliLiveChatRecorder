import { Injectable, Inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { RankingList } from './ranking-list';
import { map } from 'rxjs/operators';

interface rankingQueryParams {
  sortby?: string;
  groupby?: string;
  organization?: string;
  datatype?: string;
  distinct?: boolean;
}


@Injectable({
  providedIn: 'root'
})
export class RankingService {

  constructor(
    private http: HttpClient,
    @Inject('BASE_URL') private baseUrl: string
  ) { }
  private dailyPath = 'ranking/day';
  private monthlyPath = 'ranking/month';
  getRange() {
    return this.http.get<{ min: string, max: string }>(`${this.baseUrl}ranking/range`).pipe(map(range => {
      return { min: new Date(range.min), max: new Date(range.max) };
    }));
  }
  getRanking(year: number, month: number, day?: number, query?: rankingQueryParams) {
    const path = `ranking/${year}/${month}${day ? '/' + day : ''}`;
    return this.getFromApi(path, query);
  }
  getDailyRanking(query: rankingQueryParams) {
    return this.getFromApi(this.dailyPath, query);
  }
  getMonthlyRanking(query: rankingQueryParams) {
    return this.getFromApi(this.monthlyPath, query);
  }
  private getFromApi(path: string, query: rankingQueryParams) {
    const params = { ...query };
    if (params.organization) {
      path = 'orgs/' + query.organization + '/' + path;
      delete params.groupby;
    }
    if (!params.datatype) {
      delete params.datatype;
    }
    if (params.datatype != 'livehistory' || !params.distinct) {
      delete params.distinct;
    }
    delete params.organization;
    for (const key in params) {
      if (Object.prototype.hasOwnProperty.call(query, key)) {
        if(!params[key])
        {
          delete params[key];
        }
        
      }
    }
    return this.http.get<RankingList>(`${this.baseUrl}${path}`, { params: params as any });
  }
}
