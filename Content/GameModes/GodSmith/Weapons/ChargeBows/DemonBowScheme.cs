using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.ChargeBows
{
    /// <summary>
    /// 恶魔弓：T2 质变暗影箭（换型邪恶箭，穿透充足，紫黑拖尾），
    /// T3 命中分裂 2 缕追魂魔焰（换型暗影焰箭，60% 伤，弱追踪 300px 每帧 4°）。
    /// 追踪转率刻意压低，防越肩回旋读作制导导弹
    /// </summary>
    internal class GsDemonBow : GsChargeBowScheme
    {
        public override int TargetItemID => ItemID.DemonBow;
        protected override string GsDescFallback =>
            "Reforged: three-stage draw. A full draw transmutes the arrow into an unholy bolt; an overdrawn hit releases two soul-seeking shadowflame wisps";
        internal override float DpsTarget => 1.0f;
        internal override Color TrailMain => new(150, 90, 200);
        internal override Color TrailHot => new(224, 176, 255);
        internal override Color TrailDeep => new(58, 30, 92);

        internal override int TransformShootType(int pickedType, int tier)
            => tier >= 2 ? ProjectileID.UnholyArrow : pickedType;

        internal override void OnArrowSpawned(Projectile proj, GodSmithProjRouter router, int tier, int kind) {
            //邪恶箭自带 3 段穿透，质变不再加穿（基类默认 +1 在此关闭）
        }

        internal override void OnQualityHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router, int tier) {
            if (tier < 3) {
                return;
            }
            //追魂魔焰：越过目标斜向两侧撒出，随后弱追踪归敌（owner 端生成，承签打标）
            Vector2 dir = proj.velocity.SafeNormalize(Vector2.UnitX);
            int dmg = Math.Max(1, (int)(proj.damage * 0.6f));
            for (int i = 0; i < 2; i++) {
                float rot = MathHelper.ToRadians(38f) * (i == 0 ? 1f : -1f);
                StampNext(tier, KindSoulFlame);
                int idx = Projectile.NewProjectile(proj.GetSource_FromThis(),
                    target.Center + dir * (target.width * 0.5f + 6f), dir.RotatedBy(rot) * 9f,
                    ProjectileID.ShadowFlameArrow, dmg, proj.knockBack * 0.4f, proj.owner);
                Projectile wisp = Main.projectile[idx];
                wisp.usesLocalNPCImmunity = true;
                wisp.localNPCHitCooldown = 10;
                wisp.localNPCImmunity[target.whoAmI] = 20;
            }
        }

        internal override void ArrowPostAI(Projectile proj, GodSmithProjRouter router, int tier, int kind) {
            if (kind != KindSoulFlame) {
                return;
            }
            //弱追踪：各端跑同一确定性算法（最近敌），转率低容忍微分叉，命中以 owner 端裁决
            NPC quarry = FindNearestEnemy(proj.Center, 300f, proj);
            if (quarry != null) {
                float cur = proj.velocity.ToRotation();
                float want = (quarry.Center - proj.Center).ToRotation();
                proj.velocity = cur.AngleTowards(want, MathHelper.ToRadians(4f)).ToRotationVector2()
                    * proj.velocity.Length();
            }
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_Sparkle>(proj.Center, -proj.velocity * 0.1f,
                    TrailHot, 0.3f)?.Configure(TrailMain, Main.rand.Next(10, 16), 0.04f, 0.6f);
            }
        }
    }
}
