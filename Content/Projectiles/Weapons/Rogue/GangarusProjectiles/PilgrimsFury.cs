using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Particles.Core;
using CalamityOverhaul.Content.Particles;
using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Terraria.Audio;
using CalamityOverhaul.Content.Items.Rogue.Extras;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Rogue.GangarusProjectiles
{
    internal class PilgrimsFury : ModProjectile {
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
            Projectile.DamageType = ModContent.GetInstance<RogueDamageClass>();
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
        }

        private Vector2 targetEndPos;
        private float targetEndRot;
        private Rectangle targetEndFrame;

        public override void AI() {
            if (!Target.Alives()) {
                Projectile.Kill();
                return;
            }

            if (Time == 0) {
                targetEndPos = Target.position;
                targetEndRot = Target.rotation;
            }

            Projectile.Center = Target.Center;

            Target.position = targetEndPos;
            Target.rotation = targetEndRot;

            if (Time % 30 == 0) {
                SoundStyle belCanto = new("CalamityOverhaul/Assets/Sounds/BelCanto") { Volume = 1.5f + Time * 0.15f };
                SoundEngine.PlaySound(belCanto, Projectile.Center);
                Vector2 vr = new Vector2(0, 13);
                GangarusWave pulse = new GangarusWave(Projectile.Center + new Vector2(0, -360), vr, Color.Gold, new Vector2(1.2f, 3f), vr.ToRotation(), 0.42f, 0.82f + (Time * 0.002f), 180, Projectile);
                CWRParticleHandler.AddParticle(pulse);
                Vector2 vr2 = new Vector2(0, -13);
                GangarusWave pulse2 = new GangarusWave(Projectile.Center + new Vector2(0, 360), vr2, Color.Gold, new Vector2(1.2f, 3f), vr2.ToRotation(), 0.42f, 0.82f + (Time * 0.0015f), 180, Projectile);
                CWRParticleHandler.AddParticle(pulse2);
            }

            Time++;
        }

        public override void OnKill(int timeLeft) {
            if (Projectile.IsOwnedByLocalPlayer()) {
                for (int i = 0; i < 16; i++) {
                    Projectile proj = Projectile.NewProjectileDirect(Projectile.parent(), Projectile.Center
                    , new Vector2(0, 1), ModContent.ProjectileType<Godslight>(), Projectile.damage, 0, Projectile.owner, 0, 2f + i * 0.5f);  
                }
            }
            SoundEngine.PlaySound(Gangarus.AT, Projectile.Center);
            for (int i = 0; i < 4; i++) {
                float rot = MathHelper.PiOver2 * i;
                Vector2 vr = rot.ToRotationVector2() * 10;
                for (int j = 0; j < 116; j++) {
                    HeavenfallStarParticle spark = new HeavenfallStarParticle(Projectile.Center, vr, false, 37, Main.rand.Next(3, 17), Color.Gold);
                    CWRParticleHandler.AddParticle(spark);
                }
            }
        }
    }
}
