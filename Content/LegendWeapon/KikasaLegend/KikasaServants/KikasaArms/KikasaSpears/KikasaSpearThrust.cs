using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaArmsPalette;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaSpears
{
    /// <summary>
    /// 湖水刺：矛奴突刺的判定事件弹幕。整线判定（贴脸与刺尖都算数），
    /// 伤害窗只开前 8 帧，其后只留视觉消散。
    /// ai0 = 判定半长 px，ai1 = 刺向角；owner 生成、生成包自含
    /// </summary>
    internal class KikasaSpearThrust : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int LifeFrames = 22;
        private const int DamageWindow = 8;

        private ref float HalfLen => ref Projectile.ai[0];
        private ref float ThrustAngle => ref Projectile.ai[1];

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = LifeFrames;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        private float LifeT => 1f - Projectile.timeLeft / (float)LifeFrames;

        public override bool? CanDamage() => Projectile.timeLeft > LifeFrames - DamageWindow ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 dir = ThrustAngle.ToRotationVector2();
            Vector2 start = Projectile.Center - dir * HalfLen;
            Vector2 end = Projectile.Center + dir * HalfLen;
            float width = MathHelper.Clamp(HalfLen * 0.28f, 14f, 30f);
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                start, end, width, ref point);
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            Projectile.rotation = ThrustAngle;

            //显现帧：沿刺线撕几粒水珠
            if (!Main.dedServ && Projectile.timeLeft == LifeFrames - 1) {
                Vector2 dir = ThrustAngle.ToRotationVector2();
                for (int k = 0; k < 6; k++) {
                    float p = k / 5f * 2f - 1f;
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        Projectile.Center + dir * (HalfLen * p),
                        dir.RotatedBy(MathHelper.PiOver2 * MathF.Sign(p == 0f ? 1f : p)) * Main.rand.NextFloat(0.8f, 2f)
                            + dir * Main.rand.NextFloat(1f, 3f),
                        BloodMain * Main.rand.NextFloat(0.4f, 0.6f),
                        Main.rand.NextFloat(0.28f, 0.48f))
                        ?.Configure(Main.rand.Next(10, 18), 0f);
                }
            }
            float glow = 1f - LifeT;
            Lighting.AddLight(Projectile.Center, 0.5f * glow, 0.14f * glow, 0.12f * glow);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            Vector2 origin = glow.Size() * 0.5f;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 dir = ThrustAngle.ToRotationVector2();
            float t = LifeT;
            //显现两帧超冲、伤害窗后温柔消散
            float flash = t < 0.12f ? 1.2f : MathHelper.Clamp(1f - (t - 0.36f) / 0.64f, 0f, 1f);
            if (flash <= 0.02f) {
                return false;
            }
            float len = HalfLen * 2f * (0.92f + 0.08f * MathF.Min(t * 6f, 1f));
            float wid = MathHelper.Clamp(HalfLen * 0.16f, 7f, 15f) * flash;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            //暗血底、主体、亮芯：细长贯线
            sb.Draw(glow, pos, null, BloodDeep * (0.5f * flash), ThrustAngle, origin,
                new Vector2(len * 1.1f / glow.Width * 2f, wid * 1.9f / glow.Height * 2f), SpriteEffects.None, 0f);
            sb.Draw(glow, pos, null, BloodMain * (0.85f * flash), ThrustAngle, origin,
                new Vector2(len / glow.Width * 2f, wid / glow.Height * 2f), SpriteEffects.None, 0f);
            sb.Draw(glow, pos, null, BloodBright * (0.6f * flash), ThrustAngle, origin,
                new Vector2(len * 0.8f / glow.Width * 2f, wid * 0.4f / glow.Height * 2f), SpriteEffects.None, 0f);
            //刺尖爆点
            sb.Draw(glow, pos + dir * HalfLen, null,
                MuzzleHot * (0.5f * flash), 0f, origin,
                new Vector2(12f * 2f / glow.Width), SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}
