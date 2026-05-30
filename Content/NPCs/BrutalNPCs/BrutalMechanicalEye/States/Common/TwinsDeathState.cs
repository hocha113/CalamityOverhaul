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
    /// <summary>
    /// 双子魔眼死亡演出状态（每只眼睛独立播放）：锁血、本体急停悬停，
    /// 眼球各处由疏到密地连环殉爆，最终核心炸裂后才真正死亡，
    /// 表现"机械眼在严重过载与连环爆炸中解体"的演出。
    /// <br/>状态切换与最终击杀由服务端驱动（经 npc.ai[1] 同步），
    /// 所有爆炸粒子/音效/震动均在客户端本地生成，纯视觉，多人安全。
    /// </summary>
    internal class TwinsDeathState : TwinsStateBase
    {
        public override string StateName => "TwinsDeath";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.TwinsDeath;

        //演出节奏（单位：帧，60帧/秒）
        private const int PreludeTime = 40;   //急停 + 接缝漏火花
        private const int ChainTime = 130;    //眼球各处由疏到密的连环爆炸
        private const int FinaleHold = 40;    //核心终爆后的短暂保持
        private const int FinaleStart = PreludeTime + ChainTime;
        private const int TotalTime = FinaleStart + FinaleHold;

        //殉爆配色（按眼睛区分：魔焰眼橙红、激光眼品红）
        private Color warmA;
        private Color warmB;

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

            //清除负面buff，避免演出期间继续掉血/被控
            for (int i = 0; i < npc.buffType.Length; i++) {
                npc.buffTime[i] = 0;
            }

            //配色：魔焰眼橙红殉爆，激光眼品红能量爆
            if (context.IsSpazmatism) {
                warmA = new Color(255, 150, 50);
                warmB = new Color(255, 85, 35);
            }
            else {
                warmA = new Color(255, 90, 110);
                warmB = new Color(220, 40, 75);
            }

            //过载警报音（服务端为 no-op，单人/客户端本地播放，自然同步）
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.75f, Volume = 0.9f }, npc.Center);
            SoundEngine.PlaySound(SoundID.NPCDeath14 with { Pitch = -0.55f, Volume = 0.7f }, npc.Center);
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;

            //全程锁血、不可受伤、不造成接触伤害
            npc.dontTakeDamage = true;
            npc.damage = 0;
            if (npc.life < 1) {
                npc.life = 1;
            }

            //急停悬停（眼睛停在原地等待解体）
            npc.velocity *= 0.88f;
            if (npc.velocity.Length() < 0.1f) {
                npc.velocity = Vector2.Zero;
            }

            //演出视觉（在所有非服务端执行，单人与多人客户端都能看到）
            if (!VaultUtils.isServer) {
                UpdatePerformanceVisuals(npc);
            }

            Timer++;

            //演出结束：由服务端/单人端放行并真正击杀，触发正常掉落与击杀标记
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
            //若演出未完成被异常切走，恢复可受伤，避免无敌泄漏
            if (!context.DeathPerformanceFinished && context.Npc != null) {
                context.Npc.dontTakeDamage = false;
            }
        }

        #region 演出视觉

        private void UpdatePerformanceVisuals(NPC npc) {
            //眼睛在屏幕外则跳过所有演出粒子（性能优化）
            if (!OnScreen(npc.Center, 600f)) {
                return;
            }

            if (Timer < PreludeTime) {
                //前奏：眼球接缝漏火花 + 零星小爆，机体"卡死过载"质感
                if (Timer % 3 == 0) {
                    SpawnSparks(npc, 3, 5f);
                }
                if (Timer % 7 == 0) {
                    SpawnBlast(npc.Center + RandOffset(npc, 0.45f), Main.rand.NextFloat(0.7f, 1.1f), false);
                }
            }
            else if (Timer < FinaleStart) {
                //连环爆炸：间隔由疏到密，每波在眼球范围内点燃多团殉爆
                float chainProgress = (Timer - PreludeTime) / (float)ChainTime; //0→1
                int burstInterval = (int)MathHelper.Lerp(6f, 2f, chainProgress);
                if (Timer % Math.Max(burstInterval, 2) == 0) {
                    int blasts = 1 + (int)(chainProgress * 3f); //1→4 团/波
                    for (int i = 0; i < blasts; i++) {
                        float scale = Main.rand.NextFloat(0.85f, 1.45f) * (0.85f + chainProgress * 0.7f);
                        SpawnBlast(npc.Center + RandOffset(npc, 0.6f), scale, false);
                    }
                    DoScreenShake(npc.Center, 3.5f + chainProgress * 6f, 11);
                }
                //持续火花
                if (Timer % 2 == 0) {
                    SpawnSparks(npc, 4, 7f);
                }
            }
            else if (Timer == FinaleStart) {
                //核心终极殉爆
                SpawnFinaleBlast(npc);
            }
        }

        /// <summary>
        /// 眼球范围内的随机偏移点
        /// </summary>
        private static Vector2 RandOffset(NPC npc, float factor) {
            return Main.rand.NextVector2Circular(npc.width * factor, npc.height * factor) * npc.scale;
        }

        /// <summary>
        /// 生成一团殉爆：爆炸光团 + 火花四溅 + 余烬 + 烟雾 + 动态光照 + 音效
        /// </summary>
        private void SpawnBlast(Vector2 pos, float scale, bool isFinale) {
            if (VaultUtils.isServer) {
                return;
            }

            Color warm = Color.Lerp(warmA, warmB, Main.rand.NextFloat());

            //核心爆炸光团（SoftGlow 叠加）
            PRTLoader.NewParticle<PRT_MechExplosion>(pos, Main.rand.NextVector2Circular(1.5f, 1.5f), warm, scale)
                .Configure(Main.rand.Next(26, 38), warm);

            //火花四溅（密集连环靠数量取胜，单团精简粒子量）
            int sparkCount = isFinale ? 46 : Main.rand.Next(4, 8);
            for (int i = 0; i < sparkCount; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(1f, 1f) * Main.rand.NextFloat(3f, 11f) * (isFinale ? 1.6f : scale);
                PRTLoader.NewParticle<PRT_Spark>(pos, vel,
                    Color.Lerp(warm, Color.LightGoldenrodYellow, Main.rand.NextFloat()),
                    Main.rand.NextFloat(1.0f, 1.8f)).Configure(true, Main.rand.Next(16, 30));
            }

            //岩浆余烬
            int emberCount = isFinale ? 24 : 2;
            for (int i = 0; i < emberCount; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(3.5f, 3.5f);
                PRTLoader.NewParticle<PRT_LavaFire>(pos + Main.rand.NextVector2Circular(20f, 20f) * scale, vel,
                    Color.White, Main.rand.NextFloat(0.8f, 1.3f) * scale).SetLifetime(20, 46);
            }

            //滚滚浓烟
            int smokeCount = isFinale ? 16 : 2;
            for (int i = 0; i < smokeCount; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(2f, 2f) - Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.7f);
                PRTLoader.NewParticle<PRT_Smoke>(pos, vel,
                    Color.Lerp(new Color(60, 56, 54), new Color(20, 18, 18), Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.8f, 1.4f) * scale)
                    .Configure(Main.rand.Next(45, 72), 0.7f, Main.rand.NextFloat(-0.05f, 0.05f));
            }

            Lighting.AddLight(pos, warm.ToVector3() * (isFinale ? 3f : 1.1f) * scale);

            //密集连环爆炸时按概率播放，避免大量爆音同帧叠加导致破音
            if (isFinale || Main.rand.NextBool(3)) {
                SoundEngine.PlaySound(SoundID.Item14 with {
                    Pitch = isFinale ? -0.5f : Main.rand.NextFloat(-0.2f, 0.35f),
                    Volume = isFinale ? 1f : 0.4f
                }, pos);
            }
        }

        /// <summary>
        /// 眼球接缝喷射火花，模拟电路过载 / 接缝喷火
        /// </summary>
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

        /// <summary>
        /// 核心终极殉爆 + 眼球连锁爆裂 + 强烈屏幕震动
        /// </summary>
        private void SpawnFinaleBlast(NPC npc) {
            if (VaultUtils.isServer) {
                return;
            }

            SpawnBlast(npc.Center, 3.8f, true);

            //眼球周身连锁小爆
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
            if (VaultUtils.isServer || !CWRServerConfig.Instance.ScreenVibration) {
                return;
            }
            PunchCameraModifier modifier = new PunchCameraModifier(pos, Main.rand.NextVector2Unit(),
                strength, 8f, time, 2400f, "TwinsDeath");
            Main.instance.CameraModifiers.Add(modifier);
        }

        /// <summary>
        /// 判断世界坐标是否落在当前屏幕可见范围内（含外扩边距），用于跳过屏幕外的爆炸生成
        /// </summary>
        private static bool OnScreen(Vector2 worldPos, float margin = 260f) {
            return worldPos.X > Main.screenPosition.X - margin
                && worldPos.X < Main.screenPosition.X + Main.screenWidth + margin
                && worldPos.Y > Main.screenPosition.Y - margin
                && worldPos.Y < Main.screenPosition.Y + Main.screenHeight + margin;
        }

        #endregion
    }
}
