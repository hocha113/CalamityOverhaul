using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDreams;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaWisps
{
    /// <summary>
    /// 血湖鬼火门面：点燃门控、包络推进、鬼雨压制节拍、灼烧区扫描。
    /// 状态存在 <see cref="KikasaDomainPlayer"/>（点燃态+原点入快照，包络各端本地自算）；
    /// 设定：鬼火被鬼雨压制——翻入鬼雨后火被迅速压灭且点燃态清除，鬼雨形态下点不着。
    /// </summary>
    internal static class KikasaWisp
    {
        /// <summary>点燃包络推满帧数</summary>
        public const int IgniteFrames = 26;

        /// <summary>退场包络归零帧数（收火/湖退/入梦）</summary>
        public const int FadeFrames = 44;

        /// <summary>燃沿扫满半宽 4000px 的帧数（前沿约 44px/帧）</summary>
        public const int SpreadFrames = 90;

        /// <summary>收火时燃沿反向啃回原点的帧数</summary>
        public const int RecedeFrames = 62;

        /// <summary>鬼雨压制拍帧数：世界落回视野后火被雨压灭的可见过程</summary>
        public const int QuenchFrames = 66;

        /// <summary>火舌向水线上方的灼烧触及高度（px）</summary>
        public const float FlameReach = 70f;

        /// <summary>水线下仍会被灼烧的深度（px），与沉溺抓取深度同源</summary>
        public const float SubmergeDepth = 600f;

        /// <summary>owner 端灼烧扫描间隔（帧）：AddBuff 走原版网络包，节流防包洪</summary>
        private const int ScanInterval = 30;

        /// <summary>灼烧 debuff 时长（帧）：约 1.6s，离火自然烧尽</summary>
        private const int BurnDuration = 95;

        //只有本机玩家自己的域会扫描，单实例计时即可
        private static int scanTimer;

        //==================== 配色（金是鬼火的身份色，鬼雨压制时向苍金失温） ====================

        /// <summary>白金焰芯</summary>
        public static readonly Color GoldCore = new(255, 236, 168);

        /// <summary>金焰体</summary>
        public static readonly Color GoldBody = new(255, 186, 66);

        /// <summary>琥珀舌尖</summary>
        public static readonly Color AmberTip = new(216, 108, 30);

        /// <summary>压制中失温的苍金</summary>
        public static readonly Color PaleDying = new(196, 172, 136);

        /// <summary>血系表现色随观看域微冷，但金色身份保留（只轻推不换板）</summary>
        public static Color Tint(Color gold) => KikasaDomain.CoolTint(gold, Color.Lerp(gold, new(188, 196, 186), 0.28f));

        //==================== 门控与命令 ====================

        /// <summary>
        /// 点燃/收火受理：收火任何相位都许（火是自己点的）；
        /// 点燃要求满水血湖稳态——鬼雨压着点不着、梦里没有那面湖，白按给轻拒
        /// </summary>
        internal static bool TryToggle(Player player) {
            KikasaDomainPlayer kdp = player.GetModPlayer<KikasaDomainPlayer>();
            if (kdp.WispFireActive) {
                return kdp.ToggleWispFire();
            }
            if (kdp.Phase != KikasaDomainPhase.Open || kdp.RiseT < 0.999f || kdp.IsRainForm) {
                KikasaDreamSystem.Refuse(player);
                return false;
            }
            return kdp.ToggleWispFire();
        }

        //==================== 逐帧推进（UpdateLocal 委托，各端对每个活跃域都跑） ====================

        internal static void Update(KikasaDomainPlayer kdp) {
            //压制：形态已切进鬼雨、且倒转段收尾（世界落回视野）后才起拍——
            //倒转前半段火在翻滚的旧世界里烧着，雨落下来才开始压灭
            bool suppressed = kdp.WispFireActive && kdp.IsRainForm
                && (kdp.Phase != KikasaDomainPhase.Flipping
                    || kdp.PhaseTimer >= KikasaDomain.FlipRollEnd);
            if (suppressed) {
                kdp.WispQuench = MathF.Min(kdp.WispQuench + 1f / QuenchFrames, 1f);
                if (kdp.WispQuench >= 1f) {
                    //火死透了：点燃态清除，翻回血湖不自动复燃
                    kdp.WispFireActive = false;
                }
            }
            else if (!kdp.WispFireActive && kdp.WispQuench > 0f) {
                kdp.WispQuench = MathF.Max(kdp.WispQuench - 0.05f, 0f);
            }

            //蔓延：点燃后前沿扫向两端；收火反向啃回；压制时冻在原地——雨压灭的是站着的火
            if (kdp.WispFireActive && !suppressed) {
                kdp.WispSpread = MathF.Min(kdp.WispSpread + 1f / SpreadFrames, 1f);
            }
            else if (!kdp.WispFireActive) {
                kdp.WispSpread = MathF.Max(kdp.WispSpread - 1f / RecedeFrames, 0f);
            }

            //在场包络：满水且非梦侧才养得住火；压制中目标随 Quench 塌缩
            bool lakeHolds = kdp.RiseT >= 0.98f && !kdp.DreamWorldVisual;
            float target = kdp.WispFireActive && lakeHolds ? 1f - kdp.WispQuench : 0f;
            float rate = target > kdp.WispT ? 1f / IgniteFrames : 1f / FadeFrames;
            kdp.WispT = target > kdp.WispT
                ? MathF.Min(kdp.WispT + rate, target)
                : MathF.Max(kdp.WispT - rate, target);

            //烧净后清残量，下次点燃从干净状态起手
            if (kdp.WispT <= 0f && !kdp.WispFireActive) {
                kdp.WispSpread = 0f;
                kdp.WispQuench = 0f;
            }

            OwnerScan(kdp);
        }

        //==================== 灼烧扫描（仅 owner 端；AddBuff 骑原版 buff 同步） ====================

        private static void OwnerScan(KikasaDomainPlayer kdp) {
            if (Main.dedServ || kdp.Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (!kdp.WispFireActive || kdp.WispT < 0.45f || kdp.WispQuench > 0.3f
                || kdp.DreamWorldVisual) {
                scanTimer = 0;
                return;
            }
            if (++scanTimer < ScanInterval) {
                return;
            }
            scanTimer = 0;

            int buffType = ModContent.BuffType<KikasaWispBurn>();
            //燃沿未扫到的地方不点火，火是蔓延过去的
            float reachPx = kdp.WispSpread * KikasaLakeSurface.HalfWidth + 80f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || !npc.CanBeChasedBy()) {
                    continue;
                }
                if (!InBurnZone(kdp, npc, reachPx)) {
                    continue;
                }
                npc.AddBuff(buffType, BurnDuration);
            }
        }

        /// <summary>灼烧区：横向在湖带与燃沿内，纵向碰撞箱触及 [水线-火高, 水线+浸深]</summary>
        internal static bool InBurnZone(KikasaDomainPlayer kdp, NPC npc, float reachPx) {
            if (MathF.Abs(npc.Center.X - kdp.Player.Center.X) > KikasaLakeSurface.HalfWidth) {
                return false;
            }
            if (MathF.Abs(npc.Center.X - kdp.WispOriginX) > reachPx) {
                return false;
            }
            Rectangle box = npc.Hitbox;
            return box.Bottom >= kdp.LakeWorldY - FlameReach
                && box.Top <= kdp.LakeWorldY + SubmergeDepth;
        }
    }
}
