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
    /// 沉玩家演出层：编舞语法承沉溺（合围 → 错帧破水 → 甩到卷指 → 绷紧 → 挣扎拉锯 →
    /// 拖入过水线），但目标是活体玩家：真身走原版玩家层绘制且全程在场，
    /// 手一律画在其上（役灵收湖同例，无前后分层），腕逐帧跟随真身同步位置，
    /// 受害者本机的钉身轨迹与远端看到的身位天然一致。
    /// 独有的长押段：手在水下抱定呼吸式攥握约十秒，水面周期性冒泡起漪，
    /// 受害者本机按键挣扎会多冒气泡（纯本机装饰）；
    /// 期满松手是摊掌退场，取消是空攥缩回。时间轴唯一真相在
    /// <see cref="KikasaPlayerDrown.ClientBind.Timer"/>，本层不自走钟
    /// </summary>
    internal static class KikasaPlayerDrownFX
    {
        private const int ReachFrames = 9;
        private const int WhiffFrames = 22;
        private const int ReleaseFrames = 26;

        //==================== 槽位表 ====================
        //Dir=抓点在玩家碰撞箱椭圆上的方向（屏幕系）；RootSide=根横向偏移比例

        private readonly record struct GripSlotDef(Vector2 Dir, float RootSide, float ScaleMul);

        private static readonly GripSlotDef[] SlotTable = [
            new(new(-0.97f, 0.28f), -1.00f, 1f),     //左腰箍
            new(new(0.97f, 0.28f), 1.00f, 1f),       //右腰箍
            new(new(0f, 1f), 0.14f, 1.05f),          //托腿
            new(new(0.55f, -0.88f), 0.62f, 0.9f),    //越顶右压肩
            new(new(-0.55f, -0.88f), -0.62f, 0.9f),  //越顶左压肩
        ];

        //==================== 记录 ====================

        private sealed class BindHand
        {
            public KikasaHandRig Rig;
            public Vector2 GripLocal;
            public int BurstFrame;
            public bool Burst;
            public bool Grabbed;
        }

        private sealed class BindShow
        {
            public int BindId;
            public int OwnerIndex;
            public int VictimIndex;
            public float Seed;
            public float LakeYFallback;
            public readonly List<BindHand> Hands = [];
            //节拍闩
            public bool TenseDone;
            public bool Submerged;
            //退场：Cancelled=空攥缩回，否则摊掌松人
            public bool Ending;
            public bool Cancelled;
            public int EndTimer;
            public bool Done;
            /// <summary>退场拍参照的受害者横位（松手瞬间取样，人走了水花还在原处）</summary>
            public float EndX;
        }

        private static readonly List<BindShow> shows = [];

        //鬼雨异化时随观看域冷化为浊水灰青
        private static Color BloodTint => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));

        public static void Clear() => shows.Clear();

        internal static bool HasActiveShowFor(int ownerIndex) {
            for (int i = 0; i < shows.Count; i++) {
                if (shows[i].OwnerIndex == ownerIndex && !shows[i].Done) {
                    return true;
                }
            }
            return false;
        }

        //==================== 起演 ====================

        internal static void StartShow(KikasaPlayerDrown.ClientBind bind) {
            if (Main.dedServ || bind == null) {
                return;
            }
            for (int i = 0; i < shows.Count; i++) {
                if (shows[i].BindId == bind.BindId) {
                    return;
                }
            }
            Player victim = Main.player[bind.VictimWho];
            if (victim?.active != true) {
                return;
            }

            float lakeY = KikasaPlayerDrown.LiveLakeYFor(bind.OwnerWho, bind.LakeYFallback);
            BindShow show = new() {
                BindId = bind.BindId,
                OwnerIndex = bind.OwnerWho,
                VictimIndex = bind.VictimWho,
                Seed = bind.Seed,
                LakeYFallback = bind.LakeYFallback,
                EndX = victim.Center.X,
            };
            BuildHands(show, victim, lakeY);

            //迟到的 Apply（重播/中途加入）：跳过入场编舞直接就位，节拍闩按证据补齐（§7.5 不重播已响过的拍）
            if (bind.Timer > ConvergeFastForwardAt) {
                FastForward(show, bind, victim, lakeY);
            }
            shows.Add(show);

            if (IsViewedOwner(show.OwnerIndex) && bind.Timer <= ConvergeFastForwardAt) {
                //起手：湖面沉一口气，拖的是人，比拖怪更闷一点
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.4f, Pitch = -0.95f, MaxInstances = 2 },
                    new Vector2(victim.Center.X, lakeY));
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.4f, Pitch = -0.7f, MaxInstances = 2 }, victim.Center);
            }
        }

        private static int ConvergeFastForwardAt
            => KikasaPlayerDrown.ConvergeEnd + SlotTable.Length * 2 + ReachFrames;

        private static void BuildHands(BindShow show, Player victim, float lakeY) {
            Vector2 half = new(victim.width * 0.5f, victim.height * 0.5f);
            float spread = MathHelper.Clamp(half.X * 1.7f + 40f, 52f, 120f);

            for (int i = 0; i < SlotTable.Length; i++) {
                GripSlotDef slot = SlotTable[i];
                float jx = (Hash(show.Seed, i * 3 + 1) - 0.5f) * 18f;
                Vector2 root = new(victim.Center.X + slot.RootSide * spread + jx, lakeY + 2f);
                Vector2 gripLocal = new(
                    slot.Dir.X * half.X * 1.25f, slot.Dir.Y * half.Y * 1.1f);

                float reach = Vector2.Distance(root, victim.Center + gripLocal);
                KikasaHandRig rig = new() {
                    Root = root,
                    Wrist = new Vector2(root.X, lakeY + 12f),
                    SegmentLength = SegLenFor(reach),
                    Tension = 0.75f,
                    //肘向外拐：根在左弓向左，臂间不交叉
                    BendDir = slot.RootSide < 0f ? -1 : 1,
                    Curl = -0.1f,
                    Opacity = 0f,
                    Scale = 0.95f * slot.ScaleMul * (1f + MathHelper.Clamp((reach - 340f) / 1100f, 0f, 1f)),
                    Seed = show.Seed + i * 7.77f,
                    FrontLayer = true,
                };
                show.Hands.Add(new BindHand {
                    Rig = rig,
                    GripLocal = gripLocal,
                    //爆发错帧：2f 一根，左右交替入场
                    BurstFrame = KikasaPlayerDrown.ConvergeEnd + i * 2,
                });
            }
        }

        /// <summary>段长随实际根腕距定标，与沉溺同口径</summary>
        private static float SegLenFor(float reach)
            => MathF.Max(26f, MathF.Min(reach, KikasaDrown.MaxGrabHeightHardCap + 200f)
                * 1.15f / KikasaHandRig.ArmSegmentCount);

        /// <summary>迟到起演的就位：手直接攥在身上，闩从证据推回</summary>
        private static void FastForward(BindShow show, KikasaPlayerDrown.ClientBind bind,
            Player victim, float lakeY) {
            foreach (BindHand hand in show.Hands) {
                KikasaHandRig rig = hand.Rig;
                hand.Burst = true;
                hand.Grabbed = true;
                rig.Opacity = 1f;
                rig.Curl = 0.95f;
                rig.Tension = 0.10f;
                Vector2 gripWorld = victim.Center + hand.GripLocal;
                Vector2 approach = (gripWorld - rig.Root).SafeNormalize(-Vector2.UnitY);
                rig.Wrist = gripWorld - approach * (20f * rig.Scale + 12f);
                rig.SegmentLength = SegLenFor(Vector2.Distance(rig.Root, rig.Wrist));
                rig.Solve();
            }
            show.TenseDone = bind.Timer >= KikasaPlayerDrown.TenseBeat;
            show.Submerged = victim.Center.Y >= lakeY;
        }

        //==================== 事件入口（规则层回调）====================

        /// <summary>束缚结束：cancelled=提前取消（空攥缩回），否则期满摊掌松人</summary>
        internal static void OnBindEnd(int bindId, bool cancelled) {
            for (int i = 0; i < shows.Count; i++) {
                BindShow show = shows[i];
                if (show.BindId != bindId || show.Ending) {
                    continue;
                }
                show.Ending = true;
                show.Cancelled = cancelled;
                show.EndTimer = 0;
                Player victim = Main.player[show.VictimIndex];
                if (victim?.active == true) {
                    show.EndX = victim.Center.X;
                }
                return;
            }
        }

        //==================== 推进 ====================

        public static void Update() {
            for (int i = shows.Count - 1; i >= 0; i--) {
                BindShow show = shows[i];
                if (show.Ending) {
                    UpdateEnding(show);
                }
                else {
                    UpdateShow(show);
                }
                if (show.Done) {
                    KikasaDrown.OnLocalShowEnded(show.OwnerIndex);
                    shows.RemoveAt(i);
                }
            }
        }

        private static void UpdateShow(BindShow show) {
            KikasaPlayerDrown.ClientBind bind = KikasaPlayerDrown.GetClientBind(show.BindId);
            Player victim = Main.player[show.VictimIndex];
            //规则层已放人（正常路径会先来 OnBindEnd，这里是兜底）或真身失效：空攥收场
            if (bind == null || victim?.active != true || victim.dead || victim.ghost) {
                show.Ending = true;
                show.Cancelled = true;
                show.EndTimer = 0;
                return;
            }

            bool visible = IsViewedOwner(show.OwnerIndex);
            int t = bind.Timer;
            float lakeY = KikasaPlayerDrown.LiveLakeYFor(show.OwnerIndex, show.LakeYFallback);

            //合围：水面鼓包行进涟漪
            if (t <= KikasaPlayerDrown.ConvergeEnd && visible && t % 5 == 2) {
                for (int i = 0; i < show.Hands.Count; i++) {
                    KikasaHandRig rig = show.Hands[i].Rig;
                    float ease = 1f - MathF.Pow(
                        1f - MathHelper.Clamp(t / (float)KikasaPlayerDrown.ConvergeEnd, 0f, 1f), 2f);
                    float from = rig.Root.X + rig.BendDir * 140f;
                    KikasaDomainDeco.RippleAt(
                        new Vector2(MathHelper.Lerp(from, rig.Root.X, ease), lakeY + 4f), 0.28f);
                }
            }

            UpdateHands(show, victim, t, lakeY, visible);

            //绷紧拍：全臂骤直+重低音
            if (!show.TenseDone && t >= KikasaPlayerDrown.TenseBeat) {
                show.TenseDone = true;
                if (visible) {
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.5f, Pitch = -0.65f, MaxInstances = 1 },
                        victim.Center);
                    ShakeViewer(2.2f);
                }
            }

            //过水线拍：人没入的大水花
            if (!show.Submerged && victim.Center.Y >= lakeY) {
                show.Submerged = true;
                if (visible) {
                    Vector2 hit = new(victim.Center.X, lakeY);
                    KikasaDomainDeco.SplashAt(hit, 16);
                    KikasaDomainDeco.RippleAt(hit, 1.8f);
                    KikasaDomainDeco.RippleAt(hit + new Vector2(24f, 0f), 0.7f);
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.9f, Pitch = -0.35f, MaxInstances = 2 }, hit);
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.42f, Pitch = -0.8f, MaxInstances = 1 }, hit);
                    ShakeViewer(3.2f);
                }
            }

            //长押段的水面余韵与水下气泡：湖面上只剩一圈圈慢漪说这儿押着人
            if (show.Submerged && t > KikasaPlayerDrown.DragEnd && visible) {
                if (t % 46 == 0) {
                    KikasaDomainDeco.RippleAt(new Vector2(
                        victim.Center.X + Main.rand.NextFloat(-10f, 10f), lakeY),
                        Main.rand.NextFloat(0.28f, 0.42f));
                }
                if (t % 24 == 0) {
                    PRTLoader.NewParticle<PRT_GhostRainMist>(
                        victim.Center + Main.rand.NextVector2Circular(10f, 14f),
                        new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -Main.rand.NextFloat(0.5f, 1.1f)),
                        new Color(58, 18, 20) * 0.6f,
                        Main.rand.NextFloat(0.3f, 0.45f))?.Configure(Main.rand.Next(36, 60));
                }
                //受害者本机的挣扎装饰：按方向键在水下扑腾，气泡更密（纯本机，端间发散无害）
                if (show.VictimIndex == Main.myPlayer && t % 12 == 0
                    && (victim.controlLeft || victim.controlRight
                        || victim.controlJump || victim.controlUp)) {
                    PRTLoader.NewParticle<PRT_GhostRainMist>(
                        victim.Center + Main.rand.NextVector2Circular(12f, 10f),
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.9f, 1.6f)),
                        new Color(58, 18, 20) * 0.7f,
                        Main.rand.NextFloat(0.32f, 0.5f))?.Configure(Main.rand.Next(24, 40));
                    KikasaDomainDeco.RippleAt(new Vector2(victim.Center.X, lakeY), 0.24f);
                }
            }
        }

        //==================== 手编舞 ====================

        private static void UpdateHands(BindShow show, Player victim, int t, float lakeY, bool visible) {
            for (int i = 0; i < show.Hands.Count; i++) {
                BindHand hand = show.Hands[i];
                KikasaHandRig rig = hand.Rig;

                //根贴活水线（引潮会动），拖人十来秒水面不该脱手
                rig.Root = new Vector2(rig.Root.X, lakeY + 2f);

                //腕逐帧跟真身同步位置：受害者本机钉身轨迹与远端身位天然一致
                Vector2 gripWorld = victim.Center + hand.GripLocal;
                Vector2 approach = (gripWorld - rig.Root).SafeNormalize(-Vector2.UnitY);
                float palmPull = 20f * rig.Scale + 12f;
                Vector2 wristGoal = gripWorld - approach * palmPull;

                if (t < hand.BurstFrame) {
                    rig.Opacity = 0f;
                    continue;
                }

                //破水帧：根口水花+涟漪+破水声（音高随手递变）
                if (!hand.Burst) {
                    hand.Burst = true;
                    rig.Opacity = 1f;
                    rig.Foam = 1f;
                    if (visible) {
                        KikasaDomainDeco.SplashAt(rig.Root, 6);
                        KikasaDomainDeco.RippleAt(rig.Root, 0.85f);
                        SoundEngine.PlaySound(SoundID.SplashWeak with {
                            Volume = 0.5f,
                            Pitch = -0.45f + i * 0.07f,
                            MaxInstances = 3
                        }, rig.Root);
                    }
                }

                int localT = t - hand.BurstFrame;
                if (localT <= ReachFrames) {
                    //爆发过冲弧线：根先动腕滞后，控制点抬向外上，鞭出去的
                    float rt = localT / (float)ReachFrames;
                    float ease = 1f - MathF.Pow(1f - rt, 2.6f);
                    Vector2 start = new(rig.Root.X, lakeY + 12f);
                    Vector2 ctrl = rig.Root
                        + (wristGoal - rig.Root) * 0.5f
                        + new Vector2(rig.BendDir * 22f, -60f * rig.Scale);
                    Vector2 a = Vector2.Lerp(start, ctrl, ease);
                    Vector2 b = Vector2.Lerp(ctrl, wristGoal, ease);
                    rig.Wrist = Vector2.Lerp(a, b, ease);
                    rig.SegmentLength = SegLenFor(Vector2.Distance(rig.Root, rig.Wrist));
                    rig.Tension = 0.75f;
                    rig.Curl = MathHelper.Lerp(rig.Curl, -0.1f + rt * 0.15f, 0.4f);
                }
                else {
                    //锁定抓点：强跟随带一点分量
                    rig.Wrist = Vector2.Lerp(rig.Wrist, wristGoal, 0.55f);

                    //卷指合拢；过 0.7 的那一帧是"攥中"节拍
                    float curlGoal = t > KikasaPlayerDrown.DragEnd
                        //长押呼吸式攥握：松紧微循环，手是活的
                        ? 0.93f + MathF.Sin(t * 0.09f + i * 1.7f) * 0.07f
                        : 0.95f;
                    rig.Curl = MathHelper.Lerp(rig.Curl, curlGoal, 0.28f);
                    if (!hand.Grabbed && rig.Curl > 0.7f) {
                        hand.Grabbed = true;
                        if (visible) {
                            KikasaDomainDeco.RippleAt(new Vector2(gripWorld.X, lakeY), 0.4f);
                            SoundEngine.PlaySound(SoundID.DD2_SkeletonHurt with {
                                Volume = 0.4f,
                                Pitch = -0.75f + i * 0.05f,
                                MaxInstances = 3
                            }, gripWorld);
                            ShakeViewer(0.8f);
                            PRTLoader.NewParticle<PRT_GhostRainDrop>(gripWorld,
                                new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(1f, 2f)),
                                BloodTint * 0.55f, Main.rand.NextFloat(0.4f, 0.55f))
                                ?.Configure(Main.rand.Next(12, 20), 0f);
                        }
                    }

                    //张力编舞：合拢 0.5 → 绷紧拍骤降 → 挣扎期随拉锯回弹 → 拖入/长押绷死
                    float tensionGoal;
                    if (t < KikasaPlayerDrown.TenseBeat) {
                        tensionGoal = 0.5f;
                    }
                    else if (t <= KikasaPlayerDrown.StruggleEnd) {
                        float st = t - KikasaPlayerDrown.StruggleStart;
                        float decay = MathF.Max(0f, 1f - st
                            / (KikasaPlayerDrown.StruggleEnd - KikasaPlayerDrown.StruggleStart));
                        tensionGoal = 0.10f + MathF.Max(0f, MathF.Sin(st * 0.52f)) * 0.22f * decay;
                    }
                    else if (t <= KikasaPlayerDrown.DragEnd) {
                        tensionGoal = 0.06f;
                    }
                    else {
                        //长押微息：绷死里带一丝活气
                        tensionGoal = 0.08f + MathF.Sin(t * 0.11f + i) * 0.02f;
                    }
                    rig.Tension = MathHelper.Lerp(rig.Tension, tensionGoal,
                        t == KikasaPlayerDrown.TenseBeat ? 0.6f : 0.25f);

                    //拖入起臂收缩保持绷直，被湖收回一段
                    if (t > KikasaPlayerDrown.StruggleEnd) {
                        float taut = Vector2.Distance(rig.Root, rig.Wrist) * 1.06f
                            / KikasaHandRig.ArmSegmentCount;
                        rig.SegmentLength = MathF.Max(
                            MathHelper.Lerp(rig.SegmentLength, taut, 0.3f), 8f);
                    }
                }

                rig.Grip = MathHelper.Clamp(1f - rig.Tension * 1.4f, 0f, 1f);
                rig.Foam = MathHelper.Lerp(rig.Foam, show.Submerged ? 0.6f : 0.35f, 0.1f);
                rig.Solve();
            }
        }

        //==================== 退场 ====================

        private static void UpdateEnding(BindShow show) {
            bool visible = IsViewedOwner(show.OwnerIndex);
            show.EndTimer++;
            int frames = show.Cancelled ? WhiffFrames : ReleaseFrames;
            float wt = MathHelper.Clamp(show.EndTimer / (float)frames, 0f, 1f);
            float lakeY = KikasaPlayerDrown.LiveLakeYFor(show.OwnerIndex, show.LakeYFallback);

            //期满松手拍：水面一记轻响，人自己浮上来
            if (!show.Cancelled && show.EndTimer == 2 && visible) {
                Vector2 hit = new(show.EndX, lakeY);
                KikasaDomainDeco.SplashAt(hit, 8);
                KikasaDomainDeco.RippleAt(hit, 1.0f);
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.55f, Pitch = -0.2f, MaxInstances = 2 }, hit);
                ShakeViewer(1.5f);
            }

            foreach (BindHand hand in show.Hands) {
                KikasaHandRig rig = hand.Rig;
                if (rig.Opacity <= 0.01f) {
                    continue;
                }
                if (show.Cancelled) {
                    //空攥：先攥一拍再折返
                    rig.Curl = MathHelper.Lerp(rig.Curl, 0.95f, 0.3f);
                    rig.Tension = MathHelper.Lerp(rig.Tension, 0.45f, 0.2f);
                }
                else {
                    //摊掌松人：指张开，臂回软
                    rig.Curl = MathHelper.Lerp(rig.Curl, -0.15f, 0.25f);
                    rig.Tension = MathHelper.Lerp(rig.Tension, 0.55f, 0.15f);
                }
                Vector2 home = new(rig.Root.X, lakeY + 34f);
                rig.Wrist = Vector2.Lerp(rig.Wrist, home, 0.10f + wt * 0.22f);
                rig.Opacity = 1f - wt;
                rig.Drain = wt * 0.75f;
                rig.Solve();
                if (visible && show.EndTimer == frames / 2) {
                    KikasaDomainDeco.RippleAt(new Vector2(rig.Root.X, lakeY), 0.45f);
                }
            }
            if (show.EndTimer >= frames) {
                show.Done = true;
            }
        }

        //==================== 绘制 ====================

        /// <summary>由 <see cref="KikasaDrownFX.Draw"/> 转来，与沉溺鬼手同一批次口径；
        /// 目标真身走原版玩家层，手一律压其上（役灵收湖同例）</summary>
        internal static void Draw(SpriteBatch spriteBatch, int viewedOwner,
            Effect handFx, Texture2D noise, bool shaderOk) {
            if (shows.Count == 0) {
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
                foreach (BindShow show in shows) {
                    if (show.OwnerIndex != viewedOwner) {
                        continue;
                    }
                    foreach (BindHand hand in show.Hands) {
                        hand.Rig.DrawFallback(spriteBatch, pixel);
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

            foreach (BindShow show in shows) {
                if (show.OwnerIndex != viewedOwner) {
                    continue;
                }
                foreach (BindHand hand in show.Hands) {
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

        private static float Hash(float seed, int k) {
            float h = MathF.Sin(seed * 12.9898f + k * 78.233f) * 43758.547f;
            return h - MathF.Floor(h);
        }

        private static bool IsViewedOwner(int ownerIndex) {
            KikasaDomainPlayer viewed = KikasaDomain.Viewed;
            return viewed != null && viewed.Player.whoAmI == ownerIndex;
        }

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);
    }
}
