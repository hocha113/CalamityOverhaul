using CalamityOverhaul.Content.Projectiles.Weapons.Melee.MurasamaProj;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.UIs
{
    internal class MuraChargeUI : UIHandle
    {
        internal MurasamaHeldProj murasamaHeld;
        public override bool Active {
            get {
                if (murasamaHeld == null) {
                    return false;
                }
                if (murasamaHeld.Type != ModContent.ProjectileType<MurasamaHeldProj>()) {
                    return false;
                }
                return murasamaHeld.Projectile.active;
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            murasamaHeld.DrawMassacreBarUI();
            murasamaHeld.DrawOwnerPlayerBarUI();
        }
    }
}
