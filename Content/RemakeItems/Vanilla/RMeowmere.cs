using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RMeowmere : ItemOverride
    {
        public override int TargetID => ItemID.Meowmere;
        public override bool FormulaSubstitution => false;
        public override void SetDefaults(Item item) {
            item.rare = ItemRarityID.Red;
            item.UseSound = null;
            item.useStyle = ItemUseStyleID.Swing;
            item.damage = 130;
            item.useAnimation = 9;
            item.useTime = 9;
            item.width = 30;
            item.height = 30;
            item.shoot = ProjectileID.Meowmere;
            item.scale = 1.2f;
            item.shootSpeed = 17f;
            item.knockBack = 1.7f;
            item.DamageType = DamageClass.Melee;
            item.value = Item.sellPrice(0, 25);
            item.autoReuse = true;
            item.SetKnifeHeld<MeowmereHeld>();
        }

        public static void SpanDust(Projectile projectile, float offsetScale = 0) {
            if (projectile.type == ProjectileID.Meowmere) {
                for (float i = 0f; i < 37; i += 1f) {
                    int dust = Dust.NewDust(projectile.Center, 0, 0, DustID.RainbowTorch, 0f, 0f, 0, Color.Transparent);
                    Main.dust[dust].position = projectile.Center;
                    Main.dust[dust].velocity = CWRUtils.randVr(13, 17);
                    Main.dust[dust].color = Main.hslToRgb(i / 37, 1f, 0.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].scale = 1f + projectile.ai[0] / 3f + offsetScale;
                }
            }
        }
    }

    internal class MeowmereHeld : BaseKnife
    {
        public override int TargetID => ItemID.Meowmere;
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "AbsoluteZero_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 66;
            canDrawSlashTrail = true;
            autoSetShoot = true;
            distanceToOwner = -20;
            drawTrailBtommWidth = 20;
            drawTrailTopWidth = 30;
            drawTrailCount = 12;
            Length = 62;
            SwingData.starArg = 54;
            SwingData.baseSwingSpeed = 9.65f;
        }

        public override void Shoot() {
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity
                , ProjectileID.Meowmere, Projectile.damage, Projectile.knockBack, Owner.whoAmI);
        }

        public override bool PreInOwner() {
            ExecuteAdaptiveSwing(phase0SwingSpeed: 0.6f, phase1SwingSpeed: 9.2f, phase2SwingSpeed: 8f, swingSound: SoundID.Item58);
            return base.PreInOwner();
        }
    }
}
