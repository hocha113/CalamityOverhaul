using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 星图审判:司祭仰手在天穹连出星座,定形后沿边线延长线放光<br/>
    /// 公平阀:生成端筛种子,任何延长线与玩家当前位保持 PlayerClearance;星云主场 7 节点
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.StarChart, typeof(CultistStateContext))]
    internal class CultistStarChartState : CultistStateBase
    {
        public override string StateName => "CultistStarChart";
        public override CultistStateIndex StateIndex => CultistStateIndex.StarChart;

        private const int Timeout = 340;

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

            if (Timer == 6 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item117 with { Volume = 0.75f, Pitch = 0.1f }, npc.Center);
            }

            //落笔(权威端):筛种子保证任何延长线不贴脸(声明间距=PlayerClearance)
            if (Timer == 16 && !VaultUtils.isClient) {
                int nodeCount = context.Phase == 1 ? 7 : 6;
                Vector2 chartCenter = context.ArenaCenter
                    + (player.Center - context.ArenaCenter) * 0.25f;
                int seed = PickClearSeed(chartCenter, player.Center, nodeCount);
                Projectile.NewProjectile(npc.GetSource_FromAI(), chartCenter, Vector2.Zero,
                    ModContent.ProjectileType<CultistStarChart>(), 46, 0f, Main.myPlayer,
                    npc.whoAmI, seed, nodeCount);
            }

            if (VaultUtils.isClient) {
                return null;
            }

            if (Timer > 60 && !AnyChartAlive(npc.whoAmI)) {
                return new CultistCoilState();
            }
            if (Timer >= Timeout) {
                return new CultistCoilState();
            }
            return null;
        }

        /// <summary>筛种子:全部延长线到玩家距离超净空;都不合格取最后一个(净空由长预告兜底)</summary>
        private static int PickClearSeed(Vector2 chartCenter, Vector2 playerPos, int nodeCount) {
            Span<Vector2> nodes = stackalloc Vector2[8];
            int seed = 0;
            for (int attempt = 0; attempt < 24; attempt++) {
                seed = Main.rand.Next(100000);
                CultistStarChart.BuildNodes(seed, nodeCount, nodes);
                bool clear = true;
                for (int e = 0; e < nodeCount - 1 && clear; e++) {
                    Vector2 a = chartCenter + nodes[e];
                    Vector2 dir = (nodes[e + 1] - nodes[e]).SafeNormalize(Vector2.UnitX);
                    //点到无限直线距离
                    Vector2 toPlayer = playerPos - a;
                    float lineDist = MathF.Abs(toPlayer.X * dir.Y - toPlayer.Y * dir.X);
                    if (lineDist < CultistStarChart.PlayerClearance) {
                        clear = false;
                    }
                }
                if (clear) {
                    return seed;
                }
            }
            return seed;
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
