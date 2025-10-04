using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Skills
{
    internal static class Sparkling
    {
        public static int ID = 1;
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
        private float localTime;

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
            localTime++;
            if (Owner == null || !Owner.active) { Projectile.Kill(); return; }
            var hp = Owner.GetOverride<HalibutPlayer>();
            if (!hp.SparklingVolleyActive || hp.SparklingVolleyId != (int)VolleyId) {
                Projectile.Kill();
                return;
            }
            // 动态重新定位到玩家后方弧线上（保持结构稳定）
            Vector2 aimDir = (Main.MouseWorld - Owner.Center).SafeNormalize(Vector2.UnitX);
            Vector2 behind = (-aimDir).SafeNormalize(Vector2.UnitX);
            float arc = MathHelper.ToRadians(70f);
            float radius = 90f;
            float t = (hp.SparklingFishCount <= 1) ? 0.5f : FishIndex / (float)(hp.SparklingFishCount - 1);
            float angOff = (t - 0.5f) * arc;
            Vector2 offsetDir = behind.RotatedBy(angOff);
            Vector2 basePos = Owner.Center + offsetDir * radius;
            float bob = (float)Math.Sin(Main.GameUpdateCount * 0.08f + FishIndex) * 6f;
            Projectile.Center = Vector2.Lerp(Projectile.Center, basePos + new Vector2(0, bob), 0.25f);
            Projectile.rotation = (Owner.Center - Projectile.Center).ToRotation();

            // 逐条依次发射：依据玩家的SparklingVolleyTimer和索引
            int fireInterval = 14; // 两条鱼间隔
            int startFireTime = PreFireDelay + (int)FishIndex * fireInterval;
            if (!fired && hp.SparklingVolleyTimer >= startFireTime) {
                FireLaser(hp);
                fired = true;
            }
            // 齐射结束条件：全部发射完且超过尾声时长
            if (hp.SparklingNextFireIndex >= hp.SparklingFishCount && hp.SparklingVolleyTimer > startFireTime + 120) {
                if (FishIndex == hp.SparklingFishCount - 1) {
                    hp.SparklingVolleyActive = false;
                }
            }
        }

        private void FireLaser(HalibutPlayer hp) {
            SoundEngine.PlaySound(SoundID.Item33 with { Pitch = 0.2f, Volume = 0.7f }, Projectile.Center);
            Vector2 dir = (Main.MouseWorld - Projectile.Center).SafeNormalize(Vector2.UnitX);
            int beam = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + dir * 10f, dir * 0.1f,
                ModContent.ProjectileType<SparklingRayFriendly>(), 0, 0f, Projectile.owner);
            if (Main.projectile.IndexInRange(beam)) {
                Main.projectile[beam].rotation = dir.ToRotation();
                Main.projectile[beam].localAI[0] = 0;
            }
        }
    }

    /// <summary>
    /// 友方版本的激光
    /// </summary>
    internal class SparklingRayFriendly : ModProjectile
    {
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> MaskLaserLine = null;
        public override string Texture => CWRConstant.Placeholder;
        private Vector2[] top = new Vector2[70];
        private Vector2[] bot = new Vector2[70];
        private Vector2 topEnd, botEnd;
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
            if (Projectile.timeLeft > 32) return false;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, Projectile.Center + Projectile.rotation.ToRotationVector2() * 2400, 120, ref p);
        }
        public override void AI() {
            for (int i = 0; i < 70; i++) {
                float x = i * 15f;
                float y = 6f * (0.08f * Projectile.localAI[0]) * (float)Math.Pow(0.1f * x, 0.45);
                top[i] = new Vector2(x, y);
                bot[i] = new Vector2(x, -y);
            }
            float endX = 300 * 15f;
            float endY = 6f * (0.08f * Projectile.localAI[0]) * (float)Math.Pow(0.1f * 70 * 15, 0.45);
            topEnd = new Vector2(endX, endY);
            botEnd = new Vector2(endX, -endY);
            if (Projectile.localAI[0] <= 5 && Projectile.timeLeft > 10)
                Projectile.localAI[0] += 24f;
            if (Projectile.timeLeft <= 20 && Projectile.localAI[0] > 0) Projectile.localAI[0] -= 18f;
            if (Projectile.localAI[0] < 0) Projectile.localAI[0] = 0;
        }
        public override bool PreDraw(ref Color lightColor) {
            List<ColoredVertex> vertices = new();
            for (int i = 0; i < 70; i++) {
                vertices.Add(new ColoredVertex(top[i].RotatedBy(Projectile.rotation) + Projectile.Center - Main.screenPosition, Color.White, new Vector3(i / 70f, 0, 1 - (i / 70f))));
                vertices.Add(new ColoredVertex(bot[i].RotatedBy(Projectile.rotation) + Projectile.Center - Main.screenPosition, Color.White, new Vector3(i / 70f, 1, 1 - (i / 70f))));
            }
            vertices.Add(new ColoredVertex(topEnd.RotatedBy(Projectile.rotation) + Projectile.Center - Main.screenPosition, Color.White, new Vector3(1, 0, 1)));
            vertices.Add(new ColoredVertex(botEnd.RotatedBy(Projectile.rotation) + Projectile.Center - Main.screenPosition, Color.White, new Vector3(1, 1, 1)));
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
