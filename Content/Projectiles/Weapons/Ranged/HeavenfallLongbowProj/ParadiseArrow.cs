using CalamityOverhaul.Content.DamageModify;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeavenfallLongbowProj
{
    internal class ParadiseArrow : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        private Color chromaColor => VaultUtils.MultiStepColorLerp(Projectile.ai[0] % 35 / 35f, HeavenfallLongbow.rainbowColors);
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
        }

        public override void SetDefaults() {
            Projectile.height = 24;
            Projectile.width = 24;
            Projectile.DamageType = EndlessDamageClass.Instance;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.MaxUpdates = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 7 * Projectile.MaxUpdates;
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, chromaColor.ToVector3());
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            NPC target = Projectile.Center.FindClosestNPC(1300);
            if (target != null && Projectile.ai[0] > 30) {
                Projectile.SmoothHomingBehavior(target.Center, 1, 0.3f);
            }

            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                Vector2 vector = Projectile.velocity * 1.05f;
                float slp = Main.rand.NextFloat(0.5f, 0.9f);
                PRTLoader.AddParticle(new PRT_HeavenStar(Projectile.Center, vector, Color.White
                    , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), HeavenfallLongbow.rainbowColors), 0f, new Vector2(0.6f, 1f) * slp
                    , new Vector2(1.5f, 2.7f) * slp, 20 + Main.rand.Next(6), 0f, 3f, 0f, Main.rand.Next(7) * 2, Main.rand.NextFloat(-0.3f, 0.3f)));
            }

            Projectile.ai[0]++;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < 3; i++) {
                float slp = Main.rand.NextFloat(0.5f, 1.2f);
                PRTLoader.AddParticle(new PRT_StarPulseRing(target.Center + Main.rand.NextVector2Unit() * Main.rand.Next(13, 330), Vector2.Zero, VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(1), HeavenfallLongbow.rainbowColors), 0.05f * slp, 0.8f * slp, 8));
            }
            Projectile.timeLeft -= 15;
            if (Projectile.timeLeft <= 0)
                Projectile.timeLeft = 0;
        }

        public override void OnKill(int timeLeft) {
            Projectile.Explode(300);
        }

        public override bool PreDraw(ref Color lightColor) {
            float localIdentityOffset = Projectile.identity * 0.1372f;
            Color mainColor = VaultUtils.MultiStepColorLerp((Main.GlobalTimeWrappedHourly * 2f + localIdentityOffset) % 1f, HeavenfallLongbow.rainbowColors);
            Color secondaryColor = VaultUtils.MultiStepColorLerp((Main.GlobalTimeWrappedHourly * 2f + localIdentityOffset + 0.2f) % 1f, HeavenfallLongbow.rainbowColors);

            mainColor = Color.Lerp(Color.White, mainColor, 0.85f);
            secondaryColor = Color.Lerp(Color.White, secondaryColor, 0.85f);

            Vector2 scale = new Vector2(0.5f, 1.6f) * Projectile.scale;
            Texture2D texture = CWRUtils.GetT2DAsset(CWRConstant.Masking + "Extra_98").Value;

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, mainColor, Projectile.rotation, texture.Size() * 0.5f, scale, 0, 0f);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, secondaryColor, Projectile.rotation, texture.Size() * 0.5f, scale * new Vector2(0.45f, 1f), 0, 0f);

            return false;
        }
    }
}
