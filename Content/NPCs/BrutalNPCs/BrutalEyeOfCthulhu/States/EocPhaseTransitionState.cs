using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.States
{
    /// <summary>
    /// 撕皮转阶段：锁身痉挛→表皮沿体轴撕裂三段绽开→血爆蜕壳露出口器→喘息入二阶段<br/>
    /// 演出期无敌+清弹，公平的换幕
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EocStateIndex.PhaseTransition, typeof(EocStateContext))]
    internal class EocPhaseTransitionState : EocStateBase
    {
        public override string StateName => "EocPhaseTransition";
        public override EocStateIndex StateIndex => EocStateIndex.PhaseTransition;
        public override bool AllowFogStep => false;

        private const int LockEnd = 50;
        private const int TearEnd = 130;
        private const int BurstFrame = 130;
        private const int PantEnd = 186;
        private const int TotalTime = 212;

        //三段撕裂绽开帧
        private static readonly int[] TearBursts = [66, 94, 120];
        //痉挛心跳渐急帧
        private static readonly int[] HeartBeats = [6, 20, 32, 42, 48];

        public override void OnEnter(EocStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            context.IsInPhaseTransition = true;
            npc.dontTakeDamage = true;
            npc.damage = 0;

            //清攻击性弹幕（血雾保留成战场地形）
            if (!VaultUtils.isClient) {
                int shotType = ModContent.ProjectileType<EocBloodShot>();
                int spikeType = ModContent.ProjectileType<EocBloodSpike>();
                foreach (Projectile proj in Main.ActiveProjectiles) {
                    if (proj.type == shotType || proj.type == spikeType) {
                        proj.Kill();
                    }
                }
            }
        }

        public override IEocState OnUpdate(EocStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            npc.dontTakeDamage = Timer < PantEnd;
            npc.damage = 0;

            //锁位悬停
            Vector2 anchor = player.Center + new Vector2(0f, -330f);
            EocMotion.SpringHover(npc, anchor, 0.012f, 0.12f, 12f);
            FaceTarget(npc, player.Center, 0.16f);

            if (Timer <= LockEnd) {
                UpdateLock(npc, context);
            }
            else if (Timer <= TearEnd) {
                UpdateTear(npc, context);
            }
            else if (Timer <= PantEnd) {
                UpdatePant(npc, context);
            }

            Timer++;

            if (Timer >= TotalTime) {
                npc.dontTakeDamage = false;
                context.IsInPhaseTransition = false;
                if (VaultUtils.isClient) {
                    return null;
                }
                //二阶段以招牌撕咬直接开场
                return new EocMawFrenzyState();
            }

            return null;
        }

        private void UpdateLock(NPC npc, EocStateContext context) {
            float progress = Timer / (float)LockEnd;
            context.SkinTear = progress * 0.25f;
            context.PushIris(progress * 0.7f, EocMotion.IrisRed);
            EocScreenFX.PushVignette(0.3f * progress);

            //渐急心跳
            foreach (int beat in HeartBeats) {
                if (Timer == beat) {
                    EocScreenFX.PushPulse(0.4f + progress * 0.4f);
                    context.ScalePulse = 1.05f;
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.8f, Pitch = -0.8f + progress * 0.3f }, npc.Center);
                    }
                    break;
                }
            }

            //痉挛渐强
            if (!VaultUtils.isServer && progress > 0.4f) {
                npc.position += Main.rand.NextVector2Circular(1.5f, 1.5f) * progress;
            }
        }

        private void UpdateTear(NPC npc, EocStateContext context) {
            float progress = (Timer - LockEnd) / (float)(TearEnd - LockEnd);
            context.SkinTear = 0.25f + progress * 0.75f;
            context.PushIris(0.8f, EocMotion.BrightBlood);
            EocScreenFX.PushVignette(0.3f + 0.16f * progress);
            EocMotion.Shake(npc.Center, 1.5f + progress * 3f, 6);

            if (!VaultUtils.isServer) {
                npc.position += Main.rand.NextVector2Circular(2.4f, 2.4f) * (0.5f + progress);

                //缝口渗血
                if (Timer % 4 == 0) {
                    Vector2 seamDir = (npc.rotation + MathHelper.PiOver2).ToRotationVector2();
                    Vector2 seamPos = npc.Center + seamDir * Main.rand.NextFloat(-40f, 40f);
                    EocMotion.BloodSpray(seamPos, Main.rand.NextVector2Unit(), 2, 5f, 1.2f);
                }

                //三段绽开：原版表皮碎块+组织碎屑+溅血
                foreach (int burst in TearBursts) {
                    if (Timer != burst) {
                        continue;
                    }
                    SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 1f, Pitch = -0.45f }, npc.Center);
                    SoundEngine.PlaySound(SoundID.NPCDeath12 with { Volume = 0.85f, Pitch = -0.15f }, npc.Center);
                    for (int i = 0; i < 2; i++) {
                        Gore.NewGore(npc.GetSource_FromAI(), npc.position + Main.rand.NextVector2Circular(30f, 30f),
                            Main.rand.NextVector2Circular(5f, 5f) - Vector2.UnitY * 3f, Main.rand.Next(6, 9));
                    }
                    for (int i = 0; i < 7; i++) {
                        PRTLoader.NewParticle<PRT_EocSkinShred>(npc.Center + Main.rand.NextVector2Circular(36f, 36f),
                            Main.rand.NextVector2Unit() * Main.rand.NextFloat(4f, 10f) - Vector2.UnitY * 3f,
                            Color.Lerp(new Color(148, 108, 96), EocMotion.Arterial, Main.rand.NextFloat(0.5f)),
                            Main.rand.NextFloat(0.9f, 1.6f))?.Configure(Main.rand.Next(40, 70));
                    }
                    EocMotion.BloodBurst(npc.Center, 0.9f, playSound: false);
                    context.ScalePulse = 1.12f;
                    EocMotion.Shake(npc.Center, 5.5f, 10);
                    break;
                }
            }

            //蜕壳大爆点
            if (Timer == BurstFrame) {
                DoShellBurst(npc, context);
            }
        }

        /// <summary>蜕壳瞬间：全端本地演出+阶段旗翻转</summary>
        private void DoShellBurst(NPC npc, EocStateContext context) {
            context.IsSecondPhase = true;
            context.SkinTear = 0f;
            context.ScalePulse = 1.26f;
            context.FrameRate = 4;

            //权威端写阶段旗（原版地图头像切换也吃 ai[0]>=2）
            if (!VaultUtils.isClient) {
                npc.ai[0] = 2f;
                npc.netUpdate = true;
            }

            EocScreenFX.PushFlash(0.85f, 14);
            EocScreenFX.PushVignette(0.5f);
            EocMotion.Shake(npc.Center, 12f, 22);

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 1.3f, Pitch = -0.2f }, npc.Center);
                SoundEngine.PlaySound(SoundID.NPCDeath10 with { Volume = 1f, Pitch = -0.3f }, npc.Center);
                VaultUtils.Text(EyeOfCthulhuAI.SkinTear_Text.Value, EocMotion.BrightBlood);

                EocMotion.BloodBurst(npc.Center, 2.3f);
                PRTLoader.NewParticle<PRT_DWave>(npc.Center, Vector2.Zero, EocMotion.Arterial, 0.32f)?
                    .Configure(Vector2.One, Main.rand.NextFloat(MathHelper.TwoPi), 2.4f, 26);
                PRTLoader.NewParticle<PRT_DWave>(npc.Center, Vector2.Zero, EocMotion.BrightBlood * 0.75f, 0.2f)?
                    .Configure(Vector2.One, Main.rand.NextFloat(MathHelper.TwoPi), 1.6f, 20);

                //蜕下的表皮四散
                for (int i = 0; i < 4; i++) {
                    Gore.NewGore(npc.GetSource_FromAI(), npc.position + Main.rand.NextVector2Circular(40f, 40f),
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(4f, 9f), Main.rand.Next(6, 9));
                }
                for (int i = 0; i < 14; i++) {
                    PRTLoader.NewParticle<PRT_EocSkinShred>(npc.Center + Main.rand.NextVector2Circular(44f, 44f),
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(6f, 14f),
                        Color.Lerp(new Color(148, 108, 96), EocMotion.Arterial, Main.rand.NextFloat(0.6f)),
                        Main.rand.NextFloat(1f, 1.9f))?.Configure(Main.rand.Next(50, 84));
                }
            }
        }

        private void UpdatePant(NPC npc, EocStateContext context) {
            //喘息：口器急促开合，持续滴血
            float progress = (Timer - BurstFrame) / (float)(PantEnd - BurstFrame);
            context.FrameRate = 3;
            context.PushIris(0.6f * (1f - progress * 0.4f), EocMotion.IrisRed);
            EocScreenFX.PushVignette(0.42f * (1f - progress * 0.5f));

            if (!VaultUtils.isServer && Timer % 5 == 0) {
                Vector2 mawDir = (npc.rotation + MathHelper.PiOver2).ToRotationVector2();
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(npc.Center + mawDir * 40f,
                    mawDir * 2f + Main.rand.NextVector2Circular(1f, 1f),
                    EocMotion.Arterial, Main.rand.NextFloat(0.8f, 1.4f))?
                    .Configure(Main.rand.Next(24, 40), 0.36f, 0.985f);
            }
            if (!VaultUtils.isServer && Timer % 18 == 0) {
                SoundEngine.PlaySound(SoundID.Zombie2 with { Volume = 0.55f, Pitch = -0.35f }, npc.Center);
            }
        }

        public override void OnExit(EocStateContext context) {
            base.OnExit(context);
            context.IsInPhaseTransition = false;
            context.Npc.dontTakeDamage = false;
            context.FrameRate = context.IsSecondPhase ? 4 : 6;
            context.ClearAttackBag();
        }
    }
}
