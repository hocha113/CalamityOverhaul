using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniAnnihilates;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions
{
    /// <summary>墨丝的两种织法，共用同一副载体（照 <see cref="OniMeiGroundBurn"/> 的双风格写法）</summary>
    internal enum OniMeiThreadStyle : byte
    {
        /// <summary>蜘蛛切「墨丝」：三锚闭合成网，收紧时网内全体挨一刀</summary>
        Snare,
        /// <summary>綴樋「缀痕」：墨痕之间连缀成串，收紧时逐段切开</summary>
        Stitch,
    }

    /// <summary>
    /// 墨丝网。丝锚由 <see cref="OnikiriPlayer"/>（蜘蛛切）或墨痕引爆（綴樋）攒齐后一次性交来，
    /// 本弹幕只负责把它们连起来再收紧。<br/>
    /// 三拍读法：垂坠 8 帧（丝松松挂着，看清围住了谁）→ 绷直 12 帧（子丝并拢、转纸白、
    /// 起高频细颤）→ 切开并散毛断掉。禁"一条直线突然出现又消失"。<br/>
    /// Snare 闭环，网内全体各挨一次；Stitch 开链，只切相邻两锚之间那一段
    /// </summary>
    internal class OniMeiInkThread : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>锚位上限，蜘蛛切用 3，缀痕最多用满</summary>
        internal const int MaxAnchors = 6;
        /// <summary>垂坠亮相帧数：先让人看清网围住了谁，再收</summary>
        private const int WeaveFrames = 8;
        /// <summary>收紧动画帧数</summary>
        private const int TightenFrames = 12;
        /// <summary>切开后的散毛断丝帧数</summary>
        private const int FrayFrames = 14;
        /// <summary>切开生效的判定窗（帧）</summary>
        private const int CutWindow = 3;
        /// <summary>丝身参考宽（px），绷紧时着色器自行收窄</summary>
        private const float ThreadWidth = 16f;
        /// <summary>锚记号直径（px）</summary>
        private const float AnchorSize = 34f;
        /// <summary>松弛时每段的垂坠深度占段长比</summary>
        private const float SagRatio = 0.17f;
        /// <summary>丝线折段数（垂坠曲线的分辨率）</summary>
        private const int ThreadSegments = 14;
        /// <summary>沿丝噪声频率的归一参考长</summary>
        private const float ReferenceLength = 420f;
        /// <summary>收紧时整张网向重心缩进的比例，够读出"猛收"又不至于漏掉边上的人</summary>
        private const float ContractRatio = 0.08f;

        private static readonly Vector3 ColorHot = new(1.00f, 0.95f, 0.88f);
        private static readonly Vector3 ColorBright = new(0.86f, 0.14f, 0.12f);
        private static readonly Vector3 ColorDark = new(0.10f, 0.05f, 0.06f);

        private enum Phase : byte
        {
            Weaving,
            Tightening,
            Fraying,
        }

        private OniMeiThreadStyle style;
        private Phase phase = Phase.Weaving;
        private int phaseTimer;
        private bool initialized;
        private float seed;

        private readonly Vector2[] anchorPos = new Vector2[MaxAnchors];
        private int anchorCount;
        private readonly HashSet<int> cutRoots = [];

        /// <summary>0 松弛 → 1 绷直</summary>
        private float Tension => phase switch {
            Phase.Weaving => 0f,
            Phase.Tightening => MathHelper.Clamp(phaseTimer / (float)TightenFrames, 0f, 1f),
            _ => 1f,
        };

        /// <summary>切开那一瞬的过曝脉冲</summary>
        private float SnapPulse => phase == Phase.Fraying
            ? MathHelper.Clamp(1f - phaseTimer / 6f, 0f, 1f)
            : 0f;

        private float FrayAmount => phase == Phase.Fraying
            ? MathHelper.Clamp((phaseTimer - 4f) / (FrayFrames - 4f), 0f, 1f)
            : 0f;

        /// <summary>闭环织法（Snare 三锚以上才成环）</summary>
        private bool Closed => style == OniMeiThreadStyle.Snare && anchorCount >= 3;

        /// <summary>收紧时整张网向重心缩一档；绘制与判定共用同一份坐标，看到哪就切到哪</summary>
        private Vector2 Anchor(int index) {
            if (index < 0 || index >= anchorCount) {
                return Projectile.Center;
            }
            float pull = Tension * ContractRatio;
            return pull <= 0.0001f
                ? anchorPos[index]
                : Vector2.Lerp(anchorPos[index], Centroid, pull);
        }

        private Vector2 Centroid {
            get {
                if (anchorCount <= 0) {
                    return Projectile.Center;
                }
                Vector2 sum = Vector2.Zero;
                for (int i = 0; i < anchorCount; i++) {
                    sum += anchorPos[i];
                }
                return sum / anchorCount;
            }
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.DamageType = CWRRef.GetTrueMeleeNoSpeedDamageClass();
            Projectile.timeLeft = WeaveFrames + TightenFrames + FrayFrames + 4;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>
        /// 用一组已定好的锚点织网。Snare 需三锚起，Stitch 两锚起；
        /// 伤害由调用方按基础刀伤压好。owner 端调用
        /// </summary>
        internal static Projectile Fire(Player player, IReadOnlyList<Vector2> points,
            OniMeiThreadStyle threadStyle, int damage, float knockback, IEntitySource source = null) {
            int need = threadStyle == OniMeiThreadStyle.Snare ? 3 : 2;
            if (player == null || Main.myPlayer != player.whoAmI
                || points == null || points.Count < need) {
                return null;
            }

            Vector2 center = Vector2.Zero;
            int usable = Math.Min(points.Count, MaxAnchors);
            for (int i = 0; i < usable; i++) {
                center += points[i];
            }
            center /= usable;

            Projectile spawned = Projectile.NewProjectileDirect(
                source ?? player.GetSource_Misc("CWR_OniMeiInkThread"), center, Vector2.Zero,
                ModContent.ProjectileType<OniMeiInkThread>(), Math.Max(1, damage), knockback,
                player.whoAmI);
            if (spawned.ModProjectile is not OniMeiInkThread web) {
                spawned.Kill();
                return null;
            }

            web.style = threadStyle;
            for (int i = 0; i < usable; i++) {
                Vector2 point = points[i];
                if (float.IsFinite(point.X) && float.IsFinite(point.Y)) {
                    web.anchorPos[web.anchorCount++] = point;
                }
            }
            if (web.anchorCount < need) {
                spawned.Kill();
                return null;
            }
            web.EnsureInit();
            web.RefreshHitbox();
            spawned.netUpdate = true;
            return spawned;
        }

        //==================== 逐帧 ====================

        private void EnsureInit() {
            if (initialized) {
                return;
            }
            initialized = true;
            seed = Projectile.identity * 0.6180339887f % 1f;
        }

        public override void AI() {
            EnsureInit();
            phaseTimer++;
            RefreshHitbox();

            switch (phase) {
                case Phase.Weaving:
                    if (phaseTimer >= WeaveFrames) {
                        BeginTighten();
                    }
                    break;
                case Phase.Tightening:
                    if (phaseTimer >= TightenFrames) {
                        BeginCut();
                    }
                    break;
                case Phase.Fraying:
                    if (phaseTimer >= FrayFrames) {
                        Projectile.Kill();
                        return;
                    }
                    break;
            }

            if (!Main.dedServ && phase == Phase.Weaving) {
                SpawnDrip();
            }
        }

        /// <summary>碰撞箱收拢到全锚包围盒，Colliding 才有机会被调用</summary>
        private void RefreshHitbox() {
            if (anchorCount <= 0) {
                return;
            }
            Vector2 min = anchorPos[0];
            Vector2 max = anchorPos[0];
            for (int i = 1; i < anchorCount; i++) {
                min = Vector2.Min(min, anchorPos[i]);
                max = Vector2.Max(max, anchorPos[i]);
            }
            //垂坠段会沉到包围盒下方，箱底跟着放一截
            float sagRoom = (max - min).Length() * SagRatio + 32f;
            Vector2 center = (min + max) * 0.5f + Vector2.UnitY * sagRoom * 0.5f;
            Projectile.width = Math.Max(32, (int)(max.X - min.X) + 48);
            Projectile.height = Math.Max(32, (int)(max.Y - min.Y + sagRoom) + 48);
            Projectile.Center = center;
        }

        private void BeginTighten() {
            phase = Phase.Tightening;
            phaseTimer = 0;
            Projectile.netUpdate = true;
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item26 with { Pitch = 0.55f, Volume = 0.30f }, Projectile.Center);
            }
        }

        private void BeginCut() {
            phase = Phase.Fraying;
            phaseTimer = 0;
            Projectile.netUpdate = true;
            PlaySnapCue();
            if (Projectile.IsOwnedByLocalPlayer() && Closed) {
                ApplySnareBind();
            }
        }

        /// <summary>收紧命中：网内主体一并叠「滞缚」，伤害仍走 Colliding 那条正规路</summary>
        private void ApplySnareBind() {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.friendly || !npc.CanBeChasedBy() || !ContainsPoint(npc.Center)) {
                    continue;
                }
                NPC root = OniMeiCombat.ResolveEffectRoot(npc);
                root?.AddBuff(ModContent.BuffType<OniBindDebuff>(), OniMeiCombat.SilkSnareBindTicks);
            }
        }

        //==================== 伤害 ====================

        public override bool? CanDamage()
            => phase == Phase.Fraying && phaseTimer <= CutWindow ? null : false;

        public override bool? CanHitNPC(NPC target) {
            NPC root = OniMeiCombat.ResolveEffectRoot(target);
            return root != null && !cutRoots.Contains(root.whoAmI) ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (phase != Phase.Fraying || phaseTimer > CutWindow || anchorCount < 2) {
                return false;
            }
            //闭合网：整片网面都算切到；开链：只切相邻两锚那一段
            if (Closed) {
                return ContainsRect(targetHitbox);
            }
            for (int i = 0; i + 1 < anchorCount; i++) {
                float cp = 0f;
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    Anchor(i), Anchor(i + 1), ThreadWidth, ref cp)) {
                    return true;
                }
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            NPC root = OniMeiCombat.ResolveEffectRoot(target);
            if (root != null) {
                cutRoots.Add(root.whoAmI);
            }
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(CWRSound.KatanaHitB with { Pitch = 0.45f, Volume = 0.55f }, target.Center);
            CrimsonRendHitVFX.SpawnImpactBurst(target.Center, Vector2.UnitY * -2f, 0.28f, 0.5f,
                CWRLoad.NPCValue.ISTheofSteel(target));
        }

        /// <summary>点是否落在锚多边形内（三锚即三角，多锚按扇形分解）</summary>
        private bool ContainsPoint(Vector2 point) {
            for (int i = 1; i + 1 < anchorCount; i++) {
                if (PointInTriangle(point, Anchor(0), Anchor(i), Anchor(i + 1))) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>碰撞箱心或四角落进网即算；大体型再补一次边线相交</summary>
        private bool ContainsRect(Rectangle box) {
            if (ContainsPoint(box.Center.ToVector2())
                || ContainsPoint(box.TopLeft()) || ContainsPoint(box.TopRight())
                || ContainsPoint(box.BottomLeft()) || ContainsPoint(box.BottomRight())) {
                return true;
            }
            for (int i = 0; i < anchorCount; i++) {
                float cp = 0f;
                if (Collision.CheckAABBvLineCollision(box.TopLeft(), box.Size(),
                    Anchor(i), Anchor((i + 1) % anchorCount), ThreadWidth, ref cp)) {
                    return true;
                }
            }
            return false;
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c) {
            float d1 = Cross(p - a, b - a);
            float d2 = Cross(p - b, c - b);
            float d3 = Cross(p - c, a - c);
            bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNeg && hasPos);
        }

        private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

        //==================== 联机 ====================

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write((byte)style);
            writer.Write((byte)phase);
            writer.Write((short)phaseTimer);
            writer.Write((byte)anchorCount);
            for (int i = 0; i < anchorCount; i++) {
                writer.WriteVector2(anchorPos[i]);
            }
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            byte rawStyle = reader.ReadByte();
            style = rawStyle <= (byte)OniMeiThreadStyle.Stitch
                ? (OniMeiThreadStyle)rawStyle : OniMeiThreadStyle.Snare;
            byte rawPhase = reader.ReadByte();
            phase = rawPhase <= (byte)Phase.Fraying ? (Phase)rawPhase : Phase.Weaving;
            phaseTimer = reader.ReadInt16();
            int count = Math.Clamp((int)reader.ReadByte(), 0, MaxAnchors);
            anchorCount = 0;
            for (int i = 0; i < count; i++) {
                Vector2 pos = reader.ReadVector2();
                if (float.IsFinite(pos.X) && float.IsFinite(pos.Y)) {
                    anchorPos[anchorCount++] = pos;
                }
            }
            EnsureInit();
            RefreshHitbox();
        }

        //==================== 演出 ====================

        private void PlaySnapCue() {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.85f, Volume = 0.55f }, Projectile.Center);
            SoundEngine.PlaySound(CWRSound.KatanaSwing with { Pitch = 0.90f, Volume = 0.42f }, Projectile.Center);
            Main.player[Projectile.owner].CWR()?.GetScreenShake(2.2f);
            //断口：沿每段丝崩一排纸白细屑，方向垂直于丝，读作"绷断"而不是"消失"
            int segments = Closed ? anchorCount : anchorCount - 1;
            for (int i = 0; i < segments; i++) {
                Vector2 a = Anchor(i);
                Vector2 b = Anchor((i + 1) % anchorCount);
                Vector2 along = (b - a).SafeNormalize(Vector2.UnitX);
                Vector2 perp = along.RotatedBy(MathHelper.PiOver2);
                for (int k = 0; k < 5; k++) {
                    Vector2 at = Vector2.Lerp(a, b, Main.rand.NextFloat(0.15f, 0.85f));
                    PRTLoader.NewParticle<PRT_CrimsonSpark>(at,
                        perp * Main.rand.NextFloat(-4.5f, 4.5f) + along * Main.rand.NextFloat(-1.2f, 1.2f),
                        new Color(255, 240, 226), Main.rand.NextFloat(0.16f, 0.30f))
                        ?.Configure(Main.rand.Next(10, 18), affectedByGravity: false);
                }
            }
        }

        /// <summary>垂坠期：丝上挂着的墨往下滴，读作这网是湿的</summary>
        private void SpawnDrip() {
            if (phaseTimer % 3 != 0 || anchorCount < 2) {
                return;
            }
            int i = Main.rand.Next(Closed ? anchorCount : anchorCount - 1);
            Vector2 a = Anchor(i);
            Vector2 b = Anchor((i + 1) % anchorCount);
            float t = Main.rand.NextFloat(0.25f, 0.75f);
            float sag = Vector2.Distance(a, b) * SagRatio;
            Vector2 at = Vector2.Lerp(a, b, t) + Vector2.UnitY * (4f * t * (1f - t) * sag);
            PRTLoader.NewParticle<PRT_OniInkDrop>(at,
                Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.4f),
                new Color(30, 14, 18), Main.rand.NextFloat(0.10f, 0.18f))
                ?.Configure(Main.rand.Next(18, 28));
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || !initialized || anchorCount <= 0) {
                return;
            }
            Effect fx = EffectLoader.OniInkThread?.Value;
            Texture2D noise = CWRAsset.NoiseSoft01?.Value;
            if (fx == null || noise == null) {
                return;
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            DepthStencilState prevDepth = device.DepthStencilState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;

            float tension = Tension;
            float opacity = phase == Phase.Fraying
                ? MathHelper.Clamp(1f - phaseTimer / (float)FrayFrames, 0f, 1f)
                : MathHelper.Clamp(phaseTimer / 4f, 0f, 1f);

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uTension"]?.SetValue(tension);
            fx.Parameters["uSnap"]?.SetValue(SnapPulse);
            fx.Parameters["uFray"]?.SetValue(FrayAmount);
            fx.Parameters["uOpacity"]?.SetValue(opacity);
            fx.Parameters["uNoiseTex"]?.SetValue(noise);
            fx.Parameters["uColHot"]?.SetValue(ColorHot);
            fx.Parameters["uColBright"]?.SetValue(ColorBright);
            fx.Parameters["uColDark"]?.SetValue(ColorDark);

            int segments = Closed ? anchorCount : anchorCount - 1;
            fx.CurrentTechnique = fx.Techniques["ThreadTech"];
            for (int i = 0; i < segments; i++) {
                fx.Parameters["uSeed"]?.SetValue((seed + i * 0.137f) % 1f);
                DrawThread(device, fx, Anchor(i), Anchor((i + 1) % anchorCount), tension);
            }

            fx.CurrentTechnique = fx.Techniques["AnchorTech"];
            for (int i = 0; i < anchorCount; i++) {
                DrawAnchor(device, fx, Anchor(i), i);
            }

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
            device.DepthStencilState = prevDepth;
        }

        /// <summary>一段丝：按悬链下垂折成带状条，绷紧时垂坠归零</summary>
        private void DrawThread(GraphicsDevice device, Effect fx, Vector2 a, Vector2 b, float tension) {
            Vector2 delta = b - a;
            float length = delta.Length();
            if (length < 8f) {
                return;
            }
            fx.Parameters["uLengthScale"]?.SetValue(MathHelper.Clamp(length / ReferenceLength, 0.35f, 3f));

            float sag = length * SagRatio * (1f - tension);
            float width = ThreadWidth * MathHelper.Lerp(1f, 0.62f, tension);
            Vector2 screen = -Main.screenPosition;

            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[(ThreadSegments + 1) * 2];
            for (int i = 0; i <= ThreadSegments; i++) {
                float t = i / (float)ThreadSegments;
                Vector2 point = SagPoint(a, b, t, sag);
                //切线取自相邻采样，带宽垂直于真实走向而非弦向
                Vector2 prev = SagPoint(a, b, Math.Max(0f, t - 0.02f), sag);
                Vector2 next = SagPoint(a, b, Math.Min(1f, t + 0.02f), sag);
                Vector2 perp = (next - prev).SafeNormalize(Vector2.UnitX)
                    .RotatedBy(MathHelper.PiOver2) * width * 0.5f;
                verts[i * 2] = new VertexPositionColorTexture(
                    (point - perp + screen).ToVector3(), Color.White, new Vector2(t, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture(
                    (point + perp + screen).ToVector3(), Color.White, new Vector2(t, 1f));
            }

            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, ThreadSegments * 2);
            }
        }

        /// <summary>抛物线近似悬链：两端归零，中段最深，重力方向恒向下</summary>
        private static Vector2 SagPoint(Vector2 a, Vector2 b, float t, float sag)
            => Vector2.Lerp(a, b, t) + Vector2.UnitY * (4f * t * (1f - t) * sag);

        private void DrawAnchor(GraphicsDevice device, Effect fx, Vector2 at, int index) {
            fx.Parameters["uSeed"]?.SetValue((seed + index * 0.267f) % 1f);
            float half = AnchorSize * 0.5f * MathHelper.Lerp(1f, 1.18f, Tension);
            Vector2 screen = at - Main.screenPosition;
            VertexPositionColorTexture[] verts = [
                new((screen + new Vector2(-half, -half)).ToVector3(), Color.White, new Vector2(0f, 0f)),
                new((screen + new Vector2(half, -half)).ToVector3(), Color.White, new Vector2(1f, 0f)),
                new((screen + new Vector2(-half, half)).ToVector3(), Color.White, new Vector2(0f, 1f)),
                new((screen + new Vector2(half, half)).ToVector3(), Color.White, new Vector2(1f, 1f)),
            ];
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }
        }
    }
}
