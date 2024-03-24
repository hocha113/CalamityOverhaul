using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged.Extras;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class PetrifiedDiseaseHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Item_Ranged + "PetrifiedDisease";
        public override int targetCayItem => ModContent.ItemType<PetrifiedDisease>();
        public override int targetCWRItem => ModContent.ItemType<PetrifiedDisease>();
        public override void SetRangedProperty() {
            HandRotStartTime = 40;
        }
        public override void BowShoot() {
            AmmoTypes = ModContent.ProjectileType<PetrifiedDiseaseAorrw>();
            base.BowShoot();
        }
    }
}
