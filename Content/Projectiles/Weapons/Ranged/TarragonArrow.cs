using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Projectiles;
using CalamityMod;
using CalamityOverhaul.Common;
using System;
using Terraria.Audio;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using CalamityMod.Projectiles.Ranged;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class TarragonArrow : ModProjectile
    {
        public override string Texture => "CalamityMod/Items/Weapons/Rogue/TarragonThrowingDart";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 15;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.arrow = true;
            Projectile.penetrate = 3;
            Projectile.MaxUpdates = 3;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15 * Projectile.MaxUpdates;
        }

        public override void AI() {
            Projectile.penetrate = (int)Projectile.ai[0] * 2 + 3;
            Projectile.tileCollide = !(Projectile.ai[0] == 2);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            Lighting.AddLight(Projectile.Center, Color.Green.ToVector3() * 2);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(70, 600);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(70, 600);
        }

        public override void OnKill(int timeLeft) {
            if (Projectile.IsOwnedByLocalPlayer()) {
                for (int i = 0; i < 3; i++) {
                    Vector2 vr = Projectile.velocity.RotatedBy((-1 + i) * 0.3f) * -1.2f;
                    Projectile.NewProjectile(Projectile.parent(), Projectile.Center, vr
                        , ModContent.ProjectileType<NeedlerProj>(), Projectile.damage / 2, Projectile.knockBack / 2, Projectile.owner);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            CalamityUtils.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], lightColor, 1);
            return false;
        }
    }
}
