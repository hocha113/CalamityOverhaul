using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.Tiles;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.Extras
{
    internal class NeutronGun : ModItem, ILoader
    {
        public override string Texture => CWRConstant.Item_Ranged + "NeutronGun";
        public static int PType;
        public float Charge;
        internal static Asset<Texture2D> ShootGun;
        public void Setup() {
            PType = ModContent.ItemType<NeutronGun>();
            if (!Main.dedServ) {
                ShootGun = CWRUtils.GetT2DAsset(CWRConstant.Item_Ranged + "NeutronGun2");
            }
        }

        public override bool IsLoadingEnabled(Mod mod) {
            return !CWRServerConfig.Instance.AddExtrasContent ? false : base.IsLoadingEnabled(mod);
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
            Item.crit = 2;
            Item.SetCartridgeGun<NeutronGunHeldProj>(120);
            Item.CWR().OmigaSnyContent = SupertableRecipeDate.FullItems18;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<BlackMatterStick>(29)
                .AddBlockingSynthesisEvent()
                .AddTile(ModContent.TileType<TransmutationOfMatter>())
                .Register();
        }
    }
}
