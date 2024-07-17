using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GoreEntity
{
    internal class CaseGore : ModGore, ILoader
    {
        public override string Texture => CWRConstant.Asset + "CaseGore";
        public static int PType;
        public void SetupData() {
            PType = ModContent.GoreType<CaseGore>();
            ChildSafety.SafeGore[PType] = true;
        }
        public override bool Update(Gore gore) {
            gore.velocity.X *= 0.98f;
            Lighting.AddLight(gore.position, Color.Gold.ToVector3() * 0.2f * gore.scale);
            return base.Update(gore);
        }
    }
}
