import { Injectable, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../../environments/environment';

export type GameEvent =
  | { type: 'GameStarted'; payload: any }
  | { type: 'PlayerJoined'; payload: any }
  | { type: 'RoundStarted'; payload: any }
  | { type: 'QuestionAvailable'; payload: any }
  | { type: 'QuestionPresented'; payload: any }
  | { type: 'ScoreUpdated'; payload: any }
  | { type: 'LeaderboardUpdated'; payload: any }
  | { type: 'PlayerAnswered'; payload: any }
  | { type: 'RoundCompleted'; payload: any }
  | { type: 'PlayerWithdrawn'; payload: any }
  | { type: 'PlayerStatusChanged'; payload: any }
  | { type: 'GameFinished'; payload: any }
  | { type: 'Reconnected' };

@Injectable({ providedIn: 'root' })
export class GameRealtimeService {
  private conn: HubConnection | null = null;
  private gameId = signal<string | null>(null);

  events$ = new Subject<GameEvent>();

  async connect(gameId: string, accessTokenFactory: () => string | Promise<string>) {
    if (this.conn?.state === HubConnectionState.Connected && this.gameId() === gameId) return;
    await this.disconnect();
    this.gameId.set(gameId);
    this.conn = new HubConnectionBuilder()
      .withUrl(`${environment.gameHubUrl}?gameId=${gameId}`, { accessTokenFactory })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Information)
      .build();

    // Server emits 9 events per specs/012 (all to group game-{id}): GameStarted, PlayerJoined, RoundStarted, QuestionPresented, PlayerAnswered, ScoreUpdated, LeaderboardUpdated, RoundCompleted, GameFinished
    // Client normalizes both QuestionPresented (server) and QuestionAvailable (alias) to QuestionAvailable for existing stores.
    this.conn.on('GameStarted', p => this.events$.next({ type: 'GameStarted', payload: p }));
    this.conn.on('PlayerJoined', p => this.events$.next({ type: 'PlayerJoined', payload: p }));
    this.conn.on('RoundStarted', p => this.events$.next({ type: 'RoundStarted', payload: p }));
    // Alias handling: server sends QuestionPresented, legacy client expects QuestionAvailable
    this.conn.on('QuestionPresented', p => {
      this.events$.next({ type: 'QuestionPresented', payload: p });
      this.events$.next({ type: 'QuestionAvailable', payload: p });
    });
    this.conn.on('QuestionAvailable', p => this.events$.next({ type: 'QuestionAvailable', payload: p }));
    this.conn.on('PlayerAnswered', p => this.events$.next({ type: 'PlayerAnswered', payload: p }));
    this.conn.on('ScoreUpdated', p => this.events$.next({ type: 'ScoreUpdated', payload: p }));
    this.conn.on('LeaderboardUpdated', p => this.events$.next({ type: 'LeaderboardUpdated', payload: p }));
    this.conn.on('RoundCompleted', p => this.events$.next({ type: 'RoundCompleted', payload: p }));
    this.conn.on('PlayerWithdrawn', p => this.events$.next({ type: 'PlayerWithdrawn', payload: p }));
    this.conn.on('PlayerStatusChanged', p => this.events$.next({ type: 'PlayerStatusChanged', payload: p }));
    this.conn.on('GameFinished', p => this.events$.next({ type: 'GameFinished', payload: p }));
    // Also handle lower-case normalized names as fallback (SignalR case-insensitive)
    this.conn.on('gamestarted', p => this.events$.next({ type: 'GameStarted', payload: p }));
    this.conn.on('questionpresented', p => {
      this.events$.next({ type: 'QuestionPresented', payload: p });
      this.events$.next({ type: 'QuestionAvailable', payload: p });
    });
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
