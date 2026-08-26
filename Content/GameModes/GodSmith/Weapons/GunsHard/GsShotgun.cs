using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsHard
{
    /// <summary>
    /// 霰弹枪重铸：喉径两态。<br/>
    /// [宽喉]：6 粒 ±12 度宽扇，近战压制；单粒摊薄，总伤走神匠红利。<br/>
    /// [收颈]：3 粒 ±3 度精束、每粒 +30%，锥形瞄准线常亮读出弹道。<br/>
    /// 两档一次 use 都只耗 1 发弹药，粒数由接管生成自控
    /// </summary>
    internal class GsShotgun : GsFireModeScheme
    {
        public override int TargetItemID => ItemID.Shotgun;

        public override string GsFamily => "GunsHard";

        protected override string GsDescFallback =>
            "Reforged: right click to switch choke\n" +
            "Wide Bore throws six pellets in a broad fan; Tight Choke fires three heavy pellets dead straight\n" +
            "Every trigger pull still costs one shell";

        public override GsFireMode[] Modes { get; } = [
            new GsFireMode {
                Key = "ModeWideBore", EnName = "Wide Bore",
                DamageMul = 0.73f,
            },
            new GsFireMode {
                Key = "ModeTightChoke", EnName = "Tight Choke",
                DamageMul = 1.30f,
                AimLine = GsAimLineKind.Cone, AimConeHalfAngle = MathHelper.ToRadians(3f),
            },
        ];

        protected override bool? GsGunShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback,
            GsFireMode mode, GsGunsHardPlayer mp) {
            //接管粒数：宽喉 6 粒宽扇 / 收颈 3 粒窄束（damage 已按档摊薄或增幅）
            int pellets = mp.ModeIndex == 0 ? 6 : 3;
            float halfSpread = mp.ModeIndex == 0 ? MathHelper.ToRadians(12f) : MathHelper.ToRadians(3f);
            for (int i = 0; i < pellets; i++) {
                Vector2 pelletVel = velocity.RotatedBy(Main.rand.NextFloat(-halfSpread, halfSpread))
                    * Main.rand.NextFloat(0.94f, 1.06f);
                Projectile.NewProjectile(source, position, pelletVel, type, damage, knockback, player.whoAmI);
            }
            if (!VaultUtils.isServer) {
                MuzzleVisual(position, velocity, wide: mp.ModeIndex == 0);
            }
            return false;
        }

        /// <summary>枪口演出：宽喉大烟锥、收颈短促火舌（owner 个人反馈）</summary>
        private static void MuzzleVisual(Vector2 muzzle, Vector2 velocity, bool wide) {
            Vector2 unit = velocity.SafeNormalize(Vector2.UnitX);
            int sparkCount = wide ? 4 : 2;
            float sparkSpread = wide ? 0.5f : 0.14f;
            for (int i = 0; i < sparkCount; i++) {
                PRTLoader.NewParticle<PRT_Spark>(muzzle + unit * 16f,
                    unit.RotatedByRandom(sparkSpread) * Main.rand.NextFloat(3f, 6.5f),
                    new Color(255, 196, 110), Main.rand.NextFloat(0.3f, 0.45f))?.Configure(true, Main.rand.Next(8, 14));
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(muzzle + unit * Main.rand.NextFloat(8f, 20f),
                    unit * Main.rand.NextFloat(1f, 2.2f) - Vector2.UnitY * 0.4f,
                    new Color(120, 112, 100), Main.rand.NextFloat(0.35f, wide ? 0.6f : 0.45f))
                    ?.Configure(Main.rand.Next(18, 28), 0.42f, 0.02f);
            }
        }
    }
}
