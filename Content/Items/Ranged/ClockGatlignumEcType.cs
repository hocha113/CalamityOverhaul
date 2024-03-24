using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.Items.Ranged
{
    /// <summary>
    /// 发条鳄鱼枪
    /// </summary>
    internal class ClockGatlignumEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "ClockGatlignum";
        public override void SetDefaults() {
            Item.SetCalamitySD<ClockGatlignum>();
            Item.SetCartridgeGun<ClockGatlignumHeldProj>(145);
        }
        public override bool CanConsumeAmmo(Item ammo, Player player) => Main.rand.NextFloat() > 0.1f;
    }
}
