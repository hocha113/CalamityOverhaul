using CalamityMod.Particles;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Melee;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.MurasamaProj
{
    internal class MurasamaDownSkill : MurasamaBreakOut
    {
        public override void AI() {
            if (Projectile.IsOwnedByLocalPlayer()) {
                if (Owner.PressKey()) {
                    Projectile.Kill();
                    return;
                }
            }
            Lighting.AddLight(Projectile.Center, (Main.rand.NextBool(3) ? Color.Red : Color.IndianRed).ToVector3());
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.Pi;
            Owner.Center = Vector2.Lerp(Owner.Center, Projectile.Center, 0.15f);
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, MathHelper.ToRadians(0) * -Owner.direction);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, MathHelper.ToRadians(0) * -Owner.direction);
            //设置玩家的不可击退性并给予玩家短暂的无敌帧
            Owner.GivePlayerImmuneState(5);

            if (!CWRUtils.isServer) {
                AltSparkParticle spark = new AltSparkParticle(
                    Projectile.Center + Main.rand.NextVector2Circular(Projectile.width * 0.5f, Projectile.height * 0.5f) + Projectile.velocity * 1.2f
                    , Projectile.velocity
                    , false, 13, Main.rand.NextFloat(1.3f), Main.rand.NextBool(3) ? Color.Red : Color.IndianRed);
                GeneralParticleHandler.SpawnParticle(spark);
            }
        }

        private void SpanDust() {
            SoundEngine.PlaySound(MurasamaEcType.BigSwing with { Volume = 0.1f }, Projectile.Center);
            for (int i = 0; i < 133; i++) {
                AltSparkParticle spark = new AltSparkParticle(
                    Projectile.Center + Main.rand.NextVector2Circular(Projectile.width * 0.5f, Projectile.height * 0.5f) + Projectile.velocity * 1.2f
                    , CWRUtils.GetRandomVevtor(10, -170, Main.rand.Next(13, 33))
                    , false, 13, Main.rand.NextFloat(3.3f), Main.rand.NextBool(3) ? Color.Red : Color.IndianRed);
                GeneralParticleHandler.SpawnParticle(spark);
            }
        }

        private void OnHit() {
            SoundEngine.PlaySound(MurasamaEcType.InorganicHit, Projectile.Center);
            SpanDust();
            Projectile.Explode();

            if (CWRServerConfig.Instance.ScreenVibration) {
                PunchCameraModifier modifier2 = new PunchCameraModifier(Projectile.Center, new Vector2(0, Main.rand.NextFloat(-2, 2)), 10f, 30f, 20, 1000f, FullName);
                Main.instance.CameraModifiers.Add(modifier2);
            }
            Owner.GivePlayerImmuneState(25);
            if (Projectile.IsOwnedByLocalPlayer() && Owner.velocity.Y > -33) {//需要增加一个速度上限，否则在同时击中极多数量目标的情况下会让玩家飞的极其之高
                Owner.velocity += new Vector2(Projectile.velocity.X, -13);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.numHits == 0)
                OnHit();
            Projectile.Kill();
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.numHits == 0)
                OnHit();
            return true;
        }
    }
}
