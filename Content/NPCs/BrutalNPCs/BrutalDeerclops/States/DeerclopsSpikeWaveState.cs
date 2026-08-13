using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.States
{
    /// <summary>
    /// 冰刺波列：两次跺脚。第一波自身前向外行进，第二波自远处向内回卷(错拍)，
    /// 二阶段追加背后第三波。波内留跳跃窗口
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)DeerclopsStateIndex.SpikeWave, typeof(DeerclopsStateContext))]
    internal class DeerclopsSpikeWaveState : DeerclopsStateBase
    {
        public override string StateName => "SpikeWave";
        public override DeerclopsStateIndex StateIndex => DeerclopsStateIndex.SpikeWave;

        private const int Slam1 = 36;
        private const int Stomp2Start = 66;
        private const int Slam2 = Stomp2Start + 36;
        private const int StateEnd = 152;
        private const int Columns = 18;

        public override IDeerclopsState OnUpdate(DeerclopsStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            context.HaltMovement = true;
            npc.damage = 0;
            FaceTarget(context);

            //两段跺脚动画
            if (Timer <= Stomp2Start) {
                context.AnimMode = DeerAnimMode.Stomp;
                context.AnimTimer = Timer;
            }
            else if (Timer <= Slam2 + 12) {
                context.AnimMode = DeerAnimMode.Stomp;
                context.AnimTimer = Timer - Stomp2Start;
            }

            if (Timer == Slam1) {
                DoSlamFeedback(npc, 7f);
                SpawnWaveOutward(context);
            }

            if (Timer == Slam2) {
                DoSlamFeedback(npc, 8f);
                SpawnWaveInward(context);
                if (context.IsPhase2) {
                    SpawnWaveBehind(context);
                }
            }

            if (Timer >= StateEnd) {
                return new DeerclopsStalkState();
            }
            return null;
        }

        private static void DoSlamFeedback(NPC npc, float strength) {
            DeerclopsMotion.CameraPunch(npc.Bottom, strength, 22, "DeerSpikeSlam", Vector2.UnitY);
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.DeerclopsRubbleAttack with { Volume = 0.9f }, npc.Bottom);
                for (int i = 0; i < 10; i++) {
                    Dust dust = Dust.NewDustPerfect(npc.Bottom + new Vector2(Main.rand.NextFloat(-50f, 50f), 0f),
                        DustID.Snow, new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(1f, 4f)), 80, default, Main.rand.NextFloat(1f, 1.8f));
                    dust.noGravity = Main.rand.NextBool();
                }
            }
        }

        /// <summary>波一：自脚边向外行进的破土前沿</summary>
        private void SpawnWaveOutward(DeerclopsStateContext context) {
            if (VaultUtils.isClient) {
                return;
            }
            NPC npc = context.Npc;
            int dir = DirToTarget(context);
            Point feet = npc.Bottom.ToTileCoordinates();
            int damage = context.IsDeathMode ? 20 : 16;
            float scaleMult = context.IsPhase2 ? 1.18f : 1f;

            for (int i = 0; i < Columns; i++) {
                int tileX = feet.X + dir * (3 + i);
                float lean = dir * i * 0.7f * (MathHelper.PiOver4 / Columns);
                float scale = MathHelper.Clamp(0.55f + i * 0.05f, 0.5f, 1.45f) * scaleMult;
                int telegraph = TelegraphTime(context, 14 + i * 2, 10);
                DeerIceSpikeProj.TrySpawn(npc, tileX, feet.Y, lean, scale, telegraph, damage);
            }
        }

        /// <summary>波二：自远处向内回卷，留跳跃缺口</summary>
        private void SpawnWaveInward(DeerclopsStateContext context) {
            if (VaultUtils.isClient) {
                return;
            }
            NPC npc = context.Npc;
            int dir = DirToTarget(context);
            Point feet = npc.Bottom.ToTileCoordinates();
            int damage = context.IsDeathMode ? 20 : 16;
            float scaleMult = context.IsPhase2 ? 1.18f : 1f;

            for (int i = 0; i < Columns; i++) {
                //缺口列：可读的安全窗
                if (i % 6 == 5) {
                    continue;
                }
                int tileX = feet.X + dir * (3 + i);
                float lean = -dir * i * 0.5f * (MathHelper.PiOver4 / Columns);
                float scale = MathHelper.Clamp(0.6f + i * 0.045f, 0.5f, 1.4f) * scaleMult;
                int telegraph = TelegraphTime(context, 14 + (Columns - 1 - i) * 2, 10);
                DeerIceSpikeProj.TrySpawn(npc, tileX, feet.Y, lean, scale, telegraph, damage);
            }
        }

        /// <summary>波三(二阶段)：背后补刀，惩罚绕背贴身</summary>
        private void SpawnWaveBehind(DeerclopsStateContext context) {
            if (VaultUtils.isClient) {
                return;
            }
            NPC npc = context.Npc;
            int dir = -DirToTarget(context);
            Point feet = npc.Bottom.ToTileCoordinates();
            int damage = context.IsDeathMode ? 20 : 16;

            for (int i = 0; i < 12; i++) {
                int tileX = feet.X + dir * (3 + i);
                float lean = dir * i * 0.6f * (MathHelper.PiOver4 / 12f);
                float scale = MathHelper.Clamp(0.5f + i * 0.05f, 0.45f, 1.15f);
                int telegraph = TelegraphTime(context, 22 + i * 2, 14);
                DeerIceSpikeProj.TrySpawn(npc, tileX, feet.Y, lean, scale, telegraph, damage);
            }
        }
    }
}
