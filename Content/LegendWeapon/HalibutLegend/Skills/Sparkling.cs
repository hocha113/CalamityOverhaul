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

        internal static void TryTriggerSparklingVolley(Item item, Player player, HalibutPlayer hp) {
            if (hp.SparklingVolleyActive) return;
            if (hp.SparklingVolleyCooldown > 0) return;
            //周期性触发，可依据使用计数，也可随机微调
            if (hp.SparklingUseCounter < 6) {
                return; // 至少连续普通攻击6次后触发
            }
            hp.SparklingUseCounter = 0;

            // 激活齐射
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

        public override void SetDefaults() {
            Projectile.width = 40; Projectile.height = 40;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 120; // 容错
            Projectile.friendly = false;
            Projectile.hostile = false;
        }

        public override void AI() {
            if (Owner == null || !Owner.active) { Projectile.Kill(); return; }
            var hp = Owner.GetOverride<HalibutPlayer>();
            if (hp.SparklingVolleyId != (int)VolleyId) {
                Projectile.Kill();
                return;
            }
            // 动态重新定位到玩家后方弧线上（保持结构稳定）
            Vector2 aimDir = Projectile.To(Main.MouseWorld).UnitVector();
            Vector2 behind = (-aimDir).SafeNormalize(Vector2.UnitX);
            float arc = MathHelper.ToRadians(Sparkling.RoingArc);
            float radius = 190f;
            float t = (hp.SparklingFishCount <= 1) ? 0.5f : FishIndex / (hp.SparklingFishCount - 1);
            float angOff = (t - 0.5f) * arc;
            Vector2 offsetDir = behind.RotatedBy(angOff);
            Vector2 basePos = Owner.Center + offsetDir * radius;
            float bob = (float)Math.Sin(Main.GameUpdateCount * 0.08f + FishIndex) * 6f;
            Projectile.Center = Vector2.Lerp(Projectile.Center, basePos + new Vector2(0, bob), 0.25f);
            Projectile.rotation = aimDir.ToRotation();

            // 逐条依次发射：依据玩家的SparklingVolleyTimer和索引
            int fireInterval = 14; // 两条鱼间隔
            int startFireTime = PreFireDelay + (int)FishIndex * fireInterval;
            if (!fired && hp.SparklingVolleyTimer >= startFireTime) {
                FireLaser(hp);
                fired = true;
            }

            if (!fired) {
                Projectile.timeLeft = 120;
                if (++Projectile.localAI[1] > 220) {
                    Projectile.Kill();
                }
            }
        }

        private void FireLaser(HalibutPlayer hp) {
            SoundEngine.PlaySound(SoundID.Item33 with { Pitch = 0.2f, Volume = 0.7f }, Projectile.Center);
            Vector2 dir = (Main.MouseWorld - Projectile.Center).SafeNormalize(Vector2.UnitX);
            int beam = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + dir * 10f, dir * 0.1f,
                ModContent.ProjectileType<SparklingRay>(), Projectile.damage, 1f, Projectile.owner, Projectile.identity);
            if (Main.projectile.IndexInRange(beam)) {
                Main.projectile[beam].rotation = dir.ToRotation();
                Main.projectile[beam].localAI[0] = 0;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;//获取鱼的纹理

            // 计算绘制参数
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Rectangle sourceRect = value.Frame(1, 1, 0, 0);
            Vector2 origin = sourceRect.Size() / 2f;
            float drawRotation = Projectile.rotation + (Projectile.spriteDirection > 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4);

            // 根据朝向决定翻转
            SpriteEffects effects = Projectile.spriteDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;

            Main.spriteBatch.Draw(value, drawPosition, sourceRect, Color.White, drawRotation, origin, Projectile.scale, effects, 0f);
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
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , Projectile.Center, Projectile.Center + Projectile.rotation.ToRotationVector2() * 2400, 120, ref p);
        }
        public override void AI() {
            if (Projectile.ai[0].TryGetProjectile(out var projectile)) {
                Projectile.Center = projectile.Center;
            }

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
