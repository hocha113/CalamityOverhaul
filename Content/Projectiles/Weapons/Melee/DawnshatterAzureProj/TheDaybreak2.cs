using CalamityMod;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.Graphics.Shaders;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.DawnshatterAzureProj
{
    internal class TheDaybreak2 : TheDaybreak
    {
        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (TailDrawer is null)
                TailDrawer = new PrimitiveTrail(PrimitiveWidthFunction, PrimitiveColorFunction, PrimitiveTrail.RigidPointRetreivalFunction, GameShaders.Misc["CalamityMod:HeavenlyGaleLightningArc"]);

            GameShaders.Misc["CalamityMod:HeavenlyGaleLightningArc"].UseImage1("Images/Misc/noise");
            GameShaders.Misc["CalamityMod:HeavenlyGaleLightningArc"].Apply();

            TailDrawer.Draw(Projectile.oldPos, Projectile.Size * 0.5f - Main.screenPosition, 80);

            Texture2D value = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(value, Projectile.Center + -Main.screenPosition, null, Color.White, Projectile.rotation, value.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
