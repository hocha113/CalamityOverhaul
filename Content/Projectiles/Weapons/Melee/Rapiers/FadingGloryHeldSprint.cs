using CalamityMod;
using CalamityMod.Graphics.Primitives;
using CalamityMod.NPCs.SupremeCalamitas;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.Rapiers
{
    internal class FadingGloryHeldSprint : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Melee + "FadingGlory2";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 112;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 4;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.timeLeft = 10;
            Projectile.hide = true;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 60;
        }

        public override void AI() {
            SetHeld();
            if (Owner.PressKey(false)) {
                Projectile.timeLeft = 2;
            }
            if (Projectile.alpha < 255) {
                Projectile.alpha += 55;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            Vector2 toD = Owner.Center + Projectile.velocity.UnitVector() * 33 * Projectile.scale;
            Owner.velocity = Projectile.velocity.UnitVector();
            Owner.Center = Vector2.Lerp(toD, Owner.Center, 0.01f);
            Projectile.Center = Owner.GetPlayerStabilityCenter() + Projectile.velocity.UnitVector() * 46 * Projectile.scale;

            float rot = (MathHelper.PiOver2 * SafeGravDir - Projectile.rotation) * DirSign * SafeGravDir;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, rot * -DirSign);
            Owner.direction = Math.Sign(Projectile.velocity.X);
            Projectile.scale += 0.01f;
            Projectile.ai[0]++;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitNPC(target, hit, damageDone);
        }

        public override void OnKill(int timeLeft) {
            base.OnKill(timeLeft);
        }

        public float PrimitiveWidthFunction(float completionRatio) => Projectile.scale * 20f;

        public Color PrimitiveColorFunction(float _) => Color.Red;

        public void DrawTrild() {
            float localIdentityOffset = Projectile.identity * 0.1372f;
            Color mainColor = CalamityUtils.MulticolorLerp((Main.GlobalTimeWrappedHourly * 2f + localIdentityOffset) % 1f, Color.Red, Color.DarkRed, Color.DarkRed, Color.OrangeRed, Color.IndianRed);
            Color secondaryColor = CalamityUtils.MulticolorLerp((Main.GlobalTimeWrappedHourly * 2f + localIdentityOffset + 0.2f) % 1f, Color.DarkRed, Color.IndianRed, Color.OrangeRed, Color.IndianRed, Color.MediumVioletRed);

            mainColor = Color.Lerp(Color.Red, mainColor, 0.85f);
            secondaryColor = Color.Lerp(Color.OrangeRed, secondaryColor, 0.85f);

            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].SetMiscShaderAsset_1(ModContent.Request<Texture2D>(CWRConstant.Placeholder));
            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].UseImage2("Images/Extra_189");
            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].UseColor(mainColor);
            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].UseSecondaryColor(secondaryColor);
            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].Apply();
            PrimitiveRenderer.RenderTrail(Projectile.oldPos, new PrimitiveSettings(PrimitiveWidthFunction, PrimitiveColorFunction
                , (float _) => Projectile.Size * 0.5f, smoothen: true, pixelate: false, GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"]), 53);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(default, default, Main.DefaultSamplerState, default, RasterizerState.CullNone, default, Main.GameViewMatrix.TransformationMatrix);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D mainValue = Projectile.T2DValue();
            Texture2D glow1 = SupremeCalamitas.ShieldTopTexture.Value;
            Texture2D glow2 = SupremeCalamitas.ShieldBottomTexture.Value;
            DrawTrild();
            Main.spriteBatch.Draw(mainValue, Projectile.Center - Main.screenPosition
                , null, Color.White * (Projectile.alpha / 255f)
                , Projectile.rotation + (Projectile.velocity.X > 0 ? MathHelper.ToRadians(60) : MathHelper.ToRadians(-240))
                , mainValue.Size() / 2, Projectile.scale
                , Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0);

            float offetRoty = 0;
            Vector2 offsetTopDrawPos = new Vector2(0, 33 * (Projectile.velocity.X > 0 ? -1 : 1)).RotatedBy(Projectile.rotation);
            Main.spriteBatch.Draw(glow1, Projectile.Center - Main.screenPosition + offsetTopDrawPos
                , null, Color.White * (Projectile.alpha / 255f)
                , Projectile.rotation + (Projectile.velocity.X > 0 ? MathHelper.ToRadians(0 + offetRoty) : MathHelper.ToRadians(-180 - offetRoty))
                , glow1.Size() / 2, 1, Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0);

            Vector2 offsetBtomDrawPos = new Vector2(0, -23 * (Projectile.velocity.X > 0 ? -1 : 1)).RotatedBy(Projectile.rotation);
            Main.spriteBatch.Draw(glow2, Projectile.Center - Main.screenPosition + offsetBtomDrawPos
                , null, Color.White * (Projectile.alpha / 255f)
                , Projectile.rotation + (Projectile.velocity.X > 0 ? MathHelper.ToRadians(0 - offetRoty) : MathHelper.ToRadians(-180 + offetRoty))
                , glow2.Size() / 2, 1, Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0);

            return false;
        }
    }
}
