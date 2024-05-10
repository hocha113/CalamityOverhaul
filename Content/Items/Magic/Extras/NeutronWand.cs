using CalamityOverhaul.Common;
using Terraria.DataStructures;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.NeutronWandProjs;

namespace CalamityOverhaul.Content.Items.Magic.Extras
{
    internal class NeutronWand : ModItem, ISetupData
    {
        public override bool IsLoadingEnabled(Mod mod) {
            return false;//暂时不要在这个版本中出现
        }
        public override string Texture => CWRConstant.Item_Magic + "NeutronWand";
        internal static int PType;
        void ISetupData.SetupData() => PType = ModContent.ItemType<NeutronWand>();
        public override void SetStaticDefaults() => CWRUtils.SetAnimation(Type, 5, 10);
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.damage = 282;
            Item.DamageType = DamageClass.Ranged;
            Item.useTime = Item.useAnimation = 15;
            Item.autoReuse = true;
            Item.value = Item.buyPrice(15, 3, 5, 0);
            Item.rare = ItemRarityID.Red;
            Item.shootSpeed = 15;
            Item.mana = 15;
            Item.crit = 6;
            Item.UseSound = SoundID.NPCDeath56;
            Item.SetHeldProj<NeutronWandHeld>();
        }
    }
}
