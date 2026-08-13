using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Rendering;
using InnoVault.PRT;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.States
{
    /// <summary>
    /// 死亡演出：碾磨刹停→双眼爆浆→饥饿者连环爆→墙体塌缩沉入岩浆→终吼放行真死。
    /// 真死走原版路径(硬模式开启/砖盒/掉落均保留)
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)WofStateIndex.Death, typeof(WofStateContext))]
    internal class WofDeathState : WofStateBase
    {
        public override string StateName => "Death";
        public override WofStateIndex StateIndex => WofStateIndex.Death;

        private const int HaltEnd = 60;
        private const int EyeBurstFrame = 78;
        private const int HungryPopStart = 106;
        private const int HungryPopInterval = 9;
        private const int CollapseStart = 118;
        private const int ScreamFrame = 236;
        private const int TotalTime = 268;

        /// <summary>塌缩起始时的墙域快照(各端本地)</summary>
        private int collapseTop;
        private int collapseBottom;
        private bool collapseSeeded;

        public override void OnEnter(WofStateContext context) {
            base.OnEnter(context);
            collapseSeeded = false;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCDeath10 with { Pitch = -0.55f, Volume = 1.1f }, context.Npc.Center);
            }
        }

        public override IWofState OnUpdate(WofStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            npc.dontTakeDamage = true;
            npc.damage = 0;
            context.MouthCommand = Timer >= ScreamFrame ? 1 : 2;

            //碾磨刹停
            float halt = MathHelper.Clamp(1f - Timer / (float)HaltEnd, 0f, 1f);
            context.SpeedOverride = 2.6f * halt * halt;
            context.WallFlush = MathHelper.Clamp(1.2f - Timer / (float)TotalTime, 0f, 1f);

            if (Timer <= HaltEnd && !VaultUtils.isServer) {
                if (Timer % 5 == 0) {
                    WofMotionFX.CameraPunch(npc.Center, 3f * halt + 1f, 8, "WofDeathHalt");
                }
                WofMotionFX.SpawnWallSeep(npc, 3f);
            }

            //双眼爆浆
            if (Timer == EyeBurstFrame) {
                BurstEyes(context);
            }

            //饥饿者连环爆
            if (Timer >= HungryPopStart && Timer < CollapseStart + 60
                && (Timer - HungryPopStart) % HungryPopInterval == 0) {
                PopOneHungry(context);
            }

            //墙体塌缩：上缘沉降，血肉沉回岩浆
            if (Timer >= CollapseStart) {
                UpdateCollapse(context);
            }

            //终吼
            if (Timer == ScreamFrame && !VaultUtils.isServer) {
                WofMotionFX.MouthRoar(npc, 1.8f);
                WofMotionFX.CameraPunch(npc.Center, 11f, 26, "WofDeathScream");
                for (int i = 0; i < 16; i++) {
                    float y = Main.rand.NextFloat(WofWallField.Top, WofWallField.Bottom);
                    WofMotionFX.SpawnBloodBurst(new Vector2(WofWallField.WallFaceX(npc), y), 1.2f,
                        new Vector2(npc.direction, Main.rand.NextFloat(-0.5f, 0.5f)));
                }
            }

            //落幕前一拍关滤镜(墙移除后不再有AI帧可渐出)
            if (Timer >= TotalTime - 1) {
                WallOfFleshAI.ShutdownFilter();
            }

            //落幕：服务端放行真死，触发原版硬模式与掉落
            if (Timer >= TotalTime && !VaultUtils.isClient) {
                context.DeathPerformanceFinished = true;
                npc.dontTakeDamage = false;
                npc.life = 0;
                npc.HitEffect();
                npc.checkDead();
                npc.netUpdate = true;
            }
            return null;
        }

        /// <summary>双眼位置爆浆并移除眼部件(服务端移除，各端本地演出)</summary>
        private void BurstEyes(WofStateContext context) {
            NPC npc = context.Npc;
            foreach (var n in Main.ActiveNPCs) {
                if (n.type != NPCID.WallofFleshEye) {
                    continue;
                }
                if (!VaultUtils.isServer) {
                    WofMotionFX.SpawnBloodBurst(n.Center, 1.6f, new Vector2(npc.direction, 0f));
                    SoundEngine.PlaySound(SoundID.NPCDeath12 with { Pitch = -0.3f }, n.Center);
                    for (int i = 0; i < 5; i++) {
                        PRTLoader.NewParticle<PRT_WofGore>(n.Center, VaultUtils.RandVr(3f, 9f),
                            WofMotionFX.BloodDark, Main.rand.NextFloat(0.3f, 0.6f))?.Configure(Main.rand.Next(50, 90));
                    }
                }
                if (!VaultUtils.isClient) {
                    n.life = 0;
                    n.active = false;
                    n.netUpdate = true;
                }
            }
        }

        /// <summary>每拍引爆一只饥饿者</summary>
        private void PopOneHungry(WofStateContext context) {
            List<NPC> hungries = context.CollectHungries();
            if (hungries.Count == 0) {
                return;
            }
            NPC victim = hungries[0];
            if (!VaultUtils.isServer) {
                WofMotionFX.SpawnBloodBurst(victim.Center, 1f);
                SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 0.8f }, victim.Center);
            }
            if (!VaultUtils.isClient) {
                victim.life = 0;
                victim.active = false;
                victim.netUpdate = true;
            }
        }

        /// <summary>墙域塌缩：上缘沉向下缘，口器随中线下沉</summary>
        private void UpdateCollapse(WofStateContext context) {
            NPC npc = context.Npc;
            if (!collapseSeeded) {
                collapseSeeded = true;
                collapseTop = Main.wofDrawAreaTop;
                collapseBottom = Main.wofDrawAreaBottom;
            }

            WofWallField.CinematicAreaLock = 1;
            context.SuppressYAnchor = true;

            float p = MathHelper.Clamp((Timer - CollapseStart) / (float)(TotalTime - CollapseStart), 0f, 1f);
            float ease = p * p;
            Main.wofDrawAreaBottom = collapseBottom;
            Main.wofDrawAreaTop = (int)MathHelper.Lerp(collapseTop, collapseBottom - 160, ease);

            float middle = (Main.wofDrawAreaTop + Main.wofDrawAreaBottom) * 0.5f - npc.height / 2;
            //口器随塌缩沉降，末段沉入地面
            npc.position.Y = middle + ease * ease * 120f;
            npc.velocity.Y = 0f;

            if (!VaultUtils.isServer && Timer % 3 == 0) {
                //塌缩上缘的崩解碎肉
                float x = npc.Center.X + Main.rand.NextFloat(-420f, 420f);
                PRTLoader.NewParticle<PRT_WofGore>(new Vector2(x, Main.wofDrawAreaTop + Main.rand.NextFloat(0f, 40f)),
                    new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(1f, 4f)),
                    WofMotionFX.BloodDark, Main.rand.NextFloat(0.3f, 0.6f))?.Configure(Main.rand.Next(40, 70));
                PRTLoader.NewParticle<PRT_WofBloodMist>(new Vector2(x, Main.wofDrawAreaTop),
                    new Vector2(0f, Main.rand.NextFloat(0.5f, 1.5f)),
                    WofMotionFX.BloodDark, Main.rand.NextFloat(1f, 1.8f))?.Configure(Main.rand.Next(45, 70), 0.55f);
            }
        }
    }
}
