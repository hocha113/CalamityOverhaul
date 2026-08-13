using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.States
{
    /// <summary>地底伏击：入土→地下潜行(地表尘迹)→锁定预兆→垂直喷发+酸液扇→再入土</summary>
    [InnoVault.StateMachines.VaultState((int)EowStateIndex.BurrowAmbush, typeof(EowStateContext))]
    internal class EowBurrowAmbushState : EowStateBase
    {
        public override string StateName => "BurrowAmbush";
        public override EowStateIndex StateIndex => EowStateIndex.BurrowAmbush;
        public override bool AllowFarSnap => false;

        #region 节奏常量
        private const int DiveMaxTime = 80;
        private const int StalkMaxTime = 100;
        private const int WarnTime = 34;
        private const int EruptMaxTime = 110;
        private const int ExitTime = 60;
        #endregion

        private enum Phase
        {
            DiveIn = 0,
            Stalk = 1,
            Warn = 2,
            Erupt = 3,
            ExitArc = 4,
        }

        private int EruptionCount(EowStateContext ctx) => ctx.IsPhase2 ? 3 : 2;
        private float EruptSpeed(EowStateContext ctx) => (ctx.IsPhase2 ? 56f : 51f) + (ctx.IsDeathMode ? 4f : 0f);

        private Phase phase;
        private int eruptionsDone;
        private float groundY;
        private float lockX;
        private bool entryFired;
        private bool breachFired;
        private bool reentryFired;
        private bool exitBurstFired;

        public EowBurrowAmbushState() {
        }

        public override void OnEnter(EowStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = false;
            phase = Phase.DiveIn;
            eruptionsDone = 0;
            entryFired = false;
            exitBurstFired = false;

            Vector2 anchor = context.Target.Alives() ? context.Target.Center : context.Npc.Center;
            groundY = EowMotionFX.FindGroundBelow(anchor).Y;
            EowMotionFX.PlayRoar(context.Npc.Center, -0.5f, 0.9f);
        }

        public override IEowState OnUpdate(EowStateContext context) {
            Tick();
            switch (phase) {
                case Phase.DiveIn:
                    UpdateDiveIn(context);
                    break;
                case Phase.Stalk:
                    UpdateStalk(context);
                    break;
                case Phase.Warn:
                    UpdateWarn(context);
                    break;
                case Phase.Erupt:
                    UpdateErupt(context);
                    break;
                case Phase.ExitArc:
                    if (UpdateExitArc(context)) {
                        return new EowWeaveState();
                    }
                    break;
            }
            return null;
        }

        private void SwitchPhase(Phase next) {
            phase = next;
            Timer = 0;
        }

        #region 入土
        private void UpdateDiveIn(EowStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            npc.damage = 0;
            int side = Math.Sign(npc.Center.X - player.Center.X);
            if (side == 0) {
                side = 1;
            }
            SetMovement(context, new Vector2(player.Center.X + side * 160f, groundY + 900f),
                MathHelper.Lerp(26f, 42f, Math.Min(Timer / 34f, 1f)), 1.1f);
            context.AccelRate = 0.09f;

            if (!entryFired && npc.Center.Y > groundY + 30f) {
                entryFired = true;
                EowMotionFX.SpawnDirtBurst(new Vector2(npc.Center.X, groundY), 1.2f);
                EowMotionFX.CameraPunch(new Vector2(npc.Center.X, groundY), 4f, 11, "EowBurrowIn", Vector2.UnitY);
            }

            if (npc.Center.Y > groundY + 560f || Timer > DiveMaxTime) {
                SwitchPhase(Phase.Stalk);
            }
        }
        #endregion

        #region 地下潜行
        private void UpdateStalk(EowStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            npc.damage = 0;
            context.SlitherStrength = 0.55f;
            SetMovement(context, new Vector2(player.Center.X, groundY + 380f),
                (context.IsPhase2 ? 38f : 33f) + (context.IsDeathMode ? 4f : 0f), 1.3f);

            //地表尘迹跟随头X(猎物视角的"它来了")
            if (!VaultUtils.isServer) {
                Vector2 surface = EowMotionFX.FindGroundBelow(new Vector2(npc.Center.X, groundY - 500f));
                if (Timer % 2 == 0 && EowMotionFX.OnScreen(surface)) {
                    Dust dust = Dust.NewDustDirect(surface + new Vector2(Main.rand.NextFloat(-36f, 36f), -6f),
                        4, 4, DustID.Dirt, 0, 0, 110, default, Main.rand.NextFloat(1.1f, 1.8f));
                    dust.velocity = new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(1.5f, 4.5f));
                }
                if (Timer % 13 == 0) {
                    SoundEngine.PlaySound(SoundID.WormDigQuiet with { Volume = 0.7f, Pitch = -0.3f }, surface);
                }
                if (Timer % 16 == 0) {
                    EowMotionFX.CameraPunch(surface, 1.6f, 12, "EowStalkRumble");
                }
            }

            //水平对齐或超时→锁定
            if ((Timer > 20 && Math.Abs(npc.Center.X - player.Center.X) < 110f) || Timer > StalkMaxTime) {
                lockX = player.Center.X + player.velocity.X * 13f;
                groundY = EowMotionFX.FindGroundBelow(new Vector2(lockX, player.Center.Y)).Y;
                breachFired = false;
                reentryFired = false;
                //锁定预兆盘(服务端)
                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), new Vector2(lockX, groundY), Vector2.Zero,
                        ModContent.ProjectileType<EowBreachOmen>(), 0, 0f, Main.myPlayer, WarnTime - 2, 0f);
                }
                SwitchPhase(Phase.Warn);
            }
        }
        #endregion

        #region 锁定预警
        private void UpdateWarn(EowStateContext context) {
            NPC npc = context.Npc;

            npc.damage = 0;
            context.MawGlow = Timer / (float)WarnTime;
            //在锁点正下方蓄势盘桓
            SetMovement(context, new Vector2(lockX, groundY + 430f), 26f, 1.5f);

            if (Timer == 1) {
                SoundEngine.PlaySound(SoundID.Item17 with { Pitch = -0.5f, Volume = 0.9f }, new Vector2(lockX, groundY));
            }

            if (Timer >= WarnTime) {
                SwitchPhase(Phase.Erupt);
            }
        }
        #endregion

        #region 喷发
        private void UpdateErupt(EowStateContext context) {
            NPC npc = context.Npc;

            context.SkipDefaultMovement = true;
            context.MawGlow = 1f;

            //喷发帧：地下就位垂直射出
            if (Timer == 1) {
                npc.Center = new Vector2(lockX, groundY + 760f);
                npc.velocity = -Vector2.UnitY * EruptSpeed(context);
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
                npc.netUpdate = true;
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = 0.3f, Volume = 1.1f }, new Vector2(lockX, groundY));
            }

            //速度门控接触伤害
            npc.damage = npc.velocity.Length() > 20f ? npc.defDamage : 0;

            //破土：尘爆+酸液扇
            if (!breachFired && npc.Center.Y < groundY) {
                breachFired = true;
                Vector2 breachPoint = new Vector2(lockX, groundY);
                EowMotionFX.SpawnBreachBlast(breachPoint, 1.5f, -Vector2.UnitY);
                EowMotionFX.CameraPunch(breachPoint, 7.5f, 15, "EowErupt", -Vector2.UnitY);
                if (!VaultUtils.isClient) {
                    int fan = context.IsPhase2 ? 5 : 4;
                    for (int i = 0; i < fan; i++) {
                        float spread = MathHelper.Lerp(-0.55f, 0.55f, fan <= 1 ? 0.5f : i / (float)(fan - 1));
                        Vector2 vel = (-Vector2.UnitY).RotatedBy(spread) * Main.rand.NextFloat(8.5f, 11.5f);
                        Projectile.NewProjectile(npc.GetSource_FromAI(), breachPoint - new Vector2(0, 8f), vel,
                            ModContent.ProjectileType<EowAcidGlob>(),
                            EowSpitBarrageState.SpitDamage(npc), 0f, Main.myPlayer, 2f);
                    }
                }
            }

            //越顶拱弧回落
            if (breachFired && Timer > 22) {
                npc.velocity.Y += 1.5f;
                npc.velocity.X += Math.Sign(npc.velocity.X == 0 ? 1f : npc.velocity.X) * 0.22f;
                if (npc.velocity.Length() > EruptSpeed(context)) {
                    npc.velocity = npc.velocity.SafeNormalize(Vector2.UnitY) * EruptSpeed(context);
                }
            }
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

            //再入土
            bool reentered = breachFired && npc.velocity.Y > 0f && npc.Center.Y > groundY + 50f;
            if (reentered && !reentryFired) {
                reentryFired = true;
                EowMotionFX.SpawnDirtBurst(new Vector2(npc.Center.X, groundY), 0.9f);
            }

            if ((reentryFired && npc.Center.Y > groundY + 450f) || Timer > EruptMaxTime) {
                eruptionsDone++;
                context.SkipDefaultMovement = false;
                if (eruptionsDone >= EruptionCount(context)) {
                    SwitchPhase(Phase.ExitArc);
                }
                else {
                    SwitchPhase(Phase.Stalk);
                }
            }
        }
        #endregion

        #region 收场
        private bool UpdateExitArc(EowStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            context.SkipDefaultMovement = false;
            context.SlitherStrength = 0.65f;
            npc.damage = 0;

            int side = Math.Sign(npc.Center.X - player.Center.X);
            if (side == 0) {
                side = 1;
            }
            SetMovement(context, player.Center + new Vector2(side * 500f, -340f), 24f, 1.1f);

            if (!exitBurstFired && npc.Center.Y < groundY - 40f) {
                exitBurstFired = true;
                EowMotionFX.SpawnDirtBurst(new Vector2(npc.Center.X, groundY), 0.9f);
            }

            return Timer > ExitTime;
        }
        #endregion

        public override void OnExit(EowStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
            context.AccelRate = 0.07f;
            context.Npc.damage = context.Npc.defDamage;
        }
    }
}
