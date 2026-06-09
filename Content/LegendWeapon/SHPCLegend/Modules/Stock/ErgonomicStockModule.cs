using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock
{
    /// <summary>
    /// 人体工学枪托（滑步握持）：换向、冲刺或跳跃带来的高速移动会开启短暂的流畅窗口，
    /// 期间射出的光束会顺着身法划出弧线，并拖出流光残影。
    /// </summary>
    internal sealed class ErgonomicStockModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Stock;
        //柔和米白
        public override Color TintColor => new(230, 220, 180);

        private const int FlowWindow = 25;
        private int _flow;
        private float _prevVelX;
        private float _prevSpeed;

        public override void Apply(ref ShootContext ctx) {
            ctx.ManaCostMul += -0.3f;
            ctx.AttackSpeedMul += 0.06f;
            ctx.SpreadMul += -0.1f;
        }

        public override void OnPlayerUpdate(Player player) {
            float vx = player.velocity.X;
            float speed = player.velocity.Length();
            //仅在身法事件（换向 / 突然加速 / 起跳）时开窗，窗口随后自然衰减，避免匀速飞行时无限弧化
            bool dirFlip = Math.Sign(vx) != 0 && _prevVelX != 0f && Math.Sign(vx) != Math.Sign(_prevVelX) && Math.Abs(vx) > 2f;
            bool burst = speed - _prevSpeed > 4f;
            if (dirFlip || burst) {
                _flow = FlowWindow;
            }
            else if (_flow > 0) {
                _flow--;
            }
            _prevVelX = vx;
            _prevSpeed = speed;
        }

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived || _flow <= 0) return;
            //顺着身法给光束一个柔和的弧度；流畅窗口很短，弧化时长自然受限
            float sign = beam.Projectile.whoAmI % 2 == 0 ? 1f : -1f;
            beam.Projectile.velocity = beam.Projectile.velocity.RotatedBy(0.035f * sign);
            if (Main.netMode != Terraria.ID.NetmodeID.Server && Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_CyberSquare>(beam.Projectile.Center, beam.Projectile.velocity * -0.05f, new Color(240, 230, 190), Main.rand.NextFloat(0.3f, 0.6f)).Configure(new Color(200, 180, 120), Main.rand.Next(8, 14));
            }
        }
    }
}
