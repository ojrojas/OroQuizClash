import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface RewardView {
  id: string;
  name: string;
  description: string;
  pointsRequired: number;
  stock: number;
  status: string;
  expirationDate: string | null;
  available: boolean;
}

export interface GetRewardsResponse {
  rewards: RewardView[];
  availablePoints: number | null;
  gameId: string | null;
}

export interface RedemptionItem {
  id: string;
  rewardId: string;
  gameId: string;
  points: number;
  status: string;
  requestedAt: string;
  deliveredAt: string | null;
}

export interface GetRedemptionsResponse {
  redemptions: RedemptionItem[];
}

export interface RedeemResponse {
  redemptionId: string;
  rewardId: string;
  gameId: string;
  points: number;
  status: string;
  requestedAt: string;
}

@Injectable({ providedIn: 'root' })
export class RewardsApi {
  private http = inject(HttpClient);
  private base = environment.apiUrl;

  getRewards(gameId?: string, includeUnavailable = false): Observable<GetRewardsResponse> {
    let params = new HttpParams();
    if (gameId) params = params.set('gameId', gameId);
    if (includeUnavailable) params = params.set('includeUnavailable', 'true');
    return this.http.get<GetRewardsResponse>(`${this.base}/rewards`, { params });
  }

  getWallet(gameId?: string): Observable<GetRewardsResponse> {
    return this.getRewards(gameId);
  }

  getMyRedemptions(page = 1, pageSize = 20): Observable<GetRedemptionsResponse> {
    const params = new HttpParams().set('page', String(page)).set('pageSize', String(pageSize));
    return this.http.get<GetRedemptionsResponse>(`${this.base}/redemptions`, { params });
  }

  redeem(rewardId: string, idempotencyKey: string, gameId: string): Observable<RedeemResponse> {
    const headers = new HttpHeaders({ 'X-Idempotency-Key': idempotencyKey });
    const body: any = { gameId, idempotencyKey };
    return this.http.post<RedeemResponse>(`${this.base}/rewards/${rewardId}/redeem`, body, { headers });
  }
}
