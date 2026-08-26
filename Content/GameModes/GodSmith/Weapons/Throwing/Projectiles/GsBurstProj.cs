using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Throwing.Projectiles
{
    /// <summary>
    /// 参数化一次性 AoE 脉冲:骨投刀炸裂/大雪团碎冰/酒桶爆/刺球连锁共用。<br/>
    /// ai[0]=半径 px;ai[1]=附加效果(0 无 / 1 霜火 2s / 2 非 boss 混乱 0.5s)。<br/>
    /// 无形无贴图,存活 3 帧对圈内每目标只结算一次;粒子演出由生成方负责
    /// </summary>
    internal class GsBurstProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public const int FxNone = 0;
        public const int FxFrost = 1;
        public const int FxConfuse = 2;

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 3;
            Projectile.alpha = 255;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                int r = (int)Projectile.ai[0];
                if (r > 0) {
                    //各端首帧按参数撑开判定圈(ai 随生成包过线)
                    Projectile.Resize(r * 2, r * 2);
                }
            }
            Projectile.velocity = Vector2.Zero;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            switch ((int)Projectile.ai[1]) {
                case FxFrost:
                    target.AddBuff(BuffID.Frostburn, 120);
                    break;
                case FxConfuse:
                    if (!target.boss) {
                        target.AddBuff(BuffID.Confused, 30);
                    }
                    break;
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
