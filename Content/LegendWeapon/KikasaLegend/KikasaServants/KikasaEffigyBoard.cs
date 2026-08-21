using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaThralls;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants
{
    /// <summary>灵异亲和：每条鬼奴记忆归一系，驻湖后由沉影盘点数出灵异门控</summary>
    public enum KikasaAffinity : byte
    {
        /// <summary>无亲和（械奴记忆：占影位、不点灵异）</summary>
        None,
        /// <summary>焰：驻湖自燃鬼火</summary>
        Flame,
        /// <summary>魇：驻湖唤醒倒影、开鬼梦之门</summary>
        Nightmare,
        /// <summary>潦：驻湖养伞奴</summary>
        Rain,
        /// <summary>百搭：顶任意一系（拜月教徒的三珠杂耍）</summary>
        Wild,
    }

    /// <summary>
    /// 沉影盘门面：读 <see cref="KikasaServantPlayer"/> 的三影位，
    /// 折算灵异门控（鬼火自燃/倒影自醒/伞奴增养）与组合边（梦火/沸雨/雨魇/三影镇湖）。
    /// 槽位数据只活在所有者本机（储钱罐语义）——远端可见的后果各走既有同步通道：
    /// 鬼奴弹幕原版同步、鬼火与倒影走领域快照。非所有者端调用这些门面得到的是默认值，
    /// 消费端注意只在 owner 侧做裁决
    /// </summary>
    internal static class KikasaEffigyBoard
    {
        //==================== 亲和计数 ====================

        /// <summary>驻湖影位里某系的枚数；百搭顶任意一系</summary>
        internal static int CountAffinity(Player player, KikasaAffinity affinity) {
            KikasaServantPlayer servant = player.GetModPlayer<KikasaServantPlayer>();
            int count = 0;
            for (int i = 0; i < KikasaServantPlayer.SlotCount; i++) {
                KikasaAffinity slot = servant.SlotAffinity(i);
                if (slot == affinity || slot == KikasaAffinity.Wild) {
                    count++;
                }
            }
            return count;
        }

        internal static int FlameCount(Player player) => CountAffinity(player, KikasaAffinity.Flame);

        internal static int NightmareCount(Player player) => CountAffinity(player, KikasaAffinity.Nightmare);

        internal static int RainCount(Player player) => CountAffinity(player, KikasaAffinity.Rain);

        internal static int FilledSlotCount(Player player)
            => player.GetModPlayer<KikasaServantPlayer>().FilledSlotCount;

        //==================== 组合边 ====================

        /// <summary>梦火（焰×魇）：梦犬的牙带上鬼火灼烧</summary>
        internal static bool HasDreamFireEdge(Player player)
            => FlameCount(player) >= 1 && NightmareCount(player) >= 1;

        /// <summary>沸雨（焰×潦）：鬼雨压不灭鬼火，改作半强度蒸沸</summary>
        internal static bool HasBoilRainEdge(Player player)
            => FlameCount(player) >= 1 && RainCount(player) >= 1;

        /// <summary>雨魇（魇×潦）：伞奴转化间隔减半</summary>
        internal static bool HasRainNightmareEdge(Player player)
            => NightmareCount(player) >= 1 && RainCount(player) >= 1;

        /// <summary>三影镇湖：满盘三席且三系齐坐——鬼奴出力找回一截、湖力省着烧</summary>
        internal static bool HasTriSeal(Player player)
            => FilledSlotCount(player) >= KikasaServantPlayer.SlotCount
            && FlameCount(player) >= 1 && NightmareCount(player) >= 1 && RainCount(player) >= 1;

        //==================== 鬼奴出力 ====================

        /// <summary>
        /// 多驻同场的单只出力衰减：1 只全额、2 只 0.80、3 只 0.66（合计约 1.0/1.6/2.0 倍）；
        /// 三影镇湖找回 15%。由 <see cref="KikasaServantBalanceGlobal"/> 在命中端统一乘
        /// </summary>
        internal static float ServantDamageScale(Player player) {
            int filled = FilledSlotCount(player);
            float scale = filled <= 1 ? 1f : filled == 2 ? 0.80f : 0.66f;
            if (HasTriSeal(player)) {
                scale *= 1.15f;
            }
            return scale;
        }

        //==================== 伞奴（潦） ====================

        /// <summary>伞奴上限：基数 5，每枚潦影 +1，封顶 8</summary>
        internal static int ThrallCap(Player player)
            => Math.Min(KikasaThrall.MaxPerOwner + RainCount(player), 8);

        /// <summary>伞奴转化间隔：潦影愈多转化愈勤；雨魇边再减半，下限 10 帧</summary>
        internal static int ThrallConvertGap(Player player) {
            int gap = KikasaThrall.ConvertGapFrames - RainCount(player) * 5;
            if (HasRainNightmareEdge(player)) {
                gap /= 2;
            }
            return Math.Max(gap, 10);
        }

        //==================== 梦犬（魇） ====================

        /// <summary>梦中唤犬上限：魇 1 系 6 只，每多一枚 +2</summary>
        internal static int HoundCap(Player player)
            => 4 + 2 * Math.Max(NightmareCount(player), 1);

        /// <summary>梦犬撕咬倍率：首枚魇影全额，之后每枚 +22%</summary>
        internal static float HoundDamageScale(Player player)
            => 1f + 0.22f * Math.Max(NightmareCount(player) - 1, 0);

        //==================== 鬼火（焰） ====================

        /// <summary>灼烧 debuff 时长：基数 95 帧，每多一枚焰影 +45</summary>
        internal static int WispBurnDuration(Player player)
            => 95 + 45 * Math.Max(FlameCount(player) - 1, 0);

        /// <summary>火舌向水线上方的触及高度：每多一枚焰影 +28px</summary>
        internal static float WispFlameReach(Player player)
            => KikasaWisps.KikasaWisp.FlameReach + 28f * Math.Max(FlameCount(player) - 1, 0);

        //==================== 湖力（鬼火与鬼梦共饮的一汪水） ====================

        /// <summary>鬼火满燃排空湖力的帧数（约 25 秒）；三影镇湖省着烧（约 37 秒）</summary>
        internal static float VigorBurnPerFrame(Player player)
            => 1f / (HasTriSeal(player) ? 2250f : 1500f);

        /// <summary>火熄后湖力回满的帧数（约 12 秒）——鬼火自燃与再度入梦的天然冷却</summary>
        internal const float VigorRefillPerFrame = 1f / 720f;

        /// <summary>入梦门槛：湖力过半才拽得动；拉入受理帧把湖力整汪抽干</summary>
        internal const float DreamVigorNeed = 0.5f;

        //==================== 亲和身份色（三灵异既有的系统色） ====================

        /// <summary>亲和的身份色：焰=鬼火金、魇=烬红、潦=冷青、百搭=苍白</summary>
        internal static Color AffinityColor(KikasaAffinity affinity) => affinity switch {
            KikasaAffinity.Flame => KikasaWisps.KikasaWisp.GoldBody,
            KikasaAffinity.Nightmare => new Color(230, 96, 40),
            KikasaAffinity.Rain => new Color(108, 190, 198),
            KikasaAffinity.Wild => new Color(206, 196, 214),
            _ => new Color(150, 150, 150),
        };
    }
}
