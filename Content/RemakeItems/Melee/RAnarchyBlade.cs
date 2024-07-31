using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.CalPlayer;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.NPCs;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.VulcaniteProj;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RAnarchyBlade : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<AnarchyBlade>();
        public override int ProtogenesisID => ModContent.ItemType<AnarchyBladeEcType>();
        public override string TargetToolTipItemName => "AnarchyBladeEcType";
        public override void SetDefaults(Item item) => AnarchyBladeEcType.SetDefaultsFunc(item);
    }
}
