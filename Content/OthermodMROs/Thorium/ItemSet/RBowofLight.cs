using CalamityOverhaul.Content.OthermodMROs.Thorium.Core;
using CalamityOverhaul.Content.OthermodMROs.Thorium.ProjectileSet.Helds;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.OthermodMROs.Thorium.ItemSet
{
    internal class RBowofLight : BaseRItem, LThoriumCall
    {
        internal static int BowofLightID;
        public override int TargetID => BowofLightID;
        public override bool FormulaSubstitution => false;
        public override bool CanLoad() => FromThorium.Has;
        public void LoadThoData(Mod thoriumMod) { }
        public void PostLoadThoData(Mod thoriumMod) => BowofLightID = thoriumMod.Find<ModItem>("BowofLight").Type;
        public override void SetDefaults(Item item) => item.SetHeldProj<BowofLightHeldProj>();
    }
}
