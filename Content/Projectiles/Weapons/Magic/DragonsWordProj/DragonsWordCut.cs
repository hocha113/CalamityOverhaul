using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.DragonsWordProj
{
    internal class DragonsWordCut : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.friendly = true;
            Projectile.timeLeft = 22;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ArmorPenetration = 1000;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<EXHellfire>(), 180);
        }

        public override void OnKill(int timeLeft) {
            PRT_DragonsWordCut spark2 = new PRT_DragonsWordCut(Projectile.Center, new Vector2(0.1f, 0.1f)
                .RotatedByRandom(100), false, 19, Main.rand.NextFloat(0.65f, 0.85f), Main.rand.NextBool() ? Color.DarkRed : Color.IndianRed);
            PRTLoader.AddParticle(spark2);
            SoundStyle sound = new("CalamityMod/Sounds/Item/MurasamaHitOrganic");
            SoundEngine.PlaySound(sound with { Volume = 0.8f, PitchRange = (0.6f, 0.7f) }, Projectile.Center);
        }
    }
}
