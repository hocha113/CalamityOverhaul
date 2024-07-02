using CalamityMod.Particles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic
{
    internal abstract class BaseBloomOnSpan : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;
        public float ChargeValue;
        public float BloomSize = 1;
        public float toMouLeng;
        public float norlLeng;
        public float MaxCharge = 90f;
        public Vector2 OffsetPos = Vector2.Zero;
        public virtual float ChargeProgress {
            get {
                if (ChargeValue > MaxCharge) {
                    ChargeValue = MaxCharge;
                }
                return ChargeValue / MaxCharge;
            }
        }
        protected bool onFire;
        protected Color color1 = Color.Lime;
        protected Color color2 = Color.White;
        protected bool rightControl;

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.alpha = 255;
            Projectile.extraUpdates = 1;
            Projectile.penetrate = 1;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.light = 0.2f;
            SetBloom();
            Projectile.timeLeft = (int)MaxCharge;
        }

        public virtual void SetBloom() {

        }

        public sealed override bool? CanDamage() => false;

        /// <summary>
        /// 需要使用弹幕的AI[1]作为跟随弹幕的索引
        /// </summary>
        /// <param name="projectile"></param>
        public void FlowerAI(Projectile projectile) {
            Projectile owner = null;
            if (projectile.ai[1] >= 0 && projectile.ai[1] < Main.maxProjectiles) {
                owner = Main.projectile[(int)projectile.ai[1]];
            }
            if (owner == null) {
                projectile.Kill();
                return;
            }
            Vector2 norlVr = (Projectile.rotation + (Owner.direction > 0 ? MathHelper.PiOver2 : -MathHelper.PiOver2)).ToRotationVector2();
            OffsetPos = Projectile.rotation.ToRotationVector2() * toMouLeng + norlVr * norlLeng;
            projectile.Center = owner.Center + OffsetPos;
            projectile.rotation = owner.rotation;
        }

        public sealed override void AI() {
            FlowerAI(Projectile);
            if (rightControl ? DownRight : DownLeft) {
                ChargeValue++;
                if (++ChargeValue >= MaxCharge) {
                    onFire = true;
                }
                SpanGenericBloom();
                if (onFire && Projectile.IsOwnedByLocalPlayer()) {
                    SpanProjFunc((int)ChargeValue);
                }
            }
            else {
                Projectile.Kill();
            }

        }

        public virtual void SpanGenericBloom() {
            Particle orb = new GenericBloom(Projectile.Center, Projectile.velocity, color1, ChargeProgress * BloomSize, 2, false);
            GeneralParticleHandler.SpawnParticle(orb);
            Particle orb2 = new GenericBloom(Projectile.Center, Projectile.velocity, color2, ChargeProgress * BloomSize, 2, false);
            GeneralParticleHandler.SpawnParticle(orb2);
        }

        public virtual void SpanProjFunc(int time) {

        }

        public virtual void SpanProjFuncInKill() {
            SoundEngine.PlaySound(SoundID.Item96, Projectile.Center);
        }

        public sealed override void OnKill(int timeLeft) {
            if (Projectile.IsOwnedByLocalPlayer() && onFire) {
                SpanProjFuncInKill();
            }
        }
    }
}
