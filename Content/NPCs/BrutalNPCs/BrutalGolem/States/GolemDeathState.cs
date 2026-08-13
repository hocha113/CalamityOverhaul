using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.States
{
    /// <summary>石像崩解（死亡演出）：踉跄 → 裂纹蔓延 → 自上而下崩解 → 太阳宝石谢幕</summary>
    [InnoVault.StateMachines.VaultState((int)GolemStateIndex.Death, typeof(GolemStateContext))]
    internal class GolemDeathState : GolemStateBase
    {
        public override string StateName => "Death";
        public override GolemStateIndex StateIndex => GolemStateIndex.Death;

        internal static int StaggerEnd => 70;
        internal static int CrackEnd => 180;
        internal static int CollapseEnd => 262;
        internal static int FinaleEnd => 336;

        //裂响加速节拍表（帧间隔递减，仪式感）
        private static readonly int[] CrackBeats = [70, 92, 110, 125, 138, 149, 158, 165, 171, 176];

        public override void OnEnter(GolemStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;

            npc.ai[GolemAiSlots.BodyPhase] = GolemPhase.DeathShow;
            npc.velocity *= 0.3f;
            context.DeathTimer = 0;
            context.DeathPhase = GolemDeathPhase.Stagger;
            GolemBodyAI.ActivePerformanceBody = npc.whoAmI;

            //清debuff
            for (int i = 0; i < npc.buffType.Length; i++) {
                npc.buffTime[i] = 0;
            }

            if (!VaultUtils.isClient) {
                //公平阀：清场敌方弹幕
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile p = Main.projectile[i];
                    if (p.active && p.hostile) {
                        p.Kill();
                    }
                }
                //双拳松脱坠地
                GolemLimbStatus limbs = context.Limbs;
                if (limbs.LeftFistAlive) {
                    GolemBodyAI.CommandFist(limbs.LeftFistIndex, GolemFistCommand.DeathFall, npc.Center, 10, 10f, 0);
                }
                if (limbs.RightFistAlive) {
                    GolemBodyAI.CommandFist(limbs.RightFistIndex, GolemFistCommand.DeathFall, npc.Center, 10, 10f, 0);
                }
            }

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCDeath14 with { Pitch = -0.6f, Volume = 0.8f }, npc.Center);
                SoundEngine.PlaySound(SoundID.WormDig with { Pitch = -0.9f, Volume = 1.1f }, npc.Center);
            }
        }

        public override IGolemState OnUpdate(GolemStateContext context) {
            NPC npc = context.Npc;

            //锁血急停
            npc.dontTakeDamage = true;
            npc.damage = 0;
            npc.noTileCollide = false;
            if (npc.life < 1) {
                npc.life = 1;
            }
            GroundBrake(npc, 0.7f);
            context.FrameMode = 0;
            context.VeinGlow = 1f;

            GolemDeathPhase phase = GetDeathPhase(Timer);
            context.DeathPhase = phase;
            context.DeathTimer = Timer;

            switch (phase) {
                case GolemDeathPhase.Stagger:
                    UpdateStagger(context);
                    break;
                case GolemDeathPhase.Crack:
                    UpdateCrack(context);
                    break;
                case GolemDeathPhase.Collapse:
                    UpdateCollapse(context);
                    break;
                case GolemDeathPhase.GemFinale:
                    UpdateGemFinale(context);
                    break;
            }

            Timer++;

            //落幕：放行真死
            if (Timer >= FinaleEnd) {
                context.DeathPerformanceFinished = true;
                if (GolemBodyAI.ActivePerformanceBody == npc.whoAmI) {
                    GolemBodyAI.ActivePerformanceBody = -1;
                }
                if (!VaultUtils.isClient) {
                    npc.dontTakeDamage = false;
                    npc.life = 0;
                    npc.HitEffect();
                    npc.checkDead();
                    npc.netUpdate = true;
                }
            }
            return null;
        }

        public override void OnExit(GolemStateContext context) {
            base.OnExit(context);
            if (GolemBodyAI.ActivePerformanceBody == context.Npc.whoAmI) {
                GolemBodyAI.ActivePerformanceBody = -1;
            }
        }

        internal static GolemDeathPhase GetDeathPhase(int timer) {
            if (timer < StaggerEnd) {
                return GolemDeathPhase.Stagger;
            }
            if (timer < CrackEnd) {
                return GolemDeathPhase.Crack;
            }
            if (timer < CollapseEnd) {
                return GolemDeathPhase.Collapse;
            }
            return GolemDeathPhase.GemFinale;
        }

        /// <summary>崩解侵蚀进度 0~1（渲染层读取）</summary>
        internal static float GetCrumble(int timer) {
            if (timer < StaggerEnd) {
                return 0f;
            }
            if (timer < CrackEnd) {
                //裂纹期：只在顶缘咬出发丝缝
                return MathHelper.Lerp(0f, 0.06f, (timer - StaggerEnd) / (float)(CrackEnd - StaggerEnd));
            }
            if (timer < CollapseEnd) {
                //崩解期：自上而下吞没
                float t = (timer - CrackEnd) / (float)(CollapseEnd - CrackEnd);
                return MathHelper.Lerp(0.06f, 1f, t * t);
            }
            return 1f;
        }

        /// <summary>踉跄：躯干晃动，宝石光乱闪</summary>
        private void UpdateStagger(GolemStateContext context) {
            NPC npc = context.Npc;
            context.VeinGlow = 0.5f + 0.5f * MathF.Sin(Timer * 0.6f) * MathF.Sin(Timer * 0.23f);

            if (VaultUtils.isServer) {
                return;
            }
            if (Timer % 20 == 5) {
                SoundEngine.PlaySound(SoundID.WormDig with { Pitch = -0.5f, Volume = 0.7f }, npc.Center);
                GolemScreenEffects.Shake(2f);
            }
            if (Timer == 20) {
                GolemBodyAI.Broadcast(GolemBodyAI.GolemCrumble_Text, new Color(230, 170, 90));
            }
            if (Timer % 7 == 0) {
                Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Stone, 0f, 1f, 60, default, 1.1f);
                dust.velocity *= 0.3f;
            }
        }

        /// <summary>裂纹蔓延：加速的碎裂节拍，光从石缝漏出</summary>
        private void UpdateCrack(GolemStateContext context) {
            NPC npc = context.Npc;

            if (VaultUtils.isServer) {
                return;
            }

            //加速节拍表
            foreach (int beat in CrackBeats) {
                if (Timer == beat) {
                    float progress = (Timer - StaggerEnd) / (float)(CrackEnd - StaggerEnd);
                    SoundEngine.PlaySound(SoundID.Tink with {
                        Pitch = -0.6f + progress * 0.8f,
                        Volume = 0.7f + progress * 0.4f
                    }, npc.Center);
                    GolemScreenEffects.Shake(1.5f + progress * 3f);
                    //裂缝喷光
                    Vector2 crackPos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.4f, npc.height * 0.4f);
                    for (int i = 0; i < 5; i++) {
                        PRTLoader.NewParticle<PRT_Spark>(crackPos, VaultUtils.RandVr(1.5f, 4f),
                            new Color(255, 205, 110), Main.rand.NextFloat(0.8f, 1.2f)).Configure(true, 20);
                    }
                    break;
                }
            }

            //持续掉渣
            if (Timer % 5 == 0) {
                PRTLoader.NewParticle<PRT_MarbleChip>(
                    npc.Center + Main.rand.NextVector2Circular(npc.width * 0.45f, npc.height * 0.45f),
                    new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1f, 0.5f)),
                    new Color(122, 104, 78), Main.rand.NextFloat(0.6f, 1f)).Configure(40);
            }
        }

        /// <summary>崩解：沿侵蚀线倾泻碎屑与烟，部件清场</summary>
        private void UpdateCollapse(GolemStateContext context) {
            NPC npc = context.Npc;

            //侵蚀线高度（自上而下）
            float crumble = GetCrumble(Timer);
            float erodeY = npc.position.Y + npc.height * crumble;

            if (!VaultUtils.isServer) {
                if (Timer % 3 == 0) {
                    GolemScreenEffects.Shake(2.5f);
                }
                //侵蚀线上倾泻碎屑
                for (int i = 0; i < 3; i++) {
                    Vector2 pos = new(npc.position.X + Main.rand.NextFloat(npc.width), erodeY + Main.rand.NextFloat(-8f, 8f));
                    PRTLoader.NewParticle<PRT_MarbleChip>(pos,
                        new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), Main.rand.NextFloat(-2f, 1f)),
                        new Color(122, 104, 78), Main.rand.NextFloat(0.7f, 1.3f)).Configure(50);
                }
                if (Timer % 4 == 0) {
                    PRTLoader.NewParticle<PRT_Smoke>(new Vector2(npc.position.X + Main.rand.NextFloat(npc.width), erodeY),
                        -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.2f),
                        new Color(74, 64, 54), Main.rand.NextFloat(0.7f, 1.2f)).Configure(44, 0.6f);
                }
                if (Timer == CrackEnd + 4) {
                    SoundEngine.PlaySound(SoundID.NPCDeath14 with { Pitch = -0.5f, Volume = 1f }, npc.Center);
                }
            }

            //部件清场（服务端）
            if (!VaultUtils.isClient) {
                GolemLimbStatus limbs = context.Limbs;
                //拳先碎
                if (Timer == 218) {
                    RemovePartWithBurst(limbs.LeftFistIndex);
                    RemovePartWithBurst(limbs.RightFistIndex);
                }
                //头后碎（坠毁的飞头/仍附着的头）
                if (Timer == 242) {
                    RemovePartWithBurst(limbs.FreeHeadIndex);
                    RemovePartWithBurst(limbs.HeadIndex);
                }
            }
        }

        /// <summary>宝石谢幕：太阳宝石浮出废墟 → 三声碎响 → 金光散尽</summary>
        private void UpdateGemFinale(GolemStateContext context) {
            NPC npc = context.Npc;

            if (VaultUtils.isServer) {
                return;
            }

            //宝石位置由渲染层按同一时间轴推演，这里只管音画节拍
            if (Timer == CollapseEnd + 2) {
                SoundEngine.PlaySound(SoundID.Item4 with { Pitch = -0.2f, Volume = 0.9f }, npc.Center);
            }
            //三声碎响
            if (Timer == 292 || Timer == 307 || Timer == 318) {
                int i = Timer >= 318 ? 2 : Timer >= 307 ? 1 : 0;
                SoundEngine.PlaySound(SoundID.Tink with { Pitch = 0.2f + i * 0.25f, Volume = 1f }, npc.Center);
                GolemScreenEffects.Shake(2f + i * 1.5f);
            }
            //终幕金光
            if (Timer == 326) {
                Vector2 gemPos = GolemRenderHelperGemPos(npc, Timer);
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.3f, Volume = 1f }, gemPos);
                SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.5f, Volume = 0.9f }, gemPos);
                GolemScreenEffects.PushSunFlash(gemPos, 0.85f, 34);
                GolemScreenEffects.PushShockRing(gemPos, 1f, 820f);
                GolemScreenEffects.Shake(8f);
                for (int i = 0; i < 40; i++) {
                    PRTLoader.NewParticle<PRT_Light>(gemPos, VaultUtils.RandVr(2f, 9f),
                        new Color(255, 215, 120), Main.rand.Next(1, 3)).Configure(36);
                }
            }
        }

        /// <summary>宝石谢幕轨迹（渲染层与音画共用同一时间轴）</summary>
        internal static Vector2 GolemRenderHelperGemPos(NPC npc, int timer) {
            float t = MathHelper.Clamp((timer - CollapseEnd) / (float)(FinaleEnd - CollapseEnd - 10), 0f, 1f);
            float rise = MathHelper.SmoothStep(0f, 150f, t);
            return npc.Bottom + new Vector2(0f, -40f - rise);
        }

        private static void RemovePartWithBurst(int index) {
            if (index < 0 || index >= Main.maxNPCs) {
                return;
            }
            NPC part = Main.npc[index];
            if (!part.active) {
                return;
            }
            //碎裂表现走 HitEffect 前先清血防原版分离头逻辑
            if (part.type == NPCID.GolemHead) {
                //附着头静默移除，防止原版 HitEffect 生成分离头
                part.life = 0;
                part.active = false;
                part.netUpdate = true;
                return;
            }
            part.life = 0;
            part.HitEffect();
            part.active = false;
            part.netUpdate = true;
        }
    }
}
