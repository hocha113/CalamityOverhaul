using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Wraiths.Projectiles
{
    /// <summary>
    /// 无头鬼影的本体骨架，纯客户端。躯干是贴 Shutter 剪影的分段条带（能弯能拖），
    /// 双臂走程序化锥形条带，另有一份压扁剪切的地面投影，它是影子，不是漂浮贴图。
    /// 断颈之上不许有任何几何：无头是身份，颈口只由着色器给一线骨白。
    /// 蓄力/扑出全部写进姿态斜率（重心下压、上身抢线），不做贴图轴向缩放。
    /// </summary>
    internal sealed class HeadlessShadeRig
    {
        private const int SpineNodes = 7;
        private const int ArmNodes = 4;
        private const int MaxStripNodes = 8;
        /// <summary>地面探测上限</summary>
        private const float GroundProbe = 224f;
        /// <summary>单帧位移超过这个距离视为瞬移，链条重钉</summary>
        private const float TeleportSnapSq = 150f * 150f;
        /// <summary>R=0 是关键：肢体技法把 R 当骨白量读</summary>
        private static readonly Color StripColor = new(0f, 0f, 0f, 1f);

        private readonly Vector2[] spine = new Vector2[SpineNodes];
        private readonly Vector2[] farArm = new Vector2[ArmNodes];
        private readonly Vector2[] nearArm = new Vector2[ArmNodes];
        private readonly VertexPositionColorTexture[] stripVertices
            = new VertexPositionColorTexture[MaxStripNodes * 2];

        private bool primed;
        private Vector2 lastAnchor;
        private float seedPhase;
        private float groundY = float.NaN;
        private int facing = 1;
        /// <summary>朝向的连续量：转身时双臂扫过身前，而不是一帧镜像跳变</summary>
        private float facingSmooth = 1f;
        private float halfWidth = 92f;

        internal void SetSeed(float seed) => seedPhase = seed * MathHelper.TwoPi;

        /// <summary>本体熄灭后重新亮起时把整套链条钉回姿态，避免从旧位置拖一条线过来</summary>
        internal void Snap() => primed = false;

        internal void Update(Vector2 center, float halfHeight, float bodyHalfWidth, int direction,
            Vector2 lead, float crouch, float lunge, float time) {
            facing = direction >= 0 ? 1 : -1;
            facingSmooth = MathHelper.Lerp(facingSmooth, facing, 0.20f);
            halfWidth = bodyHalfWidth;

            //姿态而非缩放：蓄力压低拉回重心，扑出时上身抢进冲线、体长顺势微延
            float effHalf = halfHeight * (1f - crouch * 0.14f + lunge * 0.08f);
            float segLen = effHalf * 2f / (SpineNodes - 1);
            Vector2 anchor = center - Vector2.UnitY * effHalf
                + lead * effHalf * (lunge * 0.42f - crouch * 0.16f)
                + Vector2.UnitY * (crouch * effHalf * 0.10f);

            //瞬移级位移直接重钉，否则链条会被拉成一条面条
            if (!primed || Vector2.DistanceSquared(anchor, lastAnchor) > TeleportSnapSq) {
                Prime(anchor, segLen, lead, lunge);
                primed = true;
            }
            lastAnchor = anchor;

            UpdateSpine(anchor, segLen, lead, crouch, lunge, time);
            float swing = MathHelper.Clamp(MathF.Max(lunge, crouch * 0.5f), 0f, 1f);
            UpdateArms(segLen, lead, swing, time);
            groundY = ProbeGround(spine[SpineNodes - 1]);
        }

        private void Prime(Vector2 anchor, float segLen, Vector2 lead, float lunge) {
            //重钉常发生在穿体重亮的一瞬：直接按扑姿钉链，避免先立正再倒下去
            Vector2 down = (Vector2.UnitY + lead * (lunge * 0.85f)).SafeNormalize(Vector2.UnitY);
            facingSmooth = facing;
            for (int i = 0; i < SpineNodes; i++) {
                spine[i] = anchor + down * (segLen * i);
            }
            Vector2 shoulder = Vector2.Lerp(spine[2], spine[3], 0.10f);
            float armSpan = halfWidth * 0.40f;
            for (int i = 0; i < ArmNodes; i++) {
                Vector2 drop = down * (segLen * 0.70f * i);
                farArm[i] = shoulder - new Vector2(armSpan * facing, 0f) + drop;
                nearArm[i] = shoulder + new Vector2(armSpan * facing, 0f) + drop;
            }
        }

        private void UpdateSpine(Vector2 anchor, float segLen, Vector2 lead, float crouch,
            float lunge, float time) {
            spine[0] = anchor;
            float drag = crouch * 8f + lunge * 26f;
            for (int i = 1; i < SpineNodes; i++) {
                float depth = i / (SpineNodes - 1f);
                //越靠下摆跟得越慢，位移时下摆自然被拖在身后
                float follow = MathHelper.Lerp(0.62f, 0.20f, depth);
                float sway = MathF.Sin(time * 1.55f + i * 0.72f + seedPhase) * 2.6f * depth;
                Vector2 desired = spine[i - 1] + Vector2.UnitY * segLen
                    + new Vector2(sway, 0f)
                    - lead * (drag * depth);
                spine[i] = Vector2.Lerp(spine[i], desired, follow);
                Vector2 delta = spine[i] - spine[i - 1];
                spine[i] = spine[i - 1] + delta.SafeNormalize(Vector2.UnitY) * segLen;
            }
        }

        private void UpdateArms(float segLen, Vector2 lead, float swing, float time) {
            //袖口在剪影 V≈0.35 处收住，前臂从那里接出去，正好补上贴图没有的部分
            Vector2 spineDir = (spine[3] - spine[2]).SafeNormalize(Vector2.UnitY);
            Vector2 shoulderNormal = spineDir.RotatedBy(MathHelper.PiOver2);
            Vector2 shoulderCenter = Vector2.Lerp(spine[2], spine[3], 0.10f);
            float armSpan = halfWidth * 0.40f;
            Vector2 farShoulder = shoulderCenter + shoulderNormal * (armSpan * facingSmooth);
            Vector2 nearShoulder = shoulderCenter - shoulderNormal * (armSpan * facingSmooth);

            //扑杀时双臂由垂挂甩向目标，"扑"要靠这一下读出来；扑出瞬间臂链顺势探长
            Vector2 restFar = new Vector2(-0.45f * facingSmooth, 1f).SafeNormalize(Vector2.UnitY);
            Vector2 restNear = new Vector2(0.50f * facingSmooth, 1f).SafeNormalize(Vector2.UnitY);
            float reach = 1f + swing * 0.22f;
            UpdateArm(farArm, farShoulder, restFar, lead, swing, segLen * 0.66f * reach, time, 0.7f);
            UpdateArm(nearArm, nearShoulder, restNear, lead, swing, segLen * 0.72f * reach, time, 1.9f);
        }

        private void UpdateArm(Vector2[] arm, Vector2 shoulder, Vector2 rest, Vector2 lead,
            float swing, float segLen, float time, float phaseOffset) {
            Vector2 aim = lead.LengthSquared() > 0.01f
                ? Vector2.Lerp(rest, lead, swing * 0.88f).SafeNormalize(rest)
                : rest;
            arm[0] = shoulder;
            for (int i = 1; i < ArmNodes; i++) {
                float depth = i / (ArmNodes - 1f);
                float follow = MathHelper.Lerp(0.42f, 0.20f, depth);
                float droop = (1f - swing) * 0.22f * depth;
                Vector2 bend = aim.RotatedBy(droop * facingSmooth);
                float idle = MathF.Sin(time * 1.9f + phaseOffset + seedPhase) * 1.8f * depth * (1f - swing);
                Vector2 desired = arm[i - 1] + bend * segLen + new Vector2(idle, 0f);
                arm[i] = Vector2.Lerp(arm[i], desired, follow);
                Vector2 delta = arm[i] - arm[i - 1];
                arm[i] = arm[i - 1] + delta.SafeNormalize(bend) * segLen;
            }
        }

        private static float ProbeGround(Vector2 hem) {
            int tileX = (int)(hem.X / 16f);
            int startY = (int)(hem.Y / 16f);
            int endY = (int)((hem.Y + GroundProbe) / 16f);
            for (int tileY = startY; tileY <= endY; tileY++) {
                Tile tile = Framing.GetTileSafely(tileX, tileY);
                if (tile.HasUnactuatedTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType]) {
                    return tileY * 16f;
                }
            }
            return float.NaN;
        }

        /// <summary>躯干条带，剪影完全由 Shutter 的 alpha 刻出来，条带只负责让它能弯</summary>
        internal void DrawBody(GraphicsDevice device, Effect effect, float opacity, float dissolve,
            float phase, float rimFlash, float seed) {
            UseTechnique(effect, "TechBody");
            effect.Parameters["uOpacity"]?.SetValue(MathHelper.Clamp(opacity, 0f, 1f));
            effect.Parameters["uDissolve"]?.SetValue(MathHelper.Clamp(dissolve, 0f, 1f));
            effect.Parameters["uPhase"]?.SetValue(MathHelper.Clamp(phase, 0f, 1f));
            effect.Parameters["uRimFlash"]?.SetValue(MathHelper.Clamp(rimFlash, 0f, 1f));
            effect.Parameters["uSeed"]?.SetValue(seed);

            BuildChainStrip(spine, SpineNodes, halfWidth, halfWidth,
                flipU: facing < 0, alongV: true);
            DrawStrip(device, effect, SpineNodes);
        }

        /// <summary>地面投影：把躯干各节按剪切压到地面线上，越高的部位甩得越远</summary>
        internal void DrawGroundCast(GraphicsDevice device, Effect effect, float opacity,
            float dissolve, float seed) {
            if (float.IsNaN(groundY)) {
                return;
            }

            float gap = groundY - spine[SpineNodes - 1].Y;
            if (gap < 0f || gap > GroundProbe) {
                return;
            }
            //离地越高投影越淡越散，贴地时最实
            float contact = 1f - MathHelper.Clamp(gap / GroundProbe, 0f, 1f);
            float castAlpha = opacity * MathHelper.Lerp(0.10f, 0.42f, contact);
            if (castAlpha < 0.03f) {
                return;
            }

            UseTechnique(effect, "TechBody");
            effect.Parameters["uOpacity"]?.SetValue(castAlpha);
            effect.Parameters["uDissolve"]?.SetValue(MathHelper.Clamp(dissolve + 0.22f, 0f, 1f));
            effect.Parameters["uPhase"]?.SetValue(0f);
            effect.Parameters["uRimFlash"]?.SetValue(0f);
            effect.Parameters["uSeed"]?.SetValue(seed + 4.6f);

            //投影压在几何上而不是骨架上：先建常规剪影条带，再逐顶点压到地面线
            BuildChainStrip(spine, SpineNodes, halfWidth, halfWidth, flipU: facing < 0, alongV: true);
            float shear = -0.62f * facingSmooth;
            for (int i = 0; i < SpineNodes * 2; i++) {
                Vector3 point = stripVertices[i].Position;
                float rise = groundY - point.Y;
                stripVertices[i].Position = new Vector3(point.X + rise * shear,
                    groundY - rise * 0.17f, 0f);
            }
            DrawStrip(device, effect, SpineNodes);
        }

        /// <summary>后侧手臂，画在躯干之前，压在剪影底下（三明治）</summary>
        internal void DrawFarArm(GraphicsDevice device, Effect effect, float opacity, float phase,
            float rimFlash, float seed)
            => DrawLimb(device, effect, farArm, ArmNodes, halfWidth * 0.155f, halfWidth * 0.055f,
                opacity * 0.82f, phase, rimFlash, seed, tipSolid: 1f, fray: 0.92f);

        internal void DrawNearArm(GraphicsDevice device, Effect effect, float opacity, float phase,
            float rimFlash, float seed)
            => DrawLimb(device, effect, nearArm, ArmNodes, halfWidth * 0.175f, halfWidth * 0.060f,
                opacity, phase, rimFlash, seed, tipSolid: 1f, fray: 0.92f);

        private void DrawLimb(GraphicsDevice device, Effect effect, Vector2[] nodes, int count,
            float rootHalf, float tipHalf, float opacity, float phase, float rimFlash, float seed,
            float tipSolid, float fray) {
            if (opacity < 0.02f) {
                return;
            }

            UseTechnique(effect, "TechLimb");
            effect.Parameters["uOpacity"]?.SetValue(MathHelper.Clamp(opacity, 0f, 1f));
            effect.Parameters["uDissolve"]?.SetValue(0f);
            effect.Parameters["uPhase"]?.SetValue(MathHelper.Clamp(phase, 0f, 1f));
            effect.Parameters["uRimFlash"]?.SetValue(MathHelper.Clamp(rimFlash, 0f, 1f));
            effect.Parameters["uTipSolid"]?.SetValue(tipSolid);
            effect.Parameters["uFray"]?.SetValue(fray);
            effect.Parameters["uSeed"]?.SetValue(seed);

            BuildChainStrip(nodes, count, rootHalf, tipHalf, flipU: false, alongV: false);
            DrawStrip(device, effect, count);
        }

        internal static void UseTechnique(Effect effect, string name) {
            EffectTechnique technique = effect.Techniques[name];
            if (technique != null) {
                effect.CurrentTechnique = technique;
            }
        }

        private void DrawStrip(GraphicsDevice device, Effect effect, int count) {
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, stripVertices, 0, (count - 1) * 2);
            }
        }

        /// <summary>
        /// 沿骨架建三角带。alongV=true 是剪影约定（uv.x 横跨、uv.y 沿身，直接喂 Shutter 采样）；
        /// alongV=false 是肢体约定（uv.x 沿肢、uv.y 横跨）。顶点色 R 通道必须留 0，那是骨白量。
        /// </summary>
        private void BuildChainStrip(Vector2[] nodes, int count, float rootHalf, float tipHalf,
            bool flipU, bool alongV) {
            float leftU = flipU ? 1f : 0f;
            float rightU = 1f - leftU;
            for (int i = 0; i < count; i++) {
                float t = i / (count - 1f);
                Vector2 tangent = i == 0
                    ? nodes[1] - nodes[0]
                    : i == count - 1
                        ? nodes[i] - nodes[i - 1]
                        : nodes[i + 1] - nodes[i - 1];
                Vector2 normal = tangent.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
                float half = MathHelper.Lerp(rootHalf, tipHalf, t);
                Vector2 leftUV = alongV ? new Vector2(leftU, t) : new Vector2(t, 0f);
                Vector2 rightUV = alongV ? new Vector2(rightU, t) : new Vector2(t, 1f);
                stripVertices[i * 2] = new VertexPositionColorTexture(
                    (nodes[i] - normal * half).ToVector3(), StripColor, leftUV);
                stripVertices[i * 2 + 1] = new VertexPositionColorTexture(
                    (nodes[i] + normal * half).ToVector3(), StripColor, rightUV);
            }
        }
    }
}
