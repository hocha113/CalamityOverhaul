using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDrowns
{
    /// <summary>
    /// 役灵收湖演出层：每条被收的召唤物一只小手，规格按目标体型缩放。
    /// 编舞语法承沉溺：合围涟漪 → 错帧破水 → 甩到卷指攥中 → 绷紧拍 →
    /// 拖入（臂收缩绷直）→ 过水线水花墨雾（真身在此帧隐去，溅水掩护）→ 手化水退场。
    /// 目标真身走普通弹幕层绘制且全程在场，手一律画在其上（无前后分层）；
    /// 放还的浮出水花走延迟队列错拍。绘制由 <see cref="KikasaDrownFX.Draw"/> 转来，
    /// 与沉溺鬼手同一批次口径
    /// </summary>
    internal static class KikasaMinionDrownFX
    {
        private const int WhiffFrames = 20;

        private sealed class MinionHand
        {
            public KikasaHandRig Rig;
            public KikasaMinionDrown.HeldEntry Entry;
            public int BurstFrame;
            public bool Burst;
            public bool Grabbed;
        }

        private sealed class WaveShow
        {
            public KikasaMinionDrown.GrabWave Wave;
            public float LakeY;
            public readonly List<MinionHand> Hands = [];
            public bool TenseDone;
            public bool Whiffed;
            public int WhiffTimer;
            public bool Done;
        }

        private static readonly List<WaveShow> shows = [];

        //放还浮出的错拍队列
        private struct EmergenceBeat
        {
            public uint Due;
            public int OwnerWho;
            public Vector2 Pos;
            public float LakeY;
            public float Scale;
            public bool Surface;
            public int Index;
        }

        private static readonly List<EmergenceBeat> emergences = [];

        //鬼雨异化时随观看域冷化，同沉溺色板
        private static Color BloodTint => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));

        public static void Clear() {
            shows.Clear();
            emergences.Clear();
        }

        //==================== 事件入口（规则层回调）====================

        internal static void OnWaveStart(KikasaMinionDrown.GrabWave wave, float lakeY) {
            if (Main.dedServ) {
                return;
            }
            WaveShow show = new() { Wave = wave, LakeY = lakeY };
            bool visible = IsViewedOwner(wave.OwnerWho);

            foreach (KikasaMinionDrown.HeldEntry entry in wave.Entries) {
                if (entry.HandIndex < 0) {
                    //捕获时已在水下的：一口墨雾静默吞没
                    if (visible) {
                        PRTLoader.NewParticle<PRT_GhostRainMist>(entry.CapturePos,
                            new Vector2(0f, -0.3f), new Color(46, 16, 20) * 0.8f,
                            Main.rand.NextFloat(0.35f, 0.5f))?.Configure(Main.rand.Next(30, 46));
                        KikasaDomainDeco.RippleAt(new Vector2(entry.CapturePos.X, lakeY), 0.3f);
                    }
                    continue;
                }

                Projectile proj = Main.projectile[entry.ProjIndex];
                float area = proj?.active == true ? proj.width * (float)proj.height : 900f;
                float scale = MathHelper.Clamp(MathF.Sqrt(area) / 34f, 0.55f, 1.05f);

                float jx = (KikasaMinionDrown.Hash(wave.Seed, entry.HandIndex * 3 + 2) - 0.5f) * 36f;
                Vector2 root = new(entry.CapturePos.X + jx, lakeY + 2f);
                float reach = Vector2.Distance(root, entry.CapturePos);
                //远抓增幅收敛版：小手抓高处也别细成线
                scale *= 1f + MathHelper.Clamp((reach - 260f) / 1200f, 0f, 0.6f);

                KikasaHandRig rig = new() {
                    Root = root,
                    Wrist = new Vector2(root.X, lakeY + 12f),
                    SegmentLength = MathHelper.Clamp(
                        reach * 1.15f / KikasaHandRig.ArmSegmentCount, 22f, 240f),
                    Tension = 0.75f,
                    BendDir = jx < 0f ? -1 : 1,
                    Curl = -0.1f,
                    Opacity = 0f,
                    Scale = scale,
                    Seed = wave.Seed + entry.HandIndex * 7.77f,
                    FrontLayer = true,
                };
                show.Hands.Add(new MinionHand {
                    Rig = rig,
                    Entry = entry,
                    BurstFrame = KikasaMinionDrown.ConvergeEnd
                        + entry.HandIndex * KikasaMinionDrown.BurstStagger,
                });
            }

            shows.Add(show);
            if (visible) {
                //起手：湖面轻轻应一声，抓的是自家鬼东西，不必兴师动众
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.3f, Pitch = -0.85f, MaxInstances = 2 },
                    new Vector2(wave.Entries[0].CapturePos.X, lakeY));
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.32f, Pitch = -0.5f, MaxInstances = 2 },
                    wave.Entries[0].CapturePos);
            }
        }

        /// <summary>过水线拍：真身在本帧隐去，水花墨雾负责把"消失"说圆</summary>
        internal static void OnEntrySubmerge(KikasaMinionDrown.GrabWave wave,
            KikasaMinionDrown.HeldEntry entry, float lakeY) {
            if (Main.dedServ || !IsViewedOwner(wave.OwnerWho)) {
                return;
            }
            Vector2 hit = new(entry.Anchor.X, lakeY);
            KikasaDomainDeco.SplashAt(hit, 6);
            KikasaDomainDeco.RippleAt(hit, 0.9f);
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.5f, Pitch = -0.3f, MaxInstances = 3 }, hit);
            //墨雾罩口，盖住真身隐去的那一帧
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_KikasaHoundSmoke>(
                    hit + Main.rand.NextVector2Circular(8f, 4f),
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(0.3f, 0.8f)),
                    new Color(46, 16, 20) * 0.8f, Main.rand.NextFloat(0.2f, 0.3f))
                    ?.Configure(Main.rand.Next(14, 24), 0.012f);
            }
            PRTLoader.NewParticle<PRT_GhostRainDrop>(hit,
                new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(1.4f, 2.4f)),
                BloodTint * 0.5f, Main.rand.NextFloat(0.35f, 0.5f))
                ?.Configure(Main.rand.Next(12, 18), 0f);
            ShakeViewer(0.9f);
        }

        internal static void OnWaveEnd(KikasaMinionDrown.GrabWave wave) {
            foreach (WaveShow show in shows) {
                if (show.Wave == wave) {
                    show.Done = true;
                    return;
                }
            }
        }

        /// <summary>拖入中途松手：鬼手空攥一拍弧线缩回水里</summary>
        internal static void OnWaveWhiff(KikasaMinionDrown.GrabWave wave) {
            foreach (WaveShow show in shows) {
                if (show.Wave == wave && !show.Whiffed) {
                    show.Whiffed = true;
                    show.WhiffTimer = 0;
                    if (IsViewedOwner(wave.OwnerWho)) {
                        SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.35f, Pitch = -0.6f, MaxInstances = 2 },
                            new Vector2(show.Hands.Count > 0
                                ? show.Hands[0].Rig.Root.X
                                : wave.Entries[0].CapturePos.X, show.LakeY));
                    }
                    return;
                }
            }
        }

        /// <summary>放还浮出入队：按序错 4 帧，一串水花不糊成一声</summary>
        internal static void QueueEmergence(int ownerWho, Vector2 pos, float lakeY,
            float scale, bool surface, int index) {
            if (Main.dedServ) {
                return;
            }
            emergences.Add(new EmergenceBeat {
                Due = (uint)Main.GameUpdateCount + (uint)(index * 4),
                OwnerWho = ownerWho,
                Pos = pos,
                LakeY = lakeY,
                Scale = scale,
                Surface = surface,
                Index = index,
            });
        }

        //==================== 推进 ====================

        public static void Update() {
            for (int i = shows.Count - 1; i >= 0; i--) {
                WaveShow show = shows[i];
                if (show.Whiffed) {
                    UpdateWhiff(show);
                }
                else {
                    UpdateShow(show);
                }
                if (show.Done && !AnyHandVisible(show)) {
                    shows.RemoveAt(i);
                }
            }
            DrainEmergences();
        }

        private static bool AnyHandVisible(WaveShow show) {
            foreach (MinionHand hand in show.Hands) {
                if (hand.Rig.Opacity > 0.01f) {
                    return true;
                }
            }
            return false;
        }

        private static void UpdateShow(WaveShow show) {
            bool visible = IsViewedOwner(show.Wave.OwnerWho);
            int t = show.Wave.Timer;

            //合围：根位两侧涟漪渐次凑近，手还没出水湖先有预兆
            if (visible && t <= KikasaMinionDrown.ConvergeEnd && t % 4 == 1) {
                foreach (MinionHand hand in show.Hands) {
                    float ease = 1f - MathF.Pow(
                        1f - MathHelper.Clamp(t / (float)KikasaMinionDrown.ConvergeEnd, 0f, 1f), 2f);
                    float from = hand.Rig.Root.X + (hand.Rig.BendDir > 0 ? 90f : -90f);
                    KikasaDomainDeco.RippleAt(new Vector2(
                        MathHelper.Lerp(from, hand.Rig.Root.X, ease), show.LakeY + 2f), 0.22f);
                }
            }

            for (int i = 0; i < show.Hands.Count; i++) {
                MinionHand hand = show.Hands[i];
                KikasaHandRig rig = hand.Rig;

                //目标中途没了：这只手化水退掉
                if (hand.Entry.Dropped) {
                    rig.Drain = MathHelper.Clamp(rig.Drain + 0.06f, 0f, 1f);
                    rig.Opacity = MathF.Max(rig.Opacity - 0.08f, 0f);
                    if (rig.Opacity > 0.01f) {
                        rig.Solve();
                    }
                    continue;
                }

                Vector2 gripWorld = hand.Entry.Anchor;
                Vector2 approach = (gripWorld - rig.Root).SafeNormalize(-Vector2.UnitY);
                float palmPull = 16f * rig.Scale + 8f;
                Vector2 wristGoal = gripWorld - approach * palmPull;

                if (t < hand.BurstFrame) {
                    rig.Opacity = 0f;
                    continue;
                }

                if (!hand.Burst) {
                    hand.Burst = true;
                    rig.Opacity = 1f;
                    rig.Foam = 1f;
                    if (visible) {
                        KikasaDomainDeco.SplashAt(rig.Root, 5);
                        KikasaDomainDeco.RippleAt(rig.Root, 0.7f);
                        SoundEngine.PlaySound(SoundID.SplashWeak with {
                            Volume = 0.45f,
                            Pitch = -0.5f + i * 0.07f,
                            MaxInstances = 3
                        }, rig.Root);
                    }
                }

                int localT = t - hand.BurstFrame;
                if (localT <= KikasaMinionDrown.ReachFrames) {
                    //爆发过冲弧线：根先动腕滞后，小手的鞭甩幅度收一档
                    float rt = localT / (float)KikasaMinionDrown.ReachFrames;
                    float ease = 1f - MathF.Pow(1f - rt, 2.6f);
                    Vector2 start = new(rig.Root.X, show.LakeY + 12f);
                    Vector2 ctrl = rig.Root
                        + (wristGoal - rig.Root) * 0.5f
                        + new Vector2(rig.BendDir * 18f, -46f * rig.Scale);
                    Vector2 a = Vector2.Lerp(start, ctrl, ease);
                    Vector2 b = Vector2.Lerp(ctrl, wristGoal, ease);
                    rig.Wrist = Vector2.Lerp(a, b, ease);
                    rig.SegmentLength = MathHelper.Clamp(
                        Vector2.Distance(rig.Root, rig.Wrist) * 1.15f / KikasaHandRig.ArmSegmentCount,
                        22f, 240f);
                    rig.Tension = 0.75f;
                    rig.Curl = MathHelper.Lerp(rig.Curl, -0.1f + rt * 0.15f, 0.4f);
                }
                else {
                    rig.Wrist = Vector2.Lerp(rig.Wrist, wristGoal, 0.55f);
                    rig.Curl = MathHelper.Lerp(rig.Curl, 0.92f, 0.3f);
                    if (!hand.Grabbed && rig.Curl > 0.7f) {
                        hand.Grabbed = true;
                        if (visible) {
                            KikasaDomainDeco.RippleAt(new Vector2(gripWorld.X, show.LakeY), 0.35f);
                            SoundEngine.PlaySound(SoundID.DD2_SkeletonHurt with {
                                Volume = 0.26f,
                                Pitch = -0.85f + i * 0.05f,
                                MaxInstances = 3
                            }, gripWorld);
                            PRTLoader.NewParticle<PRT_GhostRainDrop>(gripWorld,
                                new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(0.8f, 1.6f)),
                                BloodTint * 0.5f, Main.rand.NextFloat(0.3f, 0.45f))
                                ?.Configure(Main.rand.Next(10, 16), 0f);
                        }
                    }

                    //张力：合拢半松 → 绷紧拍骤直 → 拖入绷死
                    float tensionGoal = t < KikasaMinionDrown.TenseBeat ? 0.5f
                        : t < KikasaMinionDrown.DragStart ? 0.10f : 0.06f;
                    rig.Tension = MathHelper.Lerp(rig.Tension, tensionGoal,
                        t == KikasaMinionDrown.TenseBeat ? 0.6f : 0.25f);

                    //拖入期臂收缩保持绷直，被湖收回
                    if (t > KikasaMinionDrown.DragStart) {
                        float taut = Vector2.Distance(rig.Root, rig.Wrist) * 1.06f
                            / KikasaHandRig.ArmSegmentCount;
                        rig.SegmentLength = MathF.Max(
                            MathHelper.Lerp(rig.SegmentLength, taut, 0.3f), 8f);
                    }
                }

                //目标没入后手化水收场
                if (hand.Entry.Splashed) {
                    rig.Drain = MathHelper.Clamp(rig.Drain + 0.07f, 0f, 1f);
                    rig.Opacity = MathHelper.Clamp(rig.Opacity - 0.09f, 0f, 1f);
                }

                rig.Grip = MathHelper.Clamp(1f - rig.Tension * 1.4f, 0f, 1f);
                rig.Foam = MathHelper.Lerp(rig.Foam, hand.Entry.Splashed ? 0.8f : 0.35f, 0.1f);
                rig.Solve();
            }

            //绷紧拍：小规格的一记闷响
            if (!show.TenseDone && t >= KikasaMinionDrown.TenseBeat) {
                show.TenseDone = true;
                if (visible) {
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.3f, Pitch = -0.75f, MaxInstances = 1 },
                        new Vector2(show.Hands.Count > 0
                            ? show.Hands[0].Entry.Anchor.X
                            : show.Wave.Entries[0].Anchor.X, show.LakeY));
                    ShakeViewer(1.4f);
                }
            }
        }

        private static void UpdateWhiff(WaveShow show) {
            bool visible = IsViewedOwner(show.Wave.OwnerWho);
            show.WhiffTimer++;
            float wt = MathHelper.Clamp(show.WhiffTimer / (float)WhiffFrames, 0f, 1f);
            foreach (MinionHand hand in show.Hands) {
                KikasaHandRig rig = hand.Rig;
                if (rig.Opacity <= 0.01f) {
                    continue;
                }
                rig.Curl = MathHelper.Lerp(rig.Curl, 0.95f, 0.3f);
                rig.Tension = MathHelper.Lerp(rig.Tension, 0.45f, 0.2f);
                Vector2 home = new(rig.Root.X, show.LakeY + 30f);
                rig.Wrist = Vector2.Lerp(rig.Wrist, home, 0.12f + wt * 0.25f);
                rig.Opacity = 1f - wt;
                rig.Drain = wt * 0.7f;
                rig.Solve();
                if (visible && show.WhiffTimer == WhiffFrames / 2) {
                    KikasaDomainDeco.RippleAt(new Vector2(rig.Root.X, show.LakeY), 0.4f);
                }
            }
            if (show.WhiffTimer >= WhiffFrames) {
                show.Done = true;
            }
        }

        //放还浮出：到拍的出水水花与上跳血珠，只在看得见这片湖的端上演

        private static void DrainEmergences() {
            uint now = (uint)Main.GameUpdateCount;
            for (int i = emergences.Count - 1; i >= 0; i--) {
                EmergenceBeat beat = emergences[i];
                if (beat.Due > now) {
                    continue;
                }
                emergences.RemoveAt(i);
                if (!IsViewedOwner(beat.OwnerWho)) {
                    continue;
                }
                if (beat.Surface) {
                    Vector2 hit = new(beat.Pos.X, beat.LakeY);
                    KikasaDomainDeco.SplashAt(hit, (int)(5 + beat.Scale * 3f));
                    KikasaDomainDeco.RippleAt(hit, 0.9f * beat.Scale);
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Volume = 0.5f,
                        Pitch = -0.15f + beat.Index * 0.03f,
                        MaxInstances = 3
                    }, hit);
                    for (int k = 0; k < 3; k++) {
                        PRTLoader.NewParticle<PRT_GhostRainDrop>(
                            hit + new Vector2(Main.rand.NextFloat(-10f, 10f), -4f),
                            new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(1.6f, 3f)),
                            BloodTint * 0.5f, Main.rand.NextFloat(0.3f, 0.5f))
                            ?.Configure(Main.rand.Next(12, 20), 0f);
                    }
                }
                else {
                    PRTLoader.NewParticle<PRT_GhostRainMist>(beat.Pos,
                        new Vector2(0f, -0.3f), new Color(46, 16, 20) * 0.7f,
                        Main.rand.NextFloat(0.3f, 0.45f))?.Configure(Main.rand.Next(24, 40));
                }
            }
        }

        //==================== 绘制（由 KikasaDrownFX.Draw 转来，批次口径一致）====================

        internal static void Draw(SpriteBatch spriteBatch, int viewedOwner,
            Effect handFx, Texture2D noise, bool shaderOk) {
            bool any = false;
            foreach (WaveShow show in shows) {
                if (show.Wave.OwnerWho == viewedOwner && AnyHandVisible(show)) {
                    any = true;
                    break;
                }
            }
            if (!any) {
                return;
            }

            if (!shaderOk) {
                Texture2D pixel = VaultAsset.placeholder2?.Value;
                if (pixel == null) {
                    return;
                }
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone,
                    null, Main.GameViewMatrix.TransformationMatrix);
                foreach (WaveShow show in shows) {
                    if (show.Wave.OwnerWho != viewedOwner) {
                        continue;
                    }
                    foreach (MinionHand hand in show.Hands) {
                        if (hand.Rig.Opacity > 0.01f) {
                            hand.Rig.DrawFallback(spriteBatch, pixel);
                        }
                    }
                }
                spriteBatch.End();
                return;
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            handFx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            handFx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            handFx.Parameters["uNoiseTex"]?.SetValue(noise);

            foreach (WaveShow show in shows) {
                if (show.Wave.OwnerWho != viewedOwner) {
                    continue;
                }
                foreach (MinionHand hand in show.Hands) {
                    KikasaHandRig rig = hand.Rig;
                    if (rig.Opacity <= 0.01f) {
                        continue;
                    }
                    handFx.Parameters["uOpacity"]?.SetValue(rig.Opacity);
                    handFx.Parameters["uGrip"]?.SetValue(rig.Grip);
                    handFx.Parameters["uSeed"]?.SetValue(rig.Seed);
                    handFx.Parameters["uFoam"]?.SetValue(rig.Foam);
                    handFx.Parameters["uDrain"]?.SetValue(rig.Drain);

                    var armVerts = rig.BuildArmStrip();
                    var palmVerts = rig.BuildPalmStrip();
                    foreach (EffectPass pass in handFx.CurrentTechnique.Passes) {
                        pass.Apply();
                        device.DrawUserPrimitives(PrimitiveType.TriangleStrip, armVerts, 0, armVerts.Length - 2);
                        device.DrawUserPrimitives(PrimitiveType.TriangleStrip, palmVerts, 0, palmVerts.Length - 2);
                        for (int k = 0; k < 5; k++) {
                            var fingerVerts = rig.BuildFingerStrip(k);
                            device.DrawUserPrimitives(PrimitiveType.TriangleStrip, fingerVerts, 0, fingerVerts.Length - 2);
                        }
                    }
                }
            }

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
        }

        private static bool IsViewedOwner(int ownerIndex) {
            KikasaDomainPlayer viewed = KikasaDomain.Viewed;
            return viewed != null && viewed.Player.whoAmI == ownerIndex;
        }

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);
    }
}
