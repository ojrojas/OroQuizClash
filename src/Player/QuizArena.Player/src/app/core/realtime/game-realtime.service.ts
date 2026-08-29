import { Injectable, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../../environments/environment';

export type GameEvent =
  | { type: 'RoundStarted'; payload: any }
  | { type: 'QuestionAvailable'; payload: any }
  | { type: 'ScoreUpdated'; payload: any }
  | { type: 'RoundCompleted'; payload: any }
  | { type: 'PlayerWithdrawn'; payload: any }
  | { type: 'GameFinished'; payload: any }
  | { type: 'Reconnected' };

@Injectable({ providedIn: 'root' })
export class GameRealtimeService {
  private conn: HubConnection | null = null;
  private gameId = signal<string | null>(null);

  events$ = new Subject<GameEvent>();

  async connect(gameId: string, accessTokenFactory: () => string) {
    if (this.conn?.state === HubConnectionState.Connected && this.gameId() === gameId) return;
    await this.disconnect();
    this.gameId.set(gameId);
    this.conn = new HubConnectionBuilder()
      .withUrl(`${environment.gameHubUrl}?gameId=${gameId}`, { accessTokenFactory })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Information)
      .build();

    this.conn.on('RoundStarted', p => this.events$.next({ type: 'RoundStarted', payload: p }));
    this.conn.on('QuestionAvailable', p => this.events$.next({ type: 'QuestionAvailable', payload: p }));
    this.conn.on('ScoreUpdated', p => this.events$.next({ type: 'ScoreUpdated', payload: p }));
    this.conn.on('RoundCompleted', p => this.events$.next({ type: 'RoundCompleted', payload: p }));
    this.conn.on('PlayerWithdrawn', p => this.events$.next({ type: 'PlayerWithdrawn', payload: p }));
    this.conn.on('GameFinished', p => this.events$.next({ type: 'GameFinished', payload: p }));
    this.conn.onreconnected(() => this.events$.next({ type: 'Reconnected' }));

    await this.conn.start();
    await this.conn.invoke('JoinGameGroup', gameId);
  }

  async disconnect() {
    if (this.conn) {
      try { await this.conn.stop(); } catch {}
      this.conn = null;
    }
  }
}
