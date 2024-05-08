using CalamityOverhaul.Common;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged.Extras
{
    internal class NeutronBow : ModItem, ISetupData
    {
        public override string Texture => CWRConstant.Item_Ranged + "NeutronBow";
        public static int PType;
        public float Charge;
        public void SetupData() {
            PType = ModContent.ItemType<NeutronBow>();
        }

        public override bool IsLoadingEnabled(Mod mod) {
            if (!CWRServerConfig.Instance.AddExtrasContent) {
                return false;
            }
            return base.IsLoadingEnabled(mod);
        }

        public override void SetStaticDefaults() {
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(5, 16));
        }

        public override void SetDefaults() {
            Item.width = Item.height = 54;
            Item.damage = 122;
            Item.useAnimation = Item.useTime = 20;
            Item.knockBack = 2.5f;
            Item.shootSpeed = 16;
            Item.UseSound = SoundID.Item5;
            Item.useAmmo = AmmoID.Arrow;
            Item.rare = ItemRarityID.Red;
            Item.DamageType = DamageClass.Ranged;
            Item.value = Item.buyPrice(13, 33, 75, 0);
            Item.crit = 20;
            Item.SetHeldProj<NeutronBowHeldProj>();
        }
    }
}
