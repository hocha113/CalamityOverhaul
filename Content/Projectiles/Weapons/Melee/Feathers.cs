using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class Feathers : ModProjectile
    {
        public override string Texture => "CalamityMod/Projectiles/Magic/TradewindsProjectile";
        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 150;
            Projectile.aiStyle = -1;
        }

        public int Status { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        public int Behavior { get => (int)Projectile.ai[1]; set => Projectile.ai[1] = value; }
        public int Time { get => (int)Projectile.ai[2]; set => Projectile.ai[2] = value; }

        private Vector2 hitOffsetVr = Vector2.Zero;
        private float hitOffsetRot = 0;

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (Status == 0) {
                Projectile.velocity += new Vector2(0, 0.1f);
            }
            if (Status == 1) {
                Projectile.velocity *= 1.02f;
                Projectile.scale *= 1.002f;
            }
            if (Status == 2) {
                Projectile.penetrate = 6;
                Projectile.usesLocalNPCImmunity = true;
                Projectile.localNPCHitCooldown = 60;
                if (Projectile.timeLeft <= 60) {
                    Projectile.tileCollide = true;
                }
                if (Behavior == 1) {
                    NPC hitTarget = CWRUtils.GetNPCInstance((int)Projectile.localAI[0]);
                    if (hitTarget.Alives()) {
                        Projectile.velocity = Vector2.Zero;
                        Projectile.Center = hitTarget.Center + hitOffsetVr;
                        Projectile.rotation = hitOffsetRot;
                    }
                    else {
                        Projectile.Kill();
                    }
                }
                if (Projectile.localAI[0] != 0) {
                    Projectile.velocity *= 0.97f;
                }
            }
            if (Status == 3) {
                Projectile.scale *= 1.01f;
                Projectile.velocity *= 1.01f;
                Projectile.alpha -= 30;
                if (Projectile.numHits > 2) {
                    Projectile.Kill();
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Status == 2) {
                if (Behavior == 0) {
                    Projectile.localAI[0] = target.whoAmI;
                    Projectile.usesLocalNPCImmunity = true;
                    Projectile.localNPCHitCooldown = 20;
                    Projectile.timeLeft = 120;
                    hitOffsetVr = target.Center.To(Projectile.Center);
                    hitOffsetRot = Projectile.rotation;
                    Behavior = 1;
                }
                Projectile.damage -= Projectile.damage / 8;
            }
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 10; i++) {
                Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.YellowTorch, Projectile.velocity.X * 0.1f, Projectile.velocity.Y * 0.1f);
            }
            if (Status == 2) {
                Lighting.AddLight(Projectile.Center, Color.Gold.ToVector3());
            }
        }
    }
}
