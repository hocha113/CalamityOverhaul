using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes
{
    /// <summary>
    /// 修罗模式：伤害下限镜像。
    /// 记录玩家最近一次对敌命中的实际伤害；来自敌怪或敌对弹幕的受击
    /// 最终伤害不会低于该值（环境伤害如岩浆、摔落不受影响）。
    /// 命中与受伤判定都在本机进行，状态无需网络同步
    /// </summary>
    internal class AsuraPlayer : ModPlayer
    {
        /// <summary>最近一次对敌命中的实际伤害；0 = 尚未出手</summary>
        internal int LastDealtDamage { get; private set; }

        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone)
            => RecordHit(target, damageDone);

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
            => RecordHit(target, damageDone);

        private void RecordHit(NPC target, int damageDone) {
            if (!GameModeSystem.AsuraActive) {
                return;
            }
            //友方与不死靶（训练假人）不计入：打靶不该给自己立死约
            if (target.friendly || target.immortal) {
                return;
            }
            LastDealtDamage = damageDone;
        }

        public override void ModifyHurt(ref Player.HurtModifiers modifiers) {
            if (!GameModeSystem.AsuraActive || LastDealtDamage <= 1 || modifiers.PvP) {
                return;
            }
            if (modifiers.DamageSource == null
                || !modifiers.DamageSource.TryGetCausingEntity(out var source)) {
                return;
            }
            bool fromEnemy = source is NPC { friendly: false } || source is Projectile { hostile: true };
            if (!fromEnemy) {
                return;
            }

            int floor = LastDealtDamage;
            modifiers.ModifyHurtInfo += (ref Player.HurtInfo info) => {
                if (info.Damage < floor) {
                    info.Damage = floor;
                }
            };
        }
    }
}
