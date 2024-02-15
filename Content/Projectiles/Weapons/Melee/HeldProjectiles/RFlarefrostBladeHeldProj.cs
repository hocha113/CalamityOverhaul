using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class RFlarefrostBladeHeldProj : BaseSwingProj
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "FlarefrostBlade";

        public ref float Combo => ref Projectile.ai[0];
        public int alpha;

        public override void SetSwingAttribute() {
            Projectile.localNPCHitCooldown = 22;
            Projectile.width = 34;
            Projectile.height = 68;
            Projectile.extraUpdates = 1;
            distanceToOwner = 6;
            minTime = 0;
            onHitFreeze = 4;
        }

        protected override void Initializer() {
            alpha = 0;
            if (Main.myPlayer == Projectile.owner)
                Owner.direction = Main.MouseWorld.X > Owner.Center.X ? 1 : -1;
            switch (Combo) {
                default:
                case 0:
                case 1:
                    maxTime = Owner.itemTimeMax * 2;
                    startAngle = 2.2f;
                    totalAngle = 4.6f;
                    Smoother = new NoSmoother();
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Owner.Center + UnitToMouseV * 64, UnitToMouseV * 13,
                    ModContent.ProjectileType<Flarefrost>(), Projectile.damage * 2, Projectile.knockBack, Projectile.owner);
                    break;
                case 2:
                    maxTime = Owner.itemTimeMax * 2;
                    startAngle = -1.2f;
                    totalAngle = -4.2f;
                    Smoother = new NoSmoother();
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Owner.Center + UnitToMouseV * 64, UnitToMouseV * 13,
                    ModContent.ProjectileType<Flarefrost>(), Projectile.damage * 2, Projectile.knockBack, Projectile.owner);
                    break;
                case 3: //强化挥舞
                    minTime = 14;
                    maxTime = 18 + Owner.itemTimeMax * 2;
                    startAngle = 2.2f;
                    totalAngle = 4.8f;
                    Smoother = new HeavySmoother();
                    SoundEngine.PlaySound(SoundID.Item1, Projectile.Center);
                    for (int i = 0; i < 3; i++) {
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), Owner.Center + UnitToMouseV * 64
                            , UnitToMouseV.RotatedBy((12 - 12 * i) * CWRUtils.atoR) * 15,
                        ModContent.ProjectileType<Flarefrost>(), Projectile.damage * 2, Projectile.knockBack, Projectile.owner);
                    }
                    break;
            }

            base.Initializer();
        }

        public override void PostAI() {
            base.PostAI();
        }

        protected override float GetStartAngle() => Owner.direction > 0 ? 0f : MathHelper.Pi;

        protected override void BeforeSlash() {
            _Rotation += -Owner.direction * 0.03f;
            Slasher();
        }

        protected override void SpawnDustOnSlash() {
            if (Main.myPlayer == Projectile.owner && Timer == maxTime / 2 && Combo == 3) {
            }
        }

        protected override void AfterSlash() {
            if (alpha > 20)
                alpha -= 5;
            Slasher();
            if (Timer > maxTime + 6)
                Projectile.Kill();
        }

        protected override void CustomDraw(Texture2D mainTex, Vector2 origin, Color lightColor, float extraRot) {
            base.CustomDraw(mainTex, origin, lightColor, extraRot);
        }
    }
}
