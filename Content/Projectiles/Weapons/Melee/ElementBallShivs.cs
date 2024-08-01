using CalamityMod;
using CalamityMod.Projectiles.Melee;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class ElementBallShivs : ModProjectile
    {
        public new string LocalizationCategory => "Projectiles.Melee";

        public override string Texture => CWRConstant.Cay_Proj_Melee + "ElementBallShiv";

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 2;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 60;
            Projectile.aiStyle = 27;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            int num = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.RainbowTorch, Projectile.direction * 2, 0f, 150, new Color(Main.DiscoR, Main.DiscoG, Main.DiscoB), 1.3f);
            Main.dust[num].noGravity = true;
            Main.dust[num].velocity = Vector2.Zero;
            CalamityUtils.HomeInOnNPC(Projectile, ignoreTiles: true, 300f, 12f, 20f);
        }

        public override bool PreDraw(ref Color lightColor) {
            return true;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 2; i++) {
                int num = Dust.NewDust(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height, DustID.RainbowTorch, Projectile.direction * 2, 0f, 150, new Color(Main.DiscoR, Main.DiscoG, Main.DiscoB));
                Main.dust[num].noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            OnHitEffects(target.Center);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            OnHitEffects(target.Center);
        }

        private void OnHitEffects(Vector2 targetPos) {
            IEntitySource source_FromThis = Projectile.GetSource_FromThis();
            for (int i = 0; i < 3; i++) {
                if (Projectile.owner == Main.myPlayer) {
                    CalamityUtils.ProjectileBarrage(source_FromThis, Projectile.Center, targetPos
                        , i > 2, 800f, 800f, 0f, 800f, 20f, ModContent.ProjectileType<SHIV>()
                        , Projectile.damage, Projectile.knockBack, Projectile.owner, clamped: false, 50f);
                }
            }
        }
    }
}
