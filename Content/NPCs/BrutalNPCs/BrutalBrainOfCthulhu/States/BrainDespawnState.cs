using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.States
{
    /// <summary>脱战：心跳渐远，身影沉入血雾散去</summary>
    [InnoVault.StateMachines.VaultState((int)BrainStateIndex.Despawn, typeof(BrainStateContext))]
    internal class BrainDespawnState : BrainStateBase
    {
        public override string StateName => "Despawn";
        public override BrainStateIndex StateIndex => BrainStateIndex.Despawn;
        public override bool AllowFarSnap => false;

        private const int FadeTime = 140;

        public BrainDespawnState() {
        }

        public override void OnEnter(BrainStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            context.Invulnerable = true;
            npc.damage = 0;

            if (!VaultUtils.isClient) {
                BrainProjectileUtils.ClearBrainProjectiles();
            }
        }

        public override IBrainState OnUpdate(BrainStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            context.Invulnerable = true;
            npc.damage = 0;
            npc.velocity = Vector2.Lerp(npc.velocity, Vector2.UnitY * 4f, 0.04f);

            float t = MathHelper.Clamp(Timer / (float)FadeTime, 0f, 1f);
            //身影散去+心跳远离
            context.GhostFade = 1f - t;
            context.BeatIntensity = 0.4f * (1f - t);
            context.BeatPeriod = (int)MathHelper.Lerp(54f, 96f, t);

            if (!VaultUtils.isServer && Timer % 6 == 0 && BrainMotion.OnScreen(npc.Center)) {
                BrainMotion.BloodMistBurst(npc.Center + Main.rand.NextVector2Circular(60f, 40f), 0.6f, 1, 2f);
            }

            //飞眼随之四散
            if (!VaultUtils.isClient && Timer == 10) {
                foreach (var creeper in context.Creepers) {
                    if (creeper.Alives()) {
                        BrainCreeperAI.CommandScatter(creeper);
                    }
                }
            }

            if (Timer > FadeTime && !VaultUtils.isClient) {
                foreach (var creeper in context.Creepers) {
                    if (creeper.Alives()) {
                        creeper.active = false;
                        creeper.netUpdate = true;
                    }
                }
                npc.active = false;
                npc.netUpdate = true;
            }

            return null;
        }
    }

    /// <summary>死亡演出：心律失常→惊惶加速→骤停死寂→心核终爆</summary>
    [InnoVault.StateMachines.VaultState((int)BrainStateIndex.Death, typeof(BrainStateContext))]
    internal class BrainDeathState : BrainStateBase
    {
        public override string StateName => "Death";
        public override BrainStateIndex StateIndex => BrainStateIndex.Death;
        public override bool AllowFarSnap => false;

        #region 节奏常量
        private const int ArrhythmiaEnd = 95;    //心律失常
        private const int PanicEnd = 215;        //惊惶加速
        private const int FlatlineEnd = 270;     //骤停死寂
        private const int TotalTime = 332;       //终爆后余韵

        /// <summary>惊惶段手调心跳帧表（相对 ArrhythmiaEnd）</summary>
        private static readonly int[] PanicBeats = [0, 34, 62, 85, 103, 117, 128, 137, 144, 150];
        /// <summary>失常段错拍帧表</summary>
        private static readonly int[] ArrhythmiaBeats = [8, 46, 60, 92];
        #endregion

        private int nextPanicBeat;
        private int nextArrhythmiaBeat;

        public BrainDeathState() {
        }

        public override void OnEnter(BrainStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            context.DeathPerformanceFinished = false;
            context.Invulnerable = true;
            npc.damage = 0;
            if (npc.life < 1) {
                npc.life = 1;
            }
            nextPanicBeat = 0;
            nextArrhythmiaBeat = 0;

            if (!VaultUtils.isClient) {
                BrainProjectileUtils.ClearBrainProjectiles();
            }
            BrainMotion.Roar(npc.Center, 1f, -0.55f, true);
        }

        public override IBrainState OnUpdate(BrainStateContext context) {
            NPC npc = context.Npc;

            context.Invulnerable = !context.DeathPerformanceFinished;
            npc.damage = 0;
            if (npc.life < 1) {
                npc.life = 1;
            }
            npc.velocity *= 0.86f;

            //整场接管心跳：自动节拍静默，全部走脚本拍
            context.BeatSilenced = true;

            if (Timer % 15 == 0) {
                context.RefreshCreepers();
            }

            //幕一：心律失常——错拍+飞眼逐只殉爆
            if (Timer < ArrhythmiaEnd) {
                UpdateArrhythmia(context);
            }
            //幕二：惊惶加速
            else if (Timer < PanicEnd) {
                UpdatePanic(context);
            }
            //幕三：骤停死寂
            else if (Timer < FlatlineEnd) {
                UpdateFlatline(context);
            }
            //终爆帧
            else if (Timer == FlatlineEnd) {
                DoFinalBurst(context);
            }
            //余韵：血雨飘落
            else {
                context.BlackoutTarget = 0f;
                if (!VaultUtils.isServer && Timer % 3 == 0 && BrainMotion.OnScreen(npc.Center)) {
                    Vector2 pos = npc.Center + new Vector2(Main.rand.NextFloat(-320f, 320f), Main.rand.NextFloat(-260f, -80f));
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, Vector2.UnitY * Main.rand.NextFloat(1f, 3f),
                        BrainMotion.BloodDark, Main.rand.NextFloat(0.9f, 1.6f))?.Configure(Main.rand.Next(30, 55), 0.4f);
                }
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

        #region 各幕

        private void UpdateArrhythmia(BrainStateContext context) {
            NPC npc = context.Npc;

            //错拍心跳
            if (nextArrhythmiaBeat < ArrhythmiaBeats.Length && Timer == ArrhythmiaBeats[nextArrhythmiaBeat]) {
                nextArrhythmiaBeat++;
                BrainHeartbeat.Thump(0.9f, 0.9f);
                BrainHeartbeat.PlayThumpSound(npc.Center, 0.9f, Main.rand.NextFloat(-0.14f, 0.2f));
                npc.velocity += Main.rand.NextVector2Circular(2.4f, 2.4f);
                if (!VaultUtils.isServer) {
                    BrainMotion.BloodMistBurst(npc.Center + Main.rand.NextVector2Circular(50f, 40f), 0.9f, 5, 6f);
                }
            }

            context.TelegraphGlow = 0.4f + 0.3f * BrainHeartbeat.Pulse;

            //飞眼逐只殉爆（服务端）
            if (!VaultUtils.isClient && Timer % 14 == 6) {
                foreach (var creeper in context.Creepers) {
                    if (creeper.Alives()) {
                        creeper.life = 0;
                        creeper.HitEffect();
                        creeper.active = false;
                        creeper.netUpdate = true;
                        break;
                    }
                }
            }

            //渗血
            if (!VaultUtils.isServer && Timer % 4 == 0 && BrainMotion.OnScreen(npc.Center)) {
                Vector2 pos = npc.Center + Main.rand.NextVector2Circular(70f, 55f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, Vector2.UnitY * Main.rand.NextFloat(0.5f, 2f),
                    BrainMotion.BloodBright, Main.rand.NextFloat(0.7f, 1.2f))?.Configure(Main.rand.Next(22, 40), 0.36f);
            }
        }

        private void UpdatePanic(BrainStateContext context) {
            NPC npc = context.Npc;
            int local = Timer - ArrhythmiaEnd;

            if (nextPanicBeat < PanicBeats.Length && local == PanicBeats[nextPanicBeat]) {
                nextPanicBeat++;
                float k = nextPanicBeat / (float)PanicBeats.Length;
                BrainHeartbeat.Thump(0.85f + k * 0.55f, 0.92f);
                BrainHeartbeat.PlayThumpSound(npc.Center, 0.75f + k * 0.35f, k * 0.4f);
                npc.velocity += Main.rand.NextVector2Circular(1.5f + k * 3f, 1.5f + k * 3f);
                if (!VaultUtils.isServer) {
                    BrainMotion.BloodMistBurst(npc.Center + Main.rand.NextVector2Circular(60f, 45f), 0.7f + k * 0.7f, 4 + (int)(k * 6), 7f);
                    BrainMotion.Shake(npc.Center, 2f + k * 4f, 10);
                }
            }

            float progress = local / (float)(PanicEnd - ArrhythmiaEnd);
            context.TelegraphGlow = 0.5f + 0.5f * BrainHeartbeat.Pulse;
            context.BlackoutTarget = progress * 0.35f;
        }

        private void UpdateFlatline(BrainStateContext context) {
            NPC npc = context.Npc;
            npc.velocity = Vector2.Zero;

            //彻底死寂：黑幕压顶，只余渗血
            context.BlackoutTarget = 0.85f;
            context.TelegraphGlow = 0f;

            if (Timer == PanicEnd + 1 && !VaultUtils.isServer) {
                //长音抽走
                SoundEngine.PlaySound(SoundID.NPCDeath10 with { Volume = 0.55f, Pitch = -0.9f }, npc.Center);
            }

            //死寂开始时清掉残余飞眼（一阶段被秒进死亡演出的情形）
            if (Timer == PanicEnd + 2 && !VaultUtils.isClient) {
                foreach (var creeper in context.Creepers) {
                    if (creeper.Alives()) {
                        creeper.life = 0;
                        creeper.HitEffect();
                        creeper.active = false;
                        creeper.netUpdate = true;
                    }
                }
            }

            if (!VaultUtils.isServer && Timer % 9 == 0 && BrainMotion.OnScreen(npc.Center)) {
                Vector2 pos = npc.Center + Main.rand.NextVector2Circular(46f, 40f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, Vector2.UnitY * 0.8f,
                    BrainMotion.BloodDark * 0.8f, Main.rand.NextFloat(0.6f, 1f))?.Configure(Main.rand.Next(26, 40), 0.3f);
            }
        }

        private static void DoFinalBurst(BrainStateContext context) {
            NPC npc = context.Npc;

            if (VaultUtils.isServer) {
                return;
            }

            //心核终爆：负片帧+血肉核爆+重震
            BrainHeartbeat.PushImpactFlash(1f);
            BrainHeartbeat.Thump(1.5f, 0.95f);
            BrainMotion.Shake(npc.Center, 19f, 40);
            BrainMotion.Roar(npc.Center, 1.2f, -0.7f, true);
            SoundEngine.PlaySound(SoundID.NPCDeath12 with { Volume = 1.1f, Pitch = -0.5f }, npc.Center);
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.9f, Pitch = -0.35f }, npc.Center);

            BrainMotion.BloodMistBurst(npc.Center, 3.2f, 26, 13f);
            for (int i = 0; i < 26; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 15f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(npc.Center, vel,
                    Color.Lerp(BrainMotion.BloodBright, BrainMotion.BloodDark, Main.rand.NextFloat()),
                    Main.rand.NextFloat(1f, 2f))?.Configure(Main.rand.Next(35, 65), 0.42f);
            }
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_StarPulseRing>(npc.Center, Vector2.Zero,
                    Color.Lerp(BrainMotion.HeartGlow, BrainMotion.BloodDark, i / 8f), 0.05f + i * 0.02f)?
                    .Configure(0.05f, 0.5f + i * 0.16f, 20 + i * 3);
            }

            Lighting.AddLight(npc.Center, BrainMotion.HeartGlow.ToVector3() * 4f);
        }

        #endregion
    }

    /// <summary>克脑弹幕清场工具</summary>
    internal static class BrainProjectileUtils
    {
        /// <summary>清空克脑所属弹幕（阶段转换/死亡/脱战阀，服务端）</summary>
        public static void ClearBrainProjectiles() {
            int mirror = ModContent.ProjectileType<BrainMirrorImage>();
            int shard = ModContent.ProjectileType<BrainBloodShard>();
            int rift = ModContent.ProjectileType<BrainTeleportRift>();
            int vein = ModContent.ProjectileType<BrainVeinTelegraph>();
            int shell = ModContent.ProjectileType<BrainShellFragment>();
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type == mirror || proj.type == shard || proj.type == rift
                    || proj.type == vein || proj.type == shell) {
                    proj.Kill();
                }
            }
        }
    }
}
