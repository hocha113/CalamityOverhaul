using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaThralls;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants
{
    /// <summary>灵异亲和：每条鬼奴记忆归一系，驻湖后由影位点数折算灵异增益</summary>
    public enum KikasaAffinity : byte
    {
        /// <summary>无亲和（械奴记忆：占影位、不点灵异）</summary>
        None,
        /// <summary>焰：驻湖养旺鬼火（灼烧更久、火舌更高）</summary>
        Flame,
        /// <summary>魇：驻湖壮大梦犬（上限更高、撕咬更狠）</summary>
        Nightmare,
        /// <summary>潦：驻湖养伞奴</summary>
        Rain,
        /// <summary>百搭：顶任意一系（拜月教徒的三珠杂耍）</summary>
        Wild,
    }

    /// <summary>
    /// 沉影盘门面：读 <see cref="KikasaServantPlayer"/> 的三影位，
    /// 折算灵异增益（鬼火养旺/梦犬壮大/伞奴增养）与组合边（梦火/沸雨/雨魇/三影镇湖）。
    /// 鬼火点燃/收火是玩家号令（KikasaWisp.TryToggle）、倒影随满水自醒，
    /// 亲和不是开门的硬条件，只做增强。
    /// 槽位数据只活在所有者本机（储钱罐语义），远端可见的后果各走既有同步通道：
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

        /// <summary>三影镇湖：满盘三席且三系齐坐，鬼奴出力找回一截</summary>
        internal static bool HasTriSeal(Player player)
            => FilledSlotCount(player) >= KikasaServantPlayer.SlotCount
            && FlameCount(player) >= 1 && NightmareCount(player) >= 1 && RainCount(player) >= 1;

        //==================== 鬼奴出力 ====================

        /// <summary>
        /// 多驻同场的单只出力衰减：按实际出战席数算（转盘收起的席不摊薄出力），
        /// 1 只全额、2 只 0.80、3 只 0.66（合计约 1.0/1.6/2.0 倍）；
        /// 三影镇湖找回 15%（镇湖看席位不看出场，收着的影也在湖里坐镇）。
        /// 由 <see cref="KikasaServantBalanceGlobal"/> 在命中端统一乘
        /// </summary>
        internal static float ServantDamageScale(Player player) {
            int active = player.GetModPlayer<KikasaServantPlayer>().ActiveSlotCount;
            float scale = active <= 1 ? 1f : active == 2 ? 0.80f : 0.66f;
            if (HasTriSeal(player)) {
                scale *= 1.15f;
            }
            return scale;
        }

        /// <summary>械奴编队每多一份复制体，编队合计出力增加的份额（单份 = 1）</summary>
        private const float PackStackStep = 0.4f;

        /// <summary>
        /// 械奴编队内的单只出力摊薄：湖藏沉了几把就出几把，但合计只按 1 + 0.4×(n−1) 涨
        /// （1/2/3/4/5 把合计 1.0/1.4/1.8/2.2/2.6，单只 1.0/0.70/0.60/0.55/0.52）。
        /// 这是械奴相对改前的全部削弱：单把不变，4 把 −45%、5 把 −48%（用户拍板 2026/9/5，"减 45% 左右"）；
        /// 5 把迷你鲨 = 5 倍是反馈里的超模根因。由 <see cref="KikasaServantBalanceGlobal"/> 在命中端统一乘，
        /// 演出（枪数/节奏）不动
        /// </summary>
        internal static float PackDamageScale(int units) {
            if (units <= 1) {
                return 1f;
            }
            return (1f + PackStackStep * (units - 1)) / units;
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

        //==================== 梦犬（魇增益，首枚即生效） ====================

        /// <summary>梦中唤犬上限：无魇基线 4 只，每枚魇影 +2（一枚回到旧基线 6）</summary>
        internal static int HoundCap(Player player)
            => 4 + 2 * NightmareCount(player);

        /// <summary>梦犬撕咬倍率：无魇基线 78%，每枚魇影 +22%（一枚回到全额）</summary>
        internal static float HoundDamageScale(Player player)
            => 0.78f + 0.22f * NightmareCount(player);

        //==================== 鬼火（焰增益，首枚即生效） ====================

        /// <summary>灼烧 debuff 时长：无焰基线 50 帧，每枚焰影 +45（一枚回到旧基线 95）</summary>
        internal static int WispBurnDuration(Player player)
            => 50 + 45 * FlameCount(player);

        /// <summary>火舌向水线上方的触及高度：无焰只灼水线近旁（基数 -28px），每枚焰影 +28px</summary>
        internal static float WispFlameReach(Player player)
            => KikasaWisps.KikasaWisp.FlameReach + 28f * (FlameCount(player) - 1);

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
