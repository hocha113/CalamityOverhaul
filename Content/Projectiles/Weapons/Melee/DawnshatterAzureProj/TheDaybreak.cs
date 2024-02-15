using CalamityOverhaul.Common;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using CalamityMod.NPCs.Yharon;
using Terraria.Audio;
using CalamityMod;
using System;
using Terraria.Graphics.Shaders;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.DawnshatterAzureProj
{
    internal class TheDaybreak : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Melee + "Daybreak";
        internal PrimitiveTrail TailDrawer;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 13;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 62;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.MaxUpdates = 2;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            if (Projectile.ai[0] % 100 == 0 && Projectile.ai[0] > 0) {
                Projectile.velocity *= -1;
            }
            Projectile.ai[0]++;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.numHits == 0) {
                target.CWR().TheEndSunOnHitNum = true;
                SoundEngine.PlaySound(Yharon.ShortRoarSound, target.position);
            }
        }

        public float PrimitiveWidthFunction(float completionRatio) => CalamityUtils.Convert01To010(completionRatio) * Projectile.scale * Projectile.width * 0.6f;

        public Color PrimitiveColorFunction(float completionRatio) {
            float colorInterpolant = (float)Math.Sin(Projectile.identity / 3f + completionRatio * 20f + Main.GlobalTimeWrappedHourly * 1.1f) * 0.5f + 0.5f;
            Color color = CalamityUtils.MulticolorLerp(colorInterpolant, Color.Gold, Color.Red, Color.DarkRed);
            return color;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (TailDrawer is null)
                TailDrawer = new PrimitiveTrail(PrimitiveWidthFunction, PrimitiveColorFunction, PrimitiveTrail.RigidPointRetreivalFunction, GameShaders.Misc["CalamityMod:HeavenlyGaleLightningArc"]);

            GameShaders.Misc["CalamityMod:HeavenlyGaleLightningArc"].UseImage1("Images/Misc/noise");
            GameShaders.Misc["CalamityMod:HeavenlyGaleLightningArc"].Apply();

            TailDrawer.Draw(Projectile.oldPos, Projectile.Size * 0.5f - Main.screenPosition, 30);

            Texture2D value = TextureAssets.Projectile[Type].Value;
            Vector2 orig = value.Size() / 2;
            Vector2 offset = Projectile.Size / 2f - Main.screenPosition;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                float sengs = (i / (float)Projectile.oldPos.Length);
                Main.EntitySpriteDraw(value, Projectile.oldPos[i] + offset, null, Color.White * sengs, Projectile.rotation, orig, Projectile.scale * sengs, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
