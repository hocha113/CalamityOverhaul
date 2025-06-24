﻿using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Magic
{
    internal class RNanoPurge : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<NanoPurge>();
        public override void SetDefaults(Item item) => item.SetHeldProj<NanoPurgeHeldProj>();
    }
}
