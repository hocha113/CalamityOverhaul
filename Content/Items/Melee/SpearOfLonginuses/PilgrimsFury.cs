using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.SpearOfLonginuses
{
    internal class PilgrimsFury : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
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
            Projectile.DamageType = DamageClass.Melee;
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
            Target.CWR().TimeFrozenTick = 2;

            if (Time % 30 == 0) {
                SoundStyle belCanto = new("CalamityOverhaul/Assets/Sounds/BelCanto") { Volume = 1f + Time * 0.05f, Pitch = -0.2f + Time * 0.007f };
                SoundEngine.PlaySound(belCanto, Projectile.Center);
                Vector2 vr = new Vector2(0, 13);
                PRTLoader.NewParticle<PRT_LonginusWave>(Projectile.Center + new Vector2(0, -360), vr, Color.Gold, 0.42f).Configure(new Vector2(1.2f, 3f), vr.ToRotation(), 0.82f + Time * 0.002f, 180, Projectile);
                Vector2 vr2 = new Vector2(0, -13);
                PRTLoader.NewParticle<PRT_LonginusWave>(Projectile.Center + new Vector2(0, 360), vr2, Color.Gold, 0.42f).Configure(new Vector2(1.2f, 3f), vr2.ToRotation(), 0.82f + Time * 0.0015f, 180, Projectile);
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
                    PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center, vr * (0.3f + j * 0.1f), Color.Gold, Main.rand.Next(2, 7)).Configure(false, 37);
                }
            }
        }
    }
}
