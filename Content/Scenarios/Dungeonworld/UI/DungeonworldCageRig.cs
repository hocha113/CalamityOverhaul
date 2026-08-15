using System;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.UI
{
    /// <summary>
    /// 加载屏吊笼物理台架（镜像 OniRope 的 UI Verlet 绳先例，改造成加载屏专用）<br/>
    /// 坐标系=纵横比空间：x∈[0,aspect]、y∈[0,1]（单位=屏高），与 DungeonworldLoading.fx 的距离场同域<br/>
    /// 结构：主索 6 节 + 笼锤 1 节共 7 粒一条链（末段=吊环→笼中心），灯笼再挂一粒二级摆——
    /// 绳摆、笼滞后、灯笼再滞后的三级跟随全部由约束自然产生，不手写相位<br/>
    /// 加载期墙钟 dt 可达 0.1s（长帧钳制），固定 1/120s 子步推进保数值稳定
    /// </summary>
    internal static class DungeonworldCageRig
    {
        private const int RopeNodes = 6;             //主索节点数(含锚点,共 5 段)
        private const int NodeCount = RopeNodes + 1; //末位=笼锤(即笼中心)
        private const float SubStep = 1f / 120f;
        private const float Damping = 0.998f;        //每子步速度保持(τ≈4s 慢衰减,摆得起也停得下)
        private const float LanternDamping = 0.995f; //灯笼阻尼略重,小摆件晃得碎
        private const float CageWeight = 1.6f;       //笼锤加重,吊索保持绷紧
        private const float WindAccel = 0.010f;      //常驻双频微风(加速度,稳态摆幅≈5px@1080p)

        private static readonly Vector2[] pos = new Vector2[NodeCount];
        private static readonly Vector2[] old = new Vector2[NodeCount];
        private static Vector2 lantPos;
        private static Vector2 lantOld;
        private static Vector2 pendingKick;
        private static float prevGain = 1f;
        private static float lastAspect;
        private static float subAccum;
        private static bool warmed;

        public static bool Warmed => warmed;

        /// <summary>主索节点(0=锚点..5=吊环原点)</summary>
        public static Vector2 RopePoint(int i) => pos[Math.Clamp(i, 0, RopeNodes - 1)];
        public static Vector2 CageCenter => pos[NodeCount - 1];
        /// <summary>笼体倾角方向(sinθ,cosθ)=吊环→笼中心单位向量,静止=(0,1)</summary>
        public static Vector2 CageTilt =>
            (pos[NodeCount - 1] - pos[RopeNodes - 1]).SafeNormalize(Vector2.UnitY);
        public static Vector2 LanternPos => lantPos;
        /// <summary>灯笼摆角方向(sinφ,cosφ)=挂点→灯笼单位向量</summary>
        public static Vector2 LanternSwing => (lantPos - LanternAttach()).SafeNormalize(Vector2.UnitY);

        //shader uniform 打包(纵横比空间原样上载)
        public static Vector4 PackRope01 => new(pos[0].X, pos[0].Y, pos[1].X, pos[1].Y);
        public static Vector4 PackRope23 => new(pos[2].X, pos[2].Y, pos[3].X, pos[3].Y);
        public static Vector4 PackRope45 => new(pos[4].X, pos[4].Y, pos[5].X, pos[5].Y);
        public static Vector4 PackCagePose {
            get {
                Vector2 c = CageCenter;
                Vector2 t = CageTilt;
                return new Vector4(c.X, c.Y, t.X, t.Y);
            }
        }
        public static Vector4 PackLanternPose {
            get {
                Vector2 t = LanternSwing;
                return new Vector4(lantPos.X, lantPos.Y, t.X, t.Y);
            }
        }

        /// <summary>过渡开始时复位;下次 Advance 按当时的入场进度重摆</summary>
        public static void Reset() {
            warmed = false;
            subAccum = 0f;
            prevGain = 1f;
            pendingKick = Vector2.Zero;
        }

        /// <summary>过层钟冲量:横向踢笼锤,方向逐层交替,深层略重(钟越沉晃越明显)</summary>
        public static void BellKick(int layer) {
            float dir = (layer & 1) == 0 ? 1f : -1f;
            pendingKick.X += dir * DungeonworldLoadTheme.CageBellKick * (0.85f + layer * 0.04f);
        }

        /// <summary>每帧推进(DrawSetup 墙钟);speedGain 变化率折算竖向惯性</summary>
        public static void Advance(float dt, float realSeconds, float speedGain, bool descending) {
            float aspect = Main.screenHeight > 0
                ? Main.screenWidth / (float)Main.screenHeight : 16f / 9f;
            //入场起吊:锚点自屏外降到定位,easeOutCubic(替代旧 shader 侧的 cageSlide)
            float slide = MathHelper.Clamp(realSeconds / DungeonworldLoadTheme.IntroFadeEnd, 0f, 1f);
            slide = 1f - (1f - slide) * (1f - slide) * (1f - slide);
            var anchor = new Vector2(aspect * 0.5f, MathHelper.Lerp(
                DungeonworldLoadTheme.CageAnchorStartY, DungeonworldLoadTheme.CageAnchorRestY, slide));

            //未布防或分辨率跳变:按当前锚位重摆,防止绳从旧位置甩来(OniRope 同款守卫)
            if (!warmed || Math.Abs(aspect - lastAspect) > 0.01f
                || Vector2.DistanceSquared(pos[0], anchor) > 0.35f) {
                WarmStart(anchor);
            }
            lastAspect = aspect;

            float gainDelta = dt > 0f ? (speedGain - prevGain) / dt : 0f;
            prevGain = speedGain;

            subAccum += MathHelper.Clamp(dt, 0f, 0.1f);
            int steps = 0;
            while (subAccum >= SubStep && steps < 12) {
                Step(anchor, realSeconds, gainDelta, descending);
                subAccum -= SubStep;
                steps++;
            }
            if (steps >= 12) {
                subAccum = 0f;  //极端长帧丢弃余量,防子步螺旋
            }
        }

        //沿锚点垂直摆好;笼锤带一点侧偏挂上,入场自带一次轻摆
        private static void WarmStart(Vector2 anchor) {
            float seg = DungeonworldLoadTheme.CageRopeLen / (RopeNodes - 1);
            for (int i = 0; i < RopeNodes; i++) {
                pos[i] = old[i] = anchor + new Vector2(0f, seg * i);
            }
            pos[NodeCount - 1] = old[NodeCount - 1] =
                pos[RopeNodes - 1] + new Vector2(0.02f, DungeonworldLoadTheme.CageDrop);
            lantPos = lantOld = LanternAttach()
                + new Vector2(0f, DungeonworldLoadTheme.CageLanternDrop);
            warmed = true;
        }

        //笼底挂点(世界)=笼中心+倾角方向×挂点距(与 shader 的 LANT_ATTACH 同一几何)
        private static Vector2 LanternAttach() {
            return CageCenter + CageTilt * DungeonworldLoadTheme.CageLanternAttach;
        }

        private static void Step(Vector2 anchor, float time, float gainDelta, bool descending) {
            const float h = SubStep;
            //视重力:下行提速=表观变轻,减速=变重(上行取反),钳 ±25%
            float g = DungeonworldLoadTheme.CageGravity
                * (1f + MathHelper.Clamp(gainDelta * (descending ? -0.2f : 0.2f), -0.25f, 0.25f));

            for (int i = 1; i < NodeCount; i++) {
                Vector2 vel = (pos[i] - old[i]) * Damping;
                old[i] = pos[i];
                pos[i] += vel;
                float weight = i == NodeCount - 1 ? CageWeight : 1f;
                pos[i].Y += g * weight * h * h;
                //双频微风,越靠下摆幅越大(OniRope 同型)
                float reach = i / (float)(NodeCount - 1);
                float wind = (float)Math.Sin(time * 1.6f + i * 0.85f)
                    + (float)Math.Sin(time * 0.53f + i * 0.37f) * 0.55f;
                pos[i].X += wind * WindAccel * reach * h * h;
            }
            //钟冲量:Δv 折算 verlet 旧位偏移,踢笼锤并带半量给吊环
            if (pendingKick != Vector2.Zero) {
                old[NodeCount - 1] -= pendingKick * h;
                old[NodeCount - 2] -= pendingKick * (0.5f * h);
                pendingKick = Vector2.Zero;
            }

            //灯笼积分:只受重力,风与冲量经挂点运动间接传入
            Vector2 lvel = (lantPos - lantOld) * LanternDamping;
            lantOld = lantPos;
            lantPos += lvel;
            lantPos.Y += g * h * h;

            //距离约束 3 轮(入场快降时 2 轮会露出可见拉伸)
            for (int k = 0; k < 3; k++) {
                pos[0] = anchor;
                for (int i = 0; i < NodeCount - 1; i++) {
                    float rest = i == NodeCount - 2
                        ? DungeonworldLoadTheme.CageDrop
                        : DungeonworldLoadTheme.CageRopeLen / (RopeNodes - 1);
                    Vector2 delta = pos[i + 1] - pos[i];
                    float len = delta.Length();
                    if (len < 0.0001f) {
                        continue;
                    }
                    float diff = (len - rest) / len;
                    if (i == 0) {
                        //首端已钉:全部修正量给下一点
                        pos[i + 1] -= delta * diff;
                    }
                    else {
                        Vector2 corr = delta * (diff * 0.5f);
                        pos[i] += corr;
                        pos[i + 1] -= corr;
                    }
                }
            }
            pos[0] = anchor;

            //灯笼单向约束:只被挂点拉,不反拉笼(质量比大)
            Vector2 toLant = lantPos - LanternAttach();
            float dl = toLant.Length();
            if (dl > 0.0001f) {
                lantPos -= toLant * ((dl - DungeonworldLoadTheme.CageLanternDrop) / dl);
            }
        }
    }
}
