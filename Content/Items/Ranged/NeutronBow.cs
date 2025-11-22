using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class NeutronBow : ModItem, ICWRLoader
    {
        public override string Texture => CWRConstant.Item_Ranged + "NeutronBow";
        public static int PType;
        public static LocalizedText Lang1;
        public static LocalizedText Lang2;
        public static LocalizedText Lang3;
        public float Charge;
        public void SetupData() => PType = ModContent.ItemType<NeutronBow>();
        public override void SetStaticDefaults() {
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(5, 7));
            Lang1 = this.GetLocalization(nameof(Lang1), () => "Trapping gravity");
            Lang2 = this.GetLocalization(nameof(Lang2), () => "Is making gravity yield");
            Lang3 = this.GetLocalization(nameof(Lang3), () => "Finished!!");
        }

        public override void SetDefaults() {
            Item.width = Item.height = 54;
            Item.damage = 152;
            Item.useAnimation = Item.useTime = 20;
            Item.knockBack = 2.5f;
            Item.shootSpeed = 16;
            Item.UseSound = SoundID.Item5;
            Item.useAmmo = AmmoID.Arrow;
            Item.rare = ItemRarityID.Red;
            Item.DamageType = DamageClass.Ranged;
            Item.value = Item.buyPrice(13, 33, 75, 0);
            Item.crit = 20;
            Item.SetHeldProj<NeutronBowHeld>();
            Item.CWR().OmigaSnyContent = SupertableRecipeData.FullItems_NeutronBow;
        }
    }
}
