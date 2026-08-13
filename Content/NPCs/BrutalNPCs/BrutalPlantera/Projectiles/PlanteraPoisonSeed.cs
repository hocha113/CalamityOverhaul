using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Projectiles
{
    /// <summary>毒种：抛物线弹道，落点炸开孢子雾</summary>
    internal class PlanteraPoisonSeed : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_276";

        internal static int GetDamage(NPC boss) => Math.Max((int)(boss.defDamage * 0.34f), 14);

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.aiStyle = -1;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            //全程受重力的沉种
            Projectile.velocity.Y += 0.16f;
            if (Projectile.velocity.Y > 16f) {
                Projectile.velocity.Y = 16f;
            }
            Projectile.rotation += Projectile.velocity.X * 0.04f;

            Lighting.AddLight(Projectile.Center, PlanteraRenderHelper.SporeGreen.ToVector3() * 0.3f);

            if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Plantera_Pink, 0f, 0f, 120, default, 1f);
                dust.noGravity = true;
                dust.velocity = -Projectile.velocity * 0.06f;
            }
        }

        public override void OnKill(int timeLeft) {
            //落点孢子雾，权威端生成
            if (!VaultUtils.isClient) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<PlanteraSporeCloud>(),
                    Math.Max(Projectile.damage / 2, 8), 0f, Main.myPlayer, 0.75f);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 0.5f, Pitch = 0.4f, MaxInstances = 4 }, Projectile.Center);
                PlanteraRenderHelper.SpawnSporePuff(Projectile.Center, 0.9f);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Poisoned, 240);
        }
    }
}
