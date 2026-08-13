using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.States
{
    /// <summary>太阳核心过载（低血大招，一场一次）：宝石充能 → 太阳核心升空 → 辐条旋灼+陨星雨 → 坍缩死寂 → 终爆</summary>
    [InnoVault.StateMachines.VaultState((int)GolemStateIndex.SolarOverdrive, typeof(GolemStateContext))]
    internal class GolemSolarOverdriveState : GolemStateBase
    {
        public override string StateName => "SolarOverdrive";
        public override GolemStateIndex StateIndex => GolemStateIndex.SolarOverdrive;

        internal static int ChargeEnd => 92;    //宝石充能
        internal static int CoreSpawn => 92;    //核心升空
        //核心自转脚本约 430f：升空60 + 辐条与陨星300 + 坍缩40 + 终爆30
        internal static int ExhaustStart => 532;
        internal static int EndTime => 600;

        public override void OnEnter(GolemStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            context.UltFired = true;

            if (!VaultUtils.isClient) {
                npc.TargetClosest();
                //锚定核心位置：躯干上空
                context.LockPoint = npc.Center + new Vector2(0f, -340f);
                npc.netUpdate = true;

                //公平阀：清场敌方弹幕，给玩家干净的读招空间
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile p = Main.projectile[i];
                    if (p.active && p.hostile) {
                        p.Kill();
                    }
                }

                //双拳收拢护卫
                GolemLimbStatus limbs = context.Limbs;
                if (limbs.LeftFistAlive) {
                    GolemBodyAI.CommandFist(limbs.LeftFistIndex, GolemFistCommand.GuardOrbit, npc.Center, 20, 20f, 0);
                }
                if (limbs.RightFistAlive) {
                    GolemBodyAI.CommandFist(limbs.RightFistIndex, GolemFistCommand.GuardOrbit, npc.Center, 20, 20f, 0);
                }
            }

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCDeath14 with { Pitch = -0.8f, Volume = 0.7f }, npc.Center);
            }
        }

        public override IGolemState OnUpdate(GolemStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            npc.noTileCollide = false;
            GroundBrake(npc);
            context.FrameMode = 1;
            //节拍广播：部件表现读取
            npc.ai[GolemAiSlots.BodyBeat] = 2f;

            if (Timer < ChargeEnd) {
                UpdateCharge(context);
            }
            else {
                //核心期躯干持续炽热+微颤
                context.SetChargeState(2, 1f);
                context.VeinGlow = 1f;
                if (!VaultUtils.isServer && Timer % 9 == 0 && Timer < ExhaustStart) {
                    GolemScreenEffects.Shake(1.5f);
                }
            }

            //核心升空
            if (Timer == CoreSpawn) {
                if (!VaultUtils.isClient) {
                    int spokeDamage = ScaleDamage(context, GolemDirector.UltSpokeDamage);
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + new Vector2(0f, -40f),
                        new Vector2(0f, -4.5f), ModContent.ProjectileType<GolemSunCore>(),
                        spokeDamage, 0f, Main.myPlayer,
                        context.LockPoint.X, context.LockPoint.Y);
                    npc.netUpdate = true;
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.5f, Volume = 1.1f }, npc.Center);
                    GolemScreenEffects.PushShockRing(npc.Center, 0.85f, 640f);
                }
            }

            //疲敝恢复窗（公平阀：大招后给输出窗口）
            if (Timer >= ExhaustStart) {
                context.ResetChargeState();
                context.VeinGlow = Math.Max(context.VeinGlow - 0.03f, 0.3f);
                context.FrameMode = 0;
            }

            Timer++;
            if (Timer >= EndTime && !VaultUtils.isClient) {
                context.PostUltRage = true;
                return new GolemConnectorState();
            }
            return null;
        }

        /// <summary>充能段：汇聚流 + 末1/4静默</summary>
        private void UpdateCharge(GolemStateContext context) {
            NPC npc = context.Npc;
            float t = Timer / (float)ChargeEnd;
            context.SetChargeState(2, t);
            context.VeinGlow = Math.Max(context.VeinGlow, t);

            if (VaultUtils.isServer) {
                return;
            }

            //汇聚粒子（密度∝sqrt(t)，72%后硬切——尖啸前的死寂）
            if (t < 0.72f && Main.rand.NextFloat() < MathF.Sqrt(t) * 0.9f) {
                Vector2 gem = npc.Center + new Vector2(0f, -6f);
                Vector2 from = gem + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(150f, 380f);
                Dust dust = Dust.NewDustPerfect(from, DustID.SolarFlare, (gem - from) * 0.075f, 0, default, 1.4f);
                dust.noGravity = true;
            }
            if (Timer == (int)(ChargeEnd * 0.72f)) {
                SoundEngine.PlaySound(SoundID.Item78 with { Pitch = -0.5f, Volume = 0.8f }, npc.Center);
            }
            //低频轰鸣震屏 ∝ t³
            if (Timer % 6 == 0) {
                GolemScreenEffects.Shake(t * t * t * 3f);
            }
        }
    }
}
