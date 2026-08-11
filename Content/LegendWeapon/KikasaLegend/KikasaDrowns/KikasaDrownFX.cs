using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDrowns
{
    /// <summary>
    /// 沉溺演出层：六幕编舞——合围（水面鼓包汇聚）→ 爆发（臂错帧破水甩向各自槽位）→
    /// 抱住（逐手卷指合拢，全员到位绷紧拍）→ 拉锯（两轮衰减挣扎，曲率即力量）→
    /// 拖入（绷直收缩下拉，过水线大水花）→ 水下溶解（鬼影快照+臂化回湖水）。
    /// 真身在权威时间轴 40 帧被移除，本层自 32 帧起用鬼影覆绘真身，时差被同路延迟抵消。
    /// 画在 EndEntityDraw：水上部分被湖面镜面自动倒影，倒影把手锚进世界。
    /// </summary>
    internal static class KikasaDrownFX
    {
        //==================== 时间轴 ====================
        //最后一只手（第 7 只 28f 破水+9f 甩到+~4f 卷指）约 41f 攥中，
        //绷紧拍必须晚于它——"全员合拢后才绷紧"是力量语义的底线

        private const int ConvergeEnd = 16;
        private const int TenseBeat = 44;
        private const int StruggleStart = 46;
        private const int StruggleEnd = 64;
        private const int DragEnd = 88;
        private const int ShowEnd = 152;
        private const int GhostOverdrawStart = 32;
        private const int WhiffFrames = 22;

        //==================== 槽位表 ====================
        //Dir=抓点在目标椭圆上的方向（屏幕系）；RootSide=根横向偏移比例；Front=画在鬼影前

        private readonly record struct GripSlotDef(Vector2 Dir, float RootSide, bool Front);

        private static readonly GripSlotDef[] SlotTable = [
            new(new(-0.97f, 0.26f), -1.00f, true),   //左腰箍
            new(new(0.97f, 0.26f), 1.00f, true),     //右腰箍
            new(new(0f, 1f), 0.12f, false),          //托底
            new(new(0.52f, -0.85f), 0.62f, true),    //越顶右压肩
            new(new(-0.52f, -0.85f), -0.62f, true),  //越顶左压肩
            new(new(-0.82f, 0.62f), -1.35f, false),  //左下背箍
            new(new(0.82f, 0.62f), 1.35f, false),    //右下背箍
        ];

        //==================== 记录 ====================

        private sealed class HandState
        {
            public KikasaHandRig Rig;
            public GripSlotDef Slot;
            public Vector2 GripLocal;
            public int BurstFrame;
            public bool Grabbed;
            public bool Burst;
        }

        private sealed class MiniGhost
        {
            public NetworkNPCIdentity Identity;
            public bool Captured;
            public int NpcType;
            public Rectangle Frame;
            public Vector2 Pos;
            public float Rot;
            public float Scale;
            public SpriteEffects Fx;
            public float CenterOffY;
            public float Vy;
            public bool Splashed;
        }

        private sealed class DrownShow
        {
            public int DrownId;
            public int OwnerIndex;
            public float Seed;
            public NetworkNPCIdentity Primary;
            public readonly List<MiniGhost> Members = [];
            public HandState[] Hands;
            public int Timer;
            public float LakeY;
            public Vector2 TargetCenter;
            public Vector2 TargetHalf;
            //鬼影快照（逐帧刷新到真身消失为止）
            public bool GhostCaptured;
            public int NpcType;
            public Rectangle NpcFrame;
            public float NpcScale;
            public float NpcRot;
            public SpriteEffects NpcFx;
            /// <summary>原版底边锚定+4px 相对中心锚定的偏移，换影不跳变</summary>
            public float NpcCenterOffY;
            public float GhostDissolve;
            public float GhostForm;
            public float GhostAlpha = 1f;
            //节拍闩
            public bool TenseDone;
            public bool SubmergeSplashed;
            public float StruggleBaseY;
            //取消收手
            public bool Cancelled;
            public int WhiffTimer;
            public bool Done;
        }

        private static readonly List<DrownShow> shows = [];

        //鬼雨异化时随观看域冷化为浊水灰青
        private static Color BloodTint => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color FoamGlow => KikasaDomain.CoolTint(new(246, 133, 112), new(176, 200, 204));

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

        internal static void StartShow(int ownerWho, int drownId, float seed,
            NetworkNPCIdentity primary, List<NetworkNPCIdentity> members) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < shows.Count; i++) {
                if (shows[i].DrownId == drownId) {
                    return;
                }
            }
            if (ownerWho < 0 || ownerWho >= Main.maxPlayers
                || Main.player[ownerWho]?.active != true
                || !primary.TryResolve(out NPC target)) {
                return;
            }

            Player owner = Main.player[ownerWho];
            float lakeY = owner.GetModPlayer<KikasaDomainPlayer>().LakeWorldY;

            DrownShow show = new() {
                DrownId = drownId,
                OwnerIndex = ownerWho,
                Seed = seed,
                Primary = primary,
                LakeY = lakeY,
                TargetCenter = target.Center,
                TargetHalf = new Vector2(target.width, target.height) * 0.5f,
            };
            foreach (NetworkNPCIdentity id in members) {
                show.Members.Add(new MiniGhost { Identity = id });
            }
            BuildHands(show, target);
            shows.Add(show);

            if (IsViewedOwner(ownerWho)) {
                //起手：湖面先沉一口气
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.35f, Pitch = -0.9f, MaxInstances = 2 },
                    new Vector2(target.Center.X, lakeY));
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.4f, Pitch = -0.6f, MaxInstances = 2 }, target.Center);
            }
        }

        //确定性布手：数量随体型，根位/段长/错帧全由种子推出，各端一致

        private static void BuildHands(DrownShow show, NPC target) {
            float area = target.width * target.height;
            int count = (int)MathHelper.Clamp(4f + area / 1600f, 4f, 7f);
            float handScale = MathHelper.Clamp(MathF.Sqrt(area) / 38f, 0.85f, 1.35f);
            float spread = MathF.Max(show.TargetHalf.X * 1.7f, 52f);

            show.Hands = new HandState[count];
            for (int i = 0; i < count; i++) {
                GripSlotDef slot = SlotTable[i];
                float jx = (Hash(show.Seed, i * 3 + 1) - 0.5f) * 22f;
                Vector2 root = new(
                    show.TargetCenter.X + slot.RootSide * spread + jx,
                    show.LakeY + 2f);
                Vector2 gripLocal = new(
                    slot.Dir.X * show.TargetHalf.X, slot.Dir.Y * show.TargetHalf.Y);
                Vector2 gripWorld = show.TargetCenter + gripLocal;

                float segLen = MathHelper.Clamp(
                    Vector2.Distance(root, gripWorld) * 1.15f / KikasaHandRig.ArmSegmentCount,
                    26f, 58f);

                KikasaHandRig rig = new() {
                    Root = root,
                    Wrist = new Vector2(root.X, show.LakeY + 12f),
                    SegmentLength = segLen,
                    Tension = 0.75f,
                    //肘向外拐：根在左弓向左，臂间不交叉
                    BendDir = slot.RootSide < 0f ? -1 : 1,
                    Curl = -0.1f,
                    Opacity = 0f,
                    Scale = handScale,
                    Seed = show.Seed + i * 7.77f,
                    FrontLayer = slot.Front,
                };
                show.Hands[i] = new HandState {
                    Rig = rig,
                    Slot = slot,
                    GripLocal = gripLocal,
                    //爆发错帧：2f 一根，左右交替入场
                    BurstFrame = ConvergeEnd + i * 2,
                };
            }
        }

        private static float Hash(float seed, int k) {
            float h = MathF.Sin(seed * 12.9898f + k * 78.233f) * 43758.547f;
            return h - MathF.Floor(h);
        }

        internal static void CancelShow(int drownId) {
            for (int i = 0; i < shows.Count; i++) {
                DrownShow show = shows[i];
                if (show.DrownId == drownId && !show.Cancelled) {
                    BeginWhiff(show);
                    return;
                }
            }
        }

        private static void BeginWhiff(DrownShow show) {
            show.Cancelled = true;
            show.WhiffTimer = 0;
            show.GhostAlpha = 0f;
            if (IsViewedOwner(show.OwnerIndex)) {
                //空手：一声轻水响，手要缩回去了
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.4f, Pitch = -0.65f, MaxInstances = 2 },
                    new Vector2(show.TargetCenter.X, show.LakeY));
            }
        }

        //==================== 推进 ====================

        public static void Update() {
            for (int i = shows.Count - 1; i >= 0; i--) {
                DrownShow show = shows[i];
                UpdateShow(show);
                if (show.Done) {
                    KikasaDrown.OnLocalShowEnded(show.OwnerIndex);
                    shows.RemoveAt(i);
                }
            }
        }

        private static bool LakeAlive(int ownerIndex) {
            if (ownerIndex < 0 || ownerIndex >= Main.maxPlayers) {
                return false;
            }
            Player owner = Main.player[ownerIndex];
            return owner?.active == true
                && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                && domain.AnyActive && domain.RiseT >= 0.9f;
        }

        private static void UpdateShow(DrownShow show) {
            bool visible = IsViewedOwner(show.OwnerIndex);

            if (show.Cancelled) {
                UpdateWhiff(show, visible);
                return;
            }
            //湖塌了演出没有舞台：收手谢幕（移除已是权威的事）
            if (!LakeAlive(show.OwnerIndex)) {
                BeginWhiff(show);
                return;
            }

            show.Timer++;
            int t = show.Timer;

            //跟踪真身并持续刷新鬼影快照；真身没了由模拟接管
            bool alive = show.Primary.TryResolve(out NPC target);
            if (alive) {
                show.TargetCenter = target.Center;
                show.TargetHalf = new Vector2(target.width, target.height) * 0.5f;
                CapturePrimary(show, target);
                show.StruggleBaseY = target.Center.Y;
            }
            else if (!show.GhostCaptured) {
                //连快照都没来得及捕获（起手就死了）：收手
                BeginWhiff(show);
                return;
            }
            else {
                SimulateGhost(show, t);
            }

            //组员：活着刷新快照，死了走小拖入
            UpdateMembers(show, t, visible);

            //合围：鼓包行进涟漪
            if (t <= ConvergeEnd && visible && t % 5 == 2) {
                for (int i = 0; i < show.Hands.Length; i++) {
                    Vector2 bulge = BulgePos(show, i, t);
                    KikasaDomainDeco.RippleAt(bulge, 0.3f);
                }
            }

            UpdateHands(show, t, visible);

            //绷紧拍：全臂骤直+重低音+拽拉
            if (!show.TenseDone && t >= TenseBeat) {
                show.TenseDone = true;
                if (visible) {
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.55f, Pitch = -0.6f, MaxInstances = 1 },
                        show.TargetCenter);
                    ShakeViewer(2.5f);
                }
            }

            //挣扎期臂上血珠下淌
            if (visible && t > StruggleStart && t < DragEnd && t % 6 == 0
                && show.Hands.Length > 0) {
                int h = (int)(Hash(show.Seed, t) * show.Hands.Length);
                HandState hand = show.Hands[h];
                if (hand.Rig.Opacity > 0.5f) {
                    Vector2 mid = Vector2.Lerp(hand.Rig.Root, hand.Rig.WristSolved, 0.55f);
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(mid,
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(1.6f, 2.6f)),
                        BloodTint * 0.5f, Main.rand.NextFloat(0.35f, 0.55f))
                        ?.Configure(Main.rand.Next(14, 22), 0f);
                }
            }

            //过水线拍
            if (!show.SubmergeSplashed && show.TargetCenter.Y >= show.LakeY) {
                show.SubmergeSplashed = true;
                if (visible) {
                    Vector2 hit = new(show.TargetCenter.X, show.LakeY);
                    KikasaDomainDeco.SplashAt(hit, 16);
                    KikasaDomainDeco.RippleAt(hit, 2.0f);
                    KikasaDomainDeco.RippleAt(hit + new Vector2(26f, 0f), 0.8f);
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.95f, Pitch = -0.3f, MaxInstances = 2 }, hit);
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.4f, Pitch = -0.8f, MaxInstances = 1 }, hit);
                    ShakeViewer(3.5f);
                }
            }

            //水下余韵
            if (visible && show.SubmergeSplashed && t < ShowEnd - 20 && t % 14 == 0) {
                KikasaDomainDeco.RippleAt(
                    new Vector2(show.TargetCenter.X + Main.rand.NextFloat(-14f, 14f), show.LakeY),
                    Main.rand.NextFloat(0.3f, 0.5f));
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    new Vector2(show.TargetCenter.X, show.LakeY - 6f),
                    new Vector2(0f, -0.3f), new Color(58, 18, 20) * 0.65f,
                    Main.rand.NextFloat(0.4f, 0.6f))?.Configure(Main.rand.Next(50, 80));
            }

            if (t == ShowEnd - 12 && visible) {
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.35f, Pitch = -0.55f, MaxInstances = 2 },
                    new Vector2(show.TargetCenter.X, show.LakeY));
            }
            if (t >= ShowEnd) {
                show.Done = true;
            }
        }

        private static void CapturePrimary(DrownShow show, NPC npc) {
            Main.instance.LoadNPC(npc.type);
            Texture2D tex = TextureAssets.Npc[npc.type]?.Value;
            if (tex == null) {
                return;
            }
            show.GhostCaptured = true;
            show.NpcType = npc.type;
            show.NpcFrame = npc.frame;
            show.NpcScale = npc.scale;
            show.NpcRot = npc.rotation;
            show.NpcFx = npc.spriteDirection > 0
                ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            show.NpcCenterOffY = VanillaCenterOffY(npc);
        }

        /// <summary>
        /// 原版把贴图底边锚在碰撞箱底+4px（步行怪贴图高于碰撞箱），
        /// 中心锚定绘制会差出几像素——换影瞬间目标钉死静止，跳变没有运动掩护
        /// </summary>
        private static float VanillaCenterOffY(NPC npc)
            => npc.Bottom.Y - npc.frame.Height * npc.scale * 0.5f + 4f + npc.gfxOffY
                - npc.Center.Y;

        //真身移除后的位置模拟：挣扎两轮衰减 → 加速拖入 → 水下缓沉

        //绷紧拍把目标拽下的量，挣扎基准随之下移

        private const float TenseJolt = 3f;

        private static void SimulateGhost(DrownShow show, int t) {
            if (t <= TenseBeat) {
                return;
            }
            if (t <= StruggleStart) {
                //绷紧拍：两帧内被拽下一小截——手赢了第一口气
                show.TargetCenter.Y = show.StruggleBaseY
                    + TenseJolt * (t - TenseBeat) / (StruggleStart - TenseBeat);
                return;
            }
            if (t <= StruggleEnd) {
                float st = t - StruggleStart;
                float decay = 1f - st / (StruggleEnd - StruggleStart);
                //上挣与被拽回的拉锯，振幅衰减、重心渐沉
                float osc = MathF.Sin(st * 0.52f) * 5f * decay;
                show.TargetCenter.Y = show.StruggleBaseY + TenseJolt - osc + st * 0.30f;
            }
            else if (t <= DragEnd) {
                float p = (t - StruggleEnd) / (float)(DragEnd - StruggleEnd);
                float startY = show.StruggleBaseY + TenseJolt
                    + (StruggleEnd - StruggleStart) * 0.30f;
                float endY = show.LakeY + 96f + show.TargetHalf.Y;
                //加速下拉，禁匀速
                show.TargetCenter.Y = MathHelper.Lerp(startY, endY, p * p);
                //被拽向根群正下方
                float rootMeanX = 0f;
                for (int i = 0; i < show.Hands.Length; i++) {
                    rootMeanX += show.Hands[i].Rig.Root.X;
                }
                rootMeanX /= show.Hands.Length;
                show.TargetCenter.X = MathHelper.Lerp(show.TargetCenter.X, rootMeanX, 0.03f);
            }
            else {
                show.TargetCenter.Y += 0.55f;
                float dt = MathHelper.Clamp((t - DragEnd - 4) / 50f, 0f, 1f);
                show.GhostDissolve = MathF.Pow(dt, 0.9f);
                show.GhostAlpha = MathHelper.Clamp((ShowEnd - t) / 12f, 0f, 1f);
            }
            //拖入期渐染血色
            if (t > StruggleEnd && t <= DragEnd) {
                show.GhostForm = MathHelper.Clamp((t - StruggleEnd) / 40f, 0f, 0.3f);
            }
        }

        private static void UpdateMembers(DrownShow show, int t, bool visible) {
            foreach (MiniGhost member in show.Members) {
                if (member.Identity.TryResolve(out NPC npc)) {
                    Main.instance.LoadNPC(npc.type);
                    member.Captured = TextureAssets.Npc[npc.type]?.Value != null;
                    member.NpcType = npc.type;
                    member.Frame = npc.frame;
                    member.Pos = npc.Center;
                    member.Rot = npc.rotation;
                    member.Scale = npc.scale;
                    member.Fx = npc.spriteDirection > 0
                        ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                    member.CenterOffY = VanillaCenterOffY(npc);
                    continue;
                }
                if (!member.Captured || t <= StruggleEnd) {
                    continue;
                }
                //组员没有专属手，拖入拍一起被拉下去
                member.Vy = MathF.Min(member.Vy + 0.9f, 16f);
                member.Pos.Y += member.Vy;
                if (!member.Splashed && member.Pos.Y >= show.LakeY) {
                    member.Splashed = true;
                    if (visible) {
                        Vector2 hit = new(member.Pos.X, show.LakeY);
                        KikasaDomainDeco.SplashAt(hit, 6);
                        KikasaDomainDeco.RippleAt(hit, 0.8f);
                        SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.5f, Pitch = -0.35f, MaxInstances = 2 }, hit);
                    }
                }
            }
        }

        //==================== 手编舞 ====================

        private static Vector2 BulgePos(DrownShow show, int handIndex, int t) {
            HandState hand = show.Hands[handIndex];
            float from = hand.Rig.Root.X
                + MathF.Sign(hand.Slot.RootSide == 0f ? 1f : hand.Slot.RootSide) * 150f;
            float ease = 1f - MathF.Pow(1f - MathHelper.Clamp(t / (float)ConvergeEnd, 0f, 1f), 2f);
            return new Vector2(MathHelper.Lerp(from, hand.Rig.Root.X, ease), show.LakeY + 4f);
        }

        private static void UpdateHands(DrownShow show, int t, bool visible) {
            for (int i = 0; i < show.Hands.Length; i++) {
                HandState hand = show.Hands[i];
                KikasaHandRig rig = hand.Rig;

                Vector2 gripWorld = show.TargetCenter + hand.GripLocal;
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
                        KikasaDomainDeco.SplashAt(rig.Root, 7);
                        KikasaDomainDeco.RippleAt(rig.Root, 0.9f);
                        SoundEngine.PlaySound(SoundID.SplashWeak with {
                            Volume = 0.55f,
                            Pitch = -0.45f + i * 0.07f,
                            MaxInstances = 3
                        }, rig.Root);
                    }
                }

                int localT = t - hand.BurstFrame;
                const int reachFrames = 9;

                if (localT <= reachFrames) {
                    //爆发过冲弧线：根先动腕滞后，控制点抬向外上——鞭出去的
                    float rt = localT / (float)reachFrames;
                    float ease = 1f - MathF.Pow(1f - rt, 2.6f);
                    Vector2 start = new(rig.Root.X, show.LakeY + 12f);
                    Vector2 ctrl = rig.Root
                        + (wristGoal - rig.Root) * 0.5f
                        + new Vector2(hand.Slot.RootSide * 26f, -70f * rig.Scale);
                    Vector2 a = Vector2.Lerp(start, ctrl, ease);
                    Vector2 b = Vector2.Lerp(ctrl, wristGoal, ease);
                    rig.Wrist = Vector2.Lerp(a, b, ease);
                    rig.Tension = 0.75f;
                    rig.Curl = MathHelper.Lerp(rig.Curl, -0.1f + rt * 0.15f, 0.4f);
                }
                else {
                    //锁定抓点：跟着目标走，强跟随带一点分量
                    rig.Wrist = Vector2.Lerp(rig.Wrist, wristGoal, 0.55f);

                    //卷指合拢；过 0.7 的那一帧是"攥中"节拍
                    rig.Curl = MathHelper.Lerp(rig.Curl, 0.95f, 0.28f);
                    if (!hand.Grabbed && rig.Curl > 0.7f) {
                        hand.Grabbed = true;
                        if (visible) {
                            KikasaDomainDeco.RippleAt(new Vector2(gripWorld.X, show.LakeY), 0.4f);
                            SoundEngine.PlaySound(SoundID.DD2_SkeletonHurt with {
                                Volume = 0.42f,
                                Pitch = -0.75f + i * 0.05f,
                                MaxInstances = 3
                            }, gripWorld);
                            ShakeViewer(0.8f);
                            PRTLoader.NewParticle<PRT_GhostRainDrop>(gripWorld,
                                new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(1f, 2f)),
                                BloodTint * 0.55f, Main.rand.NextFloat(0.4f, 0.6f))
                                ?.Configure(Main.rand.Next(12, 20), 0f);
                        }
                    }

                    //张力编舞：合拢 0.5 → 绷紧拍骤降 → 挣扎期随拉锯回弹 → 拖入绷死
                    float tensionGoal;
                    if (t < TenseBeat) {
                        tensionGoal = 0.5f;
                    }
                    else if (t <= StruggleEnd) {
                        float st = t - StruggleStart;
                        float decay = MathF.Max(0f, 1f - st / (StruggleEnd - StruggleStart));
                        //目标上挣时臂被拽弯，手赢回时绷直
                        tensionGoal = 0.10f + MathF.Max(0f, MathF.Sin(st * 0.52f)) * 0.22f * decay;
                    }
                    else {
                        tensionGoal = 0.06f;
                    }
                    rig.Tension = MathHelper.Lerp(rig.Tension, tensionGoal, t == TenseBeat ? 0.6f : 0.25f);

                    //拖入期段长收缩：臂保持绷直被湖收回，不留松弛蛇形
                    if (t > StruggleEnd) {
                        float taut = Vector2.Distance(rig.Root, rig.Wrist) * 1.06f
                            / KikasaHandRig.ArmSegmentCount;
                        rig.SegmentLength = MathF.Max(MathHelper.Lerp(rig.SegmentLength, taut, 0.3f), 8f);
                    }
                }

                //化水回收与谢幕
                if (t > DragEnd) {
                    rig.Drain = MathHelper.Clamp((t - DragEnd) / 20f, 0f, 1f);
                    rig.Opacity = MathHelper.Clamp(1f - (t - DragEnd - 14) / 12f, 0f, 1f);
                }

                rig.Grip = MathHelper.Clamp(1f - rig.Tension * 1.4f, 0f, 1f);
                rig.Foam = MathHelper.Lerp(rig.Foam, show.SubmergeSplashed ? 0.8f : 0.35f, 0.1f);
                rig.Solve();
            }
        }

        //取消收手：空攥一拍，弧线缩回水里

        private static void UpdateWhiff(DrownShow show, bool visible) {
            show.WhiffTimer++;
            float wt = MathHelper.Clamp(show.WhiffTimer / (float)WhiffFrames, 0f, 1f);
            for (int i = 0; i < show.Hands.Length; i++) {
                KikasaHandRig rig = show.Hands[i].Rig;
                if (rig.Opacity <= 0.01f) {
                    continue;
                }
                //先空攥再折返
                rig.Curl = MathHelper.Lerp(rig.Curl, 0.95f, 0.3f);
                rig.Tension = MathHelper.Lerp(rig.Tension, 0.45f, 0.2f);
                Vector2 home = new(rig.Root.X, show.LakeY + 34f);
                rig.Wrist = Vector2.Lerp(rig.Wrist, home, 0.12f + wt * 0.25f);
                rig.Opacity = 1f - wt;
                rig.Drain = wt * 0.7f;
                rig.Solve();
                if (visible && show.WhiffTimer == WhiffFrames / 2) {
                    KikasaDomainDeco.RippleAt(new Vector2(rig.Root.X, show.LakeY), 0.5f);
                }
            }
            if (show.WhiffTimer >= WhiffFrames) {
                show.Done = true;
            }
        }

        //==================== 绘制 ====================

        /// <summary>由 KikasaDomainRender.EndEntityDraw 调用；湖面镜面随后给出倒影与水下血染</summary>
        public static void Draw(SpriteBatch spriteBatch) {
            if (shows.Count == 0) {
                return;
            }
            KikasaDomainPlayer viewed = KikasaDomain.Viewed;
            if (viewed == null) {
                return;
            }
            int viewedOwner = viewed.Player.whoAmI;

            DrawGlowLayer(spriteBatch, viewedOwner);

            Effect handFx = EffectLoader.KikasaHand?.Value;
            Texture2D noise = CWRAsset.NoiseSoft01?.Value;
            bool handShaderOk = handFx != null && noise != null;

            //背层手（托底/背箍）→ 鬼影 → 前层手（越顶/侧箍）
            DrawHandLayer(spriteBatch, viewedOwner, handFx, noise, handShaderOk, front: false);
            DrawGhostLayer(spriteBatch, viewedOwner);
            DrawHandLayer(spriteBatch, viewedOwner, handFx, noise, handShaderOk, front: true);
        }

        //加色层：合围鼓包的水下行进光斑 + 出水根口的泡沫光

        private static void DrawGlowLayer(SpriteBatch spriteBatch, int viewedOwner) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            bool begun = false;
            Vector2 origin = glow.Size() * 0.5f;

            foreach (DrownShow show in shows) {
                if (show.OwnerIndex != viewedOwner || show.Cancelled) {
                    continue;
                }
                for (int i = 0; i < show.Hands.Length; i++) {
                    HandState hand = show.Hands[i];
                    float a = 0f;
                    Vector2 pos = default;
                    Vector2 scale = default;
                    if (show.Timer <= hand.BurstFrame && show.Timer <= ConvergeEnd + i * 2) {
                        //鼓包：贴水面的扁光斑向根汇聚，渐亮
                        float ct = MathHelper.Clamp(show.Timer / (float)ConvergeEnd, 0f, 1f);
                        pos = BulgePos(show, i, show.Timer);
                        a = 0.30f * ct;
                        float r = 15f + 8f * ct;
                        scale = new Vector2(r * 2.4f / glow.Width, r * 0.85f / glow.Height);
                    }
                    else if (hand.Rig.Opacity > 0.05f) {
                        //根口泡沫光：手在外面，湖在它脚下打转
                        pos = hand.Rig.Root;
                        a = 0.22f * hand.Rig.Opacity * (0.6f + 0.4f * hand.Rig.Foam);
                        scale = new Vector2(30f / glow.Width * 2.0f, 12f / glow.Height);
                    }
                    if (a <= 0.01f) {
                        continue;
                    }
                    if (!begun) {
                        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive,
                            SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                            null, Main.GameViewMatrix.TransformationMatrix);
                        begun = true;
                    }
                    spriteBatch.Draw(glow, pos - Main.screenPosition, null,
                        FoamGlow * a, 0f, origin, scale, SpriteEffects.None, 0f);
                }
            }
            if (begun) {
                spriteBatch.End();
            }
        }

        //条带层：世界空间三角带，逐手设参；缺编时线链回退

        private static void DrawHandLayer(SpriteBatch spriteBatch, int viewedOwner,
            Effect handFx, Texture2D noise, bool shaderOk, bool front) {

            if (!shaderOk) {
                Texture2D pixel = VaultAsset.placeholder2?.Value;
                if (pixel == null) {
                    return;
                }
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone,
                    null, Main.GameViewMatrix.TransformationMatrix);
                foreach (DrownShow show in shows) {
                    if (show.OwnerIndex != viewedOwner) {
                        continue;
                    }
                    for (int i = 0; i < show.Hands.Length; i++) {
                        if (show.Hands[i].Rig.FrontLayer == front) {
                            show.Hands[i].Rig.DrawFallback(spriteBatch, pixel);
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

            foreach (DrownShow show in shows) {
                if (show.OwnerIndex != viewedOwner) {
                    continue;
                }
                for (int i = 0; i < show.Hands.Length; i++) {
                    KikasaHandRig rig = show.Hands[i].Rig;
                    if (rig.FrontLayer != front || rig.Opacity <= 0.01f) {
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

        //鬼影层：真身覆绘与水下溶解，复用 KikasaItemForm 的血水材质

        private static void DrawGhostLayer(SpriteBatch spriteBatch, int viewedOwner) {
            bool any = false;
            foreach (DrownShow show in shows) {
                if (show.OwnerIndex == viewedOwner && show.GhostCaptured
                    && !show.Cancelled && show.Timer >= GhostOverdrawStart) {
                    any = true;
                    break;
                }
            }
            if (!any) {
                return;
            }

            Effect form = EffectLoader.KikasaItemForm?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            bool shaderOk = form != null && noise != null;

            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            if (shaderOk) {
                Main.instance.GraphicsDevice.Textures[1] = noise;
                Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            }

            foreach (DrownShow show in shows) {
                if (show.OwnerIndex != viewedOwner || !show.GhostCaptured
                    || show.Cancelled || show.Timer < GhostOverdrawStart
                    || show.GhostAlpha <= 0.01f) {
                    continue;
                }
                DrawNpcGhost(spriteBatch, form, shaderOk, show.NpcType, show.NpcFrame,
                    show.TargetCenter + new Vector2(0f, show.NpcCenterOffY),
                    show.NpcRot, show.NpcScale, show.NpcFx,
                    show.GhostForm, show.GhostDissolve, show.GhostAlpha, show.Seed);
                foreach (MiniGhost member in show.Members) {
                    if (!member.Captured || member.Identity.TryResolve(out _)) {
                        continue;
                    }
                    float memberDissolve = MathHelper.Clamp(
                        show.GhostDissolve + (member.Splashed ? 0.15f : 0f), 0f, 1f);
                    DrawNpcGhost(spriteBatch, form, shaderOk, member.NpcType, member.Frame,
                        member.Pos + new Vector2(0f, member.CenterOffY),
                        member.Rot, member.Scale, member.Fx,
                        show.GhostForm, memberDissolve, show.GhostAlpha, show.Seed + 3.3f);
                }
            }

            spriteBatch.End();
        }

        private static void DrawNpcGhost(SpriteBatch spriteBatch, Effect form, bool shaderOk,
            int npcType, Rectangle frame, Vector2 center, float rotation, float npcScale,
            SpriteEffects fx, float ghostForm, float dissolve, float alpha, float seed) {

            Main.instance.LoadNPC(npcType);
            Texture2D tex = TextureAssets.Npc[npcType]?.Value;
            if (tex == null || frame.Width <= 0 || frame.Height <= 0) {
                return;
            }
            Vector2 origin = frame.Size() * 0.5f;

            Color color;
            if (shaderOk) {
                form.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                form.Parameters["uSeed"]?.SetValue(seed);
                form.Parameters["uForm"]?.SetValue(ghostForm);
                form.Parameters["uDissolve"]?.SetValue(dissolve);
                form.Parameters["uScanMode"]?.SetValue(0f);
                form.Parameters["uUvRect"]?.SetValue(new Vector4(
                    frame.X / (float)tex.Width, frame.Y / (float)tex.Height,
                    frame.Width / (float)tex.Width, frame.Height / (float)tex.Height));
                form.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
                form.Parameters["uAspect"]?.SetValue(frame.Width / (float)frame.Height);
                form.CurrentTechnique.Passes[0].Apply();
                color = new Color(255, 255, 255, (byte)(alpha * 255f));
            }
            else {
                color = Color.Lerp(Color.White, BloodTint, MathHelper.Clamp(ghostForm + dissolve, 0f, 1f))
                    * (alpha * (1f - dissolve));
            }

            spriteBatch.Draw(tex, center - Main.screenPosition, frame, color,
                rotation, origin, npcScale, fx, 0f);
        }

        private static bool IsViewedOwner(int ownerIndex) {
            KikasaDomainPlayer viewed = KikasaDomain.Viewed;
            return viewed != null && viewed.Player.whoAmI == ownerIndex;
        }

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);
    }
}
