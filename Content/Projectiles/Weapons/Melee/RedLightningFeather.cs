using CalamityMod.Projectiles.Boss;
using CalamityOverhaul.Content.Particles;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class ElectrifyFeather : ModProjectile, ICWRLoader
    {
        public override string Texture => CWRConstant.Placeholder3;
        private static Asset<Texture2D> FeatherAsset;
        void ICWRLoader.LoadAsset() => FeatherAsset = TextureAssets.Projectile[ModContent.ProjectileType<RedLightningFeather>()];
        void ICWRLoader.UnLoadData() => FeatherAsset = null;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = 38;
            Projectile.height = 38;
            Projectile.alpha = 100;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 120;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public int Status { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        public int Behavior { get => (int)Projectile.ai[1]; set => Projectile.ai[1] = value; }
        public int ThisTimeValue { get => (int)Projectile.ai[2]; set => Projectile.ai[2] = value; }
        public Vector2 DashVr {
            set {
                Projectile.localAI[0] = value.X;
                Projectile.localAI[1] = value.Y;
            }
            get => new Vector2(Projectile.localAI[0], Projectile.localAI[1]);
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 16; i++) {
                    Vector2 particleSpeed = Projectile.velocity * Main.rand.NextFloat(0.7f, 0.9f);
                    Vector2 pos = Projectile.position + new Vector2(Main.rand.Next(Projectile.width), Main.rand.Next(Projectile.height));
                    BasePRT energyLeak = new PRT_Light(pos, particleSpeed
                        , Main.rand.NextFloat(0.2f, 0.5f), Main.rand.NextBool(2) ? Color.Red : Color.Gold, 30, 1, 1.5f, hueShift: 0.0f);
                    PRTLoader.AddParticle(energyLeak);
                }
            }
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            CWRUtils.ClockFrame(ref Projectile.frameCounter, 5, 3);

            if (Status == 0) {
                if (Projectile.timeLeft < 60) {
                    NPC target = Projectile.Center.FindClosestNPC(900, false);
                    if (target != null && Behavior == 0) {
                        Behavior = 1;
                    }
                    if (Behavior == 1) {
                        DashVr = Projectile.Center.To(target.Center).UnitVector() * 37;
                        Behavior = 2;
                    }
                    if (Behavior == 2) {
                        Projectile.velocity = DashVr;
                    }
                }
            }

            if (Main.netMode != NetmodeID.Server) {
                Vector2 particleSpeed = Projectile.velocity * Main.rand.NextFloat(0.7f, 0.9f);
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.Next(16) + Projectile.velocity;
                BasePRT energyLeak = new PRT_Light(pos, particleSpeed
                    , Main.rand.NextFloat(0.2f, 0.3f), Main.rand.NextBool(2) ? Color.Red : Color.Gold, 15, 1, 1.5f, hueShift: 0.0f);
                PRTLoader.AddParticle(energyLeak);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Rectangle rectangle = CWRUtils.GetRec(FeatherAsset.Value, Projectile.frameCounter, 4);
            Main.EntitySpriteDraw(FeatherAsset.Value, Projectile.Center - Main.screenPosition
                , rectangle, Color.White, Projectile.rotation, rectangle.Size() / 2
                , Projectile.scale * 0.6f, SpriteEffects.None, 0);
            return false;
        }
    }
}
