using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States
{
    /// <summary>回旋绞杀，蓄势→突入→环绕→贯穿</summary>
    [InnoVault.StateMachines.VaultState((int)DestroyerStateIndex.LoopLash, typeof(DestroyerStateContext))]
    internal class DestroyerLoopLashState : DestroyerStateBase
    {
        public override string StateName => "LoopLash";
        public override DestroyerStateIndex StateIndex => DestroyerStateIndex.LoopLash;

        #region 节奏常量
        private const int ChargeTime = 40;
        private const int LungeTime = 18;
        private const int BrakeTime = 16;
        /// <summary>蓄势末预警提前，固定36f</summary>
        private const int WarnLead = 36;
        #endregion

        private int LoopTime(DestroyerStateContext ctx) => ctx.IsEnraged ? 46 : 52;
        private float LungeSpeed(DestroyerStateContext ctx) => (ctx.IsEnraged ? 64f : 58f) + (ctx.IsAsuraMode ? 4f : 0f);
        private float ExitSpeed(DestroyerStateContext ctx) => (ctx.IsEnraged ? 70f : 64f) + (ctx.IsAsuraMode ? 4f : 0f);

        private int side;
        private int loopDir;
        private Vector2 anchorPos;
        private bool exitFired;

        public DestroyerLoopLashState() {
        }

        public override void OnEnter(DestroyerStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            exitFired = false;
            side = 0;
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.45f, Volume = 0.7f }, context.Npc.Center);
        }

        public override IDestroyerState OnUpdate(DestroyerStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            int loopTime = LoopTime(context);

            //侧位由进场相对位定，无需同步
            if (side == 0) {
                side = Math.Sign(npc.Center.X - player.Center.X);
                if (side == 0) {
                    side = 1;
                }
                loopDir = -side;
            }

            Timer++;

            //蓄势迟滞后撤
            if (Timer <= ChargeTime) {
                UpdateCharge(context);
                return null;
            }

            //突入释放
            if (Timer == ChargeTime + 1) {
                Vector2 lungeDir = (player.Center + player.velocity * 10f - npc.Center).SafeNormalize(Vector2.UnitX * -side);
                npc.velocity = lungeDir * LungeSpeed(context);
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
                npc.netUpdate = true;

                //ForceRoar，间隔短会被IgnoreNew吞
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = 0.3f, Volume = 1f }, npc.Center);
                DestroyerMotionFX.SpawnDashBurst(npc.Center, lungeDir);
                DestroyerMotionFX.CameraPunch(npc.Center, 6f, 14, "DestroyerLashLunge", lungeDir);
                if (!VaultUtils.isClient) {
                    DestroyerHeatWakeProj.EnsureForHead(npc);
                }
            }

            //突入直线
            if (Timer <= ChargeTime + LungeTime) {
                npc.damage = npc.defDamage;
                context.OrbitalVisual = 2;
                context.JawCommand = 1;
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
                return null;
            }

            //环绕定角旋转成绞索
            if (Timer <= ChargeTime + LungeTime + loopTime) {
                npc.damage = npc.defDamage;
                context.OrbitalVisual = 2;
                context.JawCommand = 1;

                float speed = MathHelper.Lerp(npc.velocity.Length(), LungeSpeed(context) * 0.86f, 0.06f);
                npc.velocity = npc.velocity.RotatedBy(MathHelper.TwoPi / loopTime * loopDir)
                    .SafeNormalize(Vector2.UnitY) * speed;
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

                //绞索预警，低强度充能波
                DestroyerChargeWave.Push(npc.whoAmI, 0f, 1f,
                    0.35f + 0.45f * ((Timer - ChargeTime - LungeTime) / (float)loopTime), fullBody: true);
                return null;
            }

            //环心贯穿(一帧)
            if (!exitFired) {
                exitFired = true;
                Vector2 exitDir = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY);
                npc.velocity = exitDir * ExitSpeed(context);
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
                npc.netUpdate = true;
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = 0.45f, Volume = 1.05f }, npc.Center);
                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + exitDir * 80f, Vector2.Zero,
                        ModContent.ProjectileType<DestroyerShockwave>(), 0, 0f, Main.myPlayer, 0);
                    DestroyerHeatWakeProj.EnsureForHead(npc);
                }
                DestroyerMotionFX.CameraPunch(npc.Center, 7f, 15, "DestroyerLashExit", exitDir);
            }

            //贯穿+阶梯刹车
            int exitEnd = ChargeTime + LungeTime + loopTime + LungeTime;
            if (Timer <= exitEnd) {
                npc.damage = npc.defDamage;
                context.OrbitalVisual = 2;
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
                return null;
            }

            if (Timer <= exitEnd + BrakeTime) {
                npc.damage = 0;
                context.OrbitalVisual = 0;
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

        /// <summary>蓄势悬停+pow迟滞后撤锁线</summary>
        private void UpdateCharge(DestroyerStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            npc.damage = 0;
            context.OrbitalVisual = 1;
            float t = Timer / (float)ChargeTime;
            context.JawCommand = Timer > ChargeTime - 12 ? 2 : 1;

            //悬停锚+后撤偏移
            Vector2 away = (npc.Center - player.Center).SafeNormalize(Vector2.UnitX * side);
            Vector2 anchor = player.Center + new Vector2(side * 600f, -130f);
            float reel = (float)Math.Pow(t, 8) * 340f;
            anchorPos = anchor + away * reel;

            Vector2 desired = (anchorPos - npc.Center) * 0.14f;
            if (desired.Length() > 30f) {
                desired = desired.SafeNormalize(Vector2.Zero) * 30f;
            }
            npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.25f);

            //转向衰减锁线
            float faceLerp = MathHelper.Lerp(0.28f, 0.05f, t);
            FaceTarget(npc, player.Center, faceLerp);

            //T-36f 预警音
            if (Timer == ChargeTime - WarnLead + 1) {
                SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.1f, Volume = 0.8f }, npc.Center);
            }

            //72%硬切粒子
            if (t < 0.72f && !VaultUtils.isServer && Timer % 3 == 0) {
                Vector2 dustPos = npc.Center + Main.rand.NextVector2Circular(50f, 50f);
                Dust dust = Dust.NewDustDirect(dustPos, 1, 1,
                    DustID.FireworkFountain_Red, 0, 0, 100, default, 1.4f + t);
                dust.noGravity = true;
                dust.velocity = (npc.Center - dustPos).SafeNormalize(Vector2.Zero) * (2f + t * 4f);
            }

            DestroyerChargeWave.Push(npc.whoAmI, 1f - t, 0.22f, 0.35f + 0.55f * t);
        }

        public override void OnExit(DestroyerStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
            context.OrbitalVisual = 0;
            context.Npc.damage = context.Npc.defDamage;
        }
    }
}
