using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Rendering;
using CalamityOverhaul.Content.Projectiles.Boss.Destroyer;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States
{
    /// <summary>回旋绞杀：蓄势→突入→环绕→收口贯穿，约2.5秒</summary>
    [InnoVault.StateMachines.VaultState((int)DestroyerStateIndex.LoopLash, typeof(DestroyerStateContext))]
    internal class DestroyerLoopLashState : DestroyerStateBase
    {
        public override string StateName => "LoopLash";
        public override DestroyerStateIndex StateIndex => DestroyerStateIndex.LoopLash;

        #region 节奏常量
        private const int ChargeTime = 40;
        private const int LungeTime = 18;
        private const int BrakeTime = 16;
        /// <summary>蓄势末端的预警提前量（固定36帧预警常数）</summary>
        private const int WarnLead = 36;
        #endregion

        private int LoopTime(DestroyerStateContext ctx) => ctx.IsEnraged ? 46 : 52;
        private float LungeSpeed(DestroyerStateContext ctx) => (ctx.IsEnraged ? 64f : 58f) + (ctx.IsDeathMode ? 4f : 0f);
        private float ExitSpeed(DestroyerStateContext ctx) => (ctx.IsEnraged ? 70f : 64f) + (ctx.IsDeathMode ? 4f : 0f);

        private int side;
        private int loopDir;
        private Vector2 anchorPos;
        private bool lungeFired;
        private bool exitFired;

        public DestroyerLoopLashState() {
        }

        public override void OnEnter(DestroyerStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            lungeFired = false;
            exitFired = false;
            side = 0;
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.45f, Volume = 0.7f }, context.Npc.Center);
        }

        public override IDestroyerState OnUpdate(DestroyerStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            int loopTime = LoopTime(context);

            //侧位由进入时的相对位置确定（确定性，无需同步）
            if (side == 0) {
                side = Math.Sign(npc.Center.X - player.Center.X);
                if (side == 0) {
                    side = 1;
                }
                loopDir = -side;
            }

            Timer++;

            //蓄势：迟滞后撤
            if (Timer <= ChargeTime) {
                UpdateCharge(context);
                return null;
            }

            //突入释放帧
            if (Timer == ChargeTime + 1) {
                Vector2 lungeDir = (player.Center + player.velocity * 10f - npc.Center).SafeNormalize(Vector2.UnitX * -side);
                npc.velocity = lungeDir * LungeSpeed(context);
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
                npc.netUpdate = true;
                lungeFired = true;
                //ForceRoar：突入/贯穿两声间隔短于Roar采样时长，普通Roar会因IgnoreNew上限丢失
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = 0.3f, Volume = 1f }, npc.Center);
                DestroyerMotionFX.SpawnDashBurst(npc.Center, lungeDir);
                DestroyerMotionFX.CameraPunch(npc.Center, 6f, 14, "DestroyerLashLunge", lungeDir);
                if (!VaultUtils.isClient) {
                    DestroyerHeatWakeProj.EnsureForHead(npc);
                }
            }

            //突入直线段
            if (Timer <= ChargeTime + LungeTime) {
                npc.damage = npc.defDamage;
                context.OrbitalVisual = 2;
                context.JawCommand = 1;
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
                return null;
            }

            //环绕段：速度向量每帧定角旋转，一整圈甩成绞索
            if (Timer <= ChargeTime + LungeTime + loopTime) {
                npc.damage = npc.defDamage;
                context.OrbitalVisual = 2;
                context.JawCommand = 1;

                float speed = MathHelper.Lerp(npc.velocity.Length(), LungeSpeed(context) * 0.86f, 0.06f);
                npc.velocity = npc.velocity.RotatedBy(MathHelper.TwoPi / loopTime * loopDir)
                    .SafeNormalize(Vector2.UnitY) * speed;
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

                //绞索成型预警：全身低强度充能波 + 环内侧火花由体节速度火花自带
                DestroyerChargeWave.Push(npc.whoAmI, 0f, 1f,
                    0.35f + 0.45f * ((Timer - ChargeTime - LungeTime) / (float)loopTime), fullBody: true);
                return null;
            }

            //环心贯穿冲出（一帧设定）
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

            //贯穿直线段 + 阶梯刹车收尾
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

        /// <summary>蓄势：侧位悬停+pow(t,8)迟滞后撤，转向率衰减锁线</summary>
        private void UpdateCharge(DestroyerStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            npc.damage = 0;
            context.OrbitalVisual = 1;
            float t = Timer / (float)ChargeTime;
            context.JawCommand = Timer > ChargeTime - 12 ? 2 : 1;

            //悬停锚点 + 迟滞后撤偏移
            Vector2 away = (npc.Center - player.Center).SafeNormalize(Vector2.UnitX * side);
            Vector2 anchor = player.Center + new Vector2(side * 600f, -130f);
            float reel = (float)Math.Pow(t, 8) * 340f;
            anchorPos = anchor + away * reel;

            Vector2 desired = (anchorPos - npc.Center) * 0.14f;
            if (desired.Length() > 30f) {
                desired = desired.SafeNormalize(Vector2.Zero) * 30f;
            }
            npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.25f);

            //转向率衰减锁线
            float faceLerp = MathHelper.Lerp(0.28f, 0.05f, t);
            FaceTarget(npc, player.Center, faceLerp);

            //T-36f 预警音（固定预警常数）
            if (Timer == ChargeTime - WarnLead + 1) {
                SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.1f, Volume = 0.8f }, npc.Center);
            }

            //72%进度硬切粒子，临爆静默
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
