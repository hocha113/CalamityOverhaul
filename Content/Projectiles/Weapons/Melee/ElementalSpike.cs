using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class ElementalSpike : ModProjectile
    {
        public override string Texture {
            get {
                switch (Status) {
                    case 0:
                        return CWRConstant.Projectile_Melee + "Spikes/" + "RedsSpike";
                    case 1:
                        return CWRConstant.Projectile_Melee + "Spikes/" + "BluesSpike";
                    case 2:
                        return CWRConstant.Projectile_Melee + "Spikes/" + "GoldsSpike";
                    default:
                        return CWRConstant.Projectile_Melee + "Spikes/" + "GreenSpike";
                }
            }
        }

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = 48;
            Projectile.height = 48;
            Projectile.scale = 1.5f;
            Projectile.alpha = 100;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Default;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 320;
            Projectile.extraUpdates = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public ref float Status => ref Projectile.ai[0];

        public override void AI() {
            NPC target = CWRUtils.GetNPCInstance((int)Projectile.ai[1]);
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (target != null && Projectile.timeLeft < 220) {
                Vector2 toTarget = Projectile.Center.To(target.Center);
                Projectile.EntityToRot(toTarget.ToRotation(), 0.1f);
                Projectile.velocity = Projectile.rotation.ToRotationVector2() * Projectile.velocity.Length();
                Projectile.velocity *= 1.001f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitNPC(target, hit, damageDone);
        }

        public override Color? GetAlpha(Color lightColor) {
            if (Projectile.timeLeft < 35) {
                byte b = (byte)(Projectile.timeLeft * 3);
                byte alpha = (byte)(100f * (b / 255f));
                return new Color(b, b, b, alpha);
            }

            return new Color(255, 255, 255, 100);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D mainValue = CWRUtils.GetT2DValue(Texture);
            Main.EntitySpriteDraw(
                mainValue,
                CWRUtils.WDEpos(Projectile.Center),
                null,
                Projectile.GetAlpha(lightColor),
                Projectile.rotation + MathHelper.PiOver4,
                CWRUtils.GetOrig(mainValue),
                Projectile.scale,
                SpriteEffects.None,
                0
                );

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.AnisotropicWrap, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.ZoomMatrix);

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                Main.EntitySpriteDraw(
                mainValue,
                CWRUtils.WDEpos(Projectile.oldPos[i] + Projectile.Center - Projectile.position),
                null,
                Projectile.GetAlpha(lightColor),
                Projectile.rotation + MathHelper.PiOver4,
                CWRUtils.GetOrig(mainValue),
                Projectile.scale - i * 0.1f,
                SpriteEffects.None,
                0
                );
            }

            Main.spriteBatch.ResetBlendState();
            return false;
        }
    }
}
