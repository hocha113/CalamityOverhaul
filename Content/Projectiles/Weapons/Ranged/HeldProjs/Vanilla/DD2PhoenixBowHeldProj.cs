using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class DD2PhoenixBowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.DD2PhoenixBow].Value;
        public override int targetCayItem => ItemID.DD2PhoenixBow;
        public override int targetCWRItem => ItemID.DD2PhoenixBow;
        int fireIndex;
        public override void SetRangedProperty() {
            base.SetRangedProperty();
        }

        public override void BowShoot() {
            if (fireIndex > 2) {
                AmmoTypes = ProjectileID.DD2PhoenixBowShot;
                fireIndex = 0;
            }
            base.BowShoot();
            fireIndex++;
        }
    }
}
