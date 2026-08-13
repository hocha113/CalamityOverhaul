using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.States
{
    /// <summary>
    /// 死亡演出：风暴弃他而去。挣扎强撑→翻肚坠海→巨大水葬→雨过天开，
    /// 浪送他最后一程。全程锁血，演出完服务端放行真死
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)FishronStateIndex.Death, typeof(FishronStateContext))]
    internal class FishronDeathState : FishronStateBase
    {
        public override string StateName => "Death";
        public override FishronStateIndex StateIndex => FishronStateIndex.Death;
        public override bool AllowFarSnap => false;

        private const int StruggleEnd = 72;
        private const int SettleTime = 170;
        /// <summary>硬性收尾兜底：任何地形下都保证放行</summary>
        private const int HardTimeout = 620;

        /// <summary>演出阶段 0挣扎 1坠落 2搁浅</summary>
        private int phase;
        private int settleTimer;
        private float fallSpin;

        public FishronDeathState() {
        }

        public override void OnEnter(FishronStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            phase = 0;
            settleTimer = 0;
            fallSpin = 0f;
            NPC npc = context.Npc;
            npc.velocity *= 0.5f;
            DukeFishronAI.ActivePerformanceBoss = npc.whoAmI;
            DukeFishronAI.ClearMinions(alsoTornado: true);
            SoundEngine.PlaySound(SoundID.Zombie20 with { Volume = 1.3f, Pitch = 0.5f }, npc.Center);
        }

        public override IFishronState OnUpdate(FishronStateContext context) {
            NPC npc = context.Npc;

            //锁血无害
            npc.dontTakeDamage = true;
            npc.damage = 0;
            if (npc.life < 1) {
                npc.life = 1;
            }

            Timer++;

            //风暴熄灭进程：越死越晴
            float clearT = MathHelper.Clamp(Timer / 300f, 0f, 1f);
            context.StormBoost = -context.PhaseStormGrade * clearT * 0.9f;

            switch (phase) {
                case 0:
                    UpdateStruggle(context, npc);
                    break;
                case 1:
                    UpdateFall(context, npc);
                    break;
                default:
                    UpdateSettle(context, npc);
                    break;
            }

            //收尾（服务端放行真死）
            bool finished = (phase == 2 && settleTimer >= SettleTime) || Timer >= HardTimeout;
            if (finished && !VaultUtils.isClient) {
                context.DeathPerformanceFinished = true;
                npc.dontTakeDamage = false;
                npc.life = 0;
                npc.HitEffect();
                npc.checkDead();
                npc.netUpdate = true;
            }

            return null;
        }

        /// <summary>幕一：挣扎强撑——抽搐、漏电、风暴在他身上打摆子</summary>
        private void UpdateStruggle(FishronStateContext context, NPC npc) {
            //确定性抽搐（各端一致的正弦抖动，不吃随机）
            float t = Timer * 0.35f;
            Vector2 jerk = new((float)Math.Sin(t * 1.7f) * 1.6f, (float)Math.Sin(t * 2.3f + 1f) * 1.2f - 0.8f);
            npc.velocity = Vector2.Lerp(npc.velocity, jerk, 0.15f);
            context.FrameCommand = 1;

            if (Timer == 14 || Timer == 44) {
                SoundEngine.PlaySound(SoundID.Zombie20 with {
                    Volume = 1f,
                    Pitch = 0.3f + Timer / 100f,
                    MaxInstances = 3
                }, npc.Center);
                FishronStormSky.PushFlash(0.4f, npc.Center);
            }
            //体表电弧退散成水花
            if (!VaultUtils.isServer && Timer % 4 == 0) {
                FishronMotionFX.SpawnSprayCone(npc.Center + Main.rand.NextVector2Circular(46f, 34f),
                    Main.rand.NextVector2Unit(), 1, 1f, 4f, MathHelper.Pi, 0.8f);
            }

            if (Timer >= StruggleEnd) {
                phase = 1;
                SoundEngine.PlaySound(SoundID.NPCDeath19 with { Volume = 0.9f, Pitch = -0.6f }, npc.Center);
            }
        }

        /// <summary>幕二：力竭翻肚，重重坠向海面</summary>
        private void UpdateFall(FishronStateContext context, NPC npc) {
            npc.velocity.X *= 0.99f;
            npc.velocity.Y = Math.Min(npc.velocity.Y + 0.34f, 15f);
            //翻肚慢旋
            fallSpin = Math.Min(fallSpin + 0.0012f, 0.035f);
            npc.rotation += fallSpin * (npc.spriteDirection == 1 ? -1f : 1f);
            context.FrameCommand = 1;

            //拖出下坠的水尾
            if (!VaultUtils.isServer && Timer % 3 == 0) {
                FishronMotionFX.SpawnSprayCone(npc.Center - npc.velocity * 0.5f,
                    -npc.velocity.SafeNormalize(Vector2.UnitY), 1, 1f, 3f, 0.4f, 0.7f);
                FishronMotionFX.SpawnMist(npc.Center, -npc.velocity * 0.1f, 0.7f);
            }

            //触及水面/地表：水葬冲击
            Vector2 surface = FishronMotionFX.FindSurfaceBelow(npc.Center - new Vector2(0, 40f), out _);
            if (npc.Center.Y >= surface.Y - 50f) {
                phase = 2;
                settleTimer = 0;
                npc.velocity = new Vector2(npc.velocity.X * 0.3f, -5.5f);
                FishronMotionFX.SpawnSplashBurst(surface, 2.6f);
                FishronMotionFX.CameraPunch(surface, 14f, 22, "FishronDeathSplash", Vector2.UnitY);
                SoundEngine.PlaySound(SoundID.Zombie20 with { Volume = 1.1f, Pitch = -0.7f }, npc.Center);
                FishronStormSky.PushFlash(0.55f, surface);
            }
        }

        /// <summary>幕三：搁浅漂浮，雨停云开，浪替他收尸</summary>
        private void UpdateSettle(FishronStateContext context, NPC npc) {
            settleTimer++;

            //死水轻漾
            npc.velocity *= 0.92f;
            npc.velocity.Y += (float)Math.Sin(settleTimer * 0.05f) * 0.05f;
            npc.rotation += 0.002f * (npc.spriteDirection == 1 ? -1f : 1f);
            context.FrameCommand = 1;

            //残余的泡沫从身下冒起
            if (!VaultUtils.isServer && settleTimer % 8 == 0) {
                InnoVault.PRT.PRTLoader.NewParticle<PRT_FishronFoam>(
                    npc.Center + Main.rand.NextVector2Circular(60f, 24f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.2f, 0.7f),
                    FishronMotionFX.FoamWhite * 0.35f, Main.rand.NextFloat(0.7f, 1.2f))
                    ?.Configure(Main.rand.Next(30, 50), 0.01f);
            }
            //第二次小落水
            if (settleTimer == 26 && !VaultUtils.isServer) {
                Vector2 surface = FishronMotionFX.FindSurfaceBelow(npc.Center - new Vector2(0, 30f), out _);
                FishronMotionFX.SpawnSplashBurst(surface, 0.9f, playSound: false);
            }
        }

        public override void OnExit(FishronStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
            DukeFishronAI.ActivePerformanceBoss = -1;
        }
    }
}
