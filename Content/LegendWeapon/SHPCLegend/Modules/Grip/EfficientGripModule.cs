using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip
{
    /// <summary>
    /// 高效握把（回收回路）：命中会从敌人身上回抽残余能量，化作能量微粒飞回玩家并返还少量法力，
    /// 让持续输出几乎自给自足。
    /// </summary>
    internal sealed class EfficientGripModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Grip;
        //高效翠绿
        public override Color TintColor => new(60, 220, 120);

        private int _cd;

        public override void Apply(ref ShootContext ctx) {
            ctx.ManaCostMul += -0.12f;
            ctx.AttackSpeedMul += 0.06f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            Recycle(beam.Projectile, target);
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            Recycle(laser.Projectile, target);
        }

        private void Recycle(Projectile src, NPC target) {
            if (src.owner != Main.myPlayer) return;
            //回收节流，避免高频命中无限回蓝
            if (_cd > 0) { _cd--; return; }
            _cd = 10;
            Player p = Main.player[src.owner];
            int amount = 2;
            if (p.statMana < p.statManaMax2) {
                p.statMana = Math.Min(p.statMana + amount, p.statManaMax2);
                p.ManaEffect(amount);
            }
            if (Main.netMode != NetmodeID.Server) {
                Vector2 toPlayer = (p.Center - target.Center).SafeNormalize(Vector2.UnitY) * 4f;
                PRTLoader.NewParticle<PRT_Sparkle>(target.Center, toPlayer, new Color(120, 255, 170), Main.rand.NextFloat(0.5f, 0.8f)).Configure(new Color(40, 200, 110), Main.rand.Next(16, 26), 0.1f, 0.8f);
            }
        }
    }
}
