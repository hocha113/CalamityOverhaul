using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RGalactusBlade : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<GalactusBlade>();
        public override void SetDefaults(Item item) => item.SetKnifeHeld<GalactusBladeHeld>();
    }

    internal class GalactusBladeHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<GalactusBlade>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "GalactusBlade_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 40;
            drawTrailHighlight = false;
            canDrawSlashTrail = true;
            SwingData.starArg = 74;
            SwingData.baseSwingSpeed = 5f;
            drawTrailBtommWidth = 30;
            distanceToOwner = 14;
            drawTrailTopWidth = 30;
            Length = 50;
        }

        public override bool PreInOwner() {
            if (Main.rand.NextBool(4)) {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, Main.rand.NextBool() ? 164 : 229);
            }
            if (Time % (12 * UpdateRate) == 0) {
                canShoot = true;
            }
            return base.PreInOwner();
        }

        public override void Shoot() {
            int type = ModContent.ProjectileType<GalacticaComet>();
            Vector2 orig = ShootSpanPos + new Vector2(0, -1000);
            Vector2 toMou = orig.To(InMousePos);
            for (int i = 0; i < 3; i++) {
                Vector2 spwanPos = orig + toMou.UnitVector() * 600;
                spwanPos.X += Main.rand.Next(-60, 60);
                spwanPos.Y -= 660;
                Vector2 ver = spwanPos.To(InMousePos).UnitVector() * 26;
                ver = ver.RotatedByRandom(0.2f);
                ver *= Main.rand.NextFloat(0.6f, 1.33f);
                Projectile.NewProjectile(Source, spwanPos, ver, type, Projectile.damage, Projectile.knockBack, Owner.whoAmI, 0f, Main.rand.Next(10));
            }
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<HolyFlames>(), 300);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<HolyFlames>(), 300);
        }
    }
}
