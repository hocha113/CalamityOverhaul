using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class RulingPrismCore : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Proj_Melee + "OrderbringerBeam";
        public Color ProjColor;
        public static Color[] richColors = new Color[]{
            new Color(255, 0, 0),
            new Color(0, 255, 0),
            new Color(0, 0, 255),
            new Color(255, 255, 0),
            new Color(255, 0, 255),
            new Color(0, 255, 255),
            new Color(255, 165, 0),
            new Color(128, 0, 128),
            new Color(255, 192, 203),
            new Color(0, 128, 0),
            new Color(128, 128, 0),
            new Color(0, 128, 128),
            new Color(128, 0, 0),
            new Color(139, 69, 19),
            new Color(0, 255, 127),
            new Color(255, 215, 0)
        };
        public override void SetDefaults() {
            Projectile.width = 4;
            Projectile.height = 4;
            Projectile.friendly = true;
            Projectile.penetrate = 2;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.MaxUpdates = 2;
            Projectile.timeLeft = 180;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.scale = 0.7f;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI() {
            if (Projectile.ai[1] == 0f) {
                ProjColor = VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), richColors);
                Projectile.ai[1] = 1f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            Lighting.AddLight(Projectile.Center, Main.DiscoColor.ToVector3());
            Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height
                , DustID.UltraBrightTorch, Projectile.velocity.X, Projectile.velocity.Y, 155, ProjColor, 1);
            dust.noGravity = true;
            dust.color = ProjColor;
            CWRRef.HomeInOnNPC(Projectile, true, 200f, 18f, 20f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState
                , DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, ProjColor, Projectile.rotation, value.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                , DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}
