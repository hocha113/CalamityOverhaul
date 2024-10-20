﻿using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class ShellshooterEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Shellshooter";
        public override void SetDefaults() {
            Item.SetItemCopySD<Shellshooter>();
            Item.SetHeldProj<ShellshooterHeldProj>();
        }
    }
}
