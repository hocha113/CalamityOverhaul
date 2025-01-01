using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Particles;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.Neutrons
{
    internal class NeutronGlaiveBeam : ModProjectile, IDrawWarp, ICWRLoader
    {
        public override string Texture => CWRConstant.Projectile_Melee + "NeutronGlaiveBeam";
        public static int PType;
        internal static Asset<Texture2D> warpTex;
        public void SetupData() {
            PType = ModContent.ProjectileType<NeutronGlaiveBeam>();
            warpTex = CWRUtils.GetT2DAsset(CWRConstant.Masking + "DiffusionCircle");
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.timeLeft = 120;
            Projectile.MaxUpdates = 3;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, Color.White.ToVector3() * 0.3f);

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;

            Projectile.ai[0] += 0.05f;
            if (Projectile.ai[0] > 0.3f) {
                Projectile.ai[0] = 0.3f;
            }
            if (Projectile.timeLeft > 15) {
                Projectile.localAI[0] += 0.15f;
                if (Projectile.localAI[0] > 0.3f) {
                    Projectile.localAI[0] = 0.3f;
                }
                Projectile.ai[1] += 0.2f;
                if (Projectile.ai[1] > 0.3f) {
                    Projectile.ai[1] = 0.3f;
                }
            }
            else {
                Projectile.localAI[0] -= 0.03f;
                Projectile.ai[1] -= 0.066f;
            }

            Projectile.localAI[1] += 0.07f;

            float rot = Main.rand.NextFloat(6.282f);
            for (int i = 0; i < 2; i++) {
                Vector2 dir = rot.ToRotationVector2();
                Vector2 vel = dir.RotatedBy(1.57f) * Main.rand.NextFloat(1.3f, 2.5f) + Projectile.velocity;
                Dust dust = Dust.NewDustPerfect(Projectile.Center + dir * Main.rand.Next(3, 10)
                    , DustID.Granite, vel, Scale: Main.rand.NextFloat(1.4f, 1.6f));
                dust.noGravity = true;

                rot = Main.rand.NextFloat(MathHelper.TwoPi);
            }

            if (++Projectile.localAI[2] > 2) {
                for (int i = 0; i < 4; i++) {
                    float rot1 = MathHelper.PiOver2 * i;
                    Vector2 vr = rot1.ToRotationVector2();
                    for (int j = 0; j < 3; j++) {
                        BasePRT spark = new PRT_HeavenfallStar(Projectile.Center
                            , vr * (0.1f + i * 0.14f), false, 17, Main.rand.NextFloat(0.2f, 0.3f), Color.BlueViolet);
                        PRTLoader.AddParticle(spark);
                    }
                }
                Projectile.localAI[2] = 0;
            }
        }

        public override void OnKill(int timeLeft) {
            Projectile.Explode(300, SoundID.Item14 with { Pitch = 0.45f });
            Vector2 randpos = CWRUtils.randVr(64);
            Projectile.Center += randpos;
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero
                , ModContent.ProjectileType<NeutronExplode>(), Projectile.damage, 0);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.velocity = oldVelocity * -0.6f;
            for (int j = 0; j < 73; j++) {
                BasePRT spark = new PRT_HeavenfallStar(Projectile.Center + oldVelocity
                    , oldVelocity.RotatedByRandom(0.3f) * -Main.rand.NextFloat(0.3f, 1.1f)
                    , false, 7, Main.rand.NextFloat(0.5f, 0.7f), Color.LightBlue);
                PRTLoader.AddParticle(spark);
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) => false;
        bool IDrawWarp.canDraw() => true;
        void IDrawWarp.Warp() {
            Color warpColor = new Color(45, 45, 45) * Projectile.ai[1];
            Vector2 orig = warpTex.Size() / 2;
            for (int i = 0; i < 3; i++) {
                Main.spriteBatch.Draw(warpTex.Value, Projectile.Center - Main.screenPosition
                    , null, warpColor, Projectile.ai[0] + i * 2f, orig, Projectile.localAI[0], SpriteEffects.None, 0f);
            }
        }

        public void costomDraw(SpriteBatch spriteBatch) {
            Texture2D mainValue = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Rectangle rectangle = CWRUtils.GetRec(mainValue);
            Vector2 orig = rectangle.Size() / 2;
            float rot = Projectile.rotation;

            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 offsetPos = Projectile.oldPos[k].To(Projectile.position);
                Vector2 drawPos2 = drawPos - offsetPos;
                Color color = Projectile.GetAlpha(Color.Pink) * ((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length);
                Main.EntitySpriteDraw(mainValue, drawPos2, rectangle, color, rot, orig, Projectile.scale, SpriteEffects.None, 0);
            }

            VaultUtils.DrawRotatingMarginEffect(Main.spriteBatch, mainValue, Projectile.timeLeft, drawPos, rectangle, Color.Blue, rot, orig, Projectile.scale, 0);
            Main.EntitySpriteDraw(mainValue, Projectile.Center - Main.screenPosition, rectangle
                , Color.White, Projectile.rotation, orig, Projectile.scale, SpriteEffects.None, 0);
        }
    }
}
