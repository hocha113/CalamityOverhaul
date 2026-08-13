using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Rendering;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.States
{
    /// <summary>腐蚀唾液连射：头部抛射齐射+体节涟漪波逐节喷吐，落点残留酸池</summary>
    [InnoVault.StateMachines.VaultState((int)EowStateIndex.SpitBarrage, typeof(EowStateContext))]
    internal class EowSpitBarrageState : EowStateBase
    {
        public override string StateName => "SpitBarrage";
        public override EowStateIndex StateIndex => EowStateIndex.SpitBarrage;

        #region 节奏常量
        private const int ApproachTime = 38;
        private const int VolleyLength = 52;
        private const int ExitTime = 24;
        /// <summary>齐射预警帧(生效于每轮前段)</summary>
        private const int VolleyCue = 14;
        #endregion

        private int VolleyCount(EowStateContext ctx) => ctx.IsPhase2 ? 4 : 3;
        private int GlobsPerVolley(EowStateContext ctx) => ctx.IsPhase2 ? 5 : 4;

        public EowSpitBarrageState() {
        }

        public override IEowState OnUpdate(EowStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Tick();

            int volleyPhaseEnd = ApproachTime + VolleyCount(context) * VolleyLength;

            //游走：绕玩家中距横∞徘徊，保持吐射姿态
            float t = Timer * 0.024f;
            int side = Math.Sign(npc.Center.X - player.Center.X);
            if (side == 0) {
                side = 1;
            }
            Vector2 anchor = player.Center + new Vector2(
                side * (620f + (float)Math.Sin(t) * 130f),
                -240f + (float)Math.Sin(t * 1.7f) * 150f);
            SetMovement(context, anchor, 14f, 1.35f);
            context.SlitherStrength = 0.65f;

            //进场
            if (Timer <= ApproachTime) {
                return null;
            }

            //齐射循环
            if (Timer <= volleyPhaseEnd) {
                UpdateVolley(context, Timer - ApproachTime - 1);
                return null;
            }

            //收势
            if (Timer > volleyPhaseEnd + ExitTime) {
                return new EowWeaveState();
            }
            return null;
        }

        private void UpdateVolley(EowStateContext context, int volleyTimer) {
            NPC npc = context.Npc;
            Player player = context.Target;

            int inVolley = volleyTimer % VolleyLength;
            int volleyIndex = volleyTimer / VolleyLength;

            //预警段：腭光渐张+湿息+微仰头
            if (inVolley < VolleyCue) {
                float cueT = inVolley / (float)VolleyCue;
                context.MawGlow = cueT;
                if (inVolley == 2) {
                    EowMotionFX.PlaySpitCue(npc.Center, volleyIndex * 0.06f);
                }
                //蓄势波：尾→头一泻
                context.PulseKind = 1;
                context.PulsePhase = 1f - cueT;
                return;
            }

            context.MawGlow = MathHelper.Clamp(1.4f - (inVolley - VolleyCue) * 0.12f, 0f, 1f);

            //释放帧：头部扇形抛射(带预判)
            if (inVolley == VolleyCue) {
                EowMotionFX.SpawnAcidBurst(MouthPos(npc), 1.2f, MouthDir(npc) * 4f);
                EowMotionFX.CameraPunch(npc.Center, 2.4f, 8, "EowSpitVolley");
                //头部后坐
                npc.velocity -= MouthDir(npc) * 3.2f;

                if (!VaultUtils.isClient) {
                    int globs = GlobsPerVolley(context);
                    Vector2 predicted = player.Center + player.velocity * 16f;
                    for (int i = 0; i < globs; i++) {
                        //抛物线解：以固定速率朝预判点上方偏斜发射
                        Vector2 toTarget = predicted - MouthPos(npc);
                        float dist = toTarget.Length();
                        float lobSpeed = MathHelper.Clamp(dist / 42f, 9f, 17f);
                        Vector2 dir = toTarget.SafeNormalize(Vector2.UnitX);
                        //上抛补偿+扇形散布
                        dir = dir.RotatedBy(-0.34f * Math.Sign(dir.X == 0 ? 1f : dir.X))
                            .RotatedBy(Main.rand.NextFloat(-0.3f, 0.3f));
                        Terraria.Projectile.NewProjectile(npc.GetSource_FromAI(), MouthPos(npc),
                            dir * lobSpeed * Main.rand.NextFloat(0.9f, 1.15f),
                            ModContent.ProjectileType<EowAcidGlob>(),
                            SpitDamage(npc), 0f, Main.myPlayer, 0f);
                    }
                }
            }

            //体节涟漪喷吐：波前扫过尾→头，途经每隔数帧一节吐小团
            float waveT = (inVolley - VolleyCue) / (float)(VolleyLength - VolleyCue);
            context.PulseKind = 1;
            context.PulsePhase = 1f - waveT;

            bool rippleActive = context.IsPhase2 || volleyIndex % 2 == 1;
            if (rippleActive && !VaultUtils.isClient && inVolley > VolleyCue && Timer % 12 == 0
                && context.Segments.Count > 4) {
                int ordinal = (int)(context.PulsePhase * (context.Segments.Count - 1));
                ordinal = Math.Clamp(ordinal, 0, context.Segments.Count - 1);
                NPC seg = context.Segments[ordinal];
                if (seg.Alives()) {
                    Vector2 toPlayer = (player.Center - seg.Center).SafeNormalize(Vector2.UnitY);
                    Vector2 vel = toPlayer.RotatedBy(Main.rand.NextFloat(-0.45f, 0.45f) - 0.3f * Math.Sign(toPlayer.X == 0 ? 1f : toPlayer.X)) * 8.5f;
                    Terraria.Projectile.NewProjectile(seg.GetSource_FromAI(), seg.Center, vel,
                        ModContent.ProjectileType<EowAcidGlob>(),
                        (int)(SpitDamage(npc) * 0.8f), 0f, Main.myPlayer, 0f);
                }
            }
        }

        /// <summary>唾液伤害基准(悬停系数按原版接触伤折算)</summary>
        internal static int SpitDamage(NPC npc) => Math.Max((int)(npc.defDamage * 0.5f), 8);

        internal static Vector2 MouthDir(NPC npc) => (npc.rotation - MathHelper.PiOver2).ToRotationVector2();
        internal static Vector2 MouthPos(NPC npc) => npc.Center + MouthDir(npc) * 20f * npc.scale;
    }
}
