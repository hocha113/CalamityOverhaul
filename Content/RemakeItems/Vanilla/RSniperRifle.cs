using CalamityMod;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RSniperRifle : BaseRItem
    {
        public override int TargetID => ItemID.SniperRifle;
        public override bool IsVanilla => true;
        public override string TargetToolTipItemName => "Wap_SniperRifle_Text";
        public override void SetDefaults(Item item) {
            item.useTime = 0;
            item.damage = 4444;
            item.CWR().Scope = true;
            item.Calamity().canFirePointBlankShots = true;
            item.SetCartridgeGun<SniperRifleHeldProj>(4);
        }

        public override bool? On_ModifyWeaponCrit(Item item, Player player, ref float crit) {
            if (player.CWR().TryGetInds_BaseFeederGun(out var gun)) {
                SniperRifleHeldProj sniperRifle = gun as SniperRifleHeldProj;
                if (sniperRifle != null) {
                    crit = sniperRifle.criticalStrike ? 100 : 1;
                }
            }
            return false;
        }
    }
}
