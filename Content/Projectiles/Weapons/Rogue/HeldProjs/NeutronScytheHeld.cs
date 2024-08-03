using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.NeutronBowProjs;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs
{
    internal class NeutronScytheHeld : BaseThrowable
    {
        public override string Texture => CWRConstant.Item + "Rogue/NeutronScythe";

        private Vector2 orig = Vector2.Zero;
        private int fireIndex;
        private int fireIndex2;
        public override void SetThrowable() {
            HandOnTwringMode = -30;
            TotalLifetime = 1200;
            Projectile.timeLeft = TotalLifetime + ChargeUpTime;
        }

        public override void PostSetThrowable() {
            if (stealthStrike && Projectile.ai[2] == 0) {
                Projectile.scale *= 1.25f;
            }
            orig = Projectile.velocity.X > 0 ? new Vector2(12, 68) : new Vector2(12, 27);
        }

        public override bool PreThrowOut() {
            if (Projectile.ai[2] == 0) {
                Projectile.extraUpdates = 2;
                if (stealthStrike) {
                    SoundEngine.PlaySound(CWRSound.Pecharge with { Pitch = 0.1f, Volume = 0.8f, MaxInstances = 3 }, Projectile.Center);
                    if (Projectile.IsOwnedByLocalPlayer()) {
                        for (int i = 0; i < 13; i++) {
                            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Owner.Center
                            , (MathHelper.TwoPi / 13f * i).ToRotationVector2() * 6, Type
                            , Projectile.damage / 2, 2, Projectile.owner, 0, 0, 1);
                        }
                    }
                }
            }
            else {
                Projectile.extraUpdates = 1;
            }
            orig = CWRUtils.GetOrig(TextureValue, 18);
            return base.PreThrowOut();
        }

        public override void FlyToMovementAI() {
            Projectile.rotation += Projectile.velocity.X * 0.01f;
            if (++fireIndex2 > 180) {
                if (!Owner.Alives()) {
                    Projectile.Kill();
                }
                Projectile.ChasingBehavior(Owner.Center, 11 + Projectile.ai[2] * 10);
                if (Projectile.Distance(Owner.Center) < 32) {
                    Projectile.Kill();
                }

            }
            if (Projectile.Distance(Owner.Center) < 1200) {
                if (++fireIndex > 15) {
                    NPC target = Projectile.Center.FindClosestNPC(11200);
                    if (target != null) {
                        SoundEngine.PlaySound(CWRSound.Pecharge with {
                            Pitch = -0.6f + Main.rand.NextFloat(-0.1f, 0.2f)
                            ,
                            Volume = 0.5f
                        }, Projectile.Center);
                        if (Projectile.IsOwnedByLocalPlayer()) {
                            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center
                        , Projectile.Center.To(target.Center).UnitVector() * 22
                        , ModContent.ProjectileType<NeutronLaser>(), Projectile.damage / 3, 0);
                        }
                    }
                    if (Projectile.IsOwnedByLocalPlayer()) {
                        int proj = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center
                        , Vector2.Zero, ModContent.ProjectileType<NeutronExplosionRanged>(), Projectile.damage / 2, 0);
                        Main.projectile[proj].DamageType = Projectile.DamageType;
                    }
                    fireIndex = 0;
                }
            }
        }

        public override void PostUpdate() => CWRUtils.ClockFrame(ref Projectile.frame, 5, 17);

        public override void DrawThrowable(Color lightColor) {
            Main.EntitySpriteDraw(TextureValue, Projectile.Center - Main.screenPosition
                , CWRUtils.GetRec(TextureValue, Projectile.frame, 18), lightColor
                , Projectile.rotation + (MathHelper.PiOver4 + OffsetRoting) * (Projectile.velocity.X > 0 ? 1 : -1)
                , orig, Projectile.scale, Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically, 0);
        }
    }
}
