namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDreams
{
    /// <summary>
    /// 鬼梦模块的时序常量。演出推进在 <see cref="KikasaDreamDirector"/>，
    /// 状态权威在 <see cref="KikasaDomains.KikasaDomainPlayer"/>——鬼梦是血湖领域之下的更深一层：
    /// 倒影醒来（恶犬替换湖镜里的人影）→ 拉入（湖沸腾、世界绕水线倒转进梦侧）→
    /// 梦中封物品、左键唤犬 → 再按拉入键归返
    /// </summary>
    public static class KikasaDream
    {
        //拉入节拍（60fps）：凶兆沸腾 0-96 → 窥犬驻留 96-166 → 倒转 166-276（含反向蓄势）→ 落定 276-330
        //比鬼雨异化（216f）更长：这不是换一件衣服，是整个世界被拽进湖底

        public const int PullBoilEnd = 96;

        public const int PullDwellEnd = 166;

        public const int PullRollEnd = 276;

        public const int PullTotalFrames = 330;

        /// <summary>拉入结算帧：倒转段时间过半，血红硬闪掩护下切到梦侧</summary>
        public const int PullCommitFrame = 221;

        //归返节拍：湖水自屏底涌回 0-70 → 短沸驻留 70-100 → 倒转 100-210 → 落定 210-260

        public const int ReturnSurgeEnd = 70;

        public const int ReturnDwellEnd = 100;

        public const int ReturnRollEnd = 210;

        public const int ReturnTotalFrames = 260;

        /// <summary>归返结算帧：暖白闪掩护下切回血湖侧</summary>
        public const int ReturnCommitFrame = 155;
    }
}
