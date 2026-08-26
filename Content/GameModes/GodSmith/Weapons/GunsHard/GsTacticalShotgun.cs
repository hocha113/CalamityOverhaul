using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsHard
{
    /// <summary>
    /// 战术霰弹枪重铸：战术节奏两态，原版 6 粒装药原样。<br/>
    /// [战术扇面]：常亮散布锥标线，读出 6 粒的覆盖面。<br/>
    /// [压制三泵]：泵速提到 1.8 倍连打 3 泵（每泵 6 粒、各耗 1 发），
    /// 然后强制 90 tick 泵闲，爆发窗口换持续节奏
    /// </summary>
    internal class GsTacticalShotgun : GsFireModeScheme
    {
        public override int TargetItemID => ItemID.TacticalShotgun;

        public override string GsFamily => "GunsHard";

        protected override string GsDescFallback =>
            "Reforged: right click to switch stance\n" +
            "Tactical Fan paints the spread cone so you know exactly what the six pellets cover\n" +
            "Triple Pump racks three fast shells then forces a rest; each pump still costs one shell";

        public override GsFireMode[] Modes { get; } = [
            new GsFireMode {
                Key = "ModeFan", EnName = "Tactical Fan",
                AimLine = GsAimLineKind.Cone, AimConeHalfAngle = MathHelper.ToRadians(8f),
            },
            new GsFireMode {
                Key = "ModeTriplePump", EnName = "Triple Pump",
                UseSpeed = 1.80f, DamageMul = 1.10f,
                BurstCount = 3, BurstRest = 90,
            },
        ];

        protected override bool? GsGunShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback,
            GsFireMode mode, GsGunsHardPlayer mp) {
            if (VaultUtils.isServer) {
                return null;
            }
            Vector2 unit = velocity.SafeNormalize(Vector2.UnitX * player.direction);
            //每泵抛一枚滚烫弹壳
            PRTLoader.NewParticle<PRT_ProcChip>(position - unit * 4f,
                unit.RotatedBy(-MathHelper.PiOver2 * player.direction) * Main.rand.NextFloat(1.2f, 2f)
                    - Vector2.UnitY * Main.rand.NextFloat(2f, 3.5f),
                new Color(196, 92, 60), Main.rand.NextFloat(0.5f, 0.7f))
                ?.Configure(new Color(255, 170, 110), Main.rand.Next(20, 32));
            //三泵档末泵：泵闲哨响 + 三壳齐出的收势演出
            if (mp.ModeIndex == 1 && mp.BurstShots == mode.BurstCount - 1) {
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.7f, Pitch = -0.5f }, position);
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_Smoke>(position + unit * Main.rand.NextFloat(6f, 16f),
                        unit * 1.4f - Vector2.UnitY * 0.5f, new Color(110, 104, 94),
                        Main.rand.NextFloat(0.4f, 0.6f))?.Configure(Main.rand.Next(20, 30), 0.45f, 0.02f);
                }
            }
            return null;
        }
    }
}
