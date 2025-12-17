using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class ExtremeColdHail : ModProjectile
    {
        public override bool IsLoadingEnabled(Mod mod) => CWRRef.Has;
        public override string Texture => CWRConstant.Cay_Proj_Ranged + "FlurrystormIceChunk";
        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 24;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 180;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 10;
        }

        public override bool PreAI() {
            if (Projectile.knockBack == 0f)
                Projectile.hostile = true;
            else Projectile.friendly = true;
            return true;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                SoundEngine.PlaySound(SoundID.Item89 with { Volume = 0.4f }, Projectile.position);
            }
            Projectile.localAI[0]++;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (Main.rand.NextBool(6)) {
                Projectile.NewProjectileDirect(Projectile.FromObjectGetParent(), Projectile.Center, Projectile.velocity * 0.1f, ModContent.ProjectileType<IceExplosionFriend>(), 13, 0, Projectile.owner, 0);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(CWRID.Buff_GlacialState, 30);
            target.AddBuff(BuffID.Frostburn2, 180);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(CWRID.Buff_GlacialState, 30);
            target.AddBuff(BuffID.Frostburn2, 180);
            if (Projectile.hostile && VaultUtils.isClient)
                return;
            Projectile.Kill();
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.oldVelocity.Y > 0f && Projectile.velocity.X != 0f) {
                Projectile.velocity.Y = -0.6f * Projectile.oldVelocity.Y;
                Projectile.velocity.X *= 0.975f;
            }
            else if (Projectile.velocity.X == 0f) {
                Projectile.velocity.X = -0.6f * Projectile.oldVelocity.X;
            }
            return false;
        }

        public override Color? GetAlpha(Color drawColor) {
            return Projectile.timeLeft < 30 && Projectile.timeLeft % 10 < 5 ? Color.Orange : Color.White;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(new SoundStyle("CalamityMod/Sounds/NPCHit/CryogenHit", 3) with { Volume = 0.55f }, Projectile.Center);
        }
    }
}
