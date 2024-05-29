using CalamityMod;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class TheMaelstromSharkOnSpan : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;
        public const float MaxCharge = 90f;
        public float ChargeProgress => (MaxCharge - Projectile.timeLeft) / MaxCharge;
        public float Spread => MathHelper.PiOver2 * (1 - (float)Math.Pow(ChargeProgress, 1.5) * 0.95f);
        private bool onFire;
        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.alpha = 255;
            Projectile.extraUpdates = 1;
            Projectile.penetrate = 1;
            Projectile.timeLeft = (int)MaxCharge;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.light = 0.2f;
        }

        public override bool? CanDamage() => false;

        public override void AI() {
            Projectile owner = null;
            if (Projectile.ai[1] >= 0 && Projectile.ai[1] < Main.maxProjectiles) {
                owner = Main.projectile[(int)Projectile.ai[1]];
            }
            if (owner == null) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = owner.Center;
            Projectile.rotation = owner.rotation;

            if (++Projectile.ai[0] > 80) {
                onFire = true;
            }
            if (!DownRight) {
                Projectile.Kill();
            }
        }

        public override void OnKill(int timeLeft) {
            if (Projectile.IsOwnedByLocalPlayer() && onFire) {
                SoundEngine.PlaySound(SoundID.Item96, Projectile.Center);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, Projectile.rotation.ToRotationVector2() * 35
                    , ModContent.ProjectileType<TheMaelstromShark>(), Projectile.damage, 0, Projectile.owner);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float angle = (Owner.Calamity().mouseWorld - Owner.MountedCenter).ToRotation();
            float blinkage = 0;
            if (Projectile.timeLeft >= MaxCharge * 1.5f) {
                blinkage = (float)Math.Sin(MathHelper.Clamp((Projectile.timeLeft - MaxCharge * 1.5f) / 15f, 0, 1) * MathHelper.PiOver2 + MathHelper.PiOver2);
            }
            Effect effect = Filters.Scene["CalamityMod:SpreadTelegraph"].GetShader().Shader;
            effect.Parameters["centerOpacity"].SetValue(ChargeProgress + 0.3f);
            effect.Parameters["mainOpacity"].SetValue((float)Math.Sqrt(ChargeProgress));
            effect.Parameters["halfSpreadAngle"].SetValue(Spread / 2f);
            effect.Parameters["edgeColor"].SetValue(Color.Lerp(Color.BlueViolet, Color.Blue, blinkage).ToVector3());
            effect.Parameters["centerColor"].SetValue(Color.Lerp(Color.AliceBlue, Color.DarkBlue, blinkage).ToVector3());
            effect.Parameters["edgeBlendLength"].SetValue(0.07f);
            effect.Parameters["edgeBlendStrength"].SetValue(8f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, effect, Main.GameViewMatrix.TransformationMatrix);

            Texture2D texture = ModContent.Request<Texture2D>("CalamityMod/Projectiles/InvisibleProj").Value;

            Main.EntitySpriteDraw(texture, Owner.MountedCenter - Main.screenPosition, null, Color.White, angle, new Vector2(texture.Width / 2f, texture.Height / 2f), 700f, 0, 0);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }
    }
}
