using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class MG42 : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "MG42";
        public override void SetDefaults() {
            Item.width = Item.height = 34;
            Item.damage = 882;
            Item.useAnimation = Item.useTime = 5;
            Item.knockBack = 1.5f;
            Item.shootSpeed = 12;
            Item.useAmmo = AmmoID.Bullet;
            Item.rare = ItemRarityID.Red;
            Item.UseSound = CWRSound.Gun_AWP_Shoot with { Pitch = -0.1f, Volume = 0.15f };
            Item.DamageType = DamageClass.Ranged;
            Item.value = Terraria.Item.buyPrice(3, 53, 5, 0);
            Item.crit = 2;
            Item.CWR().Scope = true;
            Item.SetCartridgeGun<MG42Held>(220);
        }
    }
}
