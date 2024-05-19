using CalamityMod;
using CalamityMod.Graphics.Primitives;
using CalamityOverhaul.Common;
using CalamityOverhaul.Common.Effects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.NeutronBowProjs
{
    internal class EXNeutronArrow : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "NeutronArrow";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.timeLeft = 320;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
            Projectile.light = 0.6f;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ArmorPenetration = 80;
            Projectile.MaxUpdates = 5;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 50;
        }

        public override void AI() {
            if (Projectile.alpha > 0) {
                Projectile.alpha -= 25;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.IsOwnedByLocalPlayer() && !CWRIDs.WormBodys.Contains(target.type)) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center
                    , Vector2.Zero, ModContent.ProjectileType<EXNeutronExplosionRanged>(), Projectile.damage * 3, 0);
            }
        }

        public float PrimitiveWidthFunction(float completionRatio) => Projectile.scale * 30f;

        public Color PrimitiveColorFunction(float _) => Color.AliceBlue * Projectile.Opacity;

        public void DrawTrild() {
            float localIdentityOffset = Projectile.identity * 0.1372f;
            Color mainColor = CalamityUtils.MulticolorLerp((Main.GlobalTimeWrappedHourly * 2f + localIdentityOffset) % 1f, Color.Blue, Color.White, Color.BlueViolet, Color.CadetBlue, Color.DarkBlue);
            Color secondaryColor = CalamityUtils.MulticolorLerp((Main.GlobalTimeWrappedHourly * 2f + localIdentityOffset + 0.2f) % 1f, Color.Blue, Color.White, Color.BlueViolet, Color.CadetBlue, Color.DarkBlue);

            mainColor = Color.Lerp(Color.White, mainColor, 0.85f);
            secondaryColor = Color.Lerp(Color.White, secondaryColor, 0.85f);

            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].SetMiscShaderAsset_1(ModContent.Request<Texture2D>("CalamityMod/ExtraTextures/GreyscaleGradients/EternityStreak"));
            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].UseImage2("Images/Extra_189");
            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].UseColor(mainColor);
            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].UseSecondaryColor(secondaryColor);
            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].Apply();
            PrimitiveRenderer.RenderTrail(Projectile.oldPos, new PrimitiveSettings(PrimitiveWidthFunction, PrimitiveColorFunction
                , (float _) => Projectile.Size * 0.5f, smoothen: true, pixelate: false, GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"]), 53);
        }

        public override bool PreDraw(ref Color lightColor) {
            DrawTrild();

            Texture2D value = Projectile.T2DValue();
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, CWRUtils.GetRec(value, Projectile.frame, 7), Color.White
                , Projectile.rotation, CWRUtils.GetOrig(value, 7), Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
