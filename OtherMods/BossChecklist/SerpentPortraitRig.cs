using CalamityOverhaul.Content.NPCs.BloomsandSerpents;
using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.OtherMods.BossChecklist
{
    /// <summary>
    /// 图鉴沙盒共享蛇链：荒花/脓蕾两条沙蟒的 headless 段链模拟。
    /// 编排一条固定循环的招牌动线——地表爬行 → 右端钻沙 → 沙下回返（地面鼓包）→
    /// 左侧破沙腾跃 → 落地续爬；跟链公式与八腿划桨步态镜像战斗实现
    /// （腿骨绘制与步态常量直接取自 <see cref="BssLegRig"/>），探地换成虚拟沙线。
    /// 体节蒙皮由各自演员绘制，rig 只输出位姿与腿
    /// </summary>
    internal sealed class SerpentPortraitRig
    {
        //==================== 共享贴图（两条沙蟒同用 BSS 素材，脓蕾靠 shader 换皮）====================

        [VaultLoaden(CWRConstant.NPC + "BSS/Head")]
        internal static Asset<Texture2D> HeadTex = null;
        [VaultLoaden(CWRConstant.NPC + "BSS/Body")]
        internal static Asset<Texture2D> BodyTex = null;
        [VaultLoaden(CWRConstant.NPC + "BSS/Tail")]
        internal static Asset<Texture2D> TailTex = null;

        /// <summary>体节取帧（两帧竖排：0 普通 / 1 红花或囊肿；1px 内缩防串帧，镜像战斗 SegFrame）</summary>
        internal static Rectangle BodyFrame(Texture2D tex, bool alt) {
            int frameH = tex.Height / 2;
            Rectangle frame = new(0, alt ? frameH : 0, tex.Width, frameH);
            frame.Y += 1;
            frame.Height -= 2;
            return frame;
        }

        internal struct SegmentPose
        {
            public Vector2 Center;
            /// <summary>绘制旋转（= 链向 + FacingRot，与战斗体节同约定）</summary>
            public float Rotation;
        }

        internal enum Stage
        {
            /// <summary>地表爬行（向右）</summary>
            Surface,
            /// <summary>俯冲钻沙</summary>
            Dive,
            /// <summary>沙下回返（只露鼓包）</summary>
            Buried,
            /// <summary>破沙腾跃（弹道弧线）</summary>
            Breach,
        }

        //==================== 编排参数 ====================

        private const float RideHeight = 34f;
        /// <summary>钻沙深度（头没过此深度进入沙下回返）</summary>
        private const float BurialDepth = 64f;
        private const float CruiseSpeed = 3.1f;
        private const float BuriedSpeed = 5.4f;
        /// <summary>破沙出射速度（横/纵，px每帧）</summary>
        private static readonly Vector2 BreachLaunch = new(2.4f, -16f);
        private const float BreachGravity = 0.42f;
        /// <summary>破沙点 X（巡游左端偏内）</summary>
        private const float BreachXFrac = -0.68f;

        /// <summary>髋站体节链序（沿用战斗编制）</summary>
        private static readonly int[] LegStations = BssLegRig.StationOrdinals;
        private const float LegUpperLen = BssLegRig.UpperLen;
        private const float LegLowerLen = BssLegRig.LowerLen;
        private const float LegMaxReach = LegUpperLen + LegLowerLen - 2f;

        private struct PortraitLeg
        {
            public Vector2 Hip;
            public Vector2 Foot;
            public Vector2 Back;
            public float Groundness;
            public bool Inited;
        }

        //==================== 状态 ====================

        private readonly float gap;
        private readonly float patrolHalf;
        /// <summary>沙线 Y（场景坐标）</summary>
        public readonly float SandY;

        private readonly SegmentPose[] segs;
        private readonly PortraitLeg[] legs = new PortraitLeg[8];

        private Vector2 headPos;
        private Vector2 prevHeadPos;
        private float heading;
        private float gait;
        private float speedNow;
        private Vector2 breachVel;

        public Stage CurrentStage { get; private set; }

        /// <summary>破沙瞬间（位置，出射方向）</summary>
        public Action<Vector2, Vector2> OnBreach;
        /// <summary>钻入沙面瞬间（位置）</summary>
        public Action<Vector2> OnDive;
        /// <summary>落地瞬间（位置）</summary>
        public Action<Vector2> OnLand;

        public SerpentPortraitRig(int bodyCount, float segmentGap, float sandY, float patrolHalfWidth) {
            gap = segmentGap;
            SandY = sandY;
            patrolHalf = patrolHalfWidth;
            //链序 0..bodyCount-1 = 体节，末位 = 尾
            segs = new SegmentPose[bodyCount + 1];
        }

        public Vector2 HeadPos => headPos;
        /// <summary>头绘制旋转（与战斗头同约定）</summary>
        public float HeadRotation => heading + BssHead.FacingRot;
        public SegmentPose[] Segments => segs;
        public int TailOrdinal => segs.Length - 1;
        /// <summary>当前行进速度（px每帧，演员的辉光/粒子门控用）</summary>
        public float SpeedNow => speedNow;
        /// <summary>沙下鼓包中心 X（仅 Buried 期有意义）</summary>
        public float MoundX => headPos.X;
        public bool HeadBuried => headPos.Y > SandY + 6f;

        /// <summary>指定链序是否埋在沙下（演员可据此省略被沙带盖住的开销）</summary>
        public bool SegmentBuried(int ordinal) => segs[ordinal].Center.Y > SandY + 6f;

        public void Reset() {
            headPos = new Vector2(-patrolHalf * 0.25f, SandY - RideHeight);
            prevHeadPos = headPos;
            heading = 0f;
            gait = 0f;
            speedNow = CruiseSpeed;
            CurrentStage = Stage.Surface;
            //整链向后铺直
            for (int i = 0; i < segs.Length; i++) {
                segs[i].Center = headPos - new Vector2((i + 1) * gap, 0f);
                segs[i].Rotation = BssHead.FacingRot;
            }
            for (int li = 0; li < legs.Length; li++) {
                legs[li].Inited = false;
            }
        }

        /// <summary>推进一帧（frames = dt×60，60fps 基准）</summary>
        public void Update(float dt) {
            float frames = dt * 60f;
            prevHeadPos = headPos;

            switch (CurrentStage) {
                case Stage.Surface:
                    UpdateSurface(frames);
                    break;
                case Stage.Dive:
                    UpdateDive(frames);
                    break;
                case Stage.Buried:
                    UpdateBuried(frames);
                    break;
                case Stage.Breach:
                    UpdateBreach(frames);
                    break;
            }

            Vector2 moved = headPos - prevHeadPos;
            speedNow = frames > 0.001f ? moved.Length() / frames : 0f;
            if (moved.LengthSquared() > 0.01f) {
                heading = moved.ToRotation();
            }

            gait += BssStateContext.GaitIncrement(speedNow) * frames;
            FollowChain();
            UpdateLegs();
        }

        private void UpdateSurface(float frames) {
            headPos.X += CruiseSpeed * frames;
            //贴地呼吸：随步态时钟轻沉浮
            headPos.Y = SandY - RideHeight + MathF.Sin(gait * 1.1f) * 4f;
            if (headPos.X >= patrolHalf) {
                CurrentStage = Stage.Dive;
            }
        }

        private void UpdateDive(float frames) {
            bool wasAbove = headPos.Y < SandY;
            headPos.X += CruiseSpeed * 1.2f * frames;
            //下潜速度渐增：入水角越来越陡
            float depth01 = MathHelper.Clamp((headPos.Y - (SandY - RideHeight)) / (BurialDepth + RideHeight), 0f, 1f);
            headPos.Y += MathHelper.Lerp(2.4f, 7f, depth01) * frames;
            if (wasAbove && headPos.Y >= SandY) {
                OnDive?.Invoke(new Vector2(headPos.X, SandY));
            }
            if (headPos.Y >= SandY + BurialDepth) {
                CurrentStage = Stage.Buried;
            }
        }

        private void UpdateBuried(float frames) {
            float breachX = patrolHalf * BreachXFrac;
            headPos.X -= BuriedSpeed * frames;
            headPos.Y = SandY + BurialDepth + MathF.Sin(gait * 0.7f) * 6f;
            if (headPos.X <= breachX) {
                CurrentStage = Stage.Breach;
                breachVel = BreachLaunch;
            }
        }

        private void UpdateBreach(float frames) {
            bool wasBelow = headPos.Y > SandY;
            breachVel.Y += BreachGravity * frames;
            headPos += breachVel * frames;
            if (wasBelow && headPos.Y <= SandY && breachVel.Y < 0f) {
                OnBreach?.Invoke(new Vector2(headPos.X, SandY), breachVel.SafeNormalize(-Vector2.UnitY));
            }
            //回落着地：贴回爬行高度续巡
            if (breachVel.Y > 0f && headPos.Y >= SandY - RideHeight) {
                headPos.Y = SandY - RideHeight;
                CurrentStage = Stage.Surface;
                OnLand?.Invoke(new Vector2(headPos.X, SandY));
            }
        }

        /// <summary>标准跟链（镜像 BssBody.FollowChain，压缩恒 1）</summary>
        private void FollowChain() {
            Vector2 front = headPos;
            for (int i = 0; i < segs.Length; i++) {
                Vector2 toFront = front - segs[i].Center;
                if (toFront.LengthSquared() > 0.01f) {
                    segs[i].Rotation = toFront.ToRotation() + BssHead.FacingRot;
                    segs[i].Center = front - toFront.SafeNormalize(Vector2.Zero) * gap;
                }
                front = segs[i].Center;
            }
        }

        //==================== 八腿划桨（镜像 BssLegRig.UpdateStroke，探地换沙线）====================

        private void UpdateLegs() {
            for (int li = 0; li < legs.Length; li++) {
                int station = li / 2;
                int ordinal = LegStations[station];
                if (ordinal >= segs.Length) {
                    continue;
                }
                SegmentPose seg = segs[ordinal];

                float chainDir = seg.Rotation + MathHelper.PiOver2;
                Vector2 chainVec = chainDir.ToRotationVector2();
                float flankSign = (li & 1) == 0 ? 1f : -1f;
                Vector2 normal = (chainDir + MathHelper.PiOver2).ToRotationVector2() * flankSign;
                Vector2 hip = seg.Center + normal * 10f;

                ref PortraitLeg leg = ref legs[li];
                leg.Hip = hip;
                leg.Back = -chainVec;
                leg.Groundness = MathHelper.Clamp((normal.Y + 0.6f) / 1.2f, 0f, 1f);

                if (!leg.Inited) {
                    Vector2 f0 = hip + normal * (LegMaxReach * 0.7f);
                    f0.Y = MathF.Min(f0.Y, SandY);
                    leg.Foot = f0;
                    leg.Inited = true;
                }

                //宿主体节埋沙：沿体轴后掠收拢（钻沙流线，镜像 Tuck 指令）
                if (seg.Center.Y > SandY + 4f) {
                    Vector2 fold = hip - chainVec * (30f + station * 6f + (li & 1) * 8f) + normal * 8f;
                    leg.Foot = Vector2.Lerp(leg.Foot, fold, 0.28f);
                    continue;
                }

                //划桨往复：功率段快耙全伸、恢复段折叠前探抛物线抬腿
                float t01 = SlotPhase01(li);
                float tilt, radius, clearance;
                if (t01 < BssLegRig.PowerFraction) {
                    float p = t01 / BssLegRig.PowerFraction;
                    tilt = MathHelper.Lerp(BssLegRig.TiltForward, -BssLegRig.TiltBack, p);
                    radius = 0.88f;
                    clearance = 0f;
                }
                else {
                    float r = (t01 - BssLegRig.PowerFraction) / (1f - BssLegRig.PowerFraction);
                    float arc = MathF.Sin(r * MathHelper.Pi);
                    float eased = r * r * (3f - 2f * r);
                    tilt = MathHelper.Lerp(-BssLegRig.TiltBack, BssLegRig.TiltForward, eased);
                    radius = MathHelper.Lerp(0.88f, 0.52f, arc);
                    clearance = BssLegRig.RecoveryClearance * arc;
                }

                float rotSign = (li & 1) == 0 ? -1f : 1f;
                Vector2 target = hip + normal.RotatedBy(tilt * rotSign) * (LegMaxReach * radius);
                //沙线钳制：贴地排耙沙，背侧/空中排 min() 自然无效
                target.Y = MathF.Min(target.Y, SandY - clearance);
                leg.Foot = Vector2.Lerp(leg.Foot, target, 0.4f);
            }
        }

        /// <summary>该腿的时钟槽相位 0..1：站序节律波 + 同站两侧反相（镜像战斗版）</summary>
        private float SlotPhase01(int li) {
            float phase = gait - li / 2 * BssLegRig.StationLag + ((li & 1) == 1 ? MathHelper.Pi : 0f);
            phase %= MathHelper.TwoPi;
            if (phase < 0f) {
                phase += MathHelper.TwoPi;
            }
            return phase / MathHelper.TwoPi;
        }

        /// <summary>
        /// 画八腿：按走地权重升序（暗排在底亮排在面），二骨余弦 IK 膝弯朝体后，
        /// 骨节绘制直接走 <see cref="BssLegRig.DrawBone"/>
        /// </summary>
        public void DrawLegs(SpriteBatch sb, in PortraitFrame frame, Color ambient) {
            Texture2D upperTex = BssHead.LegUpperAsset?.Value;
            Texture2D lowerTex = BssHead.LegLowerAsset?.Value;
            if (upperTex == null || lowerTex == null) {
                return;
            }

            Span<int> order = stackalloc int[legs.Length];
            for (int i = 0; i < legs.Length; i++) {
                order[i] = i;
            }
            for (int i = 1; i < legs.Length; i++) {
                int cur = order[i];
                float key = legs[cur].Groundness;
                int j = i - 1;
                while (j >= 0 && legs[order[j]].Groundness > key) {
                    order[j + 1] = order[j];
                    j--;
                }
                order[j + 1] = cur;
            }

            foreach (int li in order) {
                PortraitLeg leg = legs[li];
                if (!leg.Inited) {
                    continue;
                }
                float dim = MathHelper.Lerp(0.62f, 1f, leg.Groundness);
                Color tint = frame.Tint(ambient.MultiplyRGB(new Color(dim, dim, dim)));

                Vector2 hip = leg.Hip;
                Vector2 foot = leg.Foot;
                Vector2 d = foot - hip;
                float rawLen = d.Length();
                if (rawLen > LegMaxReach) {
                    foot = hip + d * (LegMaxReach / rawLen);
                    d = foot - hip;
                }
                float dist = MathHelper.Clamp(d.Length(), 14f, LegMaxReach);
                float baseAng = d.ToRotation();
                float cosA = MathHelper.Clamp(
                    (LegUpperLen * LegUpperLen + dist * dist - LegLowerLen * LegLowerLen)
                    / (2f * LegUpperLen * dist), -1f, 1f);
                float phi = MathF.Acos(cosA);
                Vector2 back = leg.Back;
                float kneeAng = Vector2.Dot((baseAng + phi).ToRotationVector2(), back)
                    >= Vector2.Dot((baseAng - phi).ToRotationVector2(), back)
                    ? baseAng + phi : baseAng - phi;
                Vector2 knee = hip + kneeAng.ToRotationVector2() * LegUpperLen;

                float thick = MathHelper.Lerp(0.9f, 1f, leg.Groundness);
                BssLegRig.DrawBone(sb, upperTex, hip, knee, 1.2f * thick, tint, Vector2.Zero);
                BssLegRig.DrawBone(sb, lowerTex, knee, foot, 0.95f * thick, tint, Vector2.Zero);
            }
        }
    }
}
