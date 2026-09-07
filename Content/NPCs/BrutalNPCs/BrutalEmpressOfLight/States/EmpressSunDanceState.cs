using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Projectiles;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.States
{
    /// <summary>
    /// 日舞：上浮敛势 → 按手写偏角表逐发追踪光束（宽预告窄命中）。
    /// 夜表由疏到密收到脚下；昼一阶段三叉戟固定形；昼二阶段五束扇左右错 10° 呼吸，|偏角|≥40° 变绊线
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EmpressStateIndex.SunDance, typeof(EmpressStateContext))]
    internal class EmpressSunDanceState : EmpressStateBase
    {
        public override string StateName => "EmpressSunDance";
        public override EmpressStateIndex StateIndex => EmpressStateIndex.SunDance;

        //偏角表（度）：每发一行
        private static readonly float[][] NightTable = [
            [0f], [-22.5f, 22.5f], [-32f, 0f, 32f], [-1.5f, -8f], [1.5f, 8f], [-2f, 2f],
        ];
        private static readonly float[][] DayTable = [
            [-50f, 0f, 50f], [-42f, 0f, 42f], [-42f, 0f, 42f], [-42f, 0f, 42f], [-42f, 0f, 42f], [-6f, 0f, 6f],
        ];
        private static readonly float[][] DayPhase2Table = [
            [-45f, -22.5f, 0f, 22.5f, 45f],
            [-45f, -17.5f, 5f, 27.5f, 45f],
            [-45f, -27.5f, -5f, 17.5f, 45f],
            [-45f, -17.5f, 5f, 27.5f, 45f],
            [-45f, -22.5f, 0f, 22.5f, 45f],
            [-45f, -27.5f, -5f, 17.5f, 45f],
            [-5f, 0f, 5f],
        ];

        private int WindupTime => Context.IsSecondPhase ? 45 : 20;
        private int Interval => Context.DayEmpowered ? (Context.IsSecondPhase ? 40 : 45) : 50;
        private float[][] Table => Context.DayEmpowered ? (Context.IsSecondPhase ? DayPhase2Table : DayTable) : NightTable;
        private int TotalTime => WindupTime + Table.Length * Interval + EmpressTrackBeam.MarkerTime + EmpressTrackBeam.BeamTime + 20;

        private EmpressStateContext Context;

        public override void OnEnter(EmpressStateContext context) {
            base.OnEnter(context);
            Context = context;
        }

        public override IEmpressState OnUpdate(EmpressStateContext context) {
            Context = context;
            NPC npc = context.Npc;
            Player target = context.Target;
            Timer++;

            context.Pose = EmpressPose.Dance;
            context.PoseTimer = MathHelper.Clamp(Timer, 10f, 170f);

            if (Timer <= WindupTime) {
                //起手上浮 (1-t)²，双臂由 Floating 切到日舞姿势
                float t = Timer / (float)WindupTime;
                npc.velocity.Y -= (1f - t) * (1f - t) * (context.IsSecondPhase ? 0.09f : 0.05f);
                npc.velocity.X *= 0.9f;
                context.SetChargeState(3, t * 0.6f);
                if (!VaultUtils.isServer) {
                    EmpressScreenFX.DeclareAmbient(0.25f + t * 0.25f);
                }
                if (Timer == 1) {
                    PlayLocal(SoundID.Item159 with { Volume = 0.8f, Pitch = -0.1f }, npc.Center);
                }
                return null;
            }

            //几乎静场：只随呼吸微沉，光束的转动是唯一的动
            npc.velocity *= 0.9f;
            if (target.Alives()) {
                npc.velocity.Y += (target.Center.Y - 380f - npc.Center.Y) * 0.0008f;
            }
            if (!VaultUtils.isServer) {
                EmpressScreenFX.DeclareAmbient(0.5f);
            }

            int castTick = Timer - WindupTime;
            if (castTick % Interval == 0 && target.Alives()) {
                int volley = castTick / Interval;
                float[][] table = Table;
                if (volley < table.Length) {
                    CastVolley(context, npc, target, table[volley], volley);
                }
            }

            EmpressMotion.AmbientGlow(npc, context.DayFormBlend);
            if (Timer >= TotalTime) {
                return new EmpressConnectorState();
            }
            return null;
        }

        private void CastVolley(EmpressStateContext context, NPC npc, Player target, float[] devs, int volley) {
            PlayLocal(SoundID.Item159 with { Volume = 0.9f, Pitch = 0.05f * volley }, npc.Center);
            EmpressMotion.Shake(npc.Center, 2.5f, 10);
            if (VaultUtils.isClient) {
                return;
            }
            Vector2 toTarget = (target.Center - npc.Center).SafeNormalize(Vector2.UnitY);
            bool day = context.DayEmpowered;
            //昼二阶段：非首发且 |偏角|≥40 的束变绊线（碰标记即发）
            bool tripwires = day && context.IsSecondPhase && volley != 0;
            for (int i = 0; i < devs.Length; i++) {
                float dev = devs[i];
                //夜表按玩家在左右镜像，收束方向永远朝人
                if (!day && toTarget.X < 0f) {
                    dev = -dev;
                }
                float angle = toTarget.ToRotation() + MathHelper.ToRadians(dev);
                EmpressBeamMode mode = tripwires && Math.Abs(devs[i]) >= 40f ? EmpressBeamMode.Tripwire : EmpressBeamMode.Normal;
                EmpressCast.Beam(npc, npc.Center + angle.ToRotationVector2() * 100f, angle, context.BeamDamage, mode);
            }
        }
    }
}
