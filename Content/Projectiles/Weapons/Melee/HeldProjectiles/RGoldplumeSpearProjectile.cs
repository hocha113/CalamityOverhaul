using CalamityMod.Projectiles.BaseProjectiles;
using CalamityOverhaul.Content.Items.Melee;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class RGoldplumeSpearProjectile : BaseSpearProjectile
    {
        public override LocalizedText DisplayName => CWRUtils.SafeGetItemName<GoldplumeSpearEcType>();

        public override string Texture => CWRConstant.Cay_Proj_Melee + "Spears/" + "GoldplumeSpearProjectile";

        public override float InitialSpeed => 2f;

        public override float ReelbackSpeed => 1.1f;

        public override float ForwardSpeed => 0.5f;

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.timeLeft = 90;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        private ref float Status => ref Projectile.ai[1];

        private ref float Rots => ref Projectile.localAI[1];

        private ref float Time => ref Projectile.localAI[2];

        private Player owners => CWRUtils.GetPlayerInstance(Projectile.owner);

        public override void AI() {
            if (Status == 0)
                base.AI();
            if (Status == 1) {
                Projectile.velocity = Vector2.Zero;

                if (owners == null) {
                    Projectile.Kill();
                    return;
                }
                if (Projectile.IsOwnedByLocalPlayer()) {
                    StickToOwner();
                    SpanProj();
                }
                Projectile.rotation = Rots;
                Projectile.Center = owners.Center + Rots.ToRotationVector2() * 32;
            }

            Time++;
        }

        public void SpanProj() {
            if (Time % 30 == 0 && Time > 0) {
                for (int i = 0; i < 3; i++) {
                    Vector2 spanPos = Main.MouseWorld
                        + MathHelper.ToRadians(Main.rand.NextFloat(-110, -70)).ToRotationVector2() * Main.rand.Next(670, 780);
                    Projectile proj = Projectile.NewProjectileDirect(
                        owners.parent(),
                        spanPos,
                        spanPos.To(Main.MouseWorld).UnitVector() * 15,
                        ModContent.ProjectileType<Feathers>(),
                        Projectile.damage / 3,
                        1,
                        Projectile.owner,
                        2
                        );
                    proj.netUpdate = true;
                    proj.tileCollide = false;
                }
            }
        }

        public void StickToOwner() {
            Vector2 toMous = owners.Center.To(Main.MouseWorld);
            Rots = toMous.ToRotation();
            owners.direction = toMous.X > 0 ? 1 : -1;
            owners.heldProj = Projectile.whoAmI;
            owners.SetDummyItemTime(2);
            if (owners.PressKey(false)) {
                Projectile.timeLeft = 2;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < 3; i++) {
                Vector2 spanPos = target.Center
                    + MathHelper.ToRadians(Main.rand.NextFloat(-110, -70)).ToRotationVector2() * Main.rand.Next(300, 320);
                Projectile proj = Projectile.NewProjectileDirect(
                    owners.parent(),
                    spanPos,
                    spanPos.To(target.Center).UnitVector() * 25,
                    ModContent.ProjectileType<Feathers>(),
                    Projectile.damage / 2,
                    1,
                    Projectile.owner,
                    3
                    );

                proj.scale = 1.5f;
                proj.alpha = 200;
                proj.tileCollide = false;
                proj.netUpdate = true;
            }
        }

        public override void ExtraBehavior() {
            if (Main.rand.NextBool(5)) {
                Dust.NewDust(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height, DustID.BlueTorch, Projectile.velocity.X * 0.5f, Projectile.velocity.Y * 0.5f);
            }

            Projectile.localAI[0] += 1f;
            if (Projectile.localAI[0] >= 6f) {
                Projectile.localAI[0] = 0f;
                if (Main.myPlayer == Projectile.owner) {
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + Projectile.velocity, Projectile.velocity, ModContent.ProjectileType<Feathers>(), (int)(Projectile.damage * 0.4), 0f, Projectile.owner, 1);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Status == 0)
                base.PreDraw(ref lightColor);
            if (Status == 1) {
                Texture2D value = CWRUtils.GetT2DValue(Texture);
                Main.EntitySpriteDraw(
                    value,
                    Projectile.Center - Main.screenPosition,
                    null,
                    Color.White,
                    Projectile.rotation + MathHelper.PiOver2 + MathHelper.PiOver4,
                    CWRUtils.GetOrig(value),
                    Projectile.scale,
                    SpriteEffects.None,
                    0
                    );
            }
            return false;
        }
    }
}
