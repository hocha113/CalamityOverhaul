using CalamityMod;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System;
using Terraria.DataStructures;
using Terraria.Graphics.Shaders;
using Terraria;
using Terraria.ModLoader;
using System.Linq;
using CalamityOverhaul.Content.Items.Melee;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.IridescentExcaliburpProj
{
    internal class IridescentExcaliburLight : ModProjectile
    {
        public Vector2[] ControlPoints;

        public PrimitiveTrail SlashDrawer;

        public Color projColor = Color.White;

        Player owner => CWRUtils.GetPlayerInstance(Projectile.owner);

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
            if (projColor == Color.White) {
                projColor = CWRUtils.MultiStepColorLerp(Main.rand.NextFloat(), IridescentExcaliburEcType.richColors);
            }
            if (owner != null)
                Projectile.position += owner.velocity;
            Projectile.Opacity = Utils.GetLerpValue(Projectile.localAI[0], 26f, Projectile.timeLeft, clamped: true);
            Projectile.position -= Projectile.velocity;
            Projectile.scale += 0.01f;
            Projectile.rotation -= Projectile.ai[0] == 1 ? 22 : 5;
        }

        public float SlashWidthFunction(float completionRatio) {
            return Projectile.scale * 70f;
        }

        public Color SlashColorFunction(float completionRatio) {
            if (Projectile.ai[1] == 0)
                return Color.Red * Utils.GetLerpValue(0.07f, 0.57f, completionRatio, clamped: true) * Projectile.Opacity;

            else {
                float sengs = MathF.Sin(completionRatio * MathF.PI);
                if (completionRatio < 0.4f) {
                    sengs = MathF.Pow(completionRatio, 3) * 13;
                }
                return Color.Red * sengs * Projectile.Opacity;
            }
        }

        public static void PrepareSlashShader(bool flipped) {
            //GameShaders.Misc["CalamityMod:ExobladeSlash"].SetShaderTexture(ModContent.Request<Texture2D>("CalamityMod/ExtraTextures/GreyscaleGradients/Cracks"));
            //GameShaders.Misc["CalamityMod:ExobladeSlash"].UseColor(projColor);
            //GameShaders.Misc["CalamityMod:ExobladeSlash"].UseSecondaryColor(Color.Blue);
            //GameShaders.Misc["CalamityMod:ExobladeSlash"].Shader.Parameters["fireColor"].SetValue(projColor.ToVector3());
            //GameShaders.Misc["CalamityMod:ExobladeSlash"].Shader.Parameters["flipped"].SetValue(flipped);
            //GameShaders.Misc["CalamityMod:ExobladeSlash"].Apply();
        }

        public override bool PreDraw(ref Color lightColor) {
            if (SlashDrawer == null) {
                SlashDrawer = new PrimitiveTrail(SlashWidthFunction, SlashColorFunction, null, GameShaders.Misc["CalamityMod:ExobladeSlash"]);
            }

            Main.spriteBatch.EnterShaderRegion();
            GameShaders.Misc["CalamityMod:ExobladeSlash"].SetMiscShaderAsset_1(ModContent.Request<Texture2D>("CalamityMod/ExtraTextures/GreyscaleGradients/Cracks"));
            GameShaders.Misc["CalamityMod:ExobladeSlash"].UseColor(projColor);
            GameShaders.Misc["CalamityMod:ExobladeSlash"].UseSecondaryColor(Color.Blue);
            GameShaders.Misc["CalamityMod:ExobladeSlash"].Shader.Parameters["fireColor"].SetValue(projColor.ToVector3());
            GameShaders.Misc["CalamityMod:ExobladeSlash"].Shader.Parameters["flipped"].SetValue(Flipped);
            GameShaders.Misc["CalamityMod:ExobladeSlash"].Apply();
            List<Vector2> list = new List<Vector2>();

            ControlPoints = GenerateSlashPoints(Projectile.velocity.X < 0).ToArray();

            for (int i = 0; i < ControlPoints.Length; i++) {
                list.Add(ControlPoints[i] + ControlPoints[i].SafeNormalize(Vector2.Zero) * (Projectile.scale - 1f) * 70f);
            }

            for (int j = 0; j < 3; j++) {
                SlashDrawer.Draw(list, Projectile.Center - Main.screenPosition, 65);
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

                collBool = Collision.CheckAABBvLineCollision(
                targetHitbox.TopLeft(),
                targetHitbox.Size(),
                starPos,
                endPos,
                32,
                ref point
                );
                if (collBool)
                    break;
            }

            return collBool;
        }
    }
}
