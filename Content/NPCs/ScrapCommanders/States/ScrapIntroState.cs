using CalamityOverhaul.Content.NPCs.ScrapCommanders.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.ScrapCommanders.States
{
    /// <summary>
    /// 零件雨自组装进场：落点尘圈预兆 → 四件工具先后从天砸进地面 →
    /// 静默一拍（只有烟与电弧）→ 磁力吊线亮起逐件拔起 → 头压轴坠下入位 →
    /// 目镜觉醒扫光。全程无敌不攻击，节拍全部键控本地 Timer
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)ScrapStateIndex.Intro, typeof(ScrapStateContext))]
    internal class ScrapIntroState : ScrapStateBase
    {
        public override string StateName => "Intro";
        public override ScrapStateIndex StateIndex => ScrapStateIndex.Intro;

        //==================== 时序 ====================

        private const int OmenStart = 4;
        /// <summary>第 slot 件工具开始坠落的拍</summary>
        private static int FallBeat(int slot) => 10 + slot * 16;
        private const int MagnetBeat = 150;
        /// <summary>第 slot 件工具被磁力拔起的拍</summary>
        private static int PullBeat(int slot) => 158 + slot * 14;
        private const int HeadDropBeat = 216;
        private const int HeadSlamBeat = 238;
        private const int AwakenBeat = 252;
        private const int IntroEnd = 286;

        /// <summary>坠落起始高度（相对地面线）</summary>
        private const float FallHeight = 780f;

        /// <summary>坠落次序：外侧炮先落、镭射收尾，链条不交叉</summary>
        private static readonly int[] CrashOrder = {
            ScrapCommander.ArmCannon, ScrapCommander.ArmSaw,
            ScrapCommander.ArmVice, ScrapCommander.ArmLaser,
        };
        private static readonly float[] CrashOffsetX = { -210f, -80f, 80f, 210f };

        //==================== 本地闩（Timer 单调，不回卷）====================

        private readonly bool[] landed = new bool[ScrapCommander.ArmCount];
        private readonly bool[] pulled = new bool[ScrapCommander.ArmCount];
        private bool headDropped;
        private bool headSlammed;
        private bool awakened;
        private bool magnetStarted;

        private static int SlotOfArm(int arm) => Array.IndexOf(CrashOrder, arm);

        public override IScrapState OnUpdate(ScrapStateContext ctx) {
            NPC npc = ctx.Npc;
            ScrapCommander owner = ctx.Owner;
            int t = (int)Timer;

            if (t == 0) {
                //各端本地记锚点与落点；服务器把头藏到高空并盖章
                ctx.IntroAnchor = ctx.Target.Center + new Vector2(0f, -240f);
                for (int slot = 0; slot < ScrapCommander.ArmCount; slot++) {
                    float x = ctx.IntroAnchor.X + CrashOffsetX[slot];
                    //悬空/深渊兜底：找不到近处地面就用玩家脚下虚拟地板，演出不掉深渊
                    float groundY = MathF.Min(FindGroundY(new Vector2(x, ctx.Target.Center.Y - 40f)),
                        ctx.Target.Center.Y + 320f);
                    ctx.IntroCrashSpot[CrashOrder[slot]] = new Vector2(x, groundY);
                }
                if (!VaultUtils.isClient) {
                    npc.Center = ctx.IntroAnchor + new Vector2(0f, -620f);
                    npc.velocity = Vector2.Zero;
                    npc.netUpdate = true;
                }
                owner.RebuildArms(ctx.IntroAnchor);
            }

            npc.dontTakeDamage = true;
            ctx.Phase = 0;

            //==================== 落点预兆：阴影柱 + 尘圈 ====================
            if (t >= OmenStart) {
                for (int slot = 0; slot < ScrapCommander.ArmCount; slot++) {
                    int beat = FallBeat(slot);
                    Vector2 spot = ctx.IntroCrashSpot[CrashOrder[slot]];
                    //天坠柱预警虚线：落点在哪一目了然
                    if (t < beat + 26) {
                        float colAlpha = MathHelper.Clamp((t - OmenStart) / 14f, 0f, 0.6f);
                        ctx.AddTelegraph(new Vector2(spot.X, spot.Y - FallHeight), Vector2.UnitY,
                            FallHeight, colAlpha, 0.45f);
                    }
                    //坠落前和坠落途中都留着落点警示
                    if (!Main.dedServ && t < beat + 30 && t % 5 == slot % 5) {
                        Dust dust = Dust.NewDustPerfect(
                            spot + new Vector2(Main.rand.NextFloat(-26f, 26f), -2f),
                            DustID.Smoke, new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.4f)),
                            120, default, Main.rand.NextFloat(0.8f, 1.3f));
                        dust.noGravity = true;
                    }
                }
            }

            //==================== 工具坠落与砸地 ====================
            for (int i = 0; i < ScrapCommander.ArmCount; i++) {
                int slot = SlotOfArm(i);
                int beat = FallBeat(slot);
                Vector2 spot = ctx.IntroCrashSpot[i];
                //工具中心停在略高于地面线，读出半嵌进土里的坐姿
                Vector2 restOnGround = new(spot.X, spot.Y - 16f);

                if (t < beat) {
                    //待命：钉在高空，隐形
                    ctx.Arms[i] = new ArmDirective {
                        Mode = ArmMode.Snap,
                        Target = new Vector2(spot.X, spot.Y - FallHeight),
                    };
                    ctx.ToolAlpha[i] = 0f;
                }
                else if (!pulled[i]) {
                    //坠落/嵌地
                    ctx.Arms[i] = new ArmDirective {
                        Mode = ArmMode.Fall,
                        Target = restOnGround,
                        UseRot = true,
                        //各件砸出不同的歪斜角，读出残骸感
                        WantRot = MathF.Sin(owner.Seed + i * 2.13f) * 0.5f,
                        RotRate = landed[i] ? 0.08f : 0.3f,
                    };
                    //砸地拍（位置闩：过线只响一次）
                    if (!landed[i] && owner.GetArmPos(i).Y >= restOnGround.Y - 2f) {
                        landed[i] = true;
                        ToolCrashBeat(i, spot);
                    }
                    //嵌地冒烟
                    if (landed[i] && !Main.dedServ && t % 12 == (i * 3) % 12) {
                        PRTLoader.NewParticle<PRT_GhostRainMist>(
                            owner.GetArmPos(i) + new Vector2(Main.rand.NextFloat(-14f, 14f), -8f),
                            new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -Main.rand.NextFloat(0.4f, 0.9f)),
                            ScrapCommander.SmokeGray, Main.rand.NextFloat(0.5f, 0.8f))
                            ?.Configure(Main.rand.Next(40, 70));
                    }
                }
            }

            //静默段的电弧滋滋：残骸不甘地放两次电
            if (!Main.dedServ && (t == 118 || t == 136)) {
                int arm = t == 118 ? ScrapCommander.ArmSaw : ScrapCommander.ArmLaser;
                Vector2 pos = owner.GetArmPos(arm);
                SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with { Volume = 0.5f, Pitch = -0.2f, MaxInstances = 2 }, pos);
                for (int k = 0; k < 4; k++) {
                    PRTLoader.NewParticle<PRT_Spark>(pos + Main.rand.NextVector2Circular(12f, 8f),
                        Main.rand.NextVector2Circular(2.4f, 2.4f),
                        Color.Lerp(ScrapCommander.WeldOrange, Color.White, Main.rand.NextFloat(0.5f)),
                        Main.rand.NextFloat(0.5f, 0.8f))?.Configure(true, Main.rand.Next(8, 14));
                }
            }

            //==================== 磁力启动与逐件拔起 ====================
            if (t >= MagnetBeat) {
                ctx.MagnetGlow = MathHelper.Clamp((t - MagnetBeat) / 18f, 0f, 1f);
                ctx.MagnetPull = 1f;
                if (!magnetStarted) {
                    magnetStarted = true;
                    owner.EnsureMagnetFieldProj();
                    SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Volume = 0.6f, Pitch = -0.25f, MaxInstances = 1 }, ctx.IntroAnchor);
                    SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.45f, Pitch = -0.4f, MaxInstances = 2 }, ctx.IntroAnchor);
                }
            }
            for (int slot = 0; slot < ScrapCommander.ArmCount; slot++) {
                int i = CrashOrder[slot];
                if (t < PullBeat(slot)) {
                    continue;
                }
                if (!pulled[i]) {
                    pulled[i] = true;
                    ToolPullBeat(owner, i, slot);
                }
                //吊装持位：挂到组装锚点的队形位上，强弹簧自带过冲
                ctx.Arms[i] = ArmDirective.HoldAt(
                    ctx.IntroAnchor + ScrapCommander.RestOffset[i], 0.3f, 0.78f);
            }

            //==================== 头压轴坠下 ====================
            //坠落在各端确定性积分（同一初速同一重力），到拍吸附，快照纠偏只剩十几像素
            ctx.HeadAlpha = t < HeadDropBeat ? 0f : MathHelper.Clamp((t - HeadDropBeat) / 5f, 0f, 1f);
            if (t >= HeadDropBeat && !headDropped) {
                headDropped = true;
                npc.velocity = new Vector2(0f, 8f);
                if (!VaultUtils.isClient) {
                    npc.netUpdate = true;
                }
                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.7f, Pitch = -0.5f, MaxInstances = 1 }, ctx.IntroAnchor);
            }
            if (!headDropped) {
                npc.velocity = Vector2.Zero;
            }
            else if (!headSlammed) {
                npc.velocity.Y = MathF.Min(npc.velocity.Y + 2f, 40f);
            }
            if (t >= HeadSlamBeat && !headSlammed) {
                headSlammed = true;
                npc.Center = ctx.IntroAnchor;
                npc.velocity = Vector2.Zero;
                if (!VaultUtils.isClient) {
                    npc.netUpdate = true;
                }
                HeadSlamBurst(owner, npc.Center);
            }
            if (headSlammed) {
                npc.velocity = Vector2.Zero;
            }

            //==================== 觉醒 ====================
            if (t >= AwakenBeat) {
                if (!awakened) {
                    awakened = true;
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.7f, Pitch = -0.55f, MaxInstances = 1 }, npc.Center);
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.6f, Pitch = -0.3f, MaxInstances = 2 }, npc.Center);
                    for (int i = 0; i < ScrapCommander.ArmCount; i++) {
                        owner.ImpulseArm(i, (owner.RestTarget(i) - owner.GetArmPos(i)) * 0.16f + new Vector2(0f, -1.8f));
                    }
                    ShakeNearby(npc.Center, 2.5f);
                    //组装完成的热浪脉冲
                    if (!Main.dedServ) {
                        PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(npc.Center, Vector2.Zero,
                            ScrapCommander.WeldOrange * 0.8f, 1f)?.Configure(0.1f, 0.9f, 16);
                    }
                }
                //觉醒后四臂并入头的队形（默认 Hang），目镜扫光亮起
                float sweep = (t - AwakenBeat) / 26f;
                if (sweep <= 1f) {
                    ctx.EyeScan = sweep;
                    //探照灯锥扫过玩家：第一句"我看见你了"
                    Vector2 eye = npc.Center + new Vector2(0f, 8f);
                    Vector2 toPlayer = (ctx.Target.Center - eye).SafeNormalize(Vector2.UnitY);
                    Vector2 dir = toPlayer.RotatedBy(MathHelper.Lerp(-0.5f, 0.1f, sweep));
                    ctx.AddSolidBeam(eye, dir, 900f, MathF.Sin(sweep * MathHelper.Pi) * 0.7f, 0.8f);
                }
            }

            Timer++;
            if (Timer > IntroEnd) {
                ctx.Phase = 1;
                ctx.AttackCooldown = 50;
                if (!VaultUtils.isClient) {
                    return new ScrapHubState();
                }
            }
            return null;
        }

        /// <summary>单件工具的砸地拍：闷响 + 重砸配方 + 微白闪 + 震屏</summary>
        private static void ToolCrashBeat(int arm, Vector2 spot) {
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.6f, Pitch = -0.45f + arm * 0.06f, MaxInstances = 3 }, spot);
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.45f, Pitch = -0.3f, MaxInstances = 3 }, spot);
            ShakeNearby(spot, 2f);
            ScrapVfx.GroundSlam(spot, 1.1f);
            ScrapSiegeScreen.TriggerImpactFrame(0.16f);
            if (Main.dedServ) {
                return;
            }
            for (int k = 0; k < 5; k++) {
                PRTLoader.NewParticle<PRT_Spark>(spot + new Vector2(Main.rand.NextFloat(-14f, 14f), -6f),
                    new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(1f, 4f)),
                    Color.Lerp(ScrapCommander.WeldOrange, Color.White, Main.rand.NextFloat(0.4f)),
                    Main.rand.NextFloat(0.5f, 0.9f))?.Configure(true, Main.rand.Next(10, 18));
            }
        }

        /// <summary>单件工具被磁力拔起的拍：抖土 + 音高逐件抬升的机械应答</summary>
        private static void ToolPullBeat(ScrapCommander owner, int arm, int slot) {
            Vector2 pos = owner.GetArmPos(arm);
            SoundEngine.PlaySound(SoundID.Item37 with {
                Volume = 0.6f,
                Pitch = -0.4f + slot * 0.16f,
                MaxInstances = 3
            }, pos);
            SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.35f, Pitch = -0.2f, MaxInstances = 3 }, pos);
            if (arm == ScrapCommander.ArmSaw) {
                SoundEngine.PlaySound(SoundID.Item22 with { Volume = 0.4f, Pitch = -0.3f, MaxInstances = 2 }, pos);
            }
            if (Main.dedServ) {
                return;
            }
            for (int k = 0; k < 8; k++) {
                Dust dust = Dust.NewDustPerfect(pos + new Vector2(Main.rand.NextFloat(-16f, 16f), 8f),
                    DustID.Dirt, new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), -Main.rand.NextFloat(1f, 3.2f)),
                    80, default, Main.rand.NextFloat(0.9f, 1.4f));
                dust.noGravity = Main.rand.NextBool(3);
            }
        }

        /// <summary>头入位的哐当：全进场最大的一拍</summary>
        private static void HeadSlamBurst(ScrapCommander owner, Vector2 hit) {
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.95f, Pitch = -0.65f, MaxInstances = 1 }, hit);
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.7f, Pitch = -0.5f, MaxInstances = 2 }, hit);
            ShakeNearby(hit, 6f);
            ScrapSiegeScreen.TriggerImpactFrame(0.4f);
            ScrapVfx.MetalExplosion(hit, 0.9f);
            owner.TautVibe = 12;
            //链条同时受一记下坠冲量，读出头把整架子拽了一下
            for (int i = 0; i < ScrapCommander.ArmCount; i++) {
                owner.ImpulseArm(i, new Vector2(0f, 4.5f));
            }
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(hit, new Vector2(0f, -0.4f),
                ScrapCommander.SmokeGray, 1.1f)?.Configure(60);
        }
    }
}
