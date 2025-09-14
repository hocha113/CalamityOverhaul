using CalamityOverhaul.Common;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Armor
{
    internal class ModifyShroomiteMask : CWRItemOverride
    {
        public override int TargetID => ItemID.ShroomiteMask;
        public static int SpawnTime;
        public override bool DrawingInfo => false;
        public override bool CanLoadLocalization => false;
        public static LocalizedText UpdateArmorText { get; private set; }
        public override void SetStaticDefaults() => UpdateArmorText = this.GetLocalization("UpdateArmorText", () => "A lingering mushroom barrage is generated during reloading");
        public override void UpdateArmorByHead(Player player, Item body, Item legs) => UpdateArmor(player, body, legs);
        public static void UpdateArmor(Player player, Item body, Item legs) {
            if (body.type != ItemID.ShroomiteBreastplate || legs.type != ItemID.ShroomiteLeggings) {
                return;
            }
            player.setBonus += "\n" + CWRLocText.Instance.KreloadTimeLessenText + "20%";
            player.setBonus += "\n" + UpdateArmorText.Value;
            player.CWR().KreloadTimeIncrease -= 0.2f;

            if (player.CWR().PlayerIsKreLoadTime > 0) {
                if (player.whoAmI == Main.myPlayer && ++SpawnTime > 3) {
                    int proj = Projectile.NewProjectile(player.FromObjectGetParent()
                    , player.Center + VaultUtils.RandVr(124), player.velocity / 2, ProjectileID.Mushroom, 80, 2, player.whoAmI);
                    Main.projectile[proj].DamageType = DamageClass.Ranged;
                    SpawnTime = 0;
                }
            }
            else {
                SpawnTime = 0;
            }
        }
    }

    internal class ModifyShroomiteHeadgear : CWRItemOverride
    {
        public override int TargetID => ItemID.ShroomiteHeadgear;
        public override bool DrawingInfo => false;
        public override bool CanLoadLocalization => false;
        public override void UpdateArmorByHead(Player player, Item body, Item legs) => ModifyShroomiteMask.UpdateArmor(player, body, legs);
    }

    internal class ModifyShroomiteHelmet : CWRItemOverride
    {
        public override int TargetID => ItemID.ShroomiteHelmet;
        public override bool DrawingInfo => false;
        public override bool CanLoadLocalization => false;
        public override void UpdateArmorByHead(Player player, Item body, Item legs) => ModifyShroomiteMask.UpdateArmor(player, body, legs);
    }
}
