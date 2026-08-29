import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Answer, GameSession, PlayerGameState } from './models/player.models';

@Injectable({ providedIn: 'root' })
export class GamesApi {
  private http = inject(HttpClient);
  private base = environment.apiUrl;

  joinGame(gameId: string, idempotencyKey?: string): Observable<GameSession> {
    const headers = idempotencyKey ? new HttpHeaders({ 'X-Idempotency-Key': idempotencyKey }) : undefined;
    return this.http.post<GameSession>(`${this.base}/games/${gameId}/players`, { idempotencyKey }, { headers });
  }

  getMyState(gameId: string): Observable<PlayerGameState> {
    return this.http.get<PlayerGameState>(`${this.base}/games/${gameId}/players/me`);
  }

  getGame(gameId: string): Observable<any> {
    return this.http.get(`${this.base}/games/${gameId}`);
  }

  submitAnswer(gameId: string, dto: { roundId: string; questionId: string; selectedOptionId: string; idempotencyKey: string }): Observable<Answer> {
    const headers = new HttpHeaders({ 'X-Idempotency-Key': dto.idempotencyKey });
    return this.http.post<Answer>(`${this.base}/games/${gameId}/answers`, dto, { headers });
  }

  withdraw(gameId: string, idempotencyKey?: string): Observable<GameSession> {
    const headers = idempotencyKey ? new HttpHeaders({ 'X-Idempotency-Key': idempotencyKey }) : undefined;
    return this.http.post<GameSession>(`${this.base}/games/${gameId}/withdraw`, { idempotencyKey }, { headers });
  }

  getLeaderboard(gameId: string): Observable<any> {
    return this.http.get(`${this.base}/games/${gameId}/leaderboard`);
  }
}
