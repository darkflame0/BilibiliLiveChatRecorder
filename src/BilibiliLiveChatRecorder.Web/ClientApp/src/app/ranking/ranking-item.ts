export interface RankingItem {
  liver: LiverInfo;
  data: RankingItemData
}
export interface LiverInfo {
    uid: number;
    roomId: number;
    name: string;
    face: string;
}

export interface RankingItemData {
  hourOfLive: number;
  realDanmaku: number;
  goldCoin: number;
  maxPopularity: number;
  goldUser: number;
  goldUserGreaterThen9:number;
  realDanmakuUser: number;
  silverUser: number;
  participants: number;
}
