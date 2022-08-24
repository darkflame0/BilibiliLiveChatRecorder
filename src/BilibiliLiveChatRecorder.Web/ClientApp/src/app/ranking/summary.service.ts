import { Injectable, Inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { retry, catchError } from 'rxjs/operators';
import { RankingItemData } from './ranking-item';

@Injectable({
  providedIn: 'root'
})
export class SummaryService {

  constructor(
    private http: HttpClient,
    @Inject('BASE_URL') private baseUrl: string,
  ) { }
  private dailyPath = 'summary/day';
  private monthlyPath = 'summary/month'
  getSummary(year: number, month: number, day?: number) {
    let path = `summary/${year}/${month}${day ? '/' + day : ''}`;
    return this.getFromApi(path);
  }
  getDailySummary() {
    return this.getFromApi(this.dailyPath);
  }
  getMonthlySummary() {
    return this.getFromApi(this.monthlyPath);
  }
  private getFromApi(path: string) {
    return this.http.get<{ updateTime: string, data: RankingItemData }>(`${this.baseUrl}${path}`)
      .pipe(retry(1));
  }
}
