using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States
{
    /// <summary>俯冲贯穿普攻，短整备→交叉俯冲→阶梯刹车</summary>
    [InnoVault.StateMachines.VaultState((int)DestroyerStateIndex.DiveStrike, typeof(DestroyerStateContext))]
    internal class DestroyerDiveStrikeState : DestroyerStateBase
    {
        public override string StateName => "DiveStrike";
        public override DestroyerStateIndex StateIndex => DestroyerStateIndex.DiveStrike;
        /// <summary>自带高空走位，关远距瞬移阀</summary>
        public override bool AllowFarSnap => false;

        #region 节奏常量
        private const int RepositionTime = 36;
        private const int TelegraphTime = 42;
        private const int DiveTime = 50;
        private const int GapTime = 12;
        private const int PassLength = TelegraphTime + DiveTime + GapTime;
        private const int BrakeTime = 20;
        #endregion

        private int PassCount(DestroyerStateContext ctx) => ctx.IsEnraged ? 3 : 2;
        private float DiveSpeed(DestroyerStateContext ctx)
            => (ctx.IsEnraged ? 80f : 74f) + (ctx.IsDeathMode ? 8f : 0f);

        private Vector2 lineCenter;
        private Vector2 diveDir;
        private bool passBoomFired;
        private int currentPass = -1;

        public DestroyerDiveStrikeState() {
        }

        public override void OnEnter(DestroyerStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = false;
            context.OrbitalVisual = 1;
            currentPass = -1;

            if (!VaultUtils.isClient) {
                context.Npc.ai[3] = Main.rand.Next(2);
                context.Npc.netUpdate = true;
            }
            SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.2f, Volume = 0.9f }, context.Npc.Center);
        }

        public override IDestroyerState OnUpdate(DestroyerStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            int divesEnd = RepositionTime + PassCount(context) * PassLength;

            Timer++;

            //短整备爬高
            if (Timer <= RepositionTime) {
                context.SkipDefaultMovement = false;
                context.OrbitalVisual = 1;
                npc.damage = 0;
                SetMovement(context, player.Center + new Vector2(0, -1700f), 46f, 1.1f);
                context.AccelRate = 0.1f;
                DestroyerChargeWave.Push(npc.whoAmI, 1f - Timer / (float)RepositionTime, 0.3f,
                    0.3f + 0.5f * (Timer / (float)RepositionTime));
                return null;
            }

            //交叉俯冲
            if (Timer <= divesEnd) {
                UpdatePass(context, Timer - RepositionTime - 1);
                return null;
            }

            //阶梯刹车
            if (Timer <= divesEnd + BrakeTime) {
                context.SkipDefaultMovement = true;
                context.OrbitalVisual = 0;
                npc.damage = 0;
                float spd = npc.velocity.Length();
                float brake = spd > 40f ? 0.92f : spd > 25f ? 0.94f : 0.965f;
                npc.velocity *= brake;
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
                if (!VaultUtils.isServer && Timer % 3 == 0) {
                    DestroyerMotionFX.SpawnBrakeSparks(npc);
                }
                return null;
            }

            return new DestroyerPatrolState();
        }

        private void UpdatePass(DestroyerStateContext context, int diveTimer) {
            NPC npc = context.Npc;
            Player player = context.Target;

            int passIndex = Math.Min(diveTimer / PassLength, PassCount(context) - 1);
            int t = diveTimer - passIndex * PassLength;

            //新趟，ai[3]左右交替成X
            if (passIndex != currentPass) {
                currentPass = passIndex;
                passBoomFired = false;

                int side = ((int)npc.ai[3] + passIndex) % 2 == 0 ? 1 : -1;
                float angleFromVertical = MathHelper.ToRadians(36f + passIndex * 8f) * side;
                diveDir = Vector2.UnitY.RotatedBy(angleFromVertical);
                lineCenter = player.Center + player.velocity * 22f;

                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(),
                        lineCenter - diveDir * 2400f, diveDir,
                        ModContent.ProjectileType<DestroyerStrikeTelegraph>(), 0, 0f, Main.myPlayer,
                        -1, -1, DestroyerStrikeTelegraph.PackParams(0, TelegraphTime));
                }
                SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.2f, Volume = 0.85f }, player.Center);
            }

            //预警高位，末12f咬合
            if (t < TelegraphTime) {
                context.SkipDefaultMovement = false;
                context.OrbitalVisual = 1;
                context.JawCommand = t > TelegraphTime - 12 ? 2 : 1;
                npc.damage = 0;
                SetMovement(context, player.Center + new Vector2(0, -1900f), 30f, 0.8f);
                DestroyerChargeWave.Push(npc.whoAmI, 1f - t / (float)TelegraphTime, 0.25f, 0.8f);
                return;
            }

            //释放帧
            if (t == TelegraphTime) {
                npc.Center = lineCenter - diveDir * 2500f;
                npc.velocity = diveDir * DiveSpeed(context);
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
                npc.netUpdate = true;
                //ForceRoar，间隔短于采样会被IgnoreNew吞
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = 0.35f, Volume = 1f }, player.Center);
                //闪雷+电弧对贯穿线
                MachineEffect.TriggerSkyFlash(lineCenter, 1f);
                if (!VaultUtils.isClient) {
                    DestroyerHeatWakeProj.EnsureForHead(npc);
                }
            }

            //俯冲中
            if (t <= TelegraphTime + DiveTime) {
                context.SkipDefaultMovement = true;
                context.OrbitalVisual = 2;
                context.JawCommand = 1;
                npc.damage = npc.defDamage;
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

                if (!passBoomFired && npc.Distance(lineCenter) < 340f) {
                    passBoomFired = true;
                    if (!VaultUtils.isClient) {
                        Projectile.NewProjectile(npc.GetSource_FromAI(), lineCenter, Vector2.Zero,
                            ModContent.ProjectileType<DestroyerShockwave>(), 0, 0f, Main.myPlayer, 1);
                    }
                    DestroyerMotionFX.CameraPunch(lineCenter, 7f, 16, "DestroyerDivePass", diveDir);
                }

                //越场够远进间隙
                int gapStartTimer = RepositionTime + 1 + passIndex * PassLength + TelegraphTime + DiveTime;
                if (passBoomFired && Vector2.Dot(npc.Center - lineCenter, diveDir) > 1100f && Timer < gapStartTimer) {
                    Timer = gapStartTimer;
                }
                return;
            }

            //间隙微减速
            context.SkipDefaultMovement = true;
            context.OrbitalVisual = 1;
            npc.damage = 0;
            npc.velocity *= 0.97f;
        }

        public override void OnExit(DestroyerStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
            context.OrbitalVisual = 0;
            context.AccelRate = 0.055f;
            context.Npc.damage = context.Npc.defDamage;
        }
    }
}
