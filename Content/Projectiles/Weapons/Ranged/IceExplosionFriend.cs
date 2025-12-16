using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class IceExplosionFriend : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => "CalamityMod/Projectiles/Summon/SmallAresArms/MinionPlasmaGas";
        public override bool IsLoadingEnabled(Mod mod) => CWRRef.Has;
        public float randomX;
        public float randomY;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 184;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            Projectile.hide = true;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 20;
            Projectile.localAI[1] = Projectile.timeLeft;
            Projectile.ArmorPenetration = 10;
            randomX = Main.rand.NextFloat(0f, MathHelper.TwoPi);
            randomY = Main.rand.NextFloat(0f, MathHelper.TwoPi);
        }

        public override void AI() {
            Projectile.scale = 0.5f + (Projectile.ai[1] * 0.01f);
            if (Projectile.timeLeft < 30) {
                Projectile.ai[0] = 1;
            }
            Projectile.ai[1]++;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => target.AddBuff(BuffID.Frostburn2, 180);
        public override void OnHitPlayer(Player target, Player.HurtInfo info) => target.AddBuff(BuffID.Frostburn2, 180);
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) => VaultUtils.CircleIntersectsRectangle(Projectile.Center, Projectile.scale * 92f, targetHitbox);
        public override bool? CanDamage() => Projectile.ai[0] == 0 ? null : false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (Projectile.ai[1] < 3) {
                return;
            }
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() * 0.5f;
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Color drawColor = new Color(118, 217, 222) * (Projectile.timeLeft / Projectile.localAI[1]) * 0.9f;
            Vector2 scale = Projectile.Size / texture.Size() * Projectile.scale * 1.35f;
            spriteBatch.Draw(texture, drawPosition, null, drawColor, randomX, origin, scale, 0, 0f);
            spriteBatch.Draw(texture, drawPosition, null, drawColor, randomY, origin, scale, 0, 0f);
        }
    }
}
