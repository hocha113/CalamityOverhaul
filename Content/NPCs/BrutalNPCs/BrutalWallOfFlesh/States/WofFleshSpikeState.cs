using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Rendering;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.States
{
    /// <summary>
    /// 血肉尖刺场：肉髓钻入前方地脉，地板与顶板交错喷出尖刺波列——
    /// 地狱地形本身成为死线的爪牙。裂纹预告充分，走位穿越波列
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)WofStateIndex.FleshSpike, typeof(WofStateContext))]
    internal class WofFleshSpikeState : WofStateBase
    {
        public override string StateName => "FleshSpike";
        public override WofStateIndex StateIndex => WofStateIndex.FleshSpike;

        private const int SlamWindup = 32;
        private const int Recover = 60;

        public override void OnEnter(WofStateContext context) {
            base.OnEnter(context);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit18 with { Pitch = -0.7f, Volume = 1.1f }, context.Npc.Center);
            }
        }

        public override IWofState OnUpdate(WofStateContext context) {
            NPC npc = context.Npc;
            Timer++;
            int waveEnd = SlamWindup + WofDirector.SpikeWaveCount * WofDirector.SpikeWaveInterval;
            int totalEnd = waveEnd + Recover;

            if (Timer <= SlamWindup) {
                //夯地蓄势：墙身下沉发力
                float p = Timer / (float)SlamWindup;
                context.AdvanceFactor = 0.35f;
                context.MouthCommand = 2;
                context.WallFlush = 0.4f + 0.5f * p;
                if (Timer == SlamWindup - 6 && !VaultUtils.isServer) {
                    WofMotionFX.MouthRoar(npc, 1f);
                    WofMotionFX.CameraPunch(npc.Center, 6f, 14, "WofSpikeSlam");
                }
                return null;
            }

            if (Timer <= waveEnd) {
                context.AdvanceFactor = 0.6f;
                context.WallFlush = 0.6f;
                //波列推进：每隔一拍在更远处喷发一列
                int sinceSlam = Timer - SlamWindup;
                if (sinceSlam % WofDirector.SpikeWaveInterval == 1) {
                    int waveIndex = sinceSlam / WofDirector.SpikeWaveInterval;
                    SpawnSpikeWave(context, waveIndex);
                }
                return null;
            }

            context.AdvanceFactor = 0.85f;
            if (Timer >= totalEnd) {
                return new WofAdvanceState();
            }
            return null;
        }

        /// <summary>
        /// 生成一波尖刺(服务端)：地板列与顶板列交错半列距，
        /// 从墙面向推进方向逐波远去——死线的地脉在你脚下蔓延
        /// </summary>
        private void SpawnSpikeWave(WofStateContext context, int waveIndex) {
            NPC npc = context.Npc;
            if (VaultUtils.isClient) {
                return;
            }

            float faceX = WofWallField.WallFaceX(npc);
            float baseX = faceX + npc.direction * (300f + waveIndex * WofDirector.SpikeColumnSpacing);
            float middleY = WofWallField.MiddleY;
            int damage = WallOfFleshAI.ScaleDamage(npc, WofDirector.SpikeDamage);
            int spikeType = ModContent.ProjectileType<WofFleshSpikeProj>();

            //地板刺
            Vector2? ground = WofMotionFX.FindGroundBelow(new Vector2(baseX, middleY));
            if (ground.HasValue) {
                Projectile.NewProjectile(npc.GetSource_FromAI(), ground.Value, Vector2.Zero,
                    spikeType, damage, 0f, Main.myPlayer, -1f, waveIndex);
            }
            //顶板刺(错半列距，走位呈之字)
            float ceilX = baseX + npc.direction * WofDirector.SpikeColumnSpacing * 0.5f;
            Vector2? ceiling = WofMotionFX.FindCeilingAbove(new Vector2(ceilX, middleY));
            if (ceiling.HasValue) {
                Projectile.NewProjectile(npc.GetSource_FromAI(), ceiling.Value, Vector2.Zero,
                    spikeType, damage, 0f, Main.myPlayer, 1f, waveIndex);
            }
            npc.netUpdate = true;
        }
    }
}
