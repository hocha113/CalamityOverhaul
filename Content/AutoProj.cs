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
            if (ModContent.HasAsset(projectile.Texture)) {
                TextureAssets.Projectile[projectile.Projectile.type] = ModContent.Request<Texture2D>(projectile.Texture);
            }
            else {
                TextureAssets.Projectile[projectile.Projectile.type] = VaultAsset.placeholder3;
            }
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
