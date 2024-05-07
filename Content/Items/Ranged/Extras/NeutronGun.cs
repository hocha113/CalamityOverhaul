using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using ReLogic.Content;
using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.Items.Ranged.Extras
{
    internal class NeutronGun : ModItem, ISetupData
    {
        public override string Texture => CWRConstant.Item_Ranged + "NeutronGun";
        public static int PType;
        public float Charge;
        internal static Asset<Texture2D> ShootGun;
        public void SetupData() {
            PType = ModContent.ItemType<NeutronGun>();
            if (!Main.dedServ) {
                ShootGun = CWRUtils.GetT2DAsset(CWRConstant.Item_Ranged + "NeutronGun2");
            }
        }

        public override bool IsLoadingEnabled(Mod mod) {
            if (!CWRServerConfig.Instance.AddExtrasContent) {
                return false;
            }
            return base.IsLoadingEnabled(mod);
        }

        public override void SetStaticDefaults() {
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(5, 6));
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
            Item.SetCartridgeGun<NeutronGunHeldProj>(120);
        }
    }
}
