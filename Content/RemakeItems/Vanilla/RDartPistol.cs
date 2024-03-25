using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RDartPistol : BaseRItem
    {
        public override int TargetID => ItemID.DartPistol;
        public override bool IsVanilla => true;
        public override string TargetToolTipItemName => "Wap_DartPistol_Text";
        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<DartPistolHeldProj>(12);
            item.damage = 30;
        }
    }
}
