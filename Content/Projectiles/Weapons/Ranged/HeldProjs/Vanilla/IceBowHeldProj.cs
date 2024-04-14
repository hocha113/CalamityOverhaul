using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class IceBowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.IceBow].Value;
        public override int targetCayItem => ItemID.IceBow;
        public override int targetCWRItem => ItemID.IceBow;
        int fireIndex;
        public override void SetRangedProperty() {
            ArmRotSengsBackBaseValue = 70;
            ShootSpanTypeValue = SpanTypesEnum.IceBow;
        }

        public override void PostInOwner() {
            base.PostInOwner();
        }

        public override void BowShoot() {
            Item.useTime = 5;
            base.BowShoot();
            fireIndex++;
            if (fireIndex >= 3) {
                Item.useTime = 20;
                fireIndex = 0;
            }
        }
    }
}
