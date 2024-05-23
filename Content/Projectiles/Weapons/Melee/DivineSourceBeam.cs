using CalamityMod;
using CalamityMod.Graphics.Primitives;
using CalamityMod.Projectiles.Melee;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class DivineSourceBeam : ModProjectile
    {
        public Vector2[] ControlPoints;

        private Player owner => CWRUtils.GetPlayerInstance(Projectile.owner);

        public const float EndRot = 60 * CWRUtils.atoR;
        public const float StarRot = -170 * CWRUtils.atoR;

        public const float LEndRot = -240 * CWRUtils.atoR;
        public const float LStarRot = -10 * CWRUtils.atoR;

        public new string LocalizationCategory => "Projectiles.Melee";

        public bool Flipped => Projectile.ai[0] == 1f;

        public override string Texture => "CalamityMod/Projectiles/InvisibleProj";

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 144;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = ModContent.GetInstance<TrueMeleeDamageClass>();
            Projectile.penetrate = -1;
            Projectile.timeLeft = 30;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void OnSpawn(IEntitySource source)
            => Projectile.rotation = Projectile.velocity.X > 0 ? -160 : 90;

        public IEnumerable<Vector2> GenerateSlashPoints(bool dir) {
            float starRot = StarRot;
            float endRot = EndRot;
            if (dir) {
                starRot = LStarRot;
                endRot = LEndRot + 30 * CWRUtils.atoR;
            }

            for (int i = 0; i < 30; i++) {
                float completion = MathHelper.Lerp(endRot + Projectile.rotation.AtoR(), starRot + Projectile.rotation.AtoR(), i / 30f);
                completion *= Math.Sign(Projectile.velocity.X) * -1;
                yield return completion.ToRotationVector2() * 84f;
            }
        }

        public override void AI() {
            if (owner != null) {
                Projectile.position += owner.velocity;
            }
            Projectile.Opacity = Utils.GetLerpValue(Projectile.localAI[0], 26f, Projectile.timeLeft, clamped: true);
            Projectile.velocity *= 0.91f;
            Projectile.scale *= 1.03f;
            Projectile.rotation -= 5f;
        }

        public float SlashWidthFunction(float completionRatio) {
            return Projectile.scale * 50f;
        }

        public Color SlashColorFunction(float completionRatio) {
            //return Color.Lime * Utils.GetLerpValue(0.07f, 0.57f, completionRatio, clamped: true) * Projectile.Opacity;
            float sengs = MathF.Sin(completionRatio * MathF.PI);
            if (completionRatio < 0.4f) {
                sengs = MathF.Pow(completionRatio, 3) * 13;
            }
            return Color.Lime * sengs * Projectile.Opacity;
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.spriteBatch.EnterShaderRegion();
            TerratomereHoldoutProj.PrepareSlashShader(Flipped);
            List<Vector2> list = new List<Vector2>();

            ControlPoints = GenerateSlashPoints(Projectile.velocity.X < 0).ToArray();

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
                Vector2 endPos = Projectile.Center + MathHelper.ToRadians(-160 + 11 * i).ToRotationVector2() * Projectile.scale * 120;
                if (Projectile.velocity.X < 0)
                    endPos = Projectile.Center + MathHelper.ToRadians(20 - 11 * i).ToRotationVector2() * Projectile.scale * 120;
                collBool = Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), starPos, endPos, 32, ref point);
                if (collBool) {
                    break;
                }
            }

            return collBool;
        }
    }
}
