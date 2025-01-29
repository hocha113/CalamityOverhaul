using CalamityMod;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RStarWrath : ItemOverride
    {
        public override int TargetID => ItemID.StarWrath;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) {
            item.UseSound = null;
            item.SetKnifeHeld<StarWrathHeld>();
        }
        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    internal class StarWrathHeld : BaseKnife
    {
        public override int TargetID => ItemID.StarWrath;
        public override string gradientTexturePath => CWRConstant.ColorBar + "Phaseblade6_Bar";
        public override void SetKnifeProperty() {
            drawTrailHighlight = false;
            canDrawSlashTrail = true;
            drawTrailCount = 10;
            drawTrailTopWidth = 40;
            distanceToOwner = -22;
            drawTrailBtommWidth = 0;
            unitOffsetDrawZkMode = 6;
            SwingData.baseSwingSpeed = 4f;
            SwingData.starArg = 46;
            Projectile.width = Projectile.height = 46;
            Length = 46;
        }

        public override bool PreInOwnerUpdate() {
            ExecuteAdaptiveSwing(initialMeleeSize: 1, phase0SwingSpeed: 0.3f
                , phase1SwingSpeed: 8.2f, phase2SwingSpeed: 6f
                , phase0MeleeSizeIncrement: 0, phase2MeleeSizeIncrement: 0, swingSound: SoundID.Item105);
            return base.PreInOwnerUpdate();
        }

        public override void Shoot() {
            float meleeSpeedAf = SwingMultiplication;
            float speed = 13 / meleeSpeedAf;
            for (int i = 0; i < 6; ++i) {
                float randomSpeed = speed * Main.rand.NextFloat(0.7f, 1.4f);
                CalamityUtils.ProjectileRain(Projectile.GetSource_FromAI(), InMousePos
                    , 290f, 130f, 850f, 1100f, randomSpeed, ProjectileID.StarWrath
                    , Projectile.damage / 2, 6f, Owner.whoAmI);
            }
        }
    }
}
