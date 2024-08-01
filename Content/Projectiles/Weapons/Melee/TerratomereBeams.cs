using CalamityMod;
using CalamityMod.Graphics.Primitives;
using CalamityMod.Projectiles.Melee;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class TerratomereBeams : ModProjectile
    {
        public Vector2[] ControlPoints;

        public new string LocalizationCategory => "Projectiles.Melee";

        public bool Flipped => Projectile.ai[0] == 1f;

        public override string Texture => "CalamityMod/Projectiles/InvisibleProj";

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 144;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 30;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            Player owner = CWRUtils.GetPlayerInstance(Projectile.owner);
            if (owner != null) Projectile.position += owner.velocity;
            Projectile.Opacity = Utils.GetLerpValue(Projectile.localAI[0], 26f, Projectile.timeLeft, clamped: true);
            Projectile.velocity *= 0.91f;
            Projectile.scale *= 1.03f;
        }

        public float SlashWidthFunction(float completionRatio) {
            return Projectile.scale * 50f;
        }

        public Color SlashColorFunction(float completionRatio) {
            if (Projectile.ai[1] == 0)
                return Color.Lime * Utils.GetLerpValue(0.07f, 0.57f, completionRatio, clamped: true) * Projectile.Opacity;

            else {
                float sengs = MathF.Sin(completionRatio * MathF.PI);
                if (completionRatio < 0.4f) {
                    sengs = MathF.Pow(completionRatio, 3) * 13;
                }
                return Color.Lime * sengs * Projectile.Opacity;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.spriteBatch.EnterShaderRegion();
            TerratomereHoldoutProj.PrepareSlashShader(Flipped);
            List<Vector2> list = [];

            if (ControlPoints == null)
                return false;

            for (int i = 0; i < ControlPoints.Length; i++) {
                list.Add(ControlPoints[i] + ControlPoints[i].SafeNormalize(Vector2.Zero) * (Projectile.scale - 1f) * 70f);
            }

            for (int j = 0; j < 3; j++) {
                PrimitiveRenderer.RenderTrail(list, new PrimitiveSettings(SlashWidthFunction, SlashColorFunction
                    , (float _) => Projectile.Center, smoothen: true, pixelate: false, GameShaders.Misc["CalamityMod:ExobladeSlash"]), 65);
            }

            Main.spriteBatch.ExitShaderRegion();
            return false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            bool collBool = false;
            float point = 0;
            Vector2 starPos = Projectile.Center;
            for (int i = 0; i < 20; i++) {
                collBool = Collision.CheckAABBvLineCollision(
                targetHitbox.TopLeft(),
                targetHitbox.Size(),
                starPos,
                Projectile.Center +
                (Projectile.velocity.UnitVector() * Projectile.scale * 130)
                .RotatedBy(MathHelper.ToRadians(-90 + i * 9)),
                32,
                ref point
                );
                if (collBool) break;
            }

            return collBool;
        }
    }
}
