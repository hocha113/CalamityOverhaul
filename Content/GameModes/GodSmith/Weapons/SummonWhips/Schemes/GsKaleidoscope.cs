using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Schemes
{
    /// <summary>
    /// 万花筒「棱彩终鞭」：全族最紧 10f 基准窗、空挥全归零（唯一 Reset，高手向）；
    /// 五层转印，每层鞭痕点亮一色（赤橙金青紫）。tag 之王地位与五彩鞭体原样保留。<br/>
    /// 处决 = 万华镜：以目标为心展开五道彩光折线斩（各 0.6x，五色摆角由
    /// identity 种子定，跨端一致），并给 5 秒「棱彩暴露」：
    /// 自家召唤物对其 10% 概率强制暴击。强度目标 112%（终局克制档）
    /// </summary>
    internal class GsKaleidoscope : GsWhipScheme
    {
        /// <summary>万华镜五色：赤橙金青紫</summary>
        internal static readonly Color[] PrismColors = [
            new Color(235, 64, 72),
            new Color(255, 150, 54),
            new Color(255, 222, 92),
            new Color(84, 224, 220),
            new Color(190, 96, 255)
        ];

        public override int TargetItemID => ItemID.RainbowWhip;

        public override int WhipProjType => ProjectileID.RainbowWhip;

        public override int BaseWindowFrames => 10;

        public override int MarkCap => 5;

        public override MissPolicyKind MissPolicy => MissPolicyKind.Reset;

        public override float DamageTweak => 1.03f;

        /// <summary>棱彩底金（印记与拍点基色；层数点走五色）</summary>
        public override Color MarkColor => new(255, 222, 92);

        public override Color MarkLayerColor(int layer)
            => PrismColors[Math.Clamp(layer, 0, PrismColors.Length - 1)];

        protected override string GsDescFallback =>
            "Reforged: the tightest beat window, and a missed swing resets all tempo; " +
            "each scar lights one prism color, and at five colors the next on-beat hit " +
            "unfolds a kaleidoscope of five prismatic slashes, " +
            "exposing the victim to guaranteed-crit chances for 5 seconds";

        protected override void OnExecute(Player player, NPC target, Projectile whipProj, WhipMarkState st) {
            int dmg = Math.Max(1, (int)MathF.Round(st.MarkDamage * 0.6f));
            for (int i = 0; i < 5; i++) {
                Projectile.NewProjectile(player.GetSource_Misc("GsWhipExecute"), target.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsWhipPrismSlashProj>(), dmg, 2f,
                    player.whoAmI, i, target.whoAmI);
            }
            //棱彩暴露：5 秒内自家召唤物对其 10% 概率强制暴击（+10% 暴击的真实落法）
            st.PrismExposeUntil = Main.GameUpdateCount + 300;
        }
    }
}
