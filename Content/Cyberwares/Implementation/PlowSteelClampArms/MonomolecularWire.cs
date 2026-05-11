using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.PlowSteelClampArms
{
    /// <summary>
    /// 犁钢钳臂技能产生的高热单分子线弹幕
    /// <br/>本质是一段从 <see cref="OwnerCenter"/> 到 <see cref="AnchorWorld"/> 的可视/可命中线段
    /// <list type="bullet">
    ///   <item>每帧重算线段几何，按 <see cref="PlowSteelClampArm.WireHitCooldown"/> 周期性对路径上的敌怪结算灼烧伤害</item>
    ///   <item>线段上不断产生火花/发光粒子，模拟"灼热钨丝"质感</item>
    ///   <item>没有击退，纯粹的持续性伤害判定，便于配合走位拉扯敌怪</item>
    /// </list>
    /// 两种形态：
    /// <list type="bullet">
    ///   <item><b>动态/长线模式</b>（<see cref="IsStatic"/> 为 false）：from 端始终跟随玩家，
    ///         to 端钉在 <see cref="AnchorWorld"/>，是经典的"高刚性钳臂连接"形态</item>
    ///   <item><b>静态/短线模式</b>（<see cref="IsStatic"/> 为 true）：from 与 to 均冻结于发射瞬间，
    ///         形成空中绊线，玩家移动不影响线段位置；用于无锚点的随手布线</item>
    /// </list>
    /// 多人模式下锚点位置通过 ai[0]/ai[1] 同步，静态 from 端通过 SendExtraAI 同步
    /// </summary>
    [Autoload(true)]
    internal class MonomolecularWire : ModProjectile
    {
        public const int MaxLifetime = PlowSteelClampArm.WireLifetime;

        public override string Texture => CWRConstant.Placeholder;

        /// <summary>
        /// 锚点世界坐标（线段的 to 端），由生成时写入 ai[0] / ai[1]
        /// </summary>
        public Vector2 AnchorWorld {
            get => new(Projectile.ai[0], Projectile.ai[1]);
            set {
                Projectile.ai[0] = value.X;
                Projectile.ai[1] = value.Y;
            }
        }

        /// <summary>
        /// 静态模式开关：
        /// <list type="bullet">
        ///   <item>true：from / to 都冻结，玩家移动不影响线段（"短线/绊线"）</item>
        ///   <item>false：from 跟随玩家中心，to 固定在 <see cref="AnchorWorld"/>（"长线/钳臂"）</item>
        /// </list>
        /// </summary>
        public bool IsStatic {
            get => Projectile.ai[2] > 0.5f;
            set => Projectile.ai[2] = value ? 1f : 0f;
        }

        /// <summary>
        /// 静态模式下的 from 端世界坐标快照
        /// <br/>动态模式下此字段不参与计算，直接使用 <see cref="Player.Center"/>
        /// </summary>
        public Vector2 StaticFromWorld { get; set; }

        /// <summary>
        /// 拥有者中心坐标缓存：动态模式下 = 玩家中心，静态模式下 = <see cref="StaticFromWorld"/>
        /// </summary>
        public Vector2 OwnerCenter { get; private set; }

        /// <summary>
        /// 视觉/音效用的脉冲计时器，与实际伤害无关
        /// </summary>
        private int pulseTimer;

        /// <summary>
        /// 用于绘制时计算扫光位置的全局时间
        /// </summary>
        private float visualTimer;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            //极小的判定盒，真正的命中通过 Colliding 自定义实现
            Projectile.width = 4;
            Projectile.height = 4;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxLifetime;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = PlowSteelClampArm.WireHitCooldown;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.netImportant = true;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (owner == null || !owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            //装备被卸下立即销毁，防止形成"装备外的持续输出"
            if (PlowSteelClampArm.GetEquipped(owner) == null) {
                Projectile.Kill();
                return;
            }

            if (IsStatic) {
                //静态模式：from 端固定在 StaticFromWorld，无需做距离/跟随处理
                //首帧（接收端可能 StaticFromWorld 尚未到达）兜底：先用玩家中心，等 ExtraAI 到来后再覆盖
                if (StaticFromWorld == Vector2.Zero) {
                    StaticFromWorld = owner.Center;
                }
                OwnerCenter = StaticFromWorld;
            }
            else {
                //动态模式：from 端跟随玩家；锚点过远立即断线（玩家逃跑或被击退）
                if (Vector2.DistanceSquared(owner.Center, AnchorWorld)
                    > (PlowSteelClampArm.MaxAnchorDistance * 1.4f) * (PlowSteelClampArm.MaxAnchorDistance * 1.4f)) {
                    Projectile.Kill();
                    return;
                }
                OwnerCenter = owner.Center;
            }
            //把弹幕中心固定在线段中点，便于原版的若干位置依赖逻辑（声音定位等）
            Projectile.Center = (OwnerCenter + AnchorWorld) * 0.5f;

            visualTimer += 1f / 60f;

            //pulseTimer 仅控制视觉脉冲与音效节奏，伤害节奏由 idStaticNPCHitCooldown 自然控制
            pulseTimer++;
            if (pulseTimer >= PlowSteelClampArm.WireHitCooldown) {
                pulseTimer = 0;
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.4f, Volume = 0.4f, MaxInstances = 2 }, Projectile.Center);
            }

            SpawnLineParticles();

            //接近寿命末尾时附加淡出粒子
            if (Projectile.timeLeft < 30) {
                if (Projectile.timeLeft % 3 == 0) {
                    SpawnFadeParticles();
                }
            }
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>
        /// 自定义命中：基于线段-AABB 的最短距离判定，让贯穿线段路径上的所有敌怪都能受影响
        /// 实际的命中频率由 idStaticNPCHitCooldown 控制，玩家穿过线段会被自然地按周期烧灼
        /// </summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //展开 AABB 后的线段最短距离 < 8 视为命中
            float dist = SegmentRectDistance(OwnerCenter, AnchorWorld, targetHitbox);
            if (dist <= 8f) {
                return true;
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //附加灼烧 buff，强化"高热"语义
            target.AddBuff(BuffID.OnFire3, PlowSteelClampArm.WireHitCooldown + 30);
            //命中点散开火花
            Vector2 hitPoint = ClosestPointOnSegment(OwnerCenter, AnchorWorld, target.Center);
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(3.5f, 3.5f);
                Dust dust = Dust.NewDustPerfect(hitPoint, DustID.MartianSaucerSpark, vel, 100, default, 1.3f);
                dust.noGravity = true;
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //单分子线没有击退，避免连续脉冲对小怪造成不合理的位移
            modifiers.Knockback *= 0f;
        }

        public override void OnKill(int timeLeft) {
            //断线时沿全程播散粒子，给出明显的"线段消散"反馈
            int steps = Math.Max(1, (int)(Vector2.Distance(OwnerCenter, AnchorWorld) / 12f));
            for (int i = 0; i <= steps; i++) {
                float t = (float)i / steps;
                Vector2 pos = Vector2.Lerp(OwnerCenter, AnchorWorld, t);
                Vector2 vel = Main.rand.NextVector2Circular(2.5f, 2.5f);
                Dust dust = Dust.NewDustPerfect(pos, DustID.Torch, vel, 100, default, 1.2f);
                dust.noGravity = true;
            }
            SoundEngine.PlaySound(SoundID.Item56 with { Pitch = 0.2f, Volume = 0.45f }, Projectile.Center);
        }

        /// <summary>
        /// 沿线段每帧少量产生火花/发光粒子，强度随剩余时间衰减
        /// </summary>
        private void SpawnLineParticles() {
            float lifeFactor = MathHelper.Clamp((float)Projectile.timeLeft / MaxLifetime, 0f, 1f);
            //长度越长粒子越多，但有上限避免巨量粒子
            float distance = Vector2.Distance(OwnerCenter, AnchorWorld);
            int count = Math.Min(8, 1 + (int)(distance / 64f));
            for (int i = 0; i < count; i++) {
                if (Main.rand.NextFloat() > 0.65f * lifeFactor) {
                    continue;
                }
                float t = Main.rand.NextFloat();
                Vector2 pos = Vector2.Lerp(OwnerCenter, AnchorWorld, t);
                //法向小偏移
                Vector2 dir = (AnchorWorld - OwnerCenter).SafeNormalize(Vector2.UnitX);
                Vector2 normal = new(-dir.Y, dir.X);
                pos += normal * Main.rand.NextFloat(-1.5f, 1.5f);

                Vector2 vel = normal * Main.rand.NextFloat(-1.4f, 1.4f);
                Dust dust = Dust.NewDustPerfect(pos, DustID.Torch, vel, 100, default, 1.0f + lifeFactor * 0.4f);
                dust.noGravity = true;
            }
        }

        /// <summary>
        /// 寿命末尾的淡出散点
        /// </summary>
        private void SpawnFadeParticles() {
            for (int i = 0; i < 4; i++) {
                float t = Main.rand.NextFloat();
                Vector2 pos = Vector2.Lerp(OwnerCenter, AnchorWorld, t);
                Vector2 vel = Main.rand.NextVector2Circular(1.6f, 1.6f);
                Dust dust = Dust.NewDustPerfect(pos, DustID.Smoke, vel, 130, default, 1.1f);
                dust.noGravity = false;
            }
        }

        /// <summary>
        /// 多层带光晕的线段绘制：底层柔光 + 主体橙色 + 内核高亮
        /// </summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D px = TextureAssets.MagicPixel.Value;
            if (px == null) {
                return false;
            }

            Vector2 from = OwnerCenter - Main.screenPosition;
            Vector2 to = AnchorWorld - Main.screenPosition;
            Vector2 diff = to - from;
            float length = diff.Length();
            if (length < 1f) {
                return false;
            }
            float rotation = diff.ToRotation();

            //寿命衰减：剩余时间越短整体越淡
            float lifeFactor = MathHelper.Clamp((float)Projectile.timeLeft / MaxLifetime, 0f, 1f);
            //节奏闪烁：每次脉冲计时器越接近触发时整体越亮
            float pulse = MathHelper.Clamp((float)pulseTimer / PlowSteelClampArm.WireHitCooldown, 0f, 1f);
            float globalAlpha = MathHelper.Lerp(0.55f, 1f, lifeFactor) * (0.85f + 0.15f * pulse);

            Color glow = new Color(255, 130, 60, 0) * (0.45f * globalAlpha);
            Color body = new Color(255, 200, 90, 0) * (0.95f * globalAlpha);
            Color core = new Color(255, 250, 220, 0) * globalAlpha;

            DrawLineRaw(px, from, length, rotation, 7f, glow);
            DrawLineRaw(px, from, length, rotation, 3.4f, body);
            DrawLineRaw(px, from, length, rotation, 1.4f, core);

            //扫描点：沿线段前进的明亮高光，强化"高频脉冲"质感
            float scanT = (visualTimer * 0.55f) % 1f;
            Vector2 scanPos = from + diff * scanT;
            DrawDot(px, scanPos, 6f, new Color(255, 220, 140, 0) * (0.6f * globalAlpha));
            DrawDot(px, scanPos, 3f, new Color(255, 250, 220, 0) * globalAlpha);

            //两端锚点高亮
            DrawDot(px, from, 5f, body);
            DrawDot(px, to, 5f, body);
            DrawDot(px, to, 9f, glow);
            return false;
        }

        private static void DrawLineRaw(Texture2D px, Vector2 from, float length, float rotation,
            float thickness, Color color) {
            Main.spriteBatch.Draw(px, from, new Rectangle(0, 0, 1, 1), color, rotation,
                new Vector2(0f, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);
        }

        private static void DrawDot(Texture2D px, Vector2 pos, float size, Color color) {
            int sz = Math.Max(1, (int)MathF.Round(size));
            Rectangle dst = new((int)(pos.X - sz * 0.5f), (int)(pos.Y - sz * 0.5f), sz, sz);
            Main.spriteBatch.Draw(px, dst, color);
        }

        #region 几何工具

        /// <summary>
        /// 求点 P 到线段 [A, B] 的最近点
        /// </summary>
        public static Vector2 ClosestPointOnSegment(Vector2 a, Vector2 b, Vector2 p) {
            Vector2 ab = b - a;
            float lenSq = ab.LengthSquared();
            if (lenSq < 0.0001f) {
                return a;
            }
            float t = MathHelper.Clamp(Vector2.Dot(p - a, ab) / lenSq, 0f, 1f);
            return a + ab * t;
        }

        /// <summary>
        /// 求线段 [A, B] 到指定 AABB 的最短距离（粗略而稳健）
        /// 通过将矩形按 6 个采样点映射到线段最近点，取最小距离
        /// </summary>
        public static float SegmentRectDistance(Vector2 a, Vector2 b, Rectangle rect) {
            //取矩形的 4 个顶点 + 2 个对角中点作为采样
            Vector2 tl = new(rect.Left, rect.Top);
            Vector2 tr = new(rect.Right, rect.Top);
            Vector2 bl = new(rect.Left, rect.Bottom);
            Vector2 br = new(rect.Right, rect.Bottom);
            Vector2 c1 = (tl + br) * 0.5f;
            Vector2 c2 = (tr + bl) * 0.5f;

            float min = float.MaxValue;
            min = MathF.Min(min, Vector2.Distance(c1, ClosestPointOnSegment(a, b, c1)));
            min = MathF.Min(min, Vector2.Distance(c2, ClosestPointOnSegment(a, b, c2)));
            min = MathF.Min(min, Vector2.Distance(tl, ClosestPointOnSegment(a, b, tl)));
            min = MathF.Min(min, Vector2.Distance(tr, ClosestPointOnSegment(a, b, tr)));
            min = MathF.Min(min, Vector2.Distance(bl, ClosestPointOnSegment(a, b, bl)));
            min = MathF.Min(min, Vector2.Distance(br, ClosestPointOnSegment(a, b, br)));
            return min;
        }

        #endregion

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(pulseTimer);
            //仅在静态模式下同步 from 端，避免对动态模式做无谓的带宽消耗
            if (IsStatic) {
                writer.Write(StaticFromWorld.X);
                writer.Write(StaticFromWorld.Y);
            }
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            pulseTimer = reader.ReadInt32();
            if (IsStatic) {
                float x = reader.ReadSingle();
                float y = reader.ReadSingle();
                StaticFromWorld = new Vector2(x, y);
            }
        }
    }
}
