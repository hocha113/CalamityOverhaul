using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaWisps
{
    /// <summary>
    /// 血湖鬼火门面：自燃门控、湖力收支、包络推进、鬼雨压制节拍、灼烧区扫描。
    /// 状态存在 <see cref="KikasaDomainPlayer"/>（点燃态+原点入快照，包络各端本地自算）；
    /// 满水稳态、湖力蓄满即自燃，湖力烧干即熄；键已删。
    /// 焰影只做增强（灼烧更久、火舌更高，见 KikasaEffigyBoard），不再是点火的门。
    /// 设定：鬼火被鬼雨压制——翻入鬼雨后火被迅速压灭且点燃态清除；
    /// 沸雨边（焰×潦）免压制，改作半强度蒸沸
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

        /// <summary>火舌向水线上方的灼烧触及高度基数（px）；焰影加成见 KikasaEffigyBoard</summary>
        public const float FlameReach = 70f;

        /// <summary>水线下仍会被灼烧的深度（px），与沉溺抓取深度同源</summary>
        public const float SubmergeDepth = 600f;

        /// <summary>owner 端灼烧扫描间隔（帧）：AddBuff 走原版网络包，节流防包洪</summary>
        private const int ScanInterval = 30;

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

        //==================== 逐帧推进（UpdateLocal 委托，各端对每个活跃域都跑） ====================

        internal static void Update(KikasaDomainPlayer kdp) {
            //owner 端裁决：自燃/收火/沸雨边刷新——盘只活在所有者本机
            UpdateOwnerGate(kdp);

            //压制：形态已切进鬼雨、且倒转段收尾（世界落回视野）后才起拍——
            //倒转前半段火在翻滚的旧世界里烧着，雨落下来才开始压灭；沸雨边免压制
            bool suppressed = kdp.WispFireActive && kdp.IsRainForm && !kdp.WispRainProof
                && (kdp.Phase != KikasaDomainPhase.Flipping
                    || kdp.PhaseTimer >= KikasaDomain.FlipRollEnd);
            if (suppressed) {
                kdp.WispQuench = MathF.Min(kdp.WispQuench + 1f / QuenchFrames, 1f);
                if (kdp.WispQuench >= 1f) {
                    //火死透了：点燃态清除，翻回血湖等湖力回满再自燃
                    kdp.WispFireActive = false;
                }
            }
            else if (!kdp.WispFireActive && kdp.WispQuench > 0f) {
                kdp.WispQuench = MathF.Max(kdp.WispQuench - 0.05f, 0f);
            }

            //湖力收支：燃着耗、熄着回。远端用基础耗速自算，快照每两秒矫正——
            //三影镇湖的省烧系数只有 owner 算得出，误差是慢表读数级
            if (kdp.WispFireActive && kdp.WispQuench < 0.5f) {
                float burn = !Main.dedServ && kdp.Player.whoAmI == Main.myPlayer
                    ? KikasaEffigyBoard.VigorBurnPerFrame(kdp.Player)
                    : 1f / 1500f;
                kdp.LakeVigor = MathF.Max(kdp.LakeVigor - burn, 0f);
            }
            else {
                kdp.LakeVigor = MathF.Min(kdp.LakeVigor + KikasaEffigyBoard.VigorRefillPerFrame, 1f);
            }

            //蔓延：点燃后前沿扫向两端；收火反向啃回；压制时冻在原地——雨压灭的是站着的火
            if (kdp.WispFireActive && !suppressed) {
                kdp.WispSpread = MathF.Min(kdp.WispSpread + 1f / SpreadFrames, 1f);
            }
            else if (!kdp.WispFireActive) {
                kdp.WispSpread = MathF.Max(kdp.WispSpread - 1f / RecedeFrames, 0f);
            }

            //在场包络：满水且非梦侧才养得住火；压制中目标随 Quench 塌缩；
            //沸雨形态里只烧半强度——雨里蒸沸的火
            bool lakeHolds = kdp.RiseT >= 0.98f && !kdp.DreamWorldVisual;
            float formCap = kdp.IsRainForm && kdp.WispRainProof ? 0.55f : 1f;
            float target = kdp.WispFireActive && lakeHolds
                ? (1f - kdp.WispQuench) * formCap : 0f;
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

        //==================== 自燃门控（仅 owner 端；点燃/收火命令走既有确认拍与快照） ====================

        private static void UpdateOwnerGate(KikasaDomainPlayer kdp) {
            Player player = kdp.Player;
            if (Main.dedServ || player.whoAmI != Main.myPlayer || player.dead) {
                return;
            }
            //沸雨边逐帧刷新入快照——远端读不到盘，靠这面旗决定压不压火
            kdp.WispRainProof = KikasaEffigyBoard.HasBoilRainEdge(player);

            if (kdp.WispFireActive) {
                //湖力烧干即收火（回满后会再自燃）；焰影去留只改增益，不再掐火
                if (kdp.LakeVigor <= 0f) {
                    kdp.ToggleWispFire();
                }
                return;
            }
            //自燃：满水稳态 + 湖力蓄满 + 形态许可（非鬼雨，沸雨边例外）+ 非梦——不看编成
            bool formOk = !kdp.IsRainForm || kdp.WispRainProof;
            if (kdp.Phase == KikasaDomainPhase.Open && kdp.RiseT >= 0.999f
                && formOk && kdp.LakeVigor >= 0.999f && !kdp.DreamWorldVisual) {
                kdp.ToggleWispFire();
            }
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
            //焰影愈多灼烧愈久、火舌愈高（KikasaEffigyBoard 折算）
            int burnDuration = KikasaEffigyBoard.WispBurnDuration(kdp.Player);
            float reach = KikasaEffigyBoard.WispFlameReach(kdp.Player);
            //燃沿未扫到的地方不点火，火是蔓延过去的
            float reachPx = kdp.WispSpread * KikasaLakeSurface.HalfWidth + 80f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || !npc.CanBeChasedBy()) {
                    continue;
                }
                if (!InBurnZone(kdp, npc, reachPx, reach)) {
                    continue;
                }
                npc.AddBuff(buffType, burnDuration);
            }
        }

        /// <summary>灼烧区：横向在湖带与燃沿内，纵向碰撞箱触及 [水线-火高, 水线+浸深]</summary>
        internal static bool InBurnZone(KikasaDomainPlayer kdp, NPC npc, float reachPx,
            float flameReach = FlameReach) {
            if (MathF.Abs(npc.Center.X - kdp.Player.Center.X) > KikasaLakeSurface.HalfWidth) {
                return false;
            }
            if (MathF.Abs(npc.Center.X - kdp.WispOriginX) > reachPx) {
                return false;
            }
            Rectangle box = npc.Hitbox;
            return box.Bottom >= kdp.LakeWorldY - flameReach
                && box.Top <= kdp.LakeWorldY + SubmergeDepth;
        }
    }
}
