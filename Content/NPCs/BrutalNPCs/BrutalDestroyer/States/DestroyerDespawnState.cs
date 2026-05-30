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
    /// <summary>
    /// 脱战状态
    /// </summary>
    internal class DestroyerDespawnState : DestroyerStateBase
    {
        public override string StateName => "Despawn";
        public override DestroyerStateIndex StateIndex => DestroyerStateIndex.Despawn;

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

    /// <summary>
    /// 死亡演出状态：锁血、整条蠕虫停摆，躯体各处由疏到密地连环殉爆，
    /// 最终头部巨型爆裂后才真正死亡。表现"庞大机械在严重故障与连环爆炸中解体"的演出。
    /// <br/>状态切换与最终击杀由服务端驱动（经 npc.ai[2] 同步），
    /// 所有爆炸粒子/音效/震动均在客户端本地生成，纯视觉，多人安全。
    /// </summary>
    internal class DestroyerDeathState : DestroyerStateBase
    {
        public override string StateName => "Death";
        public override DestroyerStateIndex StateIndex => DestroyerStateIndex.Death;

        //演出节奏（单位：帧，60帧/秒）
        private const int PreludeTime = 50;   //急停 + 关节漏火花
        private const int ChainTime = 185;    //沿躯体由疏到密的连环爆炸
        private const int FinaleHold = 45;    //头部终爆后的短暂保持
        private const int FinaleStart = PreludeTime + ChainTime;
        private const int TotalTime = FinaleStart + FinaleHold;

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

            //服务端清场探针，避免演出期间还有探针骚扰玩家
            if (!VaultUtils.isClient) {
                foreach (var n in Main.ActiveNPCs) {
                    if (n.type == NPCID.Probe) {
                        n.life = 0;
                        n.HitEffect();
                        n.active = false;
                        n.netUpdate = true;
                    }
                }
            }

            //过载警报音（服务端为 no-op，单人/客户端本地播放，自然同步）
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.8f, Volume = 0.9f }, npc.Center);
            SoundEngine.PlaySound(SoundID.NPCDeath14 with { Pitch = -0.6f, Volume = 0.7f }, npc.Center);
        }

        public override IDestroyerState OnUpdate(DestroyerStateContext context) {
            NPC npc = context.Npc;

            //全程锁血、不可受伤、不造成接触伤害
            npc.dontTakeDamage = true;
            npc.damage = 0;
            if (npc.life < 1) {
                npc.life = 1;
            }

            //蠕虫急停（头部停住，躯体因贴合前节而整体静止）
            npc.velocity *= 0.85f;
            if (npc.velocity.Length() < 0.1f) {
                npc.velocity = Vector2.Zero;
            }

            if (Timer % 15 == 0) {
                context.RefreshBodySegments();
            }
            KeepSegmentsHarmless(context);

            //演出视觉（在所有非服务端执行，确保单人与多人客户端都能看到）
            if (!VaultUtils.isServer) {
                UpdatePerformanceVisuals(context);
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

        #region 演出视觉

        private void UpdatePerformanceVisuals(DestroyerStateContext context) {
            if (Timer < PreludeTime) {
                //前奏：关节漏火花 + 零星电弧，机体"卡死"质感
                if (Timer % 4 == 0) {
                    SpawnSparksAlongWorm(context, 2, 5f, new Color(255, 190, 70));
                }
                if (Timer % 10 == 0) {
                    SpawnBlast(PickWormPoint(context), Main.rand.NextFloat(0.5f, 0.9f), false);
                }
            }
            else if (Timer < FinaleStart) {
                //连环爆炸：随进度由疏到密
                float chainProgress = (Timer - PreludeTime) / (float)ChainTime; //0→1
                int interval = (int)MathHelper.Lerp(11f, 3f, chainProgress);
                if (Timer % Math.Max(interval, 2) == 0) {
                    float scale = Main.rand.NextFloat(0.9f, 1.6f) * (0.8f + chainProgress * 0.7f);
                    SpawnBlast(PickWormPoint(context), scale, false);
                    DoScreenShake(context.Npc.Center, 4f + chainProgress * 6f, 12);
                }
                //持续的火花与漏烟
                if (Timer % 3 == 0) {
                    SpawnSparksAlongWorm(context, 3, 7f, Color.Orange);
                }
            }
            else if (Timer == FinaleStart) {
                //头部终极殉爆 + 全身连锁爆裂
                SpawnFinaleBlast(context);
            }
        }

        /// <summary>
        /// 在整条蠕虫上随机取一个爆点（绝大多数落在躯体，少量落在头部）
        /// </summary>
        private static Vector2 PickWormPoint(DestroyerStateContext context) {
            NPC npc = context.Npc;
            NPC chosen = npc;
            var segs = context.BodySegments;
            if (segs.Count > 0 && !Main.rand.NextBool(8)) {
                NPC candidate = segs[Main.rand.Next(segs.Count)];
                if (candidate.Alives()) {
                    chosen = candidate;
                }
            }
            return chosen.Center + Main.rand.NextVector2Circular(chosen.width * 0.5f, chosen.height * 0.5f);
        }

        /// <summary>
        /// 生成一团殉爆：爆炸光团 + 火花四溅 + 余烬 + 烟雾 + 动态光照 + 音效
        /// </summary>
        private static void SpawnBlast(Vector2 pos, float scale, bool isFinale) {
            if (VaultUtils.isServer) {
                return;
            }

            Color warm = Color.Lerp(new Color(255, 150, 50), new Color(255, 85, 35), Main.rand.NextFloat());

            //核心爆炸光团（SoftGlow 叠加）
            PRTLoader.NewParticle<PRT_MechExplosion>(pos, Main.rand.NextVector2Circular(1.5f, 1.5f), warm, scale)
                .Configure(Main.rand.Next(28, 40), warm);

            //火花四溅
            int sparkCount = isFinale ? 46 : Main.rand.Next(6, 11);
            for (int i = 0; i < sparkCount; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(1f, 1f) * Main.rand.NextFloat(3f, 11f) * (isFinale ? 1.6f : scale);
                PRTLoader.NewParticle<PRT_Spark>(pos, vel,
                    Color.Lerp(Color.Orange, Color.LightGoldenrodYellow, Main.rand.NextFloat()),
                    Main.rand.NextFloat(1.0f, 1.8f)).Configure(true, Main.rand.Next(16, 30));
            }

            //岩浆余烬
            int emberCount = isFinale ? 26 : 4;
            for (int i = 0; i < emberCount; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(3.5f, 3.5f);
                PRTLoader.NewParticle<PRT_LavaFire>(pos + Main.rand.NextVector2Circular(22f, 22f) * scale, vel,
                    Color.White, Main.rand.NextFloat(0.8f, 1.4f) * scale).SetLifetime(20, 48);
            }

            //滚滚浓烟
            int smokeCount = isFinale ? 18 : 3;
            for (int i = 0; i < smokeCount; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(2f, 2f) - Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.8f);
                PRTLoader.NewParticle<PRT_Smoke>(pos, vel,
                    Color.Lerp(new Color(60, 56, 54), new Color(20, 18, 18), Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.8f, 1.4f) * scale)
                    .Configure(Main.rand.Next(45, 75), 0.7f, Main.rand.NextFloat(-0.05f, 0.05f));
            }

            Lighting.AddLight(pos, warm.ToVector3() * (isFinale ? 3.2f : 1.2f) * scale);

            SoundEngine.PlaySound(SoundID.Item14 with {
                Pitch = isFinale ? -0.5f : Main.rand.NextFloat(-0.2f, 0.35f),
                Volume = isFinale ? 1f : 0.55f
            }, pos);
        }

        /// <summary>
        /// 沿蠕虫多点喷射火花，模拟各处接缝喷火 / 电路过载
        /// </summary>
        private static void SpawnSparksAlongWorm(DestroyerStateContext context, int count, float speed, Color color) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < count; i++) {
                Vector2 pos = PickWormPoint(context);
                Vector2 vel = Main.rand.NextVector2Circular(1f, 1f) * Main.rand.NextFloat(1f, speed);
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, color, Main.rand.NextFloat(0.7f, 1.3f))
                    .Configure(true, Main.rand.Next(12, 22));
            }
        }

        /// <summary>
        /// 头部终极殉爆 + 全身连锁爆裂 + 强烈屏幕震动
        /// </summary>
        private void SpawnFinaleBlast(DestroyerStateContext context) {
            if (VaultUtils.isServer) {
                return;
            }
            NPC npc = context.Npc;

            SpawnBlast(npc.Center, 4.5f, true);

            foreach (var seg in context.BodySegments) {
                if (seg.Alives() && Main.rand.NextBool(2)) {
                    SpawnBlast(seg.Center, Main.rand.NextFloat(1.6f, 2.8f), false);
                }
            }

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

        /// <summary>
        /// 让躯体各节停止造成伤害且不可受伤，避免演出期间还能撞死玩家或被打出异常
        /// </summary>
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

        private static void DoScreenShake(Vector2 pos, float strength, int time) {
            if (VaultUtils.isServer || !CWRServerConfig.Instance.ScreenVibration) {
                return;
            }
            PunchCameraModifier modifier = new PunchCameraModifier(pos, Main.rand.NextVector2Unit(),
                strength, 8f, time, 2400f, "DestroyerDeath");
            Main.instance.CameraModifiers.Add(modifier);
        }

        #endregion
    }
}
