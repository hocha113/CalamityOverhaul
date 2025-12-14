using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class JudgementBeam : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Melee + "JudgementBeam";
        public Color[] ProjColorDate;
        private int cooldenDamageTime;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 13;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 120;
            Projectile.DamageType = DamageClass.Melee;
            cooldenDamageTime = Main.rand.Next(10);
        }

        public override bool? CanHitNPC(NPC target) {
            if (cooldenDamageTime > 0) {
                return false;
            }
            return base.CanHitNPC(target);
        }

        private void SpawnEffect() {
            if (VaultUtils.isServer) {
                return;
            }

            ProjColorDate ??= CWRUtils.GetColorDate(TextureAssets.Projectile[Type].Value);
            Color color = VaultUtils.MultiStepColorLerp(Projectile.timeLeft / 120f, ProjColorDate);

            for (int i = 0; i < 5; i++) {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.Next(6);
                Vector2 particleSpeed = Projectile.velocity * 0.75f;
                BasePRT lightdust = new PRT_Light(pos, particleSpeed
                    , Main.rand.NextFloat(0.3f, 0.5f), color, 60, 1, 1.5f, hueShift: 0.0f);
                PRTLoader.AddParticle(lightdust);
            }
        }

        public override void AI() {
            cooldenDamageTime--;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            SpawnEffect();

            if (Projectile.timeLeft % 5 == 0) {
                SpawnGemDust(8, 3);
                SpawnGemDust(8, -3);
            }

            if (!VaultUtils.isServer) {
                var dye = GameShaders.Armor.GetShaderFromItemId(Projectile.CWR().DyeItemID);
                for (int i = 0; i < 10; i++) {
                    int shinyDust = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y)
                        , Projectile.width, Projectile.height, DustID.GemDiamond, 0f, 0f, 100, default, 1.25f);
                    Main.dust[shinyDust].noGravity = true;
                    Main.dust[shinyDust].velocity *= 0.5f;
                    Main.dust[shinyDust].velocity += Projectile.velocity * 0.1f;
                    Main.dust[shinyDust].shader = dye;
                }
            }

            CWRRef.HomeInOnNPC(Projectile, true, 350f, 15f, 10f);
        }

        //生成宝石光尘
        public void SpawnGemDust(int count, float velocityMultiplier) {
            var dye = GameShaders.Armor.GetShaderFromItemId(Projectile.CWR().DyeItemID);
            for (int i = 0; i < count; i++) {
                int shinyDust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.GemDiamond, 0f, 0f, 100, Main.DiscoColor, 2.25f);
                Main.dust[shinyDust].noGravity = true;
                Main.dust[shinyDust].velocity = Projectile.velocity.GetNormalVector() * velocityMultiplier;
                Main.dust[shinyDust].velocity += Projectile.velocity * 0.1f;
                Main.dust[shinyDust].shader = dye;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.numHits == 0) {
                SoundEngine.PlaySound(SoundID.Item122, Projectile.position);
                float randNum = Main.rand.NextFloat(MathHelper.TwoPi);
                int type = ModContent.ProjectileType<OrderbringerWhiteOrbs>();
                for (int i = 0; i < 3; i++) {
                    Vector2 vr = (MathHelper.TwoPi / 3f * i + randNum).ToRotationVector2() * 3;
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vr
                        , type, Projectile.damage / 3, Projectile.knockBack, Projectile.owner);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, Color.White
                , Projectile.rotation, value.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
