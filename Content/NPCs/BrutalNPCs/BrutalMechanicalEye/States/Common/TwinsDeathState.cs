using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Common
{
    /// <summary>死亡演出，锁血急停→疏密殉爆→终爆真死</summary>
    [InnoVault.StateMachines.VaultState((int)TwinsStateIndex.TwinsDeath, typeof(TwinsStateContext))]
    internal class TwinsDeathState : TwinsStateBase
    {
        public override string StateName => "TwinsDeath";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.TwinsDeath;

        //演出节奏，帧
        private const int PreludeTime = 40;  //急停+接缝火花
        private const int ChainTime = 130;  //疏→密连环爆
        private const int FinaleHold = 20;  //终爆后保持
        private const int FinaleStart = PreludeTime + ChainTime;
        private const int TotalTime = FinaleStart + FinaleHold;

        //殉爆配色，魔焰橙红/激光品红
        private Color warmA;
        private Color warmB;

        public TwinsDeathState() {
        }

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            context.DeathPerformanceFinished = false;
            context.ResetChargeState();
            context.IsInPhaseTransition = false;

            npc.dontTakeDamage = true;
            npc.damage = 0;
            if (npc.life < 1) {
                npc.life = 1;
            }
            npc.velocity *= 0.3f;

            //清负面buff
            for (int i = 0; i < npc.buffType.Length; i++) {
                npc.buffTime[i] = 0;
            }

            //配色分眼
            if (context.IsSpazmatism) {
                warmA = new Color(255, 150, 50);
                warmB = new Color(255, 85, 35);
            }
            else {
                warmA = new Color(255, 90, 110);
                warmB = new Color(220, 40, 75);
            }

            //过载警报，服务端 no-op
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.75f, Volume = 0.9f }, npc.Center);
            SoundEngine.PlaySound(SoundID.NPCDeath14 with { Pitch = -0.55f, Volume = 0.7f }, npc.Center);
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;

            //锁血无敌无接触伤
            npc.dontTakeDamage = true;
            npc.damage = 0;
            if (npc.life < 1) {
                npc.life = 1;
            }

            //急停悬停
            npc.velocity *= 0.88f;
            if (npc.velocity.Length() < 0.1f) {
                npc.velocity = Vector2.Zero;
            }

            //演出视觉，非服务端
            if (!VaultUtils.isServer) {
                UpdatePerformanceVisuals(npc);
            }

            Timer++;

            //演出结束，服务端/单人放行真死
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

        public override void OnExit(TwinsStateContext context) {
            base.OnExit(context);
            //异常切走则恢复可受伤
            if (!context.DeathPerformanceFinished && context.Npc != null) {
                context.Npc.dontTakeDamage = false;
            }
        }

        #region 演出视觉

        private void UpdatePerformanceVisuals(NPC npc) {
            //屏外跳过粒子
            if (!OnScreen(npc.Center, 600f)) {
                return;
            }

            if (Timer < PreludeTime) {
                //前奏，接缝火花+零星小爆
                if (Timer % 3 == 0) {
                    SpawnSparks(npc, 3, 5f);
                }
                if (Timer % 7 == 0) {
                    SpawnBlast(npc.Center + RandOffset(npc, 0.45f), Main.rand.NextFloat(0.7f, 1.1f), false);
                }
            }
            else if (Timer < FinaleStart) {
                //连环爆，疏→密
                float chainProgress = (Timer - PreludeTime) / (float)ChainTime;  //0→1
                int burstInterval = (int)MathHelper.Lerp(6f, 2f, chainProgress);
                if (Timer % Math.Max(burstInterval, 2) == 0) {
                    int blasts = 1 + (int)(chainProgress * 3f);  //1→4团/波
                    for (int i = 0; i < blasts; i++) {
                        float scale = Main.rand.NextFloat(0.85f, 1.45f) * (0.85f + chainProgress * 0.7f);
                        SpawnBlast(npc.Center + RandOffset(npc, 0.6f), scale, false);
                    }
                    DoScreenShake(npc.Center, 3.5f + chainProgress * 6f, 11);
                }
                //火花
                if (Timer % 2 == 0) {
                    SpawnSparks(npc, 4, 7f);
                }
            }
            else if (Timer == FinaleStart) {
                //终爆
                SpawnFinaleBlast(npc);
            }
        }

        private static Vector2 RandOffset(NPC npc, float factor) {
            return Main.rand.NextVector2Circular(npc.width * factor, npc.height * factor) * npc.scale;
        }

        /// <summary>一团殉爆</summary>
        private void SpawnBlast(Vector2 pos, float scale, bool isFinale) {
            if (VaultUtils.isServer) {
                return;
            }

            Color warm = Color.Lerp(warmA, warmB, Main.rand.NextFloat());

            //爆炸光团
            PRTLoader.NewParticle<PRT_MechExplosion>(pos, Main.rand.NextVector2Circular(1.5f, 1.5f), warm, scale)
                .Configure(Main.rand.Next(26, 38), warm);

            //火花
            int sparkCount = isFinale ? 46 : Main.rand.Next(4, 8);
            for (int i = 0; i < sparkCount; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(1f, 1f) * Main.rand.NextFloat(3f, 11f) * (isFinale ? 1.6f : scale);
                PRTLoader.NewParticle<PRT_Spark>(pos, vel,
                    Color.Lerp(warm, Color.LightGoldenrodYellow, Main.rand.NextFloat()),
                    Main.rand.NextFloat(1.0f, 1.8f)).Configure(true, Main.rand.Next(16, 30));
            }

            //余烬
            int emberCount = isFinale ? 24 : 2;
            for (int i = 0; i < emberCount; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(3.5f, 3.5f);
                PRTLoader.NewParticle<PRT_LavaFire>(pos + Main.rand.NextVector2Circular(20f, 20f) * scale, vel,
                    Color.White, Main.rand.NextFloat(0.8f, 1.3f) * scale).SetLifetime(20, 46);
            }

            //烟
            int smokeCount = isFinale ? 16 : 2;
            for (int i = 0; i < smokeCount; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(2f, 2f) - Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.7f);
                PRTLoader.NewParticle<PRT_Smoke>(pos, vel,
                    Color.Lerp(new Color(60, 56, 54), new Color(20, 18, 18), Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.8f, 1.4f) * scale)
                    .Configure(Main.rand.Next(45, 72), 0.7f, Main.rand.NextFloat(-0.05f, 0.05f));
            }

            Lighting.AddLight(pos, warm.ToVector3() * (isFinale ? 3f : 1.1f) * scale);

            //爆音概率播，防破音
            if (isFinale || Main.rand.NextBool(3)) {
                SoundEngine.PlaySound(SoundID.Item14 with {
                    Pitch = isFinale ? -0.5f : Main.rand.NextFloat(-0.2f, 0.35f),
                    Volume = isFinale ? 1f : 0.4f
                }, pos);
            }
        }

        /// <summary>接缝火花</summary>
        private void SpawnSparks(NPC npc, int count, float speed) {
            if (VaultUtils.isServer) {
                return;
            }
            Color color = Color.Lerp(warmA, Color.LightGoldenrodYellow, 0.3f);
            for (int i = 0; i < count; i++) {
                Vector2 pos = npc.Center + RandOffset(npc, 0.5f);
                Vector2 vel = Main.rand.NextVector2Circular(1f, 1f) * Main.rand.NextFloat(1f, speed);
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, color, Main.rand.NextFloat(0.7f, 1.3f))
                    .Configure(true, Main.rand.Next(12, 22));
            }
        }

        /// <summary>终爆+连锁+强震</summary>
        private void SpawnFinaleBlast(NPC npc) {
            if (VaultUtils.isServer) {
                return;
            }

            SpawnBlast(npc.Center, 3.8f, true);

            //周身连锁小爆
            for (int i = 0; i < 7; i++) {
                SpawnBlast(npc.Center + RandOffset(npc, 1.0f), Main.rand.NextFloat(1.3f, 2.2f), false);
            }

            DoScreenShake(npc.Center, 22f, 40);
            SoundEngine.PlaySound(SoundID.DD2_KoboldExplosion with { Volume = 1.1f, Pitch = -0.3f }, npc.Center);
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 1f }, npc.Center);
        }

        #endregion

        #region 辅助

        private static void DoScreenShake(Vector2 pos, float strength, int time) {
            if (VaultUtils.isServer || !CWRClientConfig.Instance.ScreenVibration) {
                return;
            }
            PunchCameraModifier modifier = new PunchCameraModifier(pos, Main.rand.NextVector2Unit(),
                strength, 8f, time, 2400f, "TwinsDeath");
            Main.instance.CameraModifiers.Add(modifier);
        }

        /// <summary>屏内判定，含边距</summary>
        private static bool OnScreen(Vector2 worldPos, float margin = 260f) {
            return worldPos.X > Main.screenPosition.X - margin
                && worldPos.X < Main.screenPosition.X + Main.screenWidth + margin
                && worldPos.Y > Main.screenPosition.Y - margin
                && worldPos.Y < Main.screenPosition.Y + Main.screenHeight + margin;
        }

        #endregion
    }
}
