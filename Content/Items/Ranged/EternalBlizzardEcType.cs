using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class EternalBlizzardEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "EternalBlizzard";
        public override void SetDefaults() {
            Item.SetCalamitySD<EternalBlizzard>();
            Item.damage = 48;
            Item.UseSound = CWRSound.Gun_Crossbow_Shoot with { Volume = 0.7f };
            Item.SetHeldProj<EternalBlizzardHeldProj>();
        }
    }
}
