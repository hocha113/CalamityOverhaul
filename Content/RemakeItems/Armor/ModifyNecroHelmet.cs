using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Others;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Armor
{
    //死灵
    internal class ModifyNecroHelmet : ItemOverride
    {
        public override int TargetID => ItemID.NecroHelmet;
        public static LocalizedText UpdateArmorText { get; private set; }
        public override bool CanLoadLocalization => false;
        public override bool DrawingInfo => false;
        public override void SetStaticDefaults() => UpdateArmorText = this.GetLocalization(nameof(UpdateArmorText),
            () => "During the reloading process, if you are injured, a pile of bones will be thrown out.");
        public override void UpdateArmorByHead(Player player, Item body, Item legs) => UpdateArmor(player, body, legs);
        public static void UpdateArmor(Player player, Item body, Item legs) {
            if (body.type != ItemID.NecroBreastplate || legs.type != ItemID.NecroGreaves) {
                return;
            }
            player.setBonus += "\n" + CWRLocText.Instance.KreloadTimeLessenText + "10%";
            player.setBonus += "\n" + UpdateArmorText.Value;
            player.CWR().KreloadTimeIncrease -= 0.1f;
            if (player.CWR().PlayerIsKreLoadTime > 0) {
                if (player.whoAmI == Main.myPlayer && player.ownedProjectileCounts[ModContent.ProjectileType<Hit>()] > 0) {
                    for (int i = 0; i < 6; i++) {
                        int proj = Projectile.NewProjectile(player.FromObjectGetParent()
                        , player.Center + CWRUtils.randVr(124), CWRUtils.randVr(24, 32), ProjectileID.BoneGloveProj, 30, 2, player.whoAmI);
                        Main.projectile[proj].DamageType = DamageClass.Ranged;
                    }
                }
            }
        }
    }
    //远古死灵
    internal class ModifyAncientNecroHelmet : ModifyNecroHelmet
    {
        public override int TargetID => ItemID.AncientNecroHelmet;
    }
}
