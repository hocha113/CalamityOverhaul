using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 星图审判:司祭仰手在天穹连出星座,星案形成期(描绘+预警)连发星珠压制,
    /// 预警末拍熄灭一下,光刃骤现并逐星较差自转(细节在弹体)<br/>
    /// 公平阀:生成端筛种子,主图+外环任何延长线与玩家当前位保持 PlayerClearance;
    /// 星云主场 9 主星+5 哨星,其余 8+4;全程无音效
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.StarChart, typeof(CultistStateContext))]
    internal class CultistStarChartState : CultistStateBase
    {
        public override string StateName => "CultistStarChart";
        public override CultistStateIndex StateIndex => CultistStateIndex.StarChart;

        /// <summary>Timeout 必须盖过星图全寿命(生成帧 12+描绘+预警+放光 96+谢幕尾),提前回 Coil=双重压力</summary>
        private const int Timeout = 340;
        /// <summary>形成期星珠连发间隔(帧)</summary>
        private const int VolleyGap = 14;

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            SetPose(npc, 13);
            FaceTarget(npc, player.Center);
            context.PushAura(0.8f, CultistMotion.PhaseCore(context.Phase));
            context.OrreryGlow = MathHelper.Max(context.OrreryGlow, 0.5f);

            Vector2 hover = context.ArenaCenter + new Vector2(0f, -300f)
                + CultistMotion.BreathingOffset(seed: 5.7f, 12f);
            CultistMotion.SpringHover(npc, hover, 0.013f, 0.09f, 17f);

            int nodeCount = context.Phase == 1 ? 9 : 8;

            //落笔(权威端):筛种子保证任何延长线不贴脸(声明间距=PlayerClearance);
            //图心偏向玩家 0.4=图幅铺到玩家活动区,不再只在场心小区域溅射
            if (Timer == 12 && !VaultUtils.isClient) {
                Vector2 chartCenter = context.ArenaCenter
                    + (player.Center - context.ArenaCenter) * 0.4f;
                int seed = PickClearSeed(chartCenter, player.Center, nodeCount);
                Projectile.NewProjectile(npc.GetSource_FromAI(), chartCenter, Vector2.Zero,
                    ModContent.ProjectileType<CultistStarChart>(), 46, 0f, Main.myPlayer,
                    npc.whoAmI, seed, nodeCount);
            }

            //星案形成期连发星珠(权威端):司祭不空手,落刃拍收手让位光刃主秀
            int slamTimer = 12 + CultistStarChart.BeamStartFor(nodeCount);
            if (!VaultUtils.isClient && Timer >= 20 && Timer < slamTimer && Timer % VolleyGap == 0) {
                Vector2 dir = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY)
                    .RotatedByRandom(0.18f);
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + dir * 42f,
                    dir * Main.rand.NextFloat(6.2f, 7.4f),
                    ModContent.ProjectileType<CultistStarBead>(), 40, 0f, Main.myPlayer,
                    context.Phase);
            }

            if (VaultUtils.isClient) {
                return null;
            }

            if (Timer > 48 && !AnyChartAlive(npc.whoAmI)) {
                return new CultistCoilState();
            }
            if (Timer >= Timeout) {
                return new CultistCoilState();
            }
            return null;
        }

        /// <summary>筛种子:主图+外环全部延长线到玩家距离超净空;都不合格取最后一个(净空由长预告兜底)</summary>
        private static int PickClearSeed(Vector2 chartCenter, Vector2 playerPos, int nodeCount) {
            Span<Vector2> nodes = stackalloc Vector2[10];
            Span<Vector2> outer = stackalloc Vector2[5];
            int outerCount = CultistStarChart.OuterCountFor(nodeCount);
            int seed = 0;
            for (int attempt = 0; attempt < 40; attempt++) {
                seed = Main.rand.Next(100000);
                CultistStarChart.BuildNodes(seed, nodeCount, nodes);
                CultistStarChart.BuildOuterNodes(seed, outerCount, outer);
                bool clear = true;
                for (int e = 0; e < nodeCount - 1 && clear; e++) {
                    clear = LineClear(chartCenter + nodes[e], nodes[e + 1] - nodes[e], playerPos);
                }
                for (int o = 0; o < outerCount - 1 && clear; o++) {
                    clear = LineClear(chartCenter + outer[o], outer[o + 1] - outer[o], playerPos);
                }
                if (clear) {
                    return seed;
                }
            }
            return seed;
        }

        /// <summary>点到无限直线距离≥净空</summary>
        private static bool LineClear(Vector2 a, Vector2 along, Vector2 playerPos) {
            Vector2 dir = along.SafeNormalize(Vector2.UnitX);
            Vector2 toPlayer = playerPos - a;
            return MathF.Abs(toPlayer.X * dir.Y - toPlayer.Y * dir.X) >= CultistStarChart.PlayerClearance;
        }

        private static bool AnyChartAlive(int ownerWho) {
            int type = ModContent.ProjectileType<CultistStarChart>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[0] == ownerWho) {
                    return true;
                }
            }
            return false;
        }
    }
}
