using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Particles.Core;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.AnnihilatingUniverseProj;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Utilities;
using System;
using Terraria.Audio;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using CalamityMod;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeavenfallLongbowProj
{
    internal class CelestialVortex : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        float rgs => Projectile.width * Projectile.ai[1] / 40;

        SlotId soundSlot;

        SoundStyle modSoundtyle;
        public override bool IsLoadingEnabled(Mod mod) {
            if (!CWRServerConfig.Instance.AddExtrasContent) {
                return false;
            }
            return base.IsLoadingEnabled(mod);
        }
        public override void SetDefaults() {
            Projectile.height = 24;
            Projectile.width = 24;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 560;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            Player player = CWRUtils.GetPlayerInstance(Projectile.owner);
            Projectile ownerProj = CWRUtils.GetProjectileInstance((int)Projectile.ai[0]);
            if (!player.Alives()) {
                Projectile.Kill();
                return;
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return CWRUtils.CircularHitboxCollision(Projectile.Center, rgs, targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.spriteBatch.EnterShaderRegion();

            Texture2D noiseTexture = ModContent.Request<Texture2D>("CalamityMod/ExtraTextures/GreyscaleGradients/VoronoiShapes").Value;
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Vector2 origin = noiseTexture.Size() * 0.5f;
            GameShaders.Misc["CalamityMod:DoGPortal"].UseOpacity(Projectile.scale);
            GameShaders.Misc["CalamityMod:DoGPortal"].UseColor(Color.DarkBlue);
            GameShaders.Misc["CalamityMod:DoGPortal"].UseSecondaryColor(Color.BlueViolet);
            GameShaders.Misc["CalamityMod:DoGPortal"].Apply();

            float slp = Projectile.ai[1] / 300f + MathF.Sin(Main.GameUpdateCount * CWRUtils.atoR * 2) * 0.1f;
            Main.EntitySpriteDraw(noiseTexture, drawPosition, null, Color.White, 0f, origin, slp, SpriteEffects.None, 0);
            Main.spriteBatch.ExitShaderRegion();

            return false;
        }
    }
}
