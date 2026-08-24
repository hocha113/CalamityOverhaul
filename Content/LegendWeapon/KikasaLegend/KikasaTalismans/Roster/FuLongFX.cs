using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>
    /// 泷的冲刷与白沫集中处。推力部分是机制（权威端生效），
    /// 白沫水线是纯表现（各客户端本地）；两者共用同一次落线扫描
    /// </summary>
    internal static class FuLongFX
    {
        /// <summary>冲刷推力（每帧、乘击退抗性）</summary>
        private const float WashForce = 0.55f;

        /// <summary>顺流速度上限：水在拽，不是弹弓</summary>
        private const float MaxAlongSpeed = 9f;

        /// <summary>落线探测上限（px），冲刷只关心战斗距离，不跟空射射程</summary>
        private const float WashRangeMax = 4000f;

        /// <summary>
        /// 起瀑一瞬：沿倾向甩几道白沫急线+一记沉水声，各端本地。
        /// 倾向取瀑的同步倾角（ai[0]），旁观端同样对得上
        /// </summary>
        internal static void PourStartRush(Projectile pour, Color accent) {
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = pour.ai[0].ToRotationVector2();
            KikasaInk.Play(KikasaInk.InkSplash, pour.Center, 0.42f, -0.55f, 3);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Line>(
                    pour.Center + dir * Main.rand.NextFloat(20f, 90f)
                        + dir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-16f, 16f),
                    dir * Main.rand.NextFloat(8f, 13f),
                    Color.Lerp(accent, Color.White, 0.5f) * 0.7f,
                    Main.rand.NextFloat(0.45f, 0.7f))?.Configure(false, 10);
            }
        }

        /// <summary>
        /// 冲刷主体：一次落线扫描喂两件事——
        /// 权威端（单机/服务器）给落线上的敌人顺瀑推力，
        /// 客户端给被冲的敌人拖白沫水线、给瀑身撒急流速度线
        /// </summary>
        internal static void RunWash(Projectile pour, Color accent) {
            //排空尾段判定已失能，冲刷同步收手
            if (pour.timeLeft <= KikasaInkPour.CollapseFrames) {
                return;
            }
            Vector2 dir = pour.ai[0].ToRotationVector2();
            float fill = ReadFill(pour.ai[1]);

            //粗步射线找落线长度：与瀑身逻辑同一实心判定口径，各端确定性一致
            float len = WashRangeMax;
            for (float d = 48f; d <= WashRangeMax; d += 48f) {
                Vector2 p = pour.Center + dir * d;
                if (Collision.SolidCollision(p - new Vector2(6f, 6f), 12, 12)) {
                    len = d;
                    break;
                }
            }

            float washHalf = (64f + fill * 34f) * 0.95f;
            Vector2 a = pour.Center;
            Vector2 b = pour.Center + dir * len;
            bool authority = Main.netMode != NetmodeID.MultiplayerClient;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || npc.friendly || npc.boss
                    || npc.dontTakeDamage || npc.knockBackResist <= 0f
                    || !npc.CanBeChasedBy(pour)) {
                    continue;
                }
                Vector2 closest = ClosestOnSegment(npc.Center, a, b);
                if (Vector2.Distance(npc.Center, closest) > washHalf) {
                    continue;
                }
                //顺瀑推涌：只在权威端写速度，客户端靠同步收敛（沉溺内吸同款纪律）
                if (authority && Vector2.Dot(npc.velocity, dir) < MaxAlongSpeed) {
                    npc.velocity += dir * (WashForce * npc.knockBackResist);
                }
                //被冲的敌人拖白沫水线：纯表现，各客户端本地
                if (!Main.dedServ && Main.rand.NextBool(3)) {
                    PRTLoader.NewParticle<PRT_Line>(
                        npc.Center + Main.rand.NextVector2Circular(npc.width * 0.4f, npc.height * 0.4f),
                        dir * Main.rand.NextFloat(4f, 7f),
                        Color.Lerp(accent, Color.White, 0.6f) * 0.65f,
                        Main.rand.NextFloat(0.35f, 0.55f))?.Configure(false, 9);
                }
            }

            //瀑身急流白线：沿落线随机位撒短促速度线，卖"湍"的速度感
            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
                Vector2 pos = pour.Center + dir * (Main.rand.NextFloat(0.10f, 0.90f) * len)
                    + perp * Main.rand.NextFloat(-washHalf * 0.5f, washHalf * 0.5f);
                PRTLoader.NewParticle<PRT_Line>(pos, dir * Main.rand.NextFloat(9f, 14f),
                    Color.Lerp(accent, Color.White, 0.45f) * 0.55f,
                    Main.rand.NextFloat(0.4f, 0.65f))?.Configure(false, 8);
                //偶发一粒白沫珠贴着瀑缘翻滚
                if (Main.rand.NextBool(3)) {
                    PRTLoader.NewParticle<PRT_KikasaInkBead>(pos,
                        dir * Main.rand.NextFloat(3f, 6f) + perp * Main.rand.NextFloat(-1f, 1f),
                        Color.Lerp(accent, Color.White, 0.35f),
                        Main.rand.NextFloat(0.22f, 0.34f))?.Configure(Main.rand.Next(12, 20));
                }
            }
        }

        /// <summary>自瀑的 ai[1] 量化编码解出蓄力档（口径同 KikasaInkPour）</summary>
        private static float ReadFill(float ai1) {
            int tag = ai1 > 1.001f ? (int)(ai1 / 1024f) : 0;
            return MathHelper.Clamp(tag > 0 ? (ai1 - tag * 1024f) / 1000f : ai1, 0f, 1f);
        }

        private static Vector2 ClosestOnSegment(Vector2 p, Vector2 a, Vector2 b) {
            Vector2 ab = b - a;
            float lenSq = ab.LengthSquared();
            if (lenSq < 1e-4f) {
                return a;
            }
            float t = MathHelper.Clamp(Vector2.Dot(p - a, ab) / lenSq, 0f, 1f);
            return a + ab * t;
        }
    }
}
