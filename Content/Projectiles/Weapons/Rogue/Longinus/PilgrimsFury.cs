using CalamityOverhaul.Content.Items.Rogue.Extras;
using CalamityOverhaul.Content.Particles;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Rogue.Longinus
{
    internal class PilgrimsFury : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        private NPC Target => Main.npc[(int)Projectile.ai[1]];
        private int Time {
            get => (int)Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }
        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 46;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.DamageType = CWRLoad.RogueDamageClass;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
        }

        public override void AI() {
            if (!Target.Alives()) {
                Projectile.Kill();
                return;
            }

            Projectile.Center = Target.Center;
            Target.CWR().FrozenActivity = true;

            if (Time % 30 == 0) {
                SoundStyle belCanto = new("CalamityOverhaul/Assets/Sounds/BelCanto") { Volume = 1f + Time * 0.05f, Pitch = -0.2f + Time * 0.007f };
                SoundEngine.PlaySound(belCanto, Projectile.Center);
                Vector2 vr = new Vector2(0, 13);
                PRT_LonginusWave pulse = new PRT_LonginusWave(Projectile.Center + new Vector2(0, -360), vr, Color.Gold, new Vector2(1.2f, 3f), vr.ToRotation(), 0.42f, 0.82f + (Time * 0.002f), 180, Projectile);
                PRTLoader.AddParticle(pulse);
                Vector2 vr2 = new Vector2(0, -13);
                PRT_LonginusWave pulse2 = new PRT_LonginusWave(Projectile.Center + new Vector2(0, 360), vr2, Color.Gold, new Vector2(1.2f, 3f), vr2.ToRotation(), 0.42f, 0.82f + (Time * 0.0015f), 180, Projectile);
                PRTLoader.AddParticle(pulse2);
            }

            Time++;
        }

        public override void OnKill(int timeLeft) {
            if (Projectile.IsOwnedByLocalPlayer()) {
                for (int i = 0; i < 8; i++) {
                    Projectile proj = Projectile.NewProjectileDirect(Projectile.FromObjectGetParent(), Projectile.Center
                    , new Vector2(0, 1), ModContent.ProjectileType<Godslight>(), Projectile.damage, 0, Projectile.owner, 0, 2f + i);
                }
            }
            SoundEngine.PlaySound(SpearOfLonginus.AT, Projectile.Center);
            for (int i = 0; i < 4; i++) {
                float rot = MathHelper.PiOver2 * i;
                Vector2 vr = rot.ToRotationVector2() * 10;
                for (int j = 0; j < 116; j++) {
                    PRT_HeavenfallStar spark = new PRT_HeavenfallStar(Projectile.Center, vr * (0.3f + j * 0.1f), false, 37, Main.rand.Next(3, 17), Color.Gold);
                    PRTLoader.AddParticle(spark);
                }
            }
        }
    }
}
