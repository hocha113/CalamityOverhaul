using CalamityOverhaul.Content.OthermodMROs.Thorium.Core;
using CalamityOverhaul.Content.OthermodMROs.Thorium.ProjectileSet.Helds;
using CalamityOverhaul.Content.RemakeItems.Core;
using System.Security.Policy;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.OthermodMROs.Thorium.ItemSet
{
    internal class RFrostFury : BaseRItem, LThoriumCall
    {
        internal static int FrostFuryID;
        public override int TargetID => FrostFuryID;
        public override bool FormulaSubstitution => false;
        public override bool CanLoad() => FromThorium.Has;
        public void LoadThoDate(Mod thoriumMod) { }
        public void PostLoadThoDate(Mod thoriumMod) => FrostFuryID = thoriumMod.Find<ModItem>("FrostFury").Type;
        public override void SetDefaults(Item item) => item.SetHeldProj<FrostFuryHeldProj>();
    }
}
