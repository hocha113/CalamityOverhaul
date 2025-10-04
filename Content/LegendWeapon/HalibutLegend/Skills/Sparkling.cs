using CalamityMod.Enums;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Skills
{
    internal static class Sparkling
    {
        public static int ID = 1;

        private static int _sparklingVolleyIdSeed = 0;

        internal const float RoingArc = 160f;
        internal const int DepartureDelay = 90;//全部发射后延迟进入离场
        internal const int DepartureDuration = 90;//离场动画时长

        internal static void TryTriggerSparklingVolley(Item item, Player player, HalibutPlayer hp) {
            if (hp.SparklingVolleyActive) return;
            if (hp.SparklingVolleyCooldown > 0) return;

            hp.SparklingDeparturePhase = false;
            hp.SparklingDepartureTimer = 0;

            hp.SparklingVolleyActive = true;
            hp.SparklingVolleyTimer = 0;
            hp.SparklingFishCount = 13; // 13条鱼
            hp.SparklingNextFireIndex = 0;
            hp.SparklingVolleyId = _sparklingVolleyIdSeed++;
            hp.SparklingVolleyCooldown = HalibutPlayer.SparklingBaseCooldown; // 设置基础冷却

            Vector2 aimDir = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
            Vector2 behind = (-aimDir).SafeNormalize(Vector2.UnitX);
            float arc = MathHelper.ToRadians(RoingArc); // 扇形总角度
            float radius = 90f;
            ShootState shootState = player.GetShootState();
            for (int i = 0; i < hp.SparklingFishCount; i++) {
                float t = (hp.SparklingFishCount == 1) ? 0.5f : i / (float)(hp.SparklingFishCount - 1);
                float angOff = (t - 0.5f) * arc;
                Vector2 offsetDir = behind.RotatedBy(angOff);
                Vector2 spawnPos = player.Center + offsetDir * radius + new Vector2(0, (float)Math.Sin(Main.GameUpdateCount * 0.05f + i) * 6f);
                int proj = Projectile.NewProjectile(player.GetSource_ItemUse(item), spawnPos, Vector2.Zero,
                    ModContent.ProjectileType<SparklingFishHolder>(), shootState.WeaponDamage, shootState.WeaponKnockback, player.whoAmI,
                    ai0: hp.SparklingVolleyId, ai1: i);
                if (Main.projectile[proj].ModProjectile is SparklingFishHolder holder) holder.Owner = player;
            }
            SoundEngine.PlaySound(SoundID.Item92 with { Pitch = -0.4f }, player.Center); // 预热音
        }
    }

    /// <summary>
    /// 单条闪光皇后鱼的承载弹幕，静止环绕并按顺序发射激光
    /// </summary>
    internal class SparklingFishHolder : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Item + "Fishing/SunkenSeaCatches/SparklingEmpress";
        public Player Owner;
        private bool fired;
        private const int PreFireDelay = 16; // 鱼出现后到可能开火的最小延迟

        private ref float VolleyId => ref Projectile.ai[0]; // 齐射id
        private ref float FishIndex => ref Projectile.ai[1]; // 在该齐射中的序号

        private float glowPulse;
        private float fadeOut;

        public override void SetDefaults() {
            Projectile.width = 40; Projectile.height = 40;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 600; // 容错
            Projectile.friendly = false;
            Projectile.hostile = false;
        }

        public override void AI() {
            if (Owner == null || !Owner.active) { Projectile.Kill(); return; }
            var hp = Owner.GetOverride<HalibutPlayer>();
            if (hp.SparklingVolleyId != (int)VolleyId) { Projectile.Kill(); return; }

            glowPulse = (float)Math.Sin(Main.GameUpdateCount * 0.25f + FishIndex) * 0.5f + 0.5f;

            if (!hp.SparklingDeparturePhase) {
                Vector2 aimDir = (Main.MouseWorld - Owner.Center).SafeNormalize(Vector2.UnitX);
                Vector2 behind = (-aimDir).SafeNormalize(Vector2.UnitX);
                float arc = MathHelper.ToRadians(Sparkling.RoingArc);
                float radius = 190f;
                float t = (hp.SparklingFishCount <= 1) ? 0.5f : FishIndex / (hp.SparklingFishCount - 1);
                float angOff = (t - 0.5f) * arc;
                Vector2 offsetDir = behind.RotatedBy(angOff);
                Vector2 basePos = Owner.Center + offsetDir * radius;
                float bob = (float)Math.Sin(Main.GameUpdateCount * 0.08f + FishIndex) * 6f;
                Projectile.Center = Vector2.Lerp(Projectile.Center, basePos + new Vector2(0, bob), 0.25f);
                Projectile.rotation = Projectile.To(Main.MouseWorld).ToRotation();

                // 逐条依次发射：依据玩家的SparklingVolleyTimer和索引
                int fireInterval = 14; // 两条鱼间隔
                int startFireTime = PreFireDelay + (int)FishIndex * fireInterval;
                if (!fired && hp.SparklingVolleyTimer >= startFireTime) {
                    FireLaser(hp);
                    fired = true;
                    hp.SparklingNextFireIndex++;
                }
                if (hp.SparklingNextFireIndex == hp.SparklingFishCount
                        && Owner.ownedProjectileCounts[ModContent.ProjectileType<SparklingRay>()] == 0) {
                    // 所有鱼已开火，进入离场延迟等待
                    hp.SparklingDeparturePhase = true;
                    hp.SparklingDepartureTimer = 0;
                }
            }
            else {
                // 离场阶段：先等待，再整体向屏幕外飞行并淡出
                hp.SparklingDepartureTimer++;
                if (hp.SparklingDepartureTimer < Sparkling.DepartureDelay) {
                    // 原地轻微旋转漂浮
                    Projectile.rotation += 0.02f * (FishIndex % 2 == 0 ? 1 : -1);
                }
                else {
                    int flyTime = hp.SparklingDepartureTimer - Sparkling.DepartureDelay;
                    // 平滑加速 0-1
                    float accelProgress = MathHelper.Clamp(flyTime / (float)Sparkling.DepartureDuration, 0f, 1f);
                    accelProgress = MathF.Pow(accelProgress, 0.65f);

                    // 计算目标离开距离：使用屏幕对角尺寸放大，确保真正飞出屏幕再消失
                    float diag = MathF.Sqrt(Main.screenWidth * Main.screenWidth + Main.screenHeight * Main.screenHeight);
                    float exitDistance = diag * 1.4f; // 1.4 倍对角线

                    // 计算外向方向（保持原相对朝向），若与玩家重合则使用玩家朝向
                    Vector2 outward = (Projectile.Center - Owner.Center);
                    if (outward.LengthSquared() < 4f)
                        outward = (Projectile.Center - Main.MouseWorld);
                    outward = outward.SafeNormalize(Vector2.UnitY);

                    // 当前帧速度（前期更慢，后期加速），再叠加一点随机脉动
                    float baseSpeed = MathHelper.Lerp(6f, 32f, accelProgress);
                    baseSpeed *= 1f + 0.15f * (float)Math.Sin(flyTime * 0.18f + FishIndex);

                    Vector2 move = outward * baseSpeed;
                    Projectile.Center += move;

                    // 使用 localAI[0] 记录累计位移
                    Projectile.localAI[0] += move.Length();

                    // 基于行进距离淡出（后半段才开始明显淡出）
                    float distProgress = MathHelper.Clamp(Projectile.localAI[0] / exitDistance, 0f, 1f);
                    fadeOut = MathHelper.Clamp((distProgress - 0.55f) / 0.45f, 0f, 1f); // 55% 距离后开始淡

                    // 判定是否离开屏幕区域（加 margin 做缓冲）
                    Rectangle safeBounds = new((int)Main.screenPosition.X - 180, (int)Main.screenPosition.Y - 180,
                        Main.screenWidth + 360, Main.screenHeight + 360);
                    if (!safeBounds.Contains(Projectile.Center.ToPoint()) && (fadeOut > 0.6f || distProgress >= 0.98f)) {
                        Projectile.Kill();
                    }
                }
            }
            Projectile.spriteDirection = Projectile.rotation.ToRotationVector2().X > 0 ? 1 : -1;
        }

        private void FireLaser(HalibutPlayer hp) {
            SoundEngine.PlaySound(SoundID.Item33 with { Pitch = 0.3f, Volume = 0.8f }, Projectile.Center);
            Vector2 dir = (Main.MouseWorld - Projectile.Center).SafeNormalize(Vector2.UnitX);
            int beam = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + dir * 10f, dir * 0.1f,
                ModContent.ProjectileType<SparklingRay>(), Projectile.damage, 1f, Projectile.owner, Projectile.identity);
            if (Main.projectile.IndexInRange(beam)) {
                Main.projectile[beam].rotation = dir.ToRotation();
                Main.projectile[beam].localAI[0] = 0;
                Main.projectile[beam].localAI[1] = FishIndex; // 传递颜色层次
            }
            // 发射光尘
            for (int i = 0; i < 12; i++) {
                Vector2 v = dir.RotatedByRandom(0.35f) * Main.rand.NextFloat(4f, 9f);
                var d = Dust.NewDustPerfect(Projectile.Center + dir * 16f, DustID.GemAmethyst, v, 150, default, Main.rand.NextFloat(1f, 1.4f));
                d.noGravity = true;
                d.color = Color.Lerp(Color.DeepSkyBlue, Color.HotPink, Main.rand.NextFloat());
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;//获取鱼的纹理

            // 计算绘制参数
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Rectangle sourceRect = value.Frame();
            Vector2 origin = sourceRect.Size() / 2f;
            float drawRotation = Projectile.rotation + MathHelper.PiOver4;
            float pulseScale = 1f + glowPulse * 0.15f;
            float opacity = 1f - fadeOut;
            Color baseCol = Color.Lerp(Color.DeepSkyBlue, Color.HotPink, 0.4f + 0.3f * glowPulse);
            baseCol *= opacity;
            Main.spriteBatch.Draw(value, drawPosition, sourceRect, baseCol * 0.6f, drawRotation, origin, pulseScale * 1.25f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(value, drawPosition, sourceRect, Color.White * opacity, drawRotation, origin, pulseScale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 友方版本的激光
    /// </summary>
    internal class SparklingRay : ModProjectile
    {
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> MaskLaserLine = null;
        public override string Texture => CWRConstant.Placeholder;
        private readonly Vector2[] top = new Vector2[70];
        private readonly Vector2[] bot = new Vector2[70];
        private Vector2 topEnd, botEnd;
        private Color gradientStart = new(255, 170, 230);
        private Color gradientMid = new(160, 200, 255);
        private Color gradientEnd = new(90, 140, 255);
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 10;
            Projectile.timeLeft = 40;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
        }
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float p = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, Projectile.Center + Projectile.rotation.ToRotationVector2() * 2400, 120, ref p);
        }
        public override void AI() {
            if (Projectile.ai[0].TryGetProjectile(out var projectile)) {
                Projectile.Center = projectile.Center;
            }
            float fishIndex = Projectile.localAI[1];
            float hueOffset = (fishIndex % 7) / 7f; // 简单的层次调色
            gradientStart = Color.Lerp(new Color(255, 180, 240), new Color(240, 120, 210), hueOffset);
            gradientMid = Color.Lerp(new Color(180, 210, 255), new Color(120, 170, 255), hueOffset);
            gradientEnd = Color.Lerp(new Color(100, 160, 255), new Color(70, 110, 200), hueOffset);

            for (int i = 0; i < 70; i++) {
                float x = i * 15f;
                float y = 8f * (0.08f * Projectile.localAI[0]) * (float)Math.Pow(0.1f * x, 0.45);
                top[i] = new Vector2(x, y);
                bot[i] = new Vector2(x, -y);
            }
            float endX = 300 * 15f;
            float endY = 8f * (0.08f * Projectile.localAI[0]) * (float)Math.Pow(0.1f * 70 * 15, 0.45);
            topEnd = new Vector2(endX, endY);
            botEnd = new Vector2(endX, -endY);
            if (Projectile.localAI[0] <= 5 && Projectile.timeLeft > 10)
                Projectile.localAI[0] += 30f; // 更快展开
            if (Projectile.timeLeft <= 20 && Projectile.localAI[0] > 0) Projectile.localAI[0] -= 20f;
            if (Projectile.localAI[0] < 0) Projectile.localAI[0] = 0;

            // 核心光粒
            if (Main.rand.NextBool(3)) {
                Vector2 corePos = Projectile.Center + Projectile.rotation.ToRotationVector2() * Main.rand.NextFloat(40f, 400f);
                var d = Dust.NewDustPerfect(corePos, DustID.GemDiamond, Vector2.Zero, 100, Color.White, Main.rand.NextFloat(0.6f, 1.1f));
                d.noGravity = true;
            }
            if (Main.rand.NextBool(2)) {
                Vector2 edgePos = Projectile.Center + Projectile.rotation.ToRotationVector2() * Main.rand.NextFloat(20f, 800f) + Main.rand.NextVector2Circular(60f, 30f);
                var d2 = Dust.NewDustPerfect(edgePos, DustID.GemSapphire, Vector2.Zero, 150, Color.Lerp(Color.DeepSkyBlue, Color.HotPink, 0.5f), Main.rand.NextFloat(0.5f, 0.9f));
                d2.noGravity = true;
            }
        }
        public override bool PreDraw(ref Color lightColor) {
            List<ColoredVertex> vertices = new();
            for (int i = 0; i < 70; i++) {
                float u = i / 70f;
                Color colA = Color.Lerp(gradientStart, gradientMid, u);
                Color colB = Color.Lerp(gradientMid, gradientEnd, u);
                vertices.Add(new ColoredVertex(top[i].RotatedBy(Projectile.rotation) + Projectile.Center - Main.screenPosition, colA, new Vector3(u, 0, 1 - u)));
                vertices.Add(new ColoredVertex(bot[i].RotatedBy(Projectile.rotation) + Projectile.Center - Main.screenPosition, colB, new Vector3(u, 1, 1 - u)));
            }
            vertices.Add(new ColoredVertex(topEnd.RotatedBy(Projectile.rotation) + Projectile.Center - Main.screenPosition, gradientEnd, new Vector3(1, 0, 1)));
            vertices.Add(new ColoredVertex(botEnd.RotatedBy(Projectile.rotation) + Projectile.Center - Main.screenPosition, gradientEnd, new Vector3(1, 1, 1)));
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.AnisotropicClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            Main.graphics.GraphicsDevice.Textures[0] = MaskLaserLine.Value;
            if (vertices.Count >= 3) {
                Main.graphics.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertices.ToArray(), 0, vertices.Count - 2);
            }
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}
