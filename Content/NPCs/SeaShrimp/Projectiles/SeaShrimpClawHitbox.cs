using CalamityOverhaul.Content.NPCs.SeaShrimp.Core;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Projectiles
{
    /// <summary>
    /// 螯击判定线（不可见）：每端把自己贴到本地 IK 求解的"肘→螯尖"线段上，
    /// 受击玩家的判定线与他屏幕上画出的螯严格一致（伤害窗=视觉窗）。
    /// ai[0]=主体 whoAmI，ai[1]=臂号；主体离开螯击类状态即自毁
    /// </summary>
    internal class SeaShrimpClawHitbox : SeaShrimpModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private NPC BossNpc => Main.npc[(int)Projectile.ai[0]];
        private int ArmIndex => (int)Projectile.ai[1];

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = 70;
        }

        public override void AI() {
            NPC boss = BossNpc;
            if (boss == null || !boss.active || boss.ModNPC is not SeaShrimpBoss owner) {
                Projectile.Kill();
                return;
            }
            if (owner.CurrentStateIndex is not (SeaShrimpStateIndex.ClawJab
                or SeaShrimpStateIndex.CavitationPunch or SeaShrimpStateIndex.SuperCavitation)) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = owner.Skeleton.ClawTip(ArmIndex);
        }

        /// <summary>伤害窗几何门控：状态举旗期才有效，各端贴各自画面成立（公平阀）</summary>
        public override bool? CanDamage() {
            NPC boss = BossNpc;
            if (boss == null || !boss.active || boss.ModNPC is not SeaShrimpBoss owner) {
                return false;
            }
            return owner.Context != null && owner.Context.ClawDamageWindow ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            NPC boss = BossNpc;
            if (boss == null || !boss.active || boss.ModNPC is not SeaShrimpBoss owner) {
                return false;
            }
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                owner.Skeleton.ArmSolves[ArmIndex].Elbow, owner.Skeleton.ClawTip(ArmIndex), 42f, ref _);
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
