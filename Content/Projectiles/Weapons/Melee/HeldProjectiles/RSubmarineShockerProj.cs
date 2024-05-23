using CalamityMod;
using CalamityMod.Projectiles.BaseProjectiles;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.SparkProj;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class RSubmarineShockerProj : BaseShortswordProjectile
    {
        public override LocalizedText DisplayName => CWRUtils.SafeGetItemName<SubmarineShockerEcType>();
        public override string Texture => "CalamityMod/Items/Weapons/Melee/SubmarineShocker";
        private bool trueMelee;
        public override void SetDefaults() {
            Projectile.Size = new Vector2(16);
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.scale = 1f;
            Projectile.DamageType = ModContent.GetInstance<TrueMeleeDamageClass>();
            Projectile.ownerHitCheck = true;
            Projectile.timeLeft = 360;
            Projectile.hide = true;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void SetVisualOffsets() {
            const int HalfSpriteWidth = 32 / 2;
            const int HalfSpriteHeight = 32 / 2;

            int HalfProjWidth = Projectile.width / 2;
            int HalfProjHeight = Projectile.height / 2;

            DrawOriginOffsetX = 0;
            DrawOffsetX = -(HalfSpriteWidth - HalfProjWidth);
            DrawOriginOffsetY = -(HalfSpriteHeight - HalfProjHeight);
        }

        public override void ExtraBehavior() {
            if (Projectile.IsOwnedByLocalPlayer() && Timer == TotalDuration / 2 && !trueMelee) {
                Projectile.NewProjectile(Projectile.parent(), Projectile.Center, Projectile.velocity.RotatedBy(Main.rand.NextFloat(-0.05f, 0.05f)) * 3.5f
                    , ModContent.ProjectileType<LigSpark>(), (int)(Projectile.damage * 0.7f), Projectile.knockBack, Main.myPlayer);
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.CritDamage *= 0.5f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            trueMelee = true;
            var source = Projectile.GetSource_FromThis();
            for (int i = 0; i < 3; i++) {
                int proj = Projectile.NewProjectile(source, target.Center, Main.rand.NextVector2Unit() * Main.rand.Next(2, 5)
                    , ModContent.ProjectileType<SparkBall>(), (int)(Projectile.damage * 0.7f), Projectile.knockBack, Main.myPlayer);
                Main.projectile[proj].penetrate = 2;
                Main.projectile[proj].localNPCHitCooldown = 30;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            trueMelee = true;
            var source = Projectile.GetSource_FromThis();
            for (int i = 0; i < 3; i++) {
                int proj = Projectile.NewProjectile(source, target.Center, Main.rand.NextVector2Unit() * Main.rand.Next(2, 5)
                    , ModContent.ProjectileType<SparkBall>(), (int)(Projectile.damage * 0.7f), Projectile.knockBack, Main.myPlayer);
                Main.projectile[proj].penetrate = 2;
            }
        }
    }
}
