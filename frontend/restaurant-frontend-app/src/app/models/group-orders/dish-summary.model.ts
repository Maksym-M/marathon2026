import { DishShort } from './participant.model';

export interface DishSummaryShare {
  participantId: string;
  name: string;
  quantity: number;
}

export interface DishSummaryLine {
  dish: DishShort;
  quantity: number;
  total: number;
  shares: DishSummaryShare[];
}

export interface DishSummary {
  lines: DishSummaryLine[];
  /** Σ quantity of all lines — "7 dishes". */
  quantity: number;
  total: number;
}
