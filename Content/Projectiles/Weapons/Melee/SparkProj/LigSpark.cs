using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.SparkProj
{
    internal class LigSpark : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        private HashSet<NPC> onHitNPCs = [];
        public override void SetDefaults() {
            Projectile.width = 6;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.penetrate = 2;
            Projectile.MaxUpdates = 3;
            Projectile.timeLeft = 160 * Projectile.MaxUpdates;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.DamageType = DamageClass.Melee;
            onHitNPCs = [];
        }

        private void SpanElectric() {
            int sparky = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.UnusedWhiteBluePurple, 0f, 0f, 100, default, 1f);
            Main.dust[sparky].scale += Main.rand.Next(50) * 0.01f;
            Main.dust[sparky].noGravity = true;
            if (Main.rand.NextBool()) {
                int sparkier = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.UnusedWhiteBluePurple, 0f, 0f, 100, default, 1f);
                Main.dust[sparkier].scale += 0.3f + (Main.rand.Next(50) * 0.01f);
                Main.dust[sparkier].noGravity = true;
                Main.dust[sparkier].velocity *= 0.1f;
            }
            if (Main.rand.NextBool(13)) {
                Dust.NewDust(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height, DustID.Electric, Projectile.velocity.X, Projectile.velocity.Y);
            }
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(Projectile.localAI[0]);
            writer.Write(Projectile.localAI[1]);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            Projectile.localAI[0] = reader.ReadSingle();
            Projectile.localAI[1] = reader.ReadSingle();
        }

        public override void AI() {
            // 1. 计算方向向量
            Vector2 direction = Projectile.velocity;

            NPC target = VaultUtils.FindClosestNPC(Projectile.Center, 600, false, false, onHitNPCs);
            if (target != null) {
                direction = Projectile.Center.To(target.Center);
            }

            // 电弧球的速度向量
            Vector2 velocity = Projectile.velocity;

            // 2. 加入随机扰动（制造不规则性）
            if (!VaultUtils.isClient) {//只在服务端运行随机值，然后广播给客户端
                Projectile.localAI[0] = Main.rand.NextFloat(-1f, 1f) * 111.1f; // X方向随机扰动
                Projectile.localAI[1] = Main.rand.NextFloat(-1f, 1f) * 111.1f; // Y方向随机扰动
                if (!VaultUtils.isSinglePlayer) {
                    Projectile.netUpdate = true;
                }
            }

            direction.X += Projectile.localAI[0];
            direction.Y += Projectile.localAI[1];

            direction.Normalize();
            float speed = 2.5f; // 电弧球的基础速度
            velocity += direction * speed * 0.1f; // 逐步调整速度

            velocity *= 0.98f;

            float maxSpeed = 10f;
            if (velocity.Length() > maxSpeed) {
                velocity = Vector2.Normalize(velocity) * maxSpeed;
            }

            Projectile.velocity = velocity;

            SpanElectric();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!onHitNPCs.Contains(target)) {
                onHitNPCs.Add(target);
            }
            Projectile proj = Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), target.Center, Main.rand.NextVector2Unit() * Main.rand.Next(3, 5)
                    , ModContent.ProjectileType<SparkLightning>(), Projectile.damage / 3, Projectile.knockBack, Projectile.owner);
            proj.timeLeft = 10;
            proj.penetrate = 3;
            proj.tileCollide = false;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap, Projectile.position);
            for (int i = 0; i < 13; i++) {
                int dust = Dust.NewDust(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height, DustID.Electric);
                Main.dust[dust].velocity = Main.rand.NextVector2Unit() * Main.rand.Next(3, 8);
                Main.dust[dust].scale = Main.rand.NextFloat(0.2f, 0.5f);
            }
            Projectile proj = Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), Projectile.Center, Main.rand.NextVector2Unit() * Main.rand.Next(3, 5)
                    , ModContent.ProjectileType<SparkLightning>(), Projectile.damage / 3, Projectile.knockBack, Projectile.owner);
            proj.timeLeft = 10;
            proj.penetrate = 3;
            proj.tileCollide = false;
            return true;
        }
    }
}
