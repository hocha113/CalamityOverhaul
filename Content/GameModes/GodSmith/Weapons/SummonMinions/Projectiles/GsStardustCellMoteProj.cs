using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles
{
    /// <summary>
    /// 分裂胞子：星尘细胞有丝分裂甩出的原生质小体。出生 6 帧只随初速漂移，
    /// 随后软寻的附近敌人，命中重挂细胞侵蚀；无的可寻时游过 70 帧自行消散
    /// </summary>
    internal class GsStardustCellMoteProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.StardustCellMinionShot;

        public override string LocalizationCategory => "GodSmithSummonMinionsB";

        private const int LifeFrames = 70;
        private const int SplitFrames = 6;
        private const int FadeFrames = 12;

        private ref float Life => ref Projectile.localAI[0];

        private bool Fading => Projectile.timeLeft <= FadeFrames;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.timeLeft = LifeFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            if (Life == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item28 with { Volume = 0.35f, Pitch = 0.4f },
                    Projectile.Center);
            }
            //分裂期只随初速漂移，随后软寻的
            if (Life > SplitFrames && !Fading) {
                NPC prey = FindPrey(560f);
                if (prey != null) {
                    Vector2 want = (prey.Center - Projectile.Center)
                        .SafeNormalize(Vector2.UnitY) * 9f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.09f);
                }
            }
            if (Fading) {
                Projectile.velocity *= 0.9f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        /// <summary>最近可追猎敌人（各端本地同判，寻的量随 velocity 过线容差可接受）</summary>
        private NPC FindPrey(float radius) {
            NPC best = null;
            float bestDist = radius;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy()) {
                    continue;
                }
                float dist = npc.Center.Distance(Projectile.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //重挂细胞侵蚀（骑原版 buff 同步）
            target.AddBuff(BuffID.StardustMinionBleed, 300);
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item28 with { Volume = 0.4f, Pitch = -0.1f },
                Projectile.Center);
        }
    }
}
