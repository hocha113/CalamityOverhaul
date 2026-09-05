using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonSentries.Projectiles
{
    /// <summary>
    /// 链状闪电（闪电光环 T3 超频周期技）：高速追向目标，命中后向 300px 内下一目标续跳。<br/>
    /// ai[0]=目标 NPC 槽位（NPC 数组服务器权威，各端一致）ai[1]=剩余跳数。
    /// 续跳在 owner 命中回调生成，伤害同值不衰减，封顶由初始跳数控制
    /// </summary>
    internal class GsSentryChainLightningProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.MagnetSphereBolt;

        public override string LocalizationCategory => "GodSmithSummonSentries";

        private ref float TargetIdx => ref Projectile.ai[0];
        private ref float JumpsLeft => ref Projectile.ai[1];
        private ref float Age => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 45;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            Age++;
            if (Age == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.35f, Pitch = 0.4f, MaxInstances = 3 }, Projectile.Center);
            }
            //追向锚定目标；目标失效则直飞至寿终
            int idx = (int)TargetIdx;
            if (idx >= 0 && idx < Main.maxNPCs) {
                NPC target = Main.npc[idx];
                if (target.active && target.CanBeChasedBy(Projectile)) {
                    Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 14f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.4f);
                }
            }
            //原版电矢贴图朝上，补四分之一圈对齐飞行方向
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //只在 owner 端执行：续跳生成随原生同步
            if (JumpsLeft <= 0f) {
                return;
            }
            NPC next = null;
            float bestDist = 300f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.whoAmI == target.whoAmI || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = npc.Center.Distance(target.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    next = npc;
                }
            }
            if (next == null) {
                return;
            }
            Vector2 vel = (next.Center - target.Center).SafeNormalize(Vector2.UnitX) * 14f;
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, vel,
                ModContent.ProjectileType<GsSentryChainLightningProj>(),
                Projectile.damage, Projectile.knockBack, Projectile.owner,
                next.whoAmI, JumpsLeft - 1f);
        }
    }
}
