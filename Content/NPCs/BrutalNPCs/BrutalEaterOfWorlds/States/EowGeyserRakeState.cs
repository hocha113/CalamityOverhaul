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
    /// <summary>地表犁沟：浅层横掠犁开大地，沿途拉起间歇泉篱笆，折返处海豚跃出</summary>
    [InnoVault.StateMachines.VaultState((int)EowStateIndex.GeyserRake, typeof(EowStateContext))]
    internal class EowGeyserRakeState : EowStateBase
    {
        public override string StateName => "GeyserRake";
        public override EowStateIndex StateIndex => EowStateIndex.GeyserRake;
        public override bool AllowFarSnap => false;

        #region 节奏常量
        private const int SetupMaxTime = 90;
        private const int PassMaxTime = 150;
        private const int TurnTime = 42;
        private const float RakeDepth = 140f;
        private const float GeyserSpacing = 180f;
        #endregion

        private enum Phase
        {
            Setup = 0,
            Rake = 1,
            Turn = 2,
            Exit = 3,
        }

        private float RakeSpeed(EowStateContext ctx) => (ctx.IsPhase2 ? 30f : 26f) + (ctx.IsDeathMode ? 3f : 0f);
        private int PassCount(EowStateContext ctx) => 2;

        private Phase phase;
        private int passesDone;
        private int rakeDir;
        private float groundY;
        private float lastGeyserX;
        private bool breachJumpFired;

        public EowGeyserRakeState() {
        }

        public override void OnEnter(EowStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = false;
            phase = Phase.Setup;
            passesDone = 0;
            groundY = EowMotionFX.FindGroundBelow(context.Target.Alives()
                ? context.Target.Center : context.Npc.Center).Y;
            //从远侧开始犁
            rakeDir = context.Npc.Center.X < (context.Target?.Center.X ?? context.Npc.Center.X) ? 1 : -1;
            EowMotionFX.PlayRoar(context.Npc.Center, -0.35f, 0.85f);
        }

        public override IEowState OnUpdate(EowStateContext context) {
            Tick();
            switch (phase) {
                case Phase.Setup:
                    UpdateSetup(context);
                    break;
                case Phase.Rake:
                    UpdateRake(context);
                    break;
                case Phase.Turn:
                    UpdateTurn(context);
                    break;
                case Phase.Exit:
                    if (Timer > 50) {
                        return new EowWeaveState();
                    }
                    UpdateExit(context);
                    break;
            }
            return null;
        }

        private void SwitchPhase(Phase next) {
            phase = next;
            Timer = 0;
        }

        #region 进位
        private void UpdateSetup(EowStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            npc.damage = 0;
            //扎到玩家来向一侧的浅地层
            Vector2 startPos = new Vector2(player.Center.X - rakeDir * 880f, groundY + RakeDepth + 130f);
            SetMovement(context, startPos, 30f, 1.4f);
            context.AccelRate = 0.09f;

            if (npc.WithinRange(startPos, 130f) || Timer > SetupMaxTime) {
                lastGeyserX = npc.Center.X;
                breachJumpFired = false;
                SwitchPhase(Phase.Rake);
                SoundEngine.PlaySound(SoundID.WormDig with { Volume = 1f, Pitch = -0.15f }, npc.Center);
            }
        }
        #endregion

        #region 犁沟
        private void UpdateRake(EowStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //沿地表下方定深直掠(跟随地形起伏)
            groundY = EowMotionFX.FindGroundBelow(new Vector2(npc.Center.X, player.Center.Y - 200f)).Y;
            Vector2 rakeTarget = new Vector2(npc.Center.X + rakeDir * 320f, groundY + RakeDepth);
            SetMovement(context, rakeTarget, RakeSpeed(context), 1.6f);
            context.AccelRate = 0.12f;
            context.SlitherStrength = 0.3f;
            npc.damage = 0;

            //地表犁沟尘浪
            if (!VaultUtils.isServer) {
                Vector2 surface = new Vector2(npc.Center.X, groundY);
                if (EowMotionFX.OnScreen(surface)) {
                    for (int i = 0; i < 2; i++) {
                        Dust dust = Dust.NewDustDirect(surface + new Vector2(Main.rand.NextFloat(-30f, 30f), -8f),
                            4, 4, DustID.Dirt, 0, 0, 90, default, Main.rand.NextFloat(1.4f, 2.2f));
                        dust.velocity = new Vector2(rakeDir * Main.rand.NextFloat(0.5f, 2f), -Main.rand.NextFloat(3f, 7f));
                    }
                }
                if (Timer % 9 == 0) {
                    EowMotionFX.CameraPunch(surface, 2f, 10, "EowRakeRumble");
                    SoundEngine.PlaySound(SoundID.WormDigQuiet with { Volume = 0.8f, Pitch = 0.1f, MaxInstances = 4 }, surface);
                }
            }

            //行进间歇泉：每隔固定间距在头顶地表拉一根(服务端)
            if (!VaultUtils.isClient && Math.Abs(npc.Center.X - lastGeyserX) >= GeyserSpacing) {
                lastGeyserX = npc.Center.X;
                Vector2 basePoint = EowMotionFX.FindGroundBelow(new Vector2(npc.Center.X, groundY - 300f));
                Projectile.NewProjectile(npc.GetSource_FromAI(), basePoint, Vector2.Zero,
                    ModContent.ProjectileType<EowGeyserProj>(),
                    EowSpitBarrageState.SpitDamage(npc), 0f, Main.myPlayer,
                    context.IsPhase2 ? 18f : 24f, context.IsPhase2 ? 1f : 0f);
            }

            //越过玩家足够远→折返
            bool passedFar = rakeDir > 0
                ? npc.Center.X > player.Center.X + 820f
                : npc.Center.X < player.Center.X - 820f;
            if (passedFar || Timer > PassMaxTime) {
                passesDone++;
                if (passesDone >= PassCount(context)) {
                    SwitchPhase(Phase.Exit);
                }
                else {
                    breachJumpFired = false;
                    SwitchPhase(Phase.Turn);
                }
            }
        }
        #endregion

        #region 折返海豚跃
        private void UpdateTurn(EowStateContext context) {
            NPC npc = context.Npc;

            context.SkipDefaultMovement = true;

            //前半：跃出地表的海豚弧(可打窗口+重心亮相)
            if (Timer == 1) {
                npc.velocity = new Vector2(rakeDir * 10f, -30f);
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
                npc.netUpdate = true;
            }
            if (!breachJumpFired && npc.Center.Y < groundY - 10f) {
                breachJumpFired = true;
                EowMotionFX.SpawnBreachBlast(new Vector2(npc.Center.X, groundY), 1.1f, -Vector2.UnitY);
                EowMotionFX.PlayRoar(npc.Center, 0.1f, 0.8f);
            }

            npc.velocity.Y += 1.6f;
            npc.velocity.X *= 0.98f;
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
            npc.damage = npc.velocity.Length() > 18f ? npc.defDamage : 0;

            //再度入土完成折返
            if (Timer > 12 && npc.Center.Y > groundY + 120f) {
                EowMotionFX.SpawnDirtBurst(new Vector2(npc.Center.X, groundY), 0.9f);
                rakeDir = -rakeDir;
                lastGeyserX = npc.Center.X + rakeDir * (GeyserSpacing * 0.5f); //折返错位半格，两趟篱笆交错
                context.SkipDefaultMovement = false;
                SwitchPhase(Phase.Rake);
                return;
            }
            if (Timer > TurnTime) {
                context.SkipDefaultMovement = false;
                SwitchPhase(Phase.Rake);
            }
        }
        #endregion

        #region 收场
        private void UpdateExit(EowStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            npc.damage = 0;
            context.SlitherStrength = 0.7f;
            int side = Math.Sign(npc.Center.X - player.Center.X);
            if (side == 0) {
                side = 1;
            }
            SetMovement(context, player.Center + new Vector2(side * 520f, -360f), 26f, 1.2f);
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
