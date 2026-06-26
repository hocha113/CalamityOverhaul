namespace CalamityOverhaul.Content.UIs.MainMenuOverUIs
{
    /// <summary>
    /// 致谢名单中的贡献类别，决定分节标题与强调色
    /// </summary>
    internal enum CreditRole
    {
        Artist,
        CodeAssistance,
        Musician,
        BalanceTester,
        Donor,
    }

    /// <summary>
    /// 一个致谢分节：一种角色 + 该角色下的所有名字
    /// </summary>
    internal readonly record struct CreditSection(CreditRole Role, string[] Names);

    /// <summary>
    /// ED 致谢名单的静态内容。只构建一次，替代旧实现里每帧重建的 <c>names[]</c>。
    /// 名字为专有名词不做本地化；分节标题走 <see cref="AcknowledgmentUI"/> 的本地化字段
    /// </summary>
    internal static class AckCredits
    {
        /// <summary>
        /// 捐赠者人数超过该值时，渲染层改用多列网格排布，避免冗长的单列滚动
        /// </summary>
        public const int MultiColumnThreshold = 12;

        /// <summary>
        /// 按出场顺序排列的致谢分节
        /// </summary>
        public static readonly CreditSection[] Sections =
        [
            new(CreditRole.Artist, ["雾梯"]),
            new(CreditRole.CodeAssistance, ["Cyrilly", "瓶中微光", "Monomon"]),
            new(CreditRole.Musician, ["Ryusa"]),
            new(CreditRole.BalanceTester,
                ["像樱花一样飘散吧", "洛千希", "闪耀£星辰", "蒹葭", "悬剑", "CataStrophe"]),
            new(CreditRole.Donor,
            [
                "啊,胖子", "Reficul", "星星之火", "摸鱼的龙虾", "众星环绕", "L1ng", "respect",
                "鱼过海洋", "猫猫爱睡觉觉", "阿巴巴巴", "亻尔女子", "YFeawa", "一铭_N8S", "一只giao",
                "maybe", "浮云落日", "生物音素", "快乐肥宅橘九", "半生浮云半生闲", "阿萨德沃荣托",
                "冰冷小龙", "心酱", "LEI雷克斯", "尼古丁真", "龙辰", "圣盗杰布微明", "柳冠希",
                "失联不在线", "[CENSORED]", "无尘", "阿巴巴巴",
                "天空之城", "Svetlana", "Murainm", "Sergei", "森林之心", "流浪者", "黑夜之光",
                "秋叶", "青空", "月光下的影子", "冰镇紫苏", "Montana", "八背龙", "FengD", "逐风者",
                "Ivan", "Olga", "Alexander", "Natalia", "Dmitry", "悠然见南山", "星河影",
                "ShadowHunter", "MysticWarrior", "StormBringer", "无形剑", "Сырныйбарон336",
                "IceQueen", "Yelena", "Viktor", "白日梦想家", "追梦少年", "PhoenixRising",
                "DragonSlayer", "Vladislav", "Anastasia", "行者无疆", "蓝色星辰", "BlazeKnight",
                "ThunderGod", "StarLord", "天涯海角", "梦幻旅人", "风中的歌", "花间一壶酒",
                "凌云壮志", "Maxim", "Nikolai", "Tatiana", "寂静春天Ogger1943", "无尽之海", "Yuri",
                "Sasha", "苍穹之翼", "淮海不是明月", "剑心", "Ekaterina", "Mikhail", "Igor",
                "Lyudmila", "Artem", "Katerina", "Oleg", "Fwoer'Vmoerd", "苍穹彼岸offest",
                "Кот Пельмень", "Sodayo 的 Live", "我能看看你的小学吗", "烂柯棋缘", "华屋丘墟",
                "易燃易爆品daze", "梦境使者爱梅斯",
            ]),
        ];
    }
}
