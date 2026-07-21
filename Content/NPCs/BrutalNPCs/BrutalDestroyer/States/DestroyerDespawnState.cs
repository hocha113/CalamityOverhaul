using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States
{
    /// <summary>脱战状态</summary>
    [InnoVault.StateMachines.VaultState((int)DestroyerStateIndex.Despawn, typeof(DestroyerStateContext))]
    internal class DestroyerDespawnState : DestroyerStateBase
    {
        public override string StateName => "Despawn";
        public override DestroyerStateIndex StateIndex => DestroyerStateIndex.Despawn;
        /// <summary>脱战关远距瞬移阀</summary>
        public override bool AllowFarSnap => false;

        public DestroyerDespawnState() {
        }

        public override void OnEnter(DestroyerStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
        }

        public override IDestroyerState OnUpdate(DestroyerStateContext context) {
            NPC npc = context.Npc;
            npc.velocity.Y = 82f;
            npc.dontTakeDamage = true;

            Timer++;
            if (Timer > 180) {
                if (!VaultUtils.isClient) {
                    npc.active = false;
                    npc.netUpdate = true;
                    DestroyerHeadAI.HandleDespawn();
                    DestroyerHeadAI.SendDespawn();
                }
            }

            return null;
        }
    }

    /// <summary>死亡演出，锁血→疏密殉爆→头终爆真死</summary>
    [InnoVault.StateMachines.VaultState((int)DestroyerStateIndex.Death, typeof(DestroyerStateContext))]
    internal class DestroyerDeathState : DestroyerStateBase
    {
        public override string StateName => "Death";
        public override DestroyerStateIndex StateIndex => DestroyerStateIndex.Death;
        /// <summary>死亡演出关远距瞬移阀</summary>
        public override bool AllowFarSnap => false;

        //演出节奏(帧)
        private const int PreludeTime = 50;//急停+漏火花
        private const int ChainTime = 185;//疏→密连环爆
        private const int FinaleHold = 15;//终爆后短持
        private const int FinaleStart = PreludeTime + ChainTime;
        private const int TotalTime = FinaleStart + FinaleHold;

        /// <summary>探针ai[3]死亡演出标记</summary>
        internal const float ProbeDeathPerformanceMarker = -2f;

        public DestroyerDeathState() {
        }

        public override void OnEnter(DestroyerStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            context.DeathPerformanceFinished = false;

            NPC npc = context.Npc;
            npc.dontTakeDamage = true;
            npc.damage = 0;
            if (npc.life < 1) {
                npc.life = 1;
            }

            context.RefreshBodySegments();
            FreezeWorm(context);
            PrepareProbesForDeathPerformance();

            //过载警报，本端播
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.8f, Volume = 0.9f }, npc.Center);
            SoundEngine.PlaySound(SoundID.NPCDeath14 with { Pitch = -0.6f, Volume = 0.7f }, npc.Center);
        }

        public override IDestroyerState OnUpdate(DestroyerStateContext context) {
            NPC npc = context.Npc;

            //锁血无伤无害
            npc.dontTakeDamage = true;
            npc.damage = 0;
            if (npc.life < 1) {
                npc.life = 1;
            }

            //头急停，躯体跟停
            npc.velocity *= 0.85f;
            if (npc.velocity.Length() < 0.1f) {
                npc.velocity = Vector2.Zero;
            }

            if (Timer % 15 == 0) {
                context.RefreshBodySegments();
            }
            KeepSegmentsHarmless(context);
            KeepProbesInDeathPerformance();

            //探针殉爆跟计时，服务端击杀
            if (Timer < FinaleStart) {
                UpdateProbeChainExplosions();
            }
            else if (Timer == FinaleStart && !VaultUtils.isClient) {
                ExplodeAllRemainingProbes(true);
            }

            //视觉本端生成
            if (!VaultUtils.isServer) {
                UpdatePerformanceVisuals(context);
            }

            Timer++;

            //服务端/单人放行真死
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

        #region 演出视觉

        private void UpdatePerformanceVisuals(DestroyerStateContext context) {
            if (Timer < PreludeTime) {
                //前奏漏火花+零星小爆
                if (Timer % 3 == 0) {
                    SpawnSparksOnVisibleSegments(context, 0.25f, 5f, new Color(255, 190, 70));
                }
                if (Timer % 6 == 0) {
                    SpawnBlastsOnVisibleSegments(context, 0.1f, 0f);
                }
                if (Timer % 4 == 0) {
                    SpawnSparksOnDeathPerformanceProbes(0.55f, 5f, new Color(255, 120, 120));
                }
            }
            else if (Timer < FinaleStart) {
                //连环爆，疏→密
                float chainProgress = (Timer - PreludeTime) / (float)ChainTime; //0→1
                int burstInterval = (int)MathHelper.Lerp(7f, 2f, chainProgress);
                if (Timer % Math.Max(burstInterval, 2) == 0) {
                    float perSegmentChance = MathHelper.Lerp(0.18f, 0.6f, chainProgress);
                    int blasts = SpawnBlastsOnVisibleSegments(context, perSegmentChance, chainProgress);
                    if (blasts > 0) {
                        DoScreenShake(context.Npc.Center, 4f + chainProgress * 7f, 12);
                    }
                }
                //持续火花
                if (Timer % 2 == 0) {
                    SpawnSparksOnVisibleSegments(context, 0.3f, 7f, Color.Orange);
                }
            }
            else if (Timer == FinaleStart) {
                //头终爆+全身连锁
                SpawnFinaleBlast(context);
            }
        }

        /// <summary>屏内按概率爆，回实际数</summary>
        private static int SpawnBlastsOnVisibleSegments(DestroyerStateContext context, float perSegmentChance, float intensity) {
            if (VaultUtils.isServer) {
                return 0;
            }
            int count = TrySpawnBlastAt(context.Npc, perSegmentChance, intensity);
            foreach (var seg in context.BodySegments) {
                if (seg.Alives()) {
                    count += TrySpawnBlastAt(seg, perSegmentChance, intensity);
                }
            }
            return count;
        }

        private static int TrySpawnBlastAt(NPC seg, float chance, float intensity) {
            if (Main.rand.NextFloat() >= chance) {
                return 0;
            }
            Vector2 pos = seg.Center + Main.rand.NextVector2Circular(seg.width * 0.55f, seg.height * 0.55f);
            if (!OnScreen(pos)) {
                return 0;
            }
            float scale = Main.rand.NextFloat(0.8f, 1.4f) * (0.85f + intensity * 0.6f);
            SpawnBlast(pos, scale, false);
            return 1;
        }

        /// <summary>一团殉爆(光+火花+余烬+烟+光+音)</summary>
        private static void SpawnBlast(Vector2 pos, float scale, bool isFinale) {
            if (VaultUtils.isServer) {
                return;
            }

            Color warm = Color.Lerp(new Color(255, 150, 50), new Color(255, 85, 35), Main.rand.NextFloat());

            //核心光团
            PRTLoader.NewParticle<PRT_MechExplosion>(pos, Main.rand.NextVector2Circular(1.5f, 1.5f), warm, scale)
                .Configure(Main.rand.Next(28, 40), warm);

            //火花
            int sparkCount = isFinale ? 46 : Main.rand.Next(4, 8);
            for (int i = 0; i < sparkCount; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(1f, 1f) * Main.rand.NextFloat(3f, 11f) * (isFinale ? 1.6f : scale);
                PRTLoader.NewParticle<PRT_Spark>(pos, vel,
                    Color.Lerp(Color.Orange, Color.LightGoldenrodYellow, Main.rand.NextFloat()),
                    Main.rand.NextFloat(1.0f, 1.8f)).Configure(true, Main.rand.Next(16, 30));
            }

            //余烬
            int emberCount = isFinale ? 26 : 2;
            for (int i = 0; i < emberCount; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(3.5f, 3.5f);
                PRTLoader.NewParticle<PRT_LavaFire>(pos + Main.rand.NextVector2Circular(22f, 22f) * scale, vel,
                    Color.White, Main.rand.NextFloat(0.8f, 1.4f) * scale).SetLifetime(20, 48);
            }

            //浓烟
            int smokeCount = isFinale ? 18 : 2;
            for (int i = 0; i < smokeCount; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(2f, 2f) - Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.8f);
                PRTLoader.NewParticle<PRT_Smoke>(pos, vel,
                    Color.Lerp(new Color(60, 56, 54), new Color(20, 18, 18), Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.8f, 1.4f) * scale)
                    .Configure(Main.rand.Next(45, 75), 0.7f, Main.rand.NextFloat(-0.05f, 0.05f));
            }

            Lighting.AddLight(pos, warm.ToVector3() * (isFinale ? 3.2f : 1.2f) * scale);

            //密集时按概率播爆音
            if (isFinale || Main.rand.NextBool(3)) {
                SoundEngine.PlaySound(SoundID.Item14 with {
                    Pitch = isFinale ? -0.5f : Main.rand.NextFloat(-0.2f, 0.35f),
                    Volume = isFinale ? 1f : 0.4f
                }, pos);
            }
        }

        /// <summary>屏内体节按概率喷火花</summary>
        private static void SpawnSparksOnVisibleSegments(DestroyerStateContext context, float perSegmentChance, float speed, Color color) {
            if (VaultUtils.isServer) {
                return;
            }
            foreach (var seg in context.BodySegments) {
                if (!seg.Alives() || Main.rand.NextFloat() >= perSegmentChance) {
                    continue;
                }
                Vector2 pos = seg.Center + Main.rand.NextVector2Circular(seg.width * 0.5f, seg.height * 0.5f);
                if (!OnScreen(pos)) {
                    continue;
                }
                Vector2 vel = Main.rand.NextVector2Circular(1f, 1f) * Main.rand.NextFloat(1f, speed);
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, color, Main.rand.NextFloat(0.7f, 1.3f))
                    .Configure(true, Main.rand.Next(12, 22));
            }
        }

        /// <summary>头终爆+全身连锁+震屏</summary>
        private void SpawnFinaleBlast(DestroyerStateContext context) {
            if (VaultUtils.isServer) {
                return;
            }
            NPC npc = context.Npc;

            SpawnBlast(npc.Center, 4.5f, true);

            foreach (var seg in context.BodySegments) {
                if (seg.Alives() && Main.rand.NextBool(2) && OnScreen(seg.Center)) {
                    SpawnBlast(seg.Center, Main.rand.NextFloat(1.6f, 2.8f), false);
                }
            }

            ExplodeAllRemainingProbes(true);

            DoScreenShake(npc.Center, 26f, 42);
            SoundEngine.PlaySound(SoundID.DD2_KoboldExplosion with { Volume = 1.1f, Pitch = -0.3f }, npc.Center);
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 1f }, npc.Center);
        }

        #endregion

        #region 辅助

        private static void FreezeWorm(DestroyerStateContext context) {
            context.Npc.velocity *= 0.5f;
            context.Npc.damage = 0;
            KeepSegmentsHarmless(context);
        }

        /// <summary>躯体无伤无害</summary>
        private static void KeepSegmentsHarmless(DestroyerStateContext context) {
            foreach (var seg in context.BodySegments) {
                if (seg.Alives()) {
                    seg.dontTakeDamage = true;
                    seg.damage = 0;
                    seg.velocity *= 0.85f;
                    if (seg.life < 1) {
                        seg.life = 1;
                    }
                    seg.timeLeft = 60;
                }
            }
        }

        /// <summary>探针入演出，ai[1]殉爆帧 ai[3]=Marker</summary>
        private static void PrepareProbesForDeathPerformance() {
            if (VaultUtils.isClient) {
                return;
            }

            foreach (var probe in Main.ActiveNPCs) {
                if (probe.type != NPCID.Probe) {
                    continue;
                }

                probe.ai[3] = ProbeDeathPerformanceMarker;
                probe.ai[0] = 0f;
                probe.ai[1] = PreludeTime + (probe.whoAmI * 11) % Math.Max(ChainTime - 15, 1);
                probe.velocity = Vector2.Zero;
                probe.damage = 0;
                probe.dontTakeDamage = true;
                if (probe.life < 1) {
                    probe.life = 1;
                }
                probe.timeLeft = 120;
                probe.netUpdate = true;
            }
        }

        /// <summary>探针僵直无害，计时镜像ai[2]</summary>
        private void KeepProbesInDeathPerformance() {
            foreach (var probe in Main.ActiveNPCs) {
                if (probe.type != NPCID.Probe || probe.ai[3] != ProbeDeathPerformanceMarker) {
                    continue;
                }
                if (probe.ai[0] >= 1f) {
                    continue;
                }

                probe.velocity *= 0.85f;
                if (probe.velocity.Length() < 0.1f) {
                    probe.velocity = Vector2.Zero;
                }
                probe.damage = 0;
                probe.dontTakeDamage = true;
                if (probe.life < 1) {
                    probe.life = 1;
                }
                probe.timeLeft = 120;
                probe.ai[2] = Timer;
            }
        }

        /// <summary>排定帧引爆探针</summary>
        private void UpdateProbeChainExplosions() {
            foreach (var probe in Main.ActiveNPCs) {
                if (probe.type != NPCID.Probe || probe.ai[3] != ProbeDeathPerformanceMarker || probe.ai[0] >= 1f) {
                    continue;
                }
                if (Timer != (int)probe.ai[1]) {
                    continue;
                }
                ExplodeProbe(probe, Main.rand.NextFloat(1.1f, 1.9f), false);
            }
        }

        /// <summary>终爆清剩余探针</summary>
        private static void ExplodeAllRemainingProbes(bool isFinale) {
            foreach (var probe in Main.ActiveNPCs) {
                if (probe.type != NPCID.Probe || probe.ai[3] != ProbeDeathPerformanceMarker || probe.ai[0] >= 1f) {
                    continue;
                }
                ExplodeProbe(probe, Main.rand.NextFloat(isFinale ? 1.6f : 1.1f, isFinale ? 2.6f : 1.9f), isFinale);
            }
        }

        private static void ExplodeProbe(NPC probe, float scale, bool isFinale) {
            if (probe.ai[0] >= 1f) {
                return;
            }

            if (!VaultUtils.isClient) {
                probe.ai[0] = 1f;
                probe.life = 0;
                probe.HitEffect();
                probe.active = false;
                probe.netUpdate = true;
            }

            if (!VaultUtils.isServer && OnScreen(probe.Center)) {
                SpawnBlast(probe.Center, scale, isFinale);
            }
        }

        /// <summary>僵直探针过载火花</summary>
        private static void SpawnSparksOnDeathPerformanceProbes(float chance, float speed, Color color) {
            if (VaultUtils.isServer) {
                return;
            }

            foreach (var probe in Main.ActiveNPCs) {
                if (probe.type != NPCID.Probe || probe.ai[3] != ProbeDeathPerformanceMarker || probe.ai[0] >= 1f) {
                    continue;
                }
                if (Main.rand.NextFloat() >= chance || !OnScreen(probe.Center)) {
                    continue;
                }

                Vector2 pos = probe.Center + Main.rand.NextVector2Circular(probe.width * 0.45f, probe.height * 0.45f);
                Vector2 vel = Main.rand.NextVector2Circular(1f, 1f) * Main.rand.NextFloat(1f, speed);
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, color, Main.rand.NextFloat(0.7f, 1.3f))
                    .Configure(true, Main.rand.Next(12, 22));
            }
        }

        /// <summary>ProbeAI 是否死亡演出僵直</summary>
        internal static bool IsProbeInDeathPerformance(NPC probe) {
            if (probe == null || probe.type != NPCID.Probe) {
                return false;
            }
            if (probe.ai[3] == ProbeDeathPerformanceMarker) {
                return probe.ai[0] < 1f;
            }

            //标记未同步时，头Death也算演出探针
            foreach (var npc in Main.ActiveNPCs) {
                if (npc.type == NPCID.TheDestroyer && npc.active
                    && (int)npc.ai[2] == (int)DestroyerStateIndex.Death) {
                    return true;
                }
            }
            return false;
        }

        private static void DoScreenShake(Vector2 pos, float strength, int time) {
            if (VaultUtils.isServer || !CWRServerConfig.Instance.ScreenVibration) {
                return;
            }
            PunchCameraModifier modifier = new PunchCameraModifier(pos, Main.rand.NextVector2Unit(),
                strength, 8f, time, 2400f, "DestroyerDeath");
            Main.instance.CameraModifiers.Add(modifier);
        }

        /// <summary>是否屏内(含边距)</summary>
        private static bool OnScreen(Vector2 worldPos, float margin = 260f) {
            return worldPos.X > Main.screenPosition.X - margin
                && worldPos.X < Main.screenPosition.X + Main.screenWidth + margin
                && worldPos.Y > Main.screenPosition.Y - margin
                && worldPos.Y < Main.screenPosition.Y + Main.screenHeight + margin;
        }

        #endregion
    }
}
