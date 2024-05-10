using CalamityMod.Graphics.Primitives;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Items.Melee;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class TerratomereBigSlashs : ModProjectile
    {
        public int TargetIndex = -1;

        public new string LocalizationCategory => "Projectiles.Melee";

        public override string Texture => "CalamityMod/Projectiles/InvisibleProj";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 28;
        }

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 27;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Projectile.scale = Utils.GetLerpValue(0f, 8f, Projectile.timeLeft, clamped: true);
        }

        public float SlashWidthFunction(float _) {
            return Projectile.width * Projectile.scale * Utils.GetLerpValue(0f, 0.1f, _, clamped: true);
        }

        public Color SlashColorFunction(float _) {
            return Color.Lime * Projectile.Opacity;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            TargetIndex = target.whoAmI;
            target.CWR().TerratomereBoltOnHitNum++;
            if (target.CWR().TerratomereBoltOnHitNum > 6)
                target.CWR().TerratomereBoltOnHitNum = 0;
            target.netUpdate = true;
            if (target.CWR().TerratomereBoltOnHitNum > 5 && Main.player[Projectile.owner].ownedProjectileCounts[ModContent.ProjectileType<TerratomereExplosion>()] <= 3) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero, ModContent.ProjectileType<TerratomereExplosion>(), Projectile.damage, Projectile.knockBack, Projectile.owner);
                if (Projectile.timeLeft > 30) {
                    Projectile.timeLeft = 30;
                }
                Projectile.velocity *= 0.2f;
                Projectile.damage = 0;
                Projectile.netUpdate = true;
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.myPlayer == Projectile.owner && TargetIndex >= 0) {
                int types = ModContent.ProjectileType<TerratomereSlashCreator>();
                if (Main.npc[TargetIndex].CWR().TerratomereBoltOnHitNum > 5 && Main.player[Projectile.owner].ownedProjectileCounts[types] < 3)
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Main.npc[TargetIndex].Center, Vector2.Zero
                        , types, Projectile.damage, Projectile.knockBack, Projectile.owner, TargetIndex, Main.rand.NextFloat(MathF.PI * 2f));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            GameShaders.Misc["CalamityMod:ExobladePierce"].SetMiscShaderAsset_1(ModContent.Request<Texture2D>("CalamityMod/ExtraTextures/GreyscaleGradients/BlobbyNoise"));
            GameShaders.Misc["CalamityMod:ExobladePierce"].UseImage2("Images/Extra_189");
            GameShaders.Misc["CalamityMod:ExobladePierce"].UseColor(TerratomereEcType.TerraColor1);
            GameShaders.Misc["CalamityMod:ExobladePierce"].UseSecondaryColor(TerratomereEcType.TerraColor2);
            for (int i = 0; i < 4; i++) {
                PrimitiveRenderer.RenderTrail(Projectile.oldPos, new PrimitiveSettings(SlashWidthFunction, SlashColorFunction
                    , (float _) => Projectile.Size * 0.5f, smoothen: true, pixelate: false, GameShaders.Misc["CalamityMod:ExobladePierce"]), 30);
            }

            return false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Projectile.oldPos[0] == Vector2.Zero) {
                return false;
            }

            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.oldPos[0] + Projectile.Size * 0.5f, Projectile.Center);
        }
    }
}
