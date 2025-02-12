using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.MeleeModify.Core;
using System.Linq;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class BrimstoneSwordHeld : BaseKnife
    {
        public override string Texture => "CalamityMod/Items/Weapons/Melee/BrimstoneSword";
        public override int TargetID => ModContent.ItemType<BrimstoneSword>();
        private bool trueMelee;
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 24;
            Length = 40;
            autoSetShoot = true;
        }

        public override bool PreSwingAI() {
            StabBehavior(initialLength: 40, lifetime: maxSwingTime, scaleFactorDenominator: 220f, minLength: 40, maxLength: 60, ignoreUpdateCount: true);
            return false;
        }

        public override void Shoot() {
            if (trueMelee) {
                return;
            }
            Projectile.NewProjectile(Source, ShootSpanPos, AbsolutelyShootVelocity
                    , ModContent.ProjectileType<BrimstoneSwordBall>(), Projectile.damage / 2, Projectile.knockBack / 2, Main.myPlayer);
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            trueMelee = true;
            if (CWRLoad.WormBodys.Contains(target.type) && !Main.rand.NextBool(5)) {
                return;
            }
            target.AddBuff(ModContent.BuffType<BrimstoneFlames>(), 300);
            for (int i = 0; i < 6; i++) {
                Vector2 spanPos = new(ShootSpanPos.X + Main.rand.Next(-60, 60), ShootSpanPos.Y);
                Projectile.NewProjectile(Source, spanPos, new Vector2(Main.rand.Next(-20, 20), -12)
                    , ModContent.ProjectileType<Brimblast>(), Projectile.damage, Projectile.knockBack, Main.myPlayer);
            }
        }
    }
}
