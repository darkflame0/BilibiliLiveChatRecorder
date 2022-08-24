import { Injectable, Inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Organization } from './Organization';

@Injectable({
  providedIn: 'root'
})
export class OrgService {

  constructor(
    private http: HttpClient,
    @Inject('BASE_URL') private baseUrl: string
  ) { }
  getAll() {
    return this.http.get<Organization[]>(`${this.baseUrl}orgs`);
  }
}

