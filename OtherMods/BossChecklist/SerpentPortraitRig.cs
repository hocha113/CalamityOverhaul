using CalamityOverhaul.Content.NPCs;
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
    /// 左侧破沙腾跃 → 落地续爬；跟链公式镜像战斗实现，八腿直接托管战斗端的
    /// <see cref="BssLegRig"/> 三节步足模拟（世界落足步行同一套），探地换成虚拟沙线。
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

        /// <summary>体节取帧（三帧竖排：0 绿赘 / 1 干净 / 2 橙囊；1px 内缩防串帧，镜像战斗 SegFrame）</summary>
        internal static Rectangle BodyFrame(Texture2D tex, int style) {
            int frames = Math.Max(SerpentChainMath.BodyStyleCount, 1);
            int frameH = tex.Height / frames;
            style = Math.Clamp(style, 0, frames - 1);
            Rectangle frame = new(0, style * frameH, tex.Width, frameH);
            frame.Y += 1;
            frame.Height -= 2;
            return frame;
        }

        internal static Rectangle BodyFrame(Texture2D tex, int ordinal, bool emitter)
            => BodyFrame(tex, SerpentChainMath.BodyStyleIndex(ordinal, emitter));

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

        //==================== 状态 ====================

        private readonly float gap;
        private readonly float patrolHalf;
        /// <summary>沙线 Y（场景坐标）</summary>
        public readonly float SandY;

        private readonly SegmentPose[] segs;
        /// <summary>八腿共用战斗端三节步足模拟（探地换虚拟沙线，沙效喂 motes）</summary>
        private readonly BssLegRig legRig = new();
        private Func<float, float, float> legGroundAt;
        /// <summary>落步/犁沙的沙尘出口（由演员 Reset 后经 OnLegSandFx 订阅）</summary>
        public Action<Vector2, Vector2, float> OnLegSandFx;
        /// <summary>是否带鳌足（荒花专属剪影；脓蕾共用本 rig 但无鳌足）</summary>
        public bool WithClaws;
        /// <summary>鳌足共用战斗端骨架（待机呼吸摆，埋沙自动收拢）</summary>
        private readonly BssClawRig clawRig = new();

        private Vector2 headPos;
        private Vector2 prevHeadPos;
        private float heading;
        /// <summary>显示朝向（对运动朝向做平滑：落地收平、破沙扬头读作转体而非瞬跳）</summary>
        private float displayHeading;
        /// <summary>下一步显示朝向直接对齐（沙下不可见的急转向，如出射瞬间）</summary>
        private bool snapHeading;
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
        /// <summary>头绘制旋转（与战斗头同约定，用平滑后的显示朝向）</summary>
        public float HeadRotation => displayHeading + BssHead.FacingRot;
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
            displayHeading = 0f;
            snapHeading = false;
            gait = 0f;
            speedNow = CruiseSpeed;
            CurrentStage = Stage.Surface;
            //整链向后铺直
            for (int i = 0; i < segs.Length; i++) {
                segs[i].Center = headPos - new Vector2((i + 1) * gap, 0f);
                segs[i].Rotation = BssHead.FacingRot;
            }
            legRig.ResetLegs();
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
            //显示朝向：沙下急转向直接对齐（不可见），可见段平滑过渡——
            //落地由俯冲角渐收回水平，读作收平身体而非贴图瞬跳
            if (snapHeading) {
                snapHeading = false;
                displayHeading = heading;
            }
            else {
                displayHeading = displayHeading.AngleLerp(heading, 0.3f);
            }

            gait += BssStateContext.GaitIncrement(speedNow) * frames;
            FollowChain();
            UpdateLegs();
            if (WithClaws) {
                UpdateClaws();
            }
        }

        /// <summary>鳌足推进：地表待机呼吸摆，埋沙/钻沙自动收拢（跟随头位姿）</summary>
        private void UpdateClaws() {
            BssClawRig.ClawEnv env = new() {
                Command = HeadBuried ? BssClawCommand.Tuck : BssClawCommand.Idle,
                HeadCenter = headPos,
                HeadRotation = HeadRotation,
                HeadVelocity = headPos - prevHeadPos,
                AllowDust = false,
            };
            clawRig.Advance(in env);
        }

        /// <summary>远层鳌足（蒙皮前调用）</summary>
        public void DrawClawsBack(SpriteBatch sb, in PortraitFrame frame, Color ambient) {
            if (!WithClaws) {
                return;
            }
            PortraitFrame frameCopy = frame;
            clawRig.DrawStandalone(sb, front: false,
                dim => frameCopy.Tint(ambient.MultiplyRGB(new Color(dim, dim, dim))));
        }

        /// <summary>近层鳌足（蒙皮后调用）</summary>
        public void DrawClawsFront(SpriteBatch sb, in PortraitFrame frame, Color ambient) {
            if (!WithClaws) {
                return;
            }
            PortraitFrame frameCopy = frame;
            clawRig.DrawStandalone(sb, front: true,
                dim => frameCopy.Tint(ambient.MultiplyRGB(new Color(dim, dim, dim))));
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
                //出射转向发生在沙下：显示朝向直接对齐，出水那一帧头已朝出射方向
                snapHeading = true;
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

        //==================== 八腿（共用战斗端三节步足模拟，探地换虚拟沙线）====================

        private void UpdateLegs() {
            for (int st = 0; st < BssLegRig.LegCount; st++) {
                int ordinal = LegStations[st];
                bool ok = ordinal < segs.Length;
                legRig.SetStation(st,
                    ok ? segs[ordinal].Center : Vector2.Zero,
                    ok ? segs[ordinal].Rotation : 0f, ok);
            }

            legGroundAt ??= (x, refY) => SandY;
            BssLegRig.LegEnv env = new() {
                //钻沙埋腿（髋没入沙线自动收拢）与破沙腾空（够不着沙线自动卷曲）
                //都由核心兜底，图鉴只声明常态步行
                Command = BssLegCommand.March,
                GaitPhase = gait,
                HostVelocity = headPos - prevHeadPos,
                GroundAt = legGroundAt,
                OnPlant = null,
                SandFx = OnLegSandFx,
                AllowDust = false,
            };
            legRig.Advance(in env);
        }

        /// <summary>画八腿：排序/骨节绘制都在 <see cref="BssLegRig.DrawStandalone"/>，此处只给环境色</summary>
        public void DrawLegs(SpriteBatch sb, in PortraitFrame frame, Color ambient) {
            PortraitFrame frameCopy = frame;
            legRig.DrawStandalone(sb, (li, groundness) => {
                float dim = MathHelper.Lerp(0.62f, 1f, groundness);
                return frameCopy.Tint(ambient.MultiplyRGB(new Color(dim, dim, dim)));
            });
        }
    }
}
