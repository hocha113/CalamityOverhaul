using CalamityOverhaul.Common;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GoreEntity
{
    internal class CaseGore : ModGore, ISetupData
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "GunCasing";
        public static int PType;
        public void SetupData() => PType = ModContent.GoreType<CaseGore>();
        public override bool Update(Gore gore) {
            gore.velocity.X *= 0.98f;
            return base.Update(gore);
        }
    }
}
