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
    /// <summary>酸雨播撒(二阶段限定)：高空横掠，全身体节向上喷酸，酸雨帘从天而降</summary>
    [InnoVault.StateMachines.VaultState((int)EowStateIndex.AcidRain, typeof(EowStateContext))]
    internal class EowAcidRainState : EowStateBase
    {
        public override string StateName => "AcidRain";
        public override EowStateIndex StateIndex => EowStateIndex.AcidRain;

        #region 节奏常量
        private const int ClimbMaxTime = 80;
        private const int PassTime = 66;
        private const int TurnTime = 34;
        private const int ExitTime = 30;
        private const float PassAltitude = 560f;
        #endregion

        private float PassSpeed(EowStateContext ctx) => 33f + (ctx.IsDeathMode ? 4f : 0f);

        private enum Phase
        {
            Climb = 0,
            Pass = 1,
            Turn = 2,
            Exit = 3,
        }

        private Phase phase;
        private int passesDone;
        private int passDir;

        public EowAcidRainState() {
        }

        public override void OnEnter(EowStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = false;
            phase = Phase.Climb;
            passesDone = 0;
            //状态恢复瞬间 Target 可能未就绪
            passDir = context.Target.Alives() && context.Npc.Center.X < context.Target.Center.X ? 1 : -1;
            EowMotionFX.PlayRoar(context.Npc.Center, -0.25f, 0.9f);
        }

        public override IEowState OnUpdate(EowStateContext context) {
            Tick();
            switch (phase) {
                case Phase.Climb:
                    UpdateClimb(context);
                    break;
                case Phase.Pass:
                    UpdatePass(context);
                    break;
                case Phase.Turn:
                    UpdateTurn(context);
                    break;
                case Phase.Exit:
                    UpdateExit(context);
                    if (Timer > ExitTime) {
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

        #region 爬升
        private void UpdateClimb(EowStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            npc.damage = 0;
            Vector2 highAnchor = player.Center + new Vector2(-passDir * 940f, -PassAltitude);
            SetMovement(context, highAnchor, 30f, 1.5f);
            context.AccelRate = 0.1f;
            context.SlitherStrength = 0.5f;

            if (npc.WithinRange(highAnchor, 150f) || Timer > ClimbMaxTime) {
                SwitchPhase(Phase.Pass);
                SoundEngine.PlaySound(SoundID.Zombie7 with { Pitch = 0.1f, Volume = 0.9f }, npc.Center);
            }
        }
        #endregion

        #region 高空横掠喷洒
        private void UpdatePass(EowStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            context.SkipDefaultMovement = true;
            //横掠：定高微波动直线
            float bob = (float)Math.Sin(Timer * 0.14f) * 1.6f;
            npc.velocity = new Vector2(passDir * PassSpeed(context), bob);
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
            npc.damage = 0;
            context.MawGlow = 0.7f;

            //全身向上喷酸→酸雨(服务端)；喷口沿身错开
            int cadence = context.IsDeathMode ? 2 : 3;
            if (!VaultUtils.isClient && Timer % cadence == 0 && context.Segments.Count > 10) {
                int ordinal = Main.rand.Next(4, context.Segments.Count - 4);
                NPC seg = context.Segments[ordinal];
                if (seg.Alives()) {
                    Vector2 vel = new Vector2(Main.rand.NextFloat(-2.2f, 2.2f), -Main.rand.NextFloat(7f, 10f));
                    Projectile.NewProjectile(seg.GetSource_FromAI(), seg.Center, vel,
                        ModContent.ProjectileType<EowAcidGlob>(),
                        (int)(EowSpitBarrageState.SpitDamage(npc) * 0.7f), 0f, Main.myPlayer, 1f);
                }
            }

            //体节喷吐表现(客户端)
            if (!VaultUtils.isServer && Timer % 3 == 0 && context.Segments.Count > 10) {
                NPC seg = context.Segments[Main.rand.Next(context.Segments.Count)];
                if (seg.Alives() && EowMotionFX.OnScreen(seg.Center)) {
                    EowMotionFX.SpawnAcidBurst(seg.Center - new Vector2(0, 10f), 0.45f, -Vector2.UnitY * 3f);
                }
            }

            //蓄势波沿链滚动(表现)
            context.PulseKind = 1;
            context.PulsePhase = (Timer % 30) / 30f;

            if (Timer >= PassTime) {
                passesDone++;
                if (passesDone >= 2) {
                    SwitchPhase(Phase.Exit);
                }
                else {
                    SwitchPhase(Phase.Turn);
                }
            }
        }
        #endregion

        #region 折返
        private void UpdateTurn(EowStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            context.SkipDefaultMovement = false;
            npc.damage = 0;
            passDir = -passDir;
            Vector2 turnAnchor = player.Center + new Vector2(-passDir * 940f, -PassAltitude - 90f);
            SetMovement(context, turnAnchor, 30f, 1.6f);
            context.SlitherStrength = 0.4f;

            if (npc.WithinRange(turnAnchor, 170f) || Timer > TurnTime + 30) {
                SwitchPhase(Phase.Pass);
            }
        }
        #endregion

        #region 收场
        private void UpdateExit(EowStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            context.SkipDefaultMovement = false;
            npc.damage = npc.defDamage;
            SetMovement(context, player.Center + new Vector2(0f, -380f), 22f, 1.2f);
            context.SlitherStrength = 0.7f;
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
