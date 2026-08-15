using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Rendering;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.States
{
    /// <summary>蜕皮转阶段：盘绕僵直→蜕皮波头至尾剥落旧甲→酸雾狂怒觉醒(二阶段开启)</summary>
    [InnoVault.StateMachines.VaultState((int)EowStateIndex.Molt, typeof(EowStateContext))]
    internal class EowMoltTransitionState : EowStateBase
    {
        public override string StateName => "Molt";
        public override EowStateIndex StateIndex => EowStateIndex.Molt;
        public override bool AllowFarSnap => false;

        #region 节奏常量
        private const int CoilTime = 40;
        private const int MoltWaveTime = 96;
        private const int AwakenFrame = CoilTime + MoltWaveTime + 16;
        private const int TotalTime = AwakenFrame + 34;
        #endregion

        private bool awakened;

        public EowMoltTransitionState() {
        }

        public override void OnEnter(EowStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            context.MoltDone = true;
            awakened = false;

            NPC npc = context.Npc;
            npc.dontTakeDamage = true;
            npc.damage = 0;

            context.RefreshSegments();
            SoundEngine.PlaySound(SoundID.NPCDeath10 with { Pitch = -0.65f, Volume = 0.85f }, npc.Center);
        }

        public override IEowState OnUpdate(EowStateContext context) {
            NPC npc = context.Npc;

            Tick();
            npc.dontTakeDamage = true;
            npc.damage = 0;
            context.MiasmaLevel = MathHelper.Clamp(Timer / (float)CoilTime, 0f, 1f) * 0.55f;

            //幕一 盘绕僵直：缓慢减速蜷缩
            if (Timer <= CoilTime) {
                npc.velocity *= 0.92f;
                npc.rotation = npc.rotation.AngleLerp(npc.rotation + 0.05f, 0.4f);
                float coilT = Timer / (float)CoilTime;
                context.Compression = MathHelper.Lerp(1f, 0.72f, coilT);
                context.PulseKind = 2;
                context.PulsePhase = 0f;

                if (Timer == CoilTime / 2) {
                    EowMotionFX.PlayRoar(npc.Center, -0.6f, 0.9f);
                }
                return null;
            }

            //幕二 蜕皮波：头→尾剥落(体节各自本地弹壳，见 EowBodyAI)
            if (Timer <= CoilTime + MoltWaveTime) {
                npc.velocity *= 0.96f;
                float waveT = (Timer - CoilTime) / (float)MoltWaveTime;
                context.PulseKind = 2;
                context.PulsePhase = waveT;
                context.Compression = MathHelper.Lerp(0.72f, 0.95f, waveT);

                if (Timer % 11 == 0) {
                    EowMotionFX.CameraPunch(npc.Center, 1.8f, 10, "EowMolt");
                    SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.6f, Pitch = -0.2f, MaxInstances = 4 }, npc.Center);
                }
                return null;
            }

            //幕三 觉醒：酸怒爆发
            context.PulseKind = 2;
            context.PulsePhase = 1f;
            if (Timer >= AwakenFrame && !awakened) {
                awakened = true;
                context.IsPhase2 = true;
                context.Compression = 1f;

                EowMotionFX.PlayRoar(npc.Center, 0.3f, 1.25f);
                EowMotionFX.SpawnAcidBurst(npc.Center, 2.2f);
                EowMotionFX.CameraPunch(npc.Center, 8f, 20, "EowAwaken");
                //全身酸雾亮相(客户端)
                if (!VaultUtils.isServer) {
                    foreach (var seg in context.Segments) {
                        if (seg.Alives() && EowMotionFX.OnScreen(seg.Center) && Main.rand.NextBool(3)) {
                            EowMotionFX.SpawnAcidBurst(seg.Center, 0.7f);
                        }
                    }
                }
            }

            if (Timer >= TotalTime) {
                return new EowWeaveState();
            }
            return null;
        }

        public override void OnExit(EowStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
            context.IsPhase2 = true;
            context.Npc.dontTakeDamage = false;
            context.Npc.damage = context.Npc.defDamage;
        }
    }
}
