using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>
    /// 快速枪管（热膛连射）：持续开火堆积膛温，枪口腾起热浪。高温时光束会向侧面泄出冷却火花，
    /// 把过热转化为额外的扇面压制；停火后膛温缓缓回落。
    /// </summary>
    internal sealed class RapidBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //快速节奏的青色霓虹
        public override Color TintColor => new(0, 240, 220);

        private const int MaxHeat = 120;
        private const int VentThreshold = 70;
        private int _heat;

        public override void Apply(ref ShootContext ctx) {
            ctx.AttackSpeedMul += 0.4f;
            ctx.DamageMul += -0.24f;
            ctx.SpreadMul += 0.36f;
        }

        public override void OnPlayerUpdate(Player player) {
            bool firing = player.controlUseItem || player.channel;
            if (firing) {
                _heat = Math.Min(_heat + 2, MaxHeat);
                if (player.whoAmI == Main.myPlayer && _heat >= VentThreshold && Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                    Vector2 dir = (Main.MouseWorld - player.Center).SafeNormalize(new Vector2(player.direction, 0f));
                    Vector2 muzzle = player.Center + dir * 34f;
                    float h = (_heat - VentThreshold) / (float)(MaxHeat - VentThreshold);
                    Color hot = Color.Lerp(new Color(0, 240, 220), new Color(255, 120, 40), h);
                    PRTLoader.NewParticle<PRT_CyberSquare>(muzzle, dir.RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f)) * Main.rand.NextFloat(0.5f, 2f), hot, Main.rand.NextFloat(0.4f, 0.8f)).Configure(new Color(255, 80, 20), Main.rand.Next(8, 16));
                }
            }
            else {
                _heat = Math.Max(_heat - 3, 0);
            }
        }

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer || _heat < VentThreshold) return;
            //高温泄能：偶发向侧面甩出一道短程冷却火花
            if ((int)Main.GameUpdateCount % 9 != beam.Projectile.whoAmI % 9) return;
            if (!Main.rand.NextBool(3)) return;
            Vector2 perp = beam.Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2 * (Main.rand.NextBool() ? 1 : -1));
            SHPCNaturalFx.SpawnDerivedBeam(beam.Projectile, beam.Projectile.Center, perp * 9f, Math.Max(beam.Projectile.damage / 2, 1), 0.6f, 0.3f, theme: 2);
        }
    }
}
