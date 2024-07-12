using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    /// <summary>
    /// 熔火之怒
    /// </summary>
    internal class MoltenFuryHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.MoltenFury].Value;
        public override int targetCayItem => ItemID.MoltenFury;
        public override int targetCWRItem => ItemID.MoltenFury;
        public override void SetShootAttribute() {
            if (AmmoTypes == ProjectileID.WoodenArrowFriendly) {
                AmmoTypes = ProjectileID.FireArrow;
            }
            else if (AmmoTypes == ProjectileID.FireArrow) {
                AmmoTypes = ProjectileID.HellfireArrow;
            }
        }
    }
}
