using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Eclipse
{
    /// <summary>日食「处刑与破绽」的攻击家族</summary>
    internal enum EclFamily : byte
    {
        None,
        /// <summary>地面处刑冲锋：锁向后直线重冲，判定=接触</summary>
        Rush,
        /// <summary>空中掠斩俯冲：锁线后穿越式俯冲，判定=接触</summary>
        Dive,
        /// <summary>蓄力重载荷：锁定后掷出/射出单发重弹</summary>
        Payload,
    }

    /// <summary>载荷家族的具体载荷风味</summary>
    internal enum EclPayloadKind : byte
    {
        None,
        /// <summary>DrManFly：抛物毒瓶，落点标记即预告</summary>
        Flask,
        /// <summary>Eyezor：直线重眼弹</summary>
        Orb,
        /// <summary>Nailhead：定向重钉束</summary>
        Nails,
    }

    /// <summary>单类型的处刑重击参数（预兆实体与发起端读同一张表，预告承诺单一来源）</summary>
    internal readonly struct EclProfile(EclFamily family, int telegraph, int lockFrames, int strike,
        float power, float laneLength, float rangeMin, float rangeMax, int cooldown,
        Color tint, EclPayloadKind payload = EclPayloadKind.None)
    {
        public readonly EclFamily Family = family;
        /// <summary>预告帧（契约小怪 ≥30）</summary>
        public readonly int Telegraph = telegraph;
        /// <summary>预告末段的锁定帧数（锁定起=承诺）</summary>
        public readonly int LockFrames = lockFrames;
        /// <summary>重击执行窗帧数（挥空判据的采样窗）</summary>
        public readonly int Strike = strike;
        /// <summary>冲刺/俯冲速度或载荷初速（未除提速补偿的名义值）</summary>
        public readonly float Power = power;
        /// <summary>预兆警示带长度（画得比理论行程宽裕，把原版 AI 残余转向包进警示）</summary>
        public readonly float LaneLength = laneLength;
        public readonly float RangeMin = rangeMin;
        public readonly float RangeMax = rangeMax;
        /// <summary>基础冷却帧（档位再乘 1 / 0.85 / 0.7）</summary>
        public readonly int Cooldown = cooldown;
        /// <summary>该类型的警示配色</summary>
        public readonly Color Tint = tint;
        public readonly EclPayloadKind Payload = payload;
    }

    /// <summary>
    /// 日食组类型表与共享判定助手。
    /// 吸血鬼双形态（Vampire/VampireBat）经 Transform 互变时 whoAmI 不变而 GlobalNPC 实例重建，
    /// 所有跨形态状态（破绽/血狩印）都由弹幕实体携带，锚校验按"形态对"放行
    /// </summary>
    internal static class EclEclipseSets
    {
        //——家族配色（预兆/破绽绘制统一读取）——
        internal static readonly Color RushTint = new Color(236, 62, 48);
        internal static readonly Color DiveTint = new Color(255, 168, 66);
        internal static readonly Color VenomTint = new Color(150, 222, 70);
        internal static readonly Color OcularTint = new Color(255, 96, 128);
        internal static readonly Color SpikeTint = new Color(224, 150, 92);
        /// <summary>破绽态的可读金色（格斗游戏惯例：金=可反打）</summary>
        internal static readonly Color OpeningGold = new Color(255, 208, 96);

        /// <summary>
        /// 类型 → 重击参数。预告帧全部 ≥30（公平契约下限），锁定帧为预告末段。
        /// LaneLength 刻意大于 Power×Strike 的理论行程（约 ×1.2-1.4），
        /// 把原版 AI 在执行窗内的残余转向与提速层的取整误差包进警示带
        /// </summary>
        internal static readonly Dictionary<int, EclProfile> Profiles = new() {
            //——处刑冲锋家族（地面直线重冲；M6 签名分支逻辑在 EclipseNPC，每型一句签名注释在此登记）——
            //签名：重击执行窗命中挂血狩印（双形态共享，见 OnHitPlayer）
            [NPCID.Vampire] = new(EclFamily.Rush, 34, 12, 22, 9.5f, 280f, 90f, 340f, 320, new Color(212, 40, 66)),
            //签名：突进落点滞留 8 帧电火花判定（EclFrankSparkProj）
            [NPCID.Frankenstein] = new(EclFamily.Rush, 38, 14, 24, 8.5f, 270f, 90f, 360f, 360, RushTint),
            //签名：两段蹒跚小跳接扑（LaneLength 加长覆盖跳程行进）
            [NPCID.SwampThing] = new(EclFamily.Rush, 40, 14, 26, 8.0f, 450f, 90f, 380f, 380, new Color(120, 190, 70)),
            [NPCID.Fritz] = new(EclFamily.Rush, 30, 10, 18, 10.5f, 250f, 80f, 300f, 300, RushTint),
            [NPCID.Psycho] = new(EclFamily.Rush, 30, 10, 16, 12.0f, 255f, 100f, 380f, 340, new Color(220, 226, 236)),
            [NPCID.Butcher] = new(EclFamily.Rush, 42, 16, 30, 12.5f, 450f, 120f, 460f, 400, new Color(255, 120, 40)),
            //签名：收势不急停，改力竭长滑行+水花（LaneLength 加长覆盖滑程）
            [NPCID.CreatureFromTheDeep] = new(EclFamily.Rush, 34, 12, 22, 9.0f, 380f, 90f, 340f, 330, new Color(90, 200, 190)),
            //——掠空斩家族（空中穿越俯冲）——
            [NPCID.VampireBat] = new(EclFamily.Dive, 34, 12, 24, 11.0f, 370f, 140f, 360f, 320, new Color(212, 40, 66)),
            [NPCID.Reaper] = new(EclFamily.Dive, 38, 14, 28, 10.5f, 420f, 160f, 400f, 380, new Color(178, 120, 255)),
            [NPCID.DeadlySphere] = new(EclFamily.Dive, 34, 12, 26, 12.0f, 440f, 150f, 380f, 350, DiveTint),
            //——蓄力重载荷家族——
            [NPCID.DrManFly] = new(EclFamily.Payload, 36, 36, 150, 0f, 0f, 160f, 520f, 420, VenomTint, EclPayloadKind.Flask),
            [NPCID.Eyezor] = new(EclFamily.Payload, 36, 14, 150, 0f, 140f, 140f, 560f, 400, OcularTint, EclPayloadKind.Orb),
            [NPCID.Nailhead] = new(EclFamily.Payload, 36, 14, 120, 0f, 140f, 120f, 480f, 390, SpikeTint, EclPayloadKind.Nails),
        };

        /// <summary>破绽持续帧（档位 1/2/3，越高档窗口越短；均落在 60-90 契约带内）</summary>
        internal static readonly int[] OpeningFramesByTier = [78, 70, 62];
        /// <summary>Mothron 破绽持续帧（重招更长）</summary>
        internal static readonly int[] MothronOpeningFramesByTier = [90, 82, 74];
        /// <summary>破绽期承伤加深倍率（契约带 20%-30%）</summary>
        internal const float OpeningDamageAmp = 1.25f;

        internal static EclFamily FamilyOf(int type)
            => Profiles.TryGetValue(type, out EclProfile profile) ? profile.Family : EclFamily.None;

        /// <summary>吸血鬼形态对：两形态共享一切经实体携带的状态</summary>
        internal static bool IsVampireForm(int type) => type == NPCID.Vampire || type == NPCID.VampireBat;

        /// <summary>
        /// 锚身份校验：槽位不是身份，index 回读必须配型别；
        /// 吸血鬼按形态对放行（Transform 保持 whoAmI，仅 type 翻转）
        /// </summary>
        internal static bool TypeMatches(int recordedType, int currentType)
            => recordedType == currentType
            || (IsVampireForm(recordedType) && IsVampireForm(currentType));

        /// <summary>
        /// 模式提速补偿系数：GameModeNPC.PostAI 只对非 Boss 且非体节个体追加 velocity×SpeedBonus 的位移，
        /// 注入速度按此除回（位移项除、重力项不除）。Mothron 的 boss 旗标离线未查证，
        /// 运行时直读旗标决定是否补偿（旗标无关设计），与提速层的 RageEligible 口径逐字一致
        /// </summary>
        internal static float MoveGain(NPC npc, int boundTier) {
            if (npc.boss || npc.realLife >= 0) {
                return 1f;
            }
            return 1f + GameModeTuning.SpeedBonus(boundTier);
        }
    }
}
