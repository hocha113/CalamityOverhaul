using CalamityMod;
using CalamityOverhaul.Common;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using CalamityMod.Particles;
using Terraria.Audio;
using System.IO;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic
{
    internal class WaningMoonLight : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        private Player Owner => Main.player[Projectile.owner];
        private bool onhitNPCBool = true;
        private int randomIntDownNum;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.ignoreWater = false;
            Projectile.MaxUpdates = 15;
            Projectile.timeLeft = 120 * Projectile.MaxUpdates;
        }

        public override void SendExtraAI(BinaryWriter writer) => writer.Write(randomIntDownNum);

        public override void ReceiveExtraAI(BinaryReader reader) => randomIntDownNum = reader.ReadInt32();

        public override void AI() {
            if (Projectile.ai[0] == 0 && Projectile.IsOwnedByLocalPlayer()) {
                randomIntDownNum = Main.rand.Next(155);
                Projectile.netUpdate = true;
            }
            Projectile.tileCollide = Projectile.position.Y > (Owner.position.Y + randomIntDownNum);
            if (!CWRUtils.isServer) {
                Vector2 dustVel = Projectile.velocity * 0.1f;
                for (int i = 0; i < 3; i++) {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center + dustVel, 265, dustVel, 0, default, 0.7f);
                    dust.shader = GameShaders.Armor.GetSecondaryShader(Owner.cShield, Owner);
                    dust.noGravity = true;
                    Dust dust2 = Dust.NewDustPerfect(Projectile.Center + dustVel * 2, 226, dustVel, 0, default, 0.5f);
                    dust2.shader = GameShaders.Armor.GetSecondaryShader(Owner.cShield, Owner);
                    dust2.noGravity = true;
                }
            }
            Projectile.ai[0]++;
        }

        public override void OnKill(int timeLeft) {
            Projectile.Explode(62, SoundID.Item88);
            Vector2 pos = Projectile.Center + Projectile.velocity * 13;
            for (int i = 0; i < 20; i++) {
                Vector2 randVr = CWRUtils.GetRandomVevtor(-170, -10, Main.rand.Next(7, 22));
                AltSparkParticle spark = new(pos, randVr, true, 12, Main.rand.NextFloat(0.5f, 1.2f), Color.White);
                GeneralParticleHandler.SpawnParticle(spark);
                AltSparkParticle spark2 = new(pos, randVr, false, 9, Main.rand.NextFloat(0.1f, 1.3f), Color.WhiteSmoke);
                GeneralParticleHandler.SpawnParticle(spark2);
            }
        }
    }
}
