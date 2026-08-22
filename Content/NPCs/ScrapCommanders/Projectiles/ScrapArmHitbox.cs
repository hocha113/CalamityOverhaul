using CalamityOverhaul.Content.NPCs.ScrapCommanders.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.ScrapCommanders.Projectiles
{
    /// <summary>
    /// 突刺臂击判定线（不可见）：每端把自己贴到本地臂模拟的"肩→工具口"线段上，
    /// 受击玩家的判定线与他屏幕上画出的臂严格一致。
    /// ai[0]=统帅 whoAmI，ai[1]=臂号；统帅离开突刺态即自毁
    /// </summary>
    internal class ScrapArmHitbox : ModProjectile
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
            Projectile.timeLeft = 56;
        }

        public override void AI() {
            NPC boss = BossNpc;
            if (boss == null || !boss.active || boss.ModNPC is not ScrapCommander owner) {
                Projectile.Kill();
                return;
            }
            //只在臂击类状态存活，转场即撤
            if (owner.CurrentStateIndex is not (ScrapStateIndex.SawLaunch or ScrapStateIndex.ViceSnatch
                or ScrapStateIndex.CrossSpin or ScrapStateIndex.SawCannonCombo)) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = owner.GetArmPos(ArmIndex);
        }

        /// <summary>伤害窗几何门控：突刺类只在臂伸展超过 55% 链长时有效，收臂/急停段不打人，
        /// 且该判定在每个端点都贴着各自画面成立（公平阀）；十字旋辐条全程即攻击</summary>
        public override bool? CanDamage() {
            NPC boss = BossNpc;
            if (boss == null || !boss.active || boss.ModNPC is not ScrapCommander owner) {
                return false;
            }
            if (owner.CurrentStateIndex == ScrapStateIndex.CrossSpin) {
                return null;
            }
            float ext = Vector2.Distance(owner.ShoulderWorld(ArmIndex), owner.GetArmPos(ArmIndex));
            return ext > owner.DartReach * 0.55f ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            NPC boss = BossNpc;
            if (boss == null || !boss.active || boss.ModNPC is not ScrapCommander owner) {
                return false;
            }
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                owner.ShoulderWorld(ArmIndex), owner.GetArmPos(ArmIndex), 44f, ref _);
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
