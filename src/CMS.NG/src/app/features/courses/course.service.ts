import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import { Course, CourseQuery, CourseRequest } from './course.model';

@Injectable({ providedIn: 'root' })
export class CourseService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/courses`;

  getAll(): Observable<Course[]> {
    return this.http.get<Course[]>(this.baseUrl);
  }

  query(query: CourseQuery): Observable<Course[]> {
    return this.http.post<Course[]>(`${this.baseUrl}/query`, query);
  }

  getByPkid(pkid: number): Observable<Course> {
    return this.http.get<Course>(`${this.baseUrl}/${pkid}`);
  }

  create(request: CourseRequest): Observable<Course> {
    return this.http.post<Course>(this.baseUrl, request);
  }

  /** pkid travels in the body — there is no route param on PUT. */
  update(request: CourseRequest): Observable<void> {
    return this.http.put<void>(this.baseUrl, request);
  }

  delete(pkid: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${pkid}`);
  }
}
