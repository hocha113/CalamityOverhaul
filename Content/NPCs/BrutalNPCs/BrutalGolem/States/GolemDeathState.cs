using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.States
{
    /// <summary>
    /// 石像崩解（死亡演出）：踉跄 → 裂纹蔓延 → 自上而下崩解 → 太阳宝石谢幕。
    /// 碎屑全部是原版神庙石材（砖格 / 石巨人 gore / 本体切片），由各端按共享时间轴自己生成；
    /// 服务端只做静默移除与真死，不调用 HitEffect（服务端 HitEffect 无视觉，客户端也收不到）
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)GolemStateIndex.Death, typeof(GolemStateContext))]
    internal class GolemDeathState : GolemStateBase
    {
        public override string StateName => "Death";
        public override GolemStateIndex StateIndex => GolemStateIndex.Death;

        internal static int StaggerEnd => 70;
        internal static int CrackEnd => 180;
        internal static int CollapseEnd => 262;
        internal static int FinaleEnd => 336;

        //部件清场拍：拳先碎，头后碎
        private const int FistBurstTick = 218;
        private const int HeadBurstTick = 242;

        //裂响加速节拍表（帧间隔递减，仪式感）
        private static readonly int[] CrackBeats = [70, 92, 110, 125, 138, 149, 158, 165, 171, 176];

        //原版大块 gore 按部位被蚀线吞掉的时刻投放（帧内归一高度）
        private static readonly (float Frac, int[] Gores)[] GoreDrops = [
            (0.20f, GolemRubble.ShoulderGores),
            (0.42f, [360, 361]),
            (0.58f, [364]),
            (0.80f, GolemRubble.LegGores),
        ];

        //客户端演出账本：蚀线推进、一次性拍是否已放、部件最后可见位置（部件被服务端移除后仍要在原地碎）
        private float lastErodeFrac;
        private float lastSliceFrac;
        private int goreDropCursor;
        private bool fistBurstDone;
        private bool headBurstDone;
        private Vector2? leftFistPos;
        private Vector2? rightFistPos;
        private Vector2? headPos;

        public override void OnEnter(GolemStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;

            npc.ai[GolemAiSlots.BodyPhase] = GolemPhase.DeathShow;
            npc.velocity *= 0.3f;
            context.DeathTimer = 0;
            context.DeathPhase = GolemDeathPhase.Stagger;
            GolemBodyAI.ActivePerformanceBody = npc.whoAmI;

            lastErodeFrac = 0f;
            lastSliceFrac = 0f;
            goreDropCursor = 0;
            fistBurstDone = false;
            headBurstDone = false;
            leftFistPos = rightFistPos = headPos = null;

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
                GolemRubble.CacheBodyMask(npc);
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

            if (!VaultUtils.isServer) {
                RememberPartPositions(context);
            }

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

            //落幕：放行真死。躯干早在崩解期化为碎屑，这里不再 HitEffect 凭空弹 gore
            if (Timer >= FinaleEnd) {
                context.DeathPerformanceFinished = true;
                if (GolemBodyAI.ActivePerformanceBody == npc.whoAmI) {
                    GolemBodyAI.ActivePerformanceBody = -1;
                }
                if (!VaultUtils.isClient) {
                    npc.dontTakeDamage = false;
                    npc.life = 0;
                    npc.checkDead();
                    npc.netUpdate = true;
                }
            }
            return null;
        }

        public override void OnExit(GolemStateContext context) {
            base.OnExit(context);
            GolemRubble.ReleaseBodyMask();
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

        /// <summary>蚀线在世界中的高度：帧内归一高度换算到绘制矩形（本体以帧中心对齐 npc.Center 绘制）</summary>
        private static float ErodeWorldY(NPC npc, float frac) {
            int frameHeight = npc.frame.Height > 0
                ? npc.frame.Height
                : TextureAssets.Npc[npc.type].Value.Height / Math.Max(Main.npcFrameCount[npc.type], 1);
            return npc.Center.Y + (frac - 0.5f) * frameHeight * npc.scale;
        }

        /// <summary>记住部件最后可见位置：服务端清场包先到时，客户端仍能在原地碎</summary>
        private void RememberPartPositions(GolemStateContext context) {
            GolemLimbStatus limbs = context.Limbs;
            if (limbs.LeftFistAlive) {
                leftFistPos = Main.npc[limbs.LeftFistIndex].Center;
            }
            if (limbs.RightFistAlive) {
                rightFistPos = Main.npc[limbs.RightFistIndex].Center;
            }
            if (limbs.FreeHeadAlive) {
                headPos = Main.npc[limbs.FreeHeadIndex].Center;
            }
            else if (limbs.HeadAlive) {
                headPos = Main.npc[limbs.HeadIndex].Center;
            }
        }

        /// <summary>踉跄：躯干晃动，宝石光乱闪，顶缘抖落砖屑</summary>
        private void UpdateStagger(GolemStateContext context) {
            NPC npc = context.Npc;
            context.VeinGlow = 0.5f + 0.5f * MathF.Sin(Timer * 0.6f) * MathF.Sin(Timer * 0.23f);

            if (VaultUtils.isServer) {
                return;
            }
            if (Timer % 20 == 5) {
                SoundEngine.PlaySound(SoundID.WormDig with { Pitch = -0.5f, Volume = 0.7f }, npc.Center);
                GolemScreenEffects.Shake(2f);
                //每个震动拍从顶缘抖落几块砖角
                int chips = Main.rand.Next(2, 4);
                for (int i = 0; i < chips; i++) {
                    Vector2 pos = npc.Top + new Vector2(Main.rand.NextFloat(-0.35f, 0.35f) * npc.width, Main.rand.NextFloat(0f, 12f));
                    GolemRubble.SpawnBrick(pos, new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(-0.6f, 0.4f)),
                        heat: 0.2f, quadrant: true, rest: 100, scale: npc.scale);
                }
                GolemRubble.SpawnDust(npc.Top + new Vector2(0f, 8f), 4, npc.width * 0.3f, new Vector2(0f, 0.6f));
            }
            if (Timer % 7 == 0) {
                Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Lihzahrd, 0f, 1f, 60, default, 1.1f);
                dust.velocity *= 0.3f;
            }
        }

        /// <summary>裂纹蔓延：加速的碎裂节拍，光从石缝漏出，裂点弹出热碎块</summary>
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
                    //裂点崩出带岩浆断口的小碎块
                    int chunks = Main.rand.Next(2, 4);
                    for (int i = 0; i < chunks; i++) {
                        int gore = GolemRubble.SmallChunks[Main.rand.Next(GolemRubble.SmallChunks.Length)];
                        Vector2 vel = VaultUtils.RandVr(1.5f, 3.5f);
                        vel.Y -= 1f;
                        GolemRubble.SpawnGore(gore, crackPos, vel, heat: 1f, rest: 150, scale: npc.scale);
                    }
                    GolemRubble.SpawnDust(crackPos, 6, 6f, Vector2.Zero);
                    break;
                }
            }

            //持续掉渣：砖角带余温
            if (Timer % 6 == 0) {
                Vector2 pos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.45f, npc.height * 0.45f);
                GolemRubble.SpawnBrick(pos, new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1f, 0.5f)),
                    heat: 0.6f, quadrant: true, rest: 110, scale: npc.scale);
            }
        }

        /// <summary>崩解：沿侵蚀线切下本体、投放原版大块 gore、扬尘起烟；部件清场</summary>
        private void UpdateCollapse(GolemStateContext context) {
            NPC npc = context.Npc;

            if (!VaultUtils.isServer) {
                float curFrac = GolemRubble.ErodeFraction(GetCrumble(Timer));
                float erodeY = ErodeWorldY(npc, curFrac);
                float halfW = npc.frame.Width > 0 ? npc.frame.Width * npc.scale * 0.42f : npc.width * 0.45f;

                if (Timer % 3 == 0) {
                    GolemScreenEffects.Shake(2.5f);
                }

                //沿蚀线切本体：取刚被吞掉的那一段
                if (Timer % 4 == 0 && curFrac - lastSliceFrac > 0.002f) {
                    GolemRubble.SpawnBodySlices(npc, lastSliceFrac, curFrac, Main.rand.Next(3, 5), heat: 0.9f);
                    lastSliceFrac = curFrac;
                    Vector2 brickPos = new(npc.Center.X + Main.rand.NextFloat(-halfW, halfW), erodeY);
                    GolemRubble.SpawnBrick(brickPos, new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-1.5f, 0.5f)),
                        heat: 0.5f, quadrant: false, rest: 130, scale: npc.scale);
                }

                //原版大块 gore：蚀线吞过部位时投放
                while (goreDropCursor < GoreDrops.Length && curFrac >= GoreDrops[goreDropCursor].Frac && lastErodeFrac < GoreDrops[goreDropCursor].Frac) {
                    Rectangle area = new((int)(npc.Center.X - halfW), (int)erodeY - 10, (int)(halfW * 2f), 20);
                    GolemRubble.SpawnGores(GoreDrops[goreDropCursor].Gores, area, new Vector2(0f, -1f), 2.4f, heat: 0.7f, scale: npc.scale, rest: 170);
                    goreDropCursor++;
                }
                lastErodeFrac = Math.Max(lastErodeFrac, curFrac);

                //蚀线扬尘 + 真 alpha 烟
                if (curFrac < 0.999f) {
                    GolemRubble.SpawnDust(new Vector2(npc.Center.X, erodeY), 3, halfW, new Vector2(0f, 0.8f), 1.2f);
                    if (Timer % 6 == 0) {
                        GolemRubble.SpawnSmoke(npc.GetSource_FromAI(),
                            new Vector2(npc.Center.X + Main.rand.NextFloat(-halfW, halfW), erodeY),
                            1, new Vector2(0f, -0.6f), 1.1f);
                    }
                }

                if (Timer == CrackEnd + 4) {
                    SoundEngine.PlaySound(SoundID.NPCDeath14 with { Pitch = -0.5f, Volume = 1f }, npc.Center);
                }

                //部件清场视觉：客户端按共享时间轴自己碎，用记住的位置兜底
                if (!fistBurstDone && Timer >= FistBurstTick) {
                    fistBurstDone = true;
                    BurstFist(npc, leftFistPos);
                    BurstFist(npc, rightFistPos);
                }
                if (!headBurstDone && Timer >= HeadBurstTick) {
                    headBurstDone = true;
                    BurstHead(npc, headPos);
                }
            }

            //部件清场（服务端静默移除，视觉由客户端时间轴驱动）
            if (!VaultUtils.isClient) {
                GolemLimbStatus limbs = context.Limbs;
                if (Timer == FistBurstTick) {
                    RemovePartSilently(limbs.LeftFistIndex);
                    RemovePartSilently(limbs.RightFistIndex);
                }
                if (Timer == HeadBurstTick) {
                    RemovePartSilently(limbs.FreeHeadIndex);
                    RemovePartSilently(limbs.HeadIndex);
                }
            }
        }

        /// <summary>拳碎：拳 gore + 小碎块 + 砖格 + 尘 + 烟</summary>
        private static void BurstFist(NPC body, Vector2? at) {
            if (at is not Vector2 center) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCDeath43 with { Pitch = -0.3f, Volume = 0.7f, MaxInstances = 2 }, center);
            Rectangle area = new((int)center.X - 24, (int)center.Y - 20, 48, 40);
            GolemRubble.SpawnGores(GolemRubble.FistGores, area, new Vector2(0f, -3f), 2.6f, heat: 0.8f, scale: body.scale, rest: 160);
            GolemRubble.SpawnGores([368, 368], area, new Vector2(0f, -2.5f), 3f, heat: 1f, scale: body.scale, rest: 150);
            for (int i = 0; i < 4; i++) {
                GolemRubble.SpawnBrick(center + Main.rand.NextVector2Circular(20f, 14f),
                    new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-4f, -1f)),
                    heat: 0.5f, quadrant: Main.rand.NextBool(), rest: 140, scale: body.scale);
            }
            GolemRubble.SpawnDust(center, 20, 24f, new Vector2(0f, -1f), 1.3f);
            GolemRubble.SpawnSmoke(body.GetSource_FromAI(), center, 3, new Vector2(0f, -0.8f), 1.1f);
        }

        /// <summary>头碎：头 gore + 小碎块 + 砖格 + 尘 + 烟</summary>
        private static void BurstHead(NPC body, Vector2? at) {
            if (at is not Vector2 center) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCDeath43 with { Pitch = -0.15f, Volume = 0.8f, MaxInstances = 2 }, center);
            Rectangle area = new((int)center.X - 26, (int)center.Y - 22, 52, 44);
            GolemRubble.SpawnGores(GolemRubble.HeadGores, area, new Vector2(0f, -3f), 2.6f, heat: 0.8f, scale: body.scale, rest: 170);
            GolemRubble.SpawnGores(GolemRubble.SmallChunks, area, new Vector2(0f, -2.5f), 3f, heat: 1f, scale: body.scale, rest: 150);
            for (int i = 0; i < 3; i++) {
                GolemRubble.SpawnBrick(center + Main.rand.NextVector2Circular(20f, 16f),
                    new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-4f, -1f)),
                    heat: 0.5f, quadrant: Main.rand.NextBool(), rest: 140, scale: body.scale);
            }
            GolemRubble.SpawnDust(center, 16, 24f, new Vector2(0f, -1f), 1.3f);
            GolemRubble.SpawnSmoke(body.GetSource_FromAI(), center, 3, new Vector2(0f, -0.8f), 1.1f);
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

        /// <summary>
        /// 静默移除部件：不走 HitEffect（服务端无视觉、客户端收不到，且附着头的原版 HitEffect 会生成分离头），
        /// 碎裂表现由客户端按 <see cref="FistBurstTick"/>/<see cref="HeadBurstTick"/> 自己生成
        /// </summary>
        private static void RemovePartSilently(int index) {
            if (index < 0 || index >= Main.maxNPCs) {
                return;
            }
            NPC part = Main.npc[index];
            if (!part.active) {
                return;
            }
            part.life = 0;
            part.active = false;
            part.netUpdate = true;
        }
    }
}
