using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class NeutronGun : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "NeutronGun";
        public static int ID;
        public float Charge;
        [VaultLoaden(CWRConstant.Item_Ranged + "NeutronGun2")]
        internal static Asset<Texture2D> ShootGun = null;
        public override void SetStaticDefaults() {
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(5, 7));
            ID = Type;
        }

        public override void SetDefaults() {
            Item.width = Item.height = 34;
            Item.damage = 580;
            Item.useAnimation = Item.useTime = 5;
            Item.knockBack = 1.5f;
            Item.shootSpeed = 12;
            Item.useAmmo = AmmoID.Bullet;
            Item.rare = ItemRarityID.Red;
            Item.DamageType = DamageClass.Ranged;
            Item.value = Item.buyPrice(13, 83, 5, 0);
            Item.crit = 2;
            Item.SetCartridgeGun<NeutronGunHeld>(120);
            Item.CWR().OmigaSnyContent = SupertableRecipeData.FullItems_NeutronGun;
        }
    }
}
