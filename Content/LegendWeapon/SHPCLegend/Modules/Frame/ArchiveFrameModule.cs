using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame
{
    /// <summary>
    /// 归档机匣：累积所有 SHPC 命中造成的伤害，达到阈值时在玩家位置释放一次"数据归档"爆破
    /// 累积量与冷却均存储在 ModItem 实例字段，避免污染 SHPCPlayer 等共享状态
    /// </summary>
    internal sealed class ArchiveFrameModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Frame;
        //归档琥珀金
        public override Color TintColor => new(255, 200, 100);

        private const float Threshold = 6000f;
        private const int CooldownFrames = 120;

        private float _accumulated;
        private int _cooldown;

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += 0.08f;
            ctx.ManaCostMul += 0.15f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            Accumulate(beam.Projectile, damageDone);
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            //激光命中频次高，按一半计入累积
            Accumulate(laser.Projectile, damageDone / 2);
        }

        private void Accumulate(Projectile source, int damageDone) {
            if (source.owner != Main.myPlayer) return;
            if (damageDone <= 0) return;
            _accumulated += damageDone;
            if (_cooldown > 0 || _accumulated < Threshold) return;

            //达成阈值，扣除阈值并触发数据归档爆破在玩家位置
            _accumulated -= Threshold;
            _cooldown = CooldownFrames;
            Player owner = Main.player[source.owner];
            if (owner == null || !owner.active) return;

            int dmg = Math.Max((int)(Threshold * 0.25f), 1);
            int idx = Projectile.NewProjectile(source.GetSource_FromThis(),
                owner.Center, Vector2.Zero,
                ModContent.ProjectileType<CyberDetonationProj>(),
                dmg, 0f, source.owner, ai0: 0.7f);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Main.projectile[idx].localAI[2] = 300f;
            }
            if (source.owner == Main.myPlayer) {
                CombatText.NewText(owner.getRect(), new Color(255, 200, 60),
                    "// ARCHIVE", true, false);
            }
        }

        public override void OnPlayerUpdate(Player player) {
            if (_cooldown > 0) _cooldown--;
        }
    }
}
