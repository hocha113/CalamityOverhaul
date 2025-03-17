using CalamityOverhaul.Content.Projectiles.Weapons.Magic.NeutronWandProjs;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class NeutronWand : ModItem, ICWRLoader
    {
        public override string Texture => CWRConstant.Item_Magic + "NeutronWand";
        internal static int PType;
        void ICWRLoader.SetupData() => PType = ModContent.ItemType<NeutronWand>();
        public override void SetStaticDefaults() {
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(5, 12));
        }
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.damage = 355;
            Item.DamageType = DamageClass.Magic;
            Item.useTime = Item.useAnimation = 15;
            Item.autoReuse = true;
            Item.value = Item.buyPrice(15, 3, 5, 0);
            Item.rare = ItemRarityID.Red;
            Item.shootSpeed = 15;
            Item.mana = 15;
            Item.crit = 6;
            Item.UseSound = SoundID.NPCDeath56;
            Item.SetHeldProj<NeutronWandHeld>();
            Item.CWR().OmigaSnyContent = SupertableRecipeDate.FullItems_NeutronWand;
        }
    }
}
