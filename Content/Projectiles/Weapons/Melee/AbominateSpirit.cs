using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class AbominateSpirit : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Magic + "NofaceFire";
        public ref float Status => ref Projectile.ai[0];
        private bool spwan;
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
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.extraUpdates = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            if (!spwan) {
                SoundEngine.PlaySound(SoundID.NPCDeath39, Projectile.Center);
                spwan = true;
            }

            Projectile.rotation = Projectile.velocity.ToRotation();
            CWRUtils.ClockFrame(ref Projectile.frameCounter, 5, 2);

            if (Status == 3) {
                Projectile.timeLeft = 2;
                Projectile.scale *= 1.001f;
                Player target = CWRUtils.GetPlayerInstance(Projectile.owner);
                if (target != null) {
                    float leng = Projectile.Center.To(target.Center).Length();
                    Projectile.ChasingBehavior(target.Center, 6 + leng / 100f);

                    if (leng < 60) {
                        if (Projectile.ai[1] > 10000) {
                            target.Heal(Main.rand.Next(10, 15));
                        }
                        else {
                            target.Heal(Main.rand.Next(1, 3));
                        }

                        for (int i = 0; i < 13; i++) {
                            Vector2 vr = CWRUtils.GetRandomVevtor(0, 360, Main.rand.Next(4, 7));
                            Dust.NewDust(target.Center, 13, 13, DustID.HealingPlus, vr.X, vr.Y);
                        }

                        Projectile.Kill();
                    }
                }
                if (Projectile.scale > 5) {
                    for (int i = 0; i < 13; i++) {
                        Vector2 vr = CWRUtils.GetRandomVevtor(0, 360, Main.rand.Next(4, 7));
                        Dust.NewDust(target.Center, 13, 13, DustID.HealingPlus, vr.X, vr.Y);
                        Projectile.Kill();
                    }
                }
            }

            Projectile.alpha = Projectile.timeLeft < 85 ? Projectile.timeLeft * 3 : 195;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            switch (Status) {
                case 0:
                    target.AddBuff(BuffID.ShadowFlame, 360);
                    target.AddBuff(BuffID.OnFire3, 360);
                    target.AddBuff(BuffID.CursedInferno, 360);
                    break;
                case 1:
                    int types = Main.rand.Next(0, 5);
                    Player owner = CWRUtils.GetPlayerInstance(Projectile.owner);
                    if (owner == null) return;
                    switch (types) {
                        case 0:
                            owner.AddBuff(BuffID.WeaponImbueCursedFlames, 160);
                            break;
                        case 1:
                            owner.AddBuff(BuffID.WeaponImbueFire, 160);
                            break;
                        case 2:
                            owner.AddBuff(BuffID.WeaponImbueIchor, 160);
                            break;
                        case 3:
                            owner.AddBuff(BuffID.WeaponImbuePoison, 160);
                            break;
                        case 4:
                            owner.AddBuff(BuffID.WeaponImbueNanites, 160);
                            break;
                    }
                    break;
                case 2:
                    target.AddBuff(BuffID.BloodButcherer, 360);
                    target.AddBuff(BuffID.Daybreak, 360);
                    break;
            }
            Projectile.damage -= 20;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D mainValue = TextureAssets.Projectile[Type].Value;
            Color color = Status == 0 ? Color.DarkRed : Status == 1 ? Color.DarkGreen : Status == 2 ? Color.Blue : Color.Gold;

            float alp = Projectile.alpha / 255f;
            color = VaultUtils.MultiStepColorLerp(0.5f, color, new Color(255, 255, 255));
            Rectangle rectangle = CWRUtils.GetRec(mainValue, Projectile.frameCounter, 3);

            Main.EntitySpriteDraw(
                mainValue,
                Projectile.Center - Main.screenPosition,
                rectangle,
                color * alp,
                Projectile.rotation - MathHelper.PiOver2,
                rectangle.Size() / 2,
                Projectile.scale,
                SpriteEffects.None,
                0
                );
            return false;
        }
    }
}
