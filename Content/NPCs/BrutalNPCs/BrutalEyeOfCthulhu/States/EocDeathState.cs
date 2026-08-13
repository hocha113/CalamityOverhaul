using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.States
{
    /// <summary>
    /// 死亡演出：锁血痉挛，心跳渐急→三段内溢血喷发逐次瘪缩→死寂两拍→血雾终爆蜕散→<br/>
    /// 悬雾滴血的余韵中放行真死，遗骸落在血雾里
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EocStateIndex.Death, typeof(EocStateContext))]
    internal class EocDeathState : EocStateBase
    {
        public override string StateName => "EocDeath";
        public override EocStateIndex StateIndex => EocStateIndex.Death;
        public override bool AllowFogStep => false;

        //心跳渐急帧表（间隔 42→8 收缩）
        private static readonly int[] HeartBeats = [4, 46, 82, 112, 136, 155, 169, 179, 187, 193];
        //三段内溢血喷发
        private static readonly int[] Hemorrhages = [70, 116, 156];
        private const int SilenceFrame = 172;   //死寂起点，粒子全停
        private const int NovaFrame = 176;      //终爆
        private const int TrueDeathFrame = 218; //放行真死

        public override void OnEnter(EocStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            context.DeathPerformanceFinished = false;
            context.ResetChargeState();
            context.IsInPhaseTransition = false;
            //雾中被打死也要迅速显形，痉挛演出必须可见
            context.FogHideGoal = 0f;
            if (context.FogHide > 0.35f) {
                context.FogHide = 0.35f;
            }

            npc.dontTakeDamage = true;
            npc.damage = 0;
            if (npc.life < 1) {
                npc.life = 1;
            }
            npc.velocity *= 0.4f;

            for (int i = 0; i < npc.buffType.Length; i++) {
                npc.buffTime[i] = 0;
            }

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCDeath10 with { Volume = 0.9f, Pitch = -0.55f }, npc.Center);
                SoundEngine.PlaySound(SoundID.Zombie3 with { Volume = 1f, Pitch = -0.7f }, npc.Center);
            }
        }

        public override IEocState OnUpdate(EocStateContext context) {
            NPC npc = context.Npc;
            npc.dontTakeDamage = true;
            npc.damage = 0;
            if (npc.life < 1) {
                npc.life = 1;
            }

            //缓停悬滞
            npc.velocity *= 0.9f;
            npc.rotation += npc.velocity.X * 0.002f;

            if (Timer < SilenceFrame) {
                UpdateConvulsion(npc, context);
            }
            else if (Timer < NovaFrame) {
                //死寂：一切演出骤停，尖叫前的吸气
                npc.velocity = Vector2.Zero;
            }
            else if (Timer == NovaFrame) {
                DoFinalNova(npc, context);
            }
            else {
                UpdateAfterglow(npc, context);
            }

            Timer++;

            //放行真死（权威端），遗骸自血雾中坠出
            if (Timer >= TrueDeathFrame && !VaultUtils.isClient && !context.DeathPerformanceFinished) {
                context.DeathPerformanceFinished = true;
                npc.dontTakeDamage = false;
                npc.life = 0;
                npc.HitEffect();
                npc.checkDead();
                npc.netUpdate = true;
            }

            return null;
        }

        private void UpdateConvulsion(NPC npc, EocStateContext context) {
            float progress = Timer / (float)SilenceFrame;
            EocScreenFX.PushVignette(0.3f + progress * 0.22f);
            context.PushIris(0.5f + progress * 0.5f, EocMotion.BrightBlood);

            //心跳渐急
            foreach (int beat in HeartBeats) {
                if (Timer == beat) {
                    EocScreenFX.PushPulse(0.5f + progress * 0.5f);
                    context.ScalePulse = 1.06f;
                    EocMotion.Shake(npc.Center, 2f + progress * 2.5f, 6);
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.NPCHit13 with {
                            Volume = 0.85f, Pitch = -0.85f + progress * 0.45f
                        }, npc.Center);
                    }
                    break;
                }
            }

            //痉挛
            if (!VaultUtils.isServer) {
                npc.position += Main.rand.NextVector2Circular(1.6f, 1.6f) * (0.4f + progress * 1.4f);
                //渗血渐密
                if (Timer % Math.Max(7 - (int)(progress * 5f), 2) == 0) {
                    Vector2 seep = Main.rand.NextVector2Unit();
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(npc.Center + seep * 40f, seep * 2.4f,
                        EocMotion.Arterial, Main.rand.NextFloat(0.8f, 1.5f))?
                        .Configure(Main.rand.Next(22, 38), 0.35f, 0.985f);
                }
            }

            //三段内溢血喷发，身体逐次瘪缩
            for (int i = 0; i < Hemorrhages.Length; i++) {
                if (Timer != Hemorrhages[i]) {
                    continue;
                }
                float strength = 1.1f + i * 0.4f;
                npc.scale = 1f - (i + 1) * 0.035f;
                context.ScalePulse = 1.1f;
                EocMotion.Shake(npc.Center, 4.5f + i * 2.2f, 10);
                if (!VaultUtils.isServer) {
                    //三向血泉锥
                    for (int c = 0; c < 3; c++) {
                        Vector2 dir = Main.rand.NextVector2Unit();
                        EocMotion.BloodSpray(npc.Center + dir * 30f, dir, 8, 12f * strength, 0.5f);
                    }
                    EocMotion.BloodBurst(npc.Center, strength, playSound: false);
                    Gore.NewGore(npc.GetSource_FromAI(), npc.position + Main.rand.NextVector2Circular(30f, 30f),
                        Main.rand.NextVector2Unit() * 4f, Main.rand.Next(6, 9));
                    SoundEngine.PlaySound(SoundID.NPCDeath12 with { Volume = 0.95f, Pitch = -0.35f + i * 0.12f }, npc.Center);
                    SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.8f, Pitch = -0.5f }, npc.Center);
                }
                break;
            }
        }

        /// <summary>终爆：血雾蜕散+全屏血闪，全场唯一的大震</summary>
        private void DoFinalNova(NPC npc, EocStateContext context) {
            context.FogHideGoal = 1.2f;
            context.FogHide = 0.6f;
            EocScreenFX.PushFlash(1f, 16);
            EocScreenFX.PushVignette(0.6f);
            EocMotion.Shake(npc.Center, 17f, 30);

            if (VaultUtils.isServer) {
                return;
            }

            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 1.2f, Pitch = -0.4f }, npc.Center);
            SoundEngine.PlaySound(SoundID.NPCDeath12 with { Volume = 1.1f, Pitch = -0.6f }, npc.Center);
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.7f, Pitch = -0.7f }, npc.Center);

            EocMotion.BloodBurst(npc.Center, 2.6f, playSound: false);
            PRTLoader.NewParticle<PRT_DWave>(npc.Center, Vector2.Zero, EocMotion.Arterial, 0.36f)?
                .Configure(Vector2.One, Main.rand.NextFloat(MathHelper.TwoPi), 2.8f, 30);
            PRTLoader.NewParticle<PRT_DWave>(npc.Center, Vector2.Zero, EocMotion.BrightBlood * 0.7f, 0.22f)?
                .Configure(Vector2.One, Main.rand.NextFloat(MathHelper.TwoPi), 1.9f, 24);

            //血雨四散
            for (int i = 0; i < 30; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(4f, 16f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(npc.Center, vel,
                    Color.Lerp(EocMotion.Arterial, EocMotion.BrightBlood, Main.rand.NextFloat()),
                    Main.rand.NextFloat(1.2f, 2.3f))?.Configure(Main.rand.Next(34, 58), 0.34f, 0.988f);
            }
            //浓雾尸场
            EocMotion.MistPuff(npc.Center, 12, 1.9f, 0.62f);
            //组织碎屑
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_EocSkinShred>(npc.Center + Main.rand.NextVector2Circular(40f, 40f),
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(5f, 12f),
                    Color.Lerp(new Color(148, 108, 96), EocMotion.VenousDark, Main.rand.NextFloat(0.6f)),
                    Main.rand.NextFloat(1f, 1.8f))?.Configure(Main.rand.Next(46, 78));
            }
        }

        private void UpdateAfterglow(NPC npc, EocStateContext context) {
            //悬雾余韵：本体已蜕散不可见，血珠自爆点缓缓滴落
            context.FogHideGoal = 1.2f;
            npc.velocity = Vector2.Zero;
            float fade = 1f - (Timer - NovaFrame) / (float)(TrueDeathFrame - NovaFrame);
            EocScreenFX.PushVignette(0.45f * fade);

            if (!VaultUtils.isServer && Timer % 4 == 0) {
                Vector2 pos = npc.Center + Main.rand.NextVector2Circular(70f, 50f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, Vector2.UnitY * Main.rand.NextFloat(0.5f, 2f),
                    EocMotion.VenousDark, Main.rand.NextFloat(0.7f, 1.3f))?
                    .Configure(Main.rand.Next(26, 44), 0.3f, 0.99f);
            }
        }

        public override void OnExit(EocStateContext context) {
            base.OnExit(context);
            if (!context.DeathPerformanceFinished && context.Npc != null) {
                context.Npc.dontTakeDamage = false;
            }
        }
    }
}
