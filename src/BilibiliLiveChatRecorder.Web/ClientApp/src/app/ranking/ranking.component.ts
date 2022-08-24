import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, ParamMap, Router } from '@angular/router';
import { RankingList } from './ranking-list';
import { switchMap, debounceTime, catchError, map, distinctUntilChanged, shareReplay, filter } from 'rxjs/operators';
import { RankingService } from './ranking.service';
import { EMPTY, Observable, merge, of, range } from 'rxjs';
import { differenceInCalendarDays, differenceInCalendarMonths, addDays, getDate, getYear, addSeconds } from 'date-fns';
import { HttpErrorResponse } from '@angular/common/http';
import { SummaryService } from './summary.service';
import { Summary } from './summary';
import { Organization } from '../shared/orgs/Organization';
import { OrgService } from '../shared/orgs/org.service';

interface RankingQueryParams {
  sortby?: string;
  groupby?: string;
  organization?: string;
  datatype?: string;
  distinct?: boolean;
}

@Component({
  selector: 'app-ranking',
  templateUrl: './ranking.component.html',
  styleUrls: ['./ranking.component.less']
})
export class RankingComponent implements OnInit {

  constructor(private route: ActivatedRoute, private router: Router, private rs: RankingService, private ss: SummaryService, private os: OrgService) { }
  public get distinct(): boolean {
    return this.queryParams.distinct;
  }
  public set distinct(value: boolean) {
    if (this.dataType == 'livehistory') {
      this.queryParams.distinct = value;
      this.queryParams.groupby = null;
    }
    else {
      this.queryParams.distinct = null;
    }
  }
  public get sortBy(): string {
    return this.queryParams.sortby??'participant';
  }
  public set sortBy(value: string) {
    if (value == 'livetime' && (this.dataType == 'livehistory' || this.dataType == 'dailytop')) {
      this.queryParams.sortby = null;
    }
    else {
      this.queryParams.sortby = value;
    }
  }
  public get groupBy(): string {
    return this.queryParams.groupby ??'individual';
  }
  public set groupBy(value: string) {
    this.queryParams.groupby = value;
    if (value == 'organization') {
      this.queryParams.organization = null;
      this.queryParams.datatype = null;
      this.queryParams.distinct = null;
    }
  }

  public get dataType(): string {
    return this.queryParams.datatype??'total';
  }
  public set dataType(value: string) {
    if (value == 'livehistory') {
      this.queryParams.groupby = null;
      if (this.queryParams.distinct == null) {
        this.queryParams.distinct = !this.isDailyRanking;
      }
    }
    else {
      this.queryParams.distinct = null;
    }
    if (this.isDailyRanking && value == 'dailytop') {
      this.queryParams.datatype = 'total';
    }
    else {
      this.queryParams.datatype = value;
    }
  }
  public get organization() {
    return this.queryParams.organization;
  }
  public set organization(value) {
    this.queryParams.organization = value
  }
  private _detail = false;
  public get detail() {
    return this._detail;
  }
  public set detail(value) {
    this._detail = value;
    this.router.navigate(['.'], { replaceUrl: true, queryParamsHandling: 'merge', queryParams: { detail: this.detail }, relativeTo: this.route });
  }
  data?: RankingList;
  summary?: Summary;
  loading = true;
  organizations: Organization[] = [];
  organizationsOptions: Organization[] = [];
  summaryLoading = true;
  isDailyRanking = false;
  dateRange: { min: Date, max: Date };
  date: Date;
  dataDate: Date;
  queryParams: RankingQueryParams = {};
  rankingQuerystringNavigate() {
    this.router.navigate(['.'], { queryParamsHandling: 'merge', queryParams: this.queryParams, relativeTo: this.route, replaceUrl: this.route.snapshot.queryParamMap.keys.length == 0 });
  }
  genurl(url: string) {
    return `url(${url})`;
  }
  onSearch(value: string) {
    if (value && value.length > 1) {
      this.organizationsOptions = this.organizations.filter(item => item.label.toLowerCase().indexOf(value) > -1);
    } else {
      this.organizationsOptions = this.organizations;
    }
  }
  disabledDate = (date: Date): boolean => {
    return !this.dateRange || differenceInCalendarDays(date, this.dateRange.max) > 0 || differenceInCalendarDays(this.dateRange.min, date) > 0;
  }
  disabledMonth = (date: Date) => {
    return !this.dateRange || differenceInCalendarMonths(date, addDays(this.dateRange.max, -1)) > 0
      || differenceInCalendarMonths(this.dateRange.min, date) > 0;
  }
  onDateChange(date: Date) {
    if (differenceInCalendarDays(this.dataDate, date) != 0) {
      this.router.navigate(['/ranking', 'day', getYear(date), date.getMonth() + 1, date.getDate()], { queryParamsHandling: 'merge' });
    }
  }
  onMonthChange(date: Date) {
    if (differenceInCalendarMonths(this.dataDate, date) != 0) {
      this.router.navigate(['/ranking', 'month', getYear(date), date.getMonth() + 1], { queryParamsHandling: 'merge' });
    }
  }
  ngOnInit() {
    const summary$ = this.route.paramMap.pipe(switchMap(_ => {
      this.summaryLoading = true;
      const route = this.route.snapshot;
      let summary$: Observable<Summary>;
      if (route.routeConfig.path == 'day') {
        this.isDailyRanking = true;
        summary$ = this.ss.getDailySummary();
      }
      else if (route.routeConfig.path == 'month') {
        this.isDailyRanking = false;
        summary$ = this.ss.getMonthlySummary();
      }
      else {
        const year = Number(route.paramMap.get('year'));
        const month = Number(route.paramMap.get('month'));
        const day = Number(route.paramMap.get('day'));
        if (day) {
          this.isDailyRanking = true;
        }
        else {
          this.isDailyRanking = false;
        }
        summary$ = this.ss.getSummary(year, month, day).pipe(
          catchError((err: HttpErrorResponse) => {
            if (err.status == 404) {
              this.router.navigate([`/ranking/${day ? 'day' : 'month'}`], { queryParamsHandling: 'merge' });
            }
            else {
              console.log(err);
            }
            return EMPTY;
          }));
      }
      return summary$;
    }
    ), shareReplay());
    summary$.subscribe(_ => { }).unsubscribe();
    this.rs.getRange().toPromise()
      .then(range => {
        this.dateRange = range;
      }).finally(() => {
        summary$.subscribe(data => {
          this.summaryLoading = false;
          this.dataDate = addSeconds(Date.parse(data.updateTime), -1);
          this.date = this.dataDate;
          this.summary = data;
        });
      });

    this.os.getAll().subscribe(data => this.organizations = this.organizationsOptions = data);

    merge(this.route.queryParamMap.pipe(map(a => this.loadQuery(a)),
      distinctUntilChanged(null, a => JSON.stringify(a))
    ),
      this.route.paramMap.pipe(map(_ => this.queryParams))
    )
      .pipe(debounceTime(1), switchMap(_ => this.loadData()))
      .subscribe(
        (data: RankingList) => {
          this.data = data;
          this.loading = false;
        });

    this.detail = JSON.parse(this.route.snapshot.queryParamMap.get('detail'));
  }
  loadQuery(a: ParamMap) {
    const distinct = a.get('distinct');
    this.sortBy = a.get('sortby');
    this.groupBy = a.get('groupby') ;
    this.organization = a.get('organization');
    this.dataType = a.get('datatype');
    this.distinct = distinct != null ? Boolean(JSON.parse(distinct)) : null;
    return this.queryParams;
  }
  loadData() {
    {

      this.loading = true;
      const route = this.route.snapshot;
      let ranking$: Observable<RankingList>;
      if (route.routeConfig.path == 'day') {
        ranking$ = this.rs.getDailyRanking(this.queryParams);
      }
      else if (route.routeConfig.path == 'month') {
        ranking$ = this.rs.getMonthlyRanking(this.queryParams);
      }
      else {
        const year = Number(route.paramMap.get('year'));
        const month = Number(route.paramMap.get('month'));
        const day = Number(route.paramMap.get('day'));
        ranking$ = this.rs.getRanking(year, month, day, this.queryParams);
      }
      return ranking$.pipe(catchError(() => of(null)));
    }
  }
}
