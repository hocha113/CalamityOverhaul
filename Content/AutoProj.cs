using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content
{
    internal abstract class AutoProj : ModProjectile
    {
        public override void AutoStaticDefaults() => AutoStaticDefaults(this);

        public static void AutoStaticDefaults(ModProjectile projectile) {
            TextureAssets.Projectile[projectile.Projectile.type] = ModContent.HasAsset(projectile.Texture)
                ? ModContent.Request<Texture2D>(projectile.Texture) : ModContent.Request<Texture2D>(CWRConstant.Placeholder3);
            Main.projFrames[projectile.Projectile.type] = 1;
            if (projectile.Projectile.hostile) {
                Main.projHostile[projectile.Projectile.type] = true;
            }
            if (projectile.Projectile.aiStyle == ProjAIStyleID.Hook) {
                Main.projHook[projectile.Projectile.type] = true;
            }
        }
    }
}
