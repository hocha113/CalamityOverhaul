using CalamityOverhaul.Content.Projectiles.Weapons.Magic;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RLunarFlareBook : BaseRItem
    {
        public override int TargetID => ItemID.LunarFlareBook;
        public override bool FormulaSubstitution => false;
        public override void SetDefaults(Item item) => item.shoot = ModContent.ProjectileType<WaningMoonLight>();
    }
}
