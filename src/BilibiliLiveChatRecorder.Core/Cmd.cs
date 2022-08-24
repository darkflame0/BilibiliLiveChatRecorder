namespace Darkflame.BilibiliLiveChatRecorder
{
    public static class Cmd
    {
        public const string Popularity = "POPULARITY";
        public const string Danmaku = "DANMU_MSG";
        public const string Welcome = "WELCOME";
        public const string WelcomeGuard = "WELCOME_GUARD";
        public const string SendGift = "SEND_GIFT";
        public const string GuardBuy = "GUARD_BUY";
        public const string LiveStart = "LIVE";
        public const string LiveEnd = "PREPARING";
        public const string SystemMessage = "SYS_MSG";
        public const string NoticeMessage = "NOTICE_MSG";
        public const string GuardMessage = "GUARD_MSG";
        public const string ComboSend = "COMBO_SEND";
        public const string ComboEnd = "COMBO_END";
        public const string CutOff = "CUT_OFF";
        public const string EntryEffect = "ENTRY_EFFECT";
        public const string RaffleStart = "RAFFLE_START";
        public const string GuardLotteryStart = "GUARD_LOTTERY_START";
        public const string RaffleEnd = "RAFFLE_END";
        public const string RoomRealTimeMessageUpdate = "ROOM_REAL_TIME_MESSAGE_UPDATE";
        public const string RoomInfo = "ROOM_INFO";
        public const string ROOM_CHANGE = "ROOM_CHANGE";
        public const string ActivityBannerRedNoticeClose = "ACTIVITY_BANNER_RED_NOTICE_CLOSE";
        public const string RoomRank = "ROOM_RANK";
        public const string SpecialGift = "SPECIAL_GIFT";
        public const string SysGift = "SYS_GIFT";
        public const string PK_PROCESS = "PK_PROCESS";
        public const string PK_MIC_END = "PK_MIC_END";
        public const string PK_PRE = "PK_PRE";
        public const string PK_START = "PK_START";
        public const string PK_MATCH = "PK_MATCH";
        public const string PK_SETTLE = "PK_SETTLE";
        public const string PK_END = "PK_END";
        public const string PK_AGAIN = "PK_AGAIN";
        public const string PK_CLICK_AGAIN = "PK_CLICK_AGAIN";
        public const string SUPER_CHAT_MESSAGE = "SUPER_CHAT_MESSAGE";
        public const string USER_TOAST_MSG = "USER_TOAST_MSG";
        public const string WEEK_STAR_CLOCK = "WEEK_STAR_CLOCK";
        public const string INTERACT_WORD = "INTERACT_WORD";
        public static readonly string[] GiftCmd = { SendGift, USER_TOAST_MSG, SUPER_CHAT_MESSAGE };
    }
    public enum InteractType
    {
        /// <summary>
        /// 进入
        /// </summary>
        Enter = 1,

        /// <summary>
        /// 关注
        /// </summary>
        Follow = 2,

        /// <summary>
        /// 分享直播间
        /// </summary>
        Share = 3,

        /// <summary>
        /// 特别关注
        /// </summary>
        SpecialFollow = 4,

        /// <summary>
        /// 互相关注
        /// </summary>
        MutualFollow = 5,

    }
}
