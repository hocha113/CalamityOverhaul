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
    internal class RDartRifle : BaseRItem
    {
        public override int TargetID => ItemID.DartRifle;
        public override bool IsVanilla => true;
        public override string TargetToolTipItemName => "Wap_DartRifle_Text";
        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<DartRifleHeldProj>(24);
            item.damage = 40;
        }
    }
}
