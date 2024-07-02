using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;

namespace CalamityOverhaul.Content.Items.Magic
{
    /// <summary>
    /// 以太之低语
    /// </summary>
    internal class AethersWhisperEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "AethersWhisper";
        public override void SetDefaults() {
            Item.SetCalamitySD<AethersWhisper>();
            Item.useTime = 30;
            Item.SetHeldProj<AethersWhisperHeldProj>();
        }
    }
}
