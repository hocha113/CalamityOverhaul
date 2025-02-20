using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RHellfireFlamberge : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<HellfireFlamberge>();
        public override void SetDefaults(Item item) {
            item.UseSound = null;
            item.SetKnifeHeld<HellfireFlambergeHeld>();
        }
    }

    internal class HellfireFlambergeHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<HellfireFlamberge>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "RedSun_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 66;
            canDrawSlashTrail = true;
            drawTrailHighlight = false;
            distanceToOwner = -20;
            drawTrailBtommWidth = 20;
            drawTrailTopWidth = 40;
            drawTrailCount = 13;
            Length = 62;
            SwingData.starArg = 48;
            SwingData.baseSwingSpeed = 3.5f;
            ShootSpeed = 20;
        }

        public override bool PreInOwner() {
            ExecuteAdaptiveSwing(initialMeleeSize: 1, phase1Ratio: 0.2f, phase0SwingSpeed: -0.1f
                , phase1SwingSpeed: 5.2f, phase2SwingSpeed: 2f);
            return base.PreInOwner();
        }

        public override void Shoot() {
            SoundEngine.PlaySound(SoundID.Item20, Owner.Center);
            int type = ModContent.ProjectileType<VolcanicFireball>();
            for (int index = 0; index < 6; ++index) {
                float damageMult = 0.4f;
                switch (index) {
                    case 0:
                    case 1:
                        type = ModContent.ProjectileType<VolcanicFireball>();
                        break;
                    case 2:
                        type = ModContent.ProjectileType<VolcanicFireballLarge>();
                        damageMult = 0.75f;
                        break;
                    default:
                        break;
                }
                Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity.RotatedByRandom(0.1f) * Main.rand.NextFloat(0.6f, 3.2f)
                    , type, (int)(Projectile.damage * damageMult), Projectile.knockBack, Owner.whoAmI, 0f, 0f);
            }
        }

        public override void MeleeEffect() {
            if (Main.rand.NextBool(3)) {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, Main.rand.NextBool(3) ? 16 : 174);
            }
            if (Main.rand.NextBool(5) && Main.netMode != NetmodeID.Server) {
                int smoke = Gore.NewGore(Owner.GetSource_ItemUse(Item), Projectile.position, default, Main.rand.Next(375, 378), 0.75f);
                Main.gore[smoke].behindTiles = true;
            }
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 300);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.OnFire3, 300);
        }
    }
}
