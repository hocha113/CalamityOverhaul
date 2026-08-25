using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs
{
    /// <summary>
    /// 铐击/链旋判定体（不可见）：每端把自己贴到本地铐位模拟上，
    /// 受击玩家的判定与他屏幕上画出的铁铐严格一致（公平阀，承 ScrapArmHitbox 先例）。
    /// ai[0]=怨灵 whoAmI，ai[1]=铐号；怨灵离开挥击/链旋态即自毁
    /// </summary>
    internal class GaolCuffHitbox : GaolModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private NPC BossNpc => Main.npc[(int)Projectile.ai[0]];
        private int CuffIndex => (int)Projectile.ai[1];

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = 60;
        }

        public override void AI() {
            NPC boss = BossNpc;
            if (boss == null || !boss.active || boss.ModNPC is not DeepGaolWraith owner) {
                Projectile.Kill();
                return;
            }
            //只在挥抡类状态存活，转场即撤
            if (owner.State is not (DeepGaolWraith.StateSwipe or DeepGaolWraith.StateFlail)) {
                Projectile.Kill();
                return;
            }
            Projectile.timeLeft = 60;
            Projectile.Center = owner.GetCuffPos(CuffIndex);
        }

        /// <summary>伤害窗几何门控：铐击只吃出手铐的挥击窗，链旋只吃达速漂移段</summary>
        public override bool? CanDamage() {
            NPC boss = BossNpc;
            if (boss == null || !boss.active || boss.ModNPC is not DeepGaolWraith owner) {
                return false;
            }
            if (owner.InFlailDamageWindow) {
                return null;
            }
            return owner.InSwipeStrikeWindow(CuffIndex) ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            NPC boss = BossNpc;
            if (boss == null || !boss.active || boss.ModNPC is not DeepGaolWraith owner) {
                return false;
            }
            //本帧扫掠线段，防高速穿隧
            Vector2 pos = owner.GetCuffPos(CuffIndex);
            Vector2 prev = pos - owner.GetCuffVel(CuffIndex);
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                prev, pos, 40f, ref _);
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
