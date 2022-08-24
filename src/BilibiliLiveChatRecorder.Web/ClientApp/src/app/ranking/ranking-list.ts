import { RankingItem } from './ranking-item';

export interface RankingList {
    updateTime: Date;
    top: number;
    list: any;
}
