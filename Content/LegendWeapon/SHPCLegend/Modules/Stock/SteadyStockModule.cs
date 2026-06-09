using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock
{
    /// <summary>
    /// 稳压枪托（稳态架枪）：站稳片刻后展开虚拟脚架，以射速换取更重的单发。
    /// 架稳并持续开火时会周期性释放校准波，对周围进行稳定的覆盖压制。
    /// </summary>
    internal sealed class SteadyStockModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Stock;
        //沉稳金属灰
        public override Color TintColor => new(180, 200, 220);

        private const int BraceNeeded = 40;
        private const int WaveInterval = 50;
        private int _brace;
        private int _waveTimer;

        public override void Apply(ref ShootContext ctx) {
            ctx.AttackSpeedMul += -0.2f;
            ctx.DamageMul += 0.12f;
        }

        public override void OnPlayerUpdate(Player player) {
            bool steady = player.velocity.Length() < 1f;
            if (steady) {
                _brace = Math.Min(_brace + 1, BraceNeeded + 10);
            }
            else {
                _brace = 0;
                _waveTimer = 0;
                return;
            }

            bool braced = _brace >= BraceNeeded;
            if (braced && Main.netMode != NetmodeID.Server && Main.rand.NextBool(6)) {
                //脚架稳态微光
                Vector2 foot = player.Bottom + new Vector2(Main.rand.NextFloat(-14f, 14f), -2f);
                PRTLoader.NewParticle<PRT_CyberSquare>(foot, new Vector2(0f, Main.rand.NextFloat(-0.6f, -0.1f)), new Color(180, 210, 240), Main.rand.NextFloat(0.3f, 0.6f)).Configure(new Color(90, 120, 170), Main.rand.Next(10, 18));
            }

            bool firing = player.controlUseItem || player.channel;
            if (braced && firing) {
                if (++_waveTimer >= WaveInterval) {
                    _waveTimer = 0;
                    if (player.whoAmI == Main.myPlayer) {
                        EmitCalibrationWave(player);
                    }
                }
            }
            else {
                _waveTimer = 0;
            }
        }

        private static void EmitCalibrationWave(Player player) {
            int dmg = Math.Max((player.HeldItem?.damage ?? 1) / 2, 1);
            int idx = Projectile.NewProjectile(player.GetSource_FromThis(),
                player.Center, Vector2.Zero,
                ModContent.ProjectileType<CyberDetonationProj>(),
                dmg, 0f, player.whoAmI, ai0: 0.2f);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                //校准波：稳定的中等范围（150px）
                Main.projectile[idx].localAI[2] = 150f;
                Main.projectile[idx].usesLocalNPCImmunity = true;
                Main.projectile[idx].localNPCHitCooldown = -1;
            }
            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item84 with { Volume = 0.3f, Pitch = 0.4f }, player.Center);
            }
        }
    }
}
