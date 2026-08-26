using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsHard
{
    /// <summary>
    /// 发条突击步枪重铸：点射与过热轴心件。<br/>
    /// [齿轮点射]：原版三连点射原样保留，每个动画链的第 3 发咬合齿轮 +30% 伤并带亮曳光。<br/>
    /// [超频扫射]：发条超转 +40% 射速，持续开火约 5 秒积满热量，
    /// 齿轮卡壳 45 tick（散架音 + 齿轮屑喷溅），松手即自然散热。<br/>
    /// 弹药经济原样：原版「三连只首发耗弹」词条不动，卡壳期间不能开火也不耗弹
    /// </summary>
    internal class GsClockworkAssaultRifle : GsFireModeScheme
    {
        public override int TargetItemID => ItemID.ClockworkAssaultRifle;

        public override string GsFamily => "GunsHard";

        protected override string GsDescFallback =>
            "Reforged: right click to switch fire mode\n" +
            "Gear Burst keeps the classic three round burst, the third round bites 30% harder\n" +
            "Overclock trades heat for 40% more speed; run it too hot and the gears jam";

        /// <summary>齿轮咬合弹（动画链第 3 发）私有 flag</summary>
        private const int FlagGearBite = 1;

        /// <summary>齿轮铜色</summary>
        private static readonly Color GearBrass = new(214, 158, 74);

        /// <summary>本次射击为咬合弹的世界帧（打标窗口消费）；只在 owner 射击链读写</summary>
        private uint gearBiteTick = uint.MaxValue;

        public override GsFireMode[] Modes { get; } = [
            new GsFireMode {
                Key = "ModeGearBurst", EnName = "Gear Burst",
            },
            new GsFireMode {
                Key = "ModeOverclock", EnName = "Overclock",
                UseSpeed = 1.40f,
                HeatPerShot = 0.055f, JamTicks = 45,
            },
        ];

        //==================== 射击：动画链第 3 发咬合 ====================

        protected override void GsGunModifyShoot(Item item, Player player, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback,
            GsFireMode mode, GsGunsHardPlayer mp) {
            //原版三连 = 一个 useAnimation 内 3 次 Shoot；CurAnimShot 为动画链内序号
            if (mp.ModeIndex == 0 && mp.CurAnimShot % 3 == 2) {
                gearBiteTick = Main.GameUpdateCount;
                damage = (int)(damage * 1.30f);
            }
        }

        protected override bool? GsGunShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback,
            GsFireMode mode, GsGunsHardPlayer mp) {
            if (gearBiteTick == Main.GameUpdateCount && !VaultUtils.isServer) {
                //咬合发出手：齿轮咔哒 + 铜色枪口花（owner 个人反馈）
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.5f, Pitch = 0.7f }, position);
                Vector2 unit = velocity.SafeNormalize(Vector2.UnitX * player.direction);
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(position + unit * 20f,
                        unit.RotatedByRandom(0.5) * Main.rand.NextFloat(2f, 5f),
                        GearBrass, Main.rand.NextFloat(0.28f, 0.42f))?.Configure(true, Main.rand.Next(10, 16));
                }
            }
            return null;
        }

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            GsGunsHardPlayer mp = Main.player[proj.owner].GetModPlayer<GsGunsHardPlayer>();
            router.MarkData = PackMark(mp.ModeIndex, gearBiteTick == Main.GameUpdateCount ? FlagGearBite : 0);
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            //咬合弹飞行相：铜亮曳光（各端可见，预算克制）
            if (MarkFlagOf(router.MarkData) != FlagGearBite || VaultUtils.isServer) {
                return;
            }
            if (proj.timeLeft % 3 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(proj.Center - proj.velocity * 0.3f,
                    -proj.velocity * 0.04f, GearBrass,
                    Main.rand.NextFloat(0.22f, 0.34f))?.Configure(false, Main.rand.Next(7, 12));
            }
        }

        //==================== 过热：卡壳演出 ====================

        protected override void OnJam(Item item, Player player, GsFireMode mode) {
            if (VaultUtils.isServer) {
                return;
            }
            //发条散架：金属崩响 + 齿轮屑迸出 + 两团机油烟
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.8f, Pitch = -0.4f }, player.Center);
            SoundEngine.PlaySound(SoundID.Item16 with { Volume = 0.6f, Pitch = -0.6f }, player.Center);
            Vector2 muzzle = player.MountedCenter + GsAimUnit(player) * 30f;
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_ProcChip>(muzzle,
                    Main.rand.NextVector2Circular(3f, 2f) - Vector2.UnitY * Main.rand.NextFloat(1f, 3f),
                    GearBrass, Main.rand.NextFloat(0.5f, 0.8f))?.Configure(GearBrass, Main.rand.Next(24, 40));
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(muzzle + Main.rand.NextVector2Circular(6f, 6f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.2f),
                    new Color(70, 62, 52), Main.rand.NextFloat(0.5f, 0.8f))?.Configure(Main.rand.Next(26, 38), 0.5f, 0.02f);
            }
        }

        protected override void GsGunHoldLocal(Item item, Player player, GsGunsHardPlayer mp) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 muzzle = player.MountedCenter + GsAimUnit(player) * 30f;
            //卡壳期间齿轮烟持续外冒
            if (mp.JamTimer > 0 && mp.JamTimer % 7 == 0) {
                PRTLoader.NewParticle<PRT_Smoke>(muzzle, -Vector2.UnitY * Main.rand.NextFloat(0.4f, 0.9f),
                    new Color(70, 62, 52), Main.rand.NextFloat(0.35f, 0.55f))?.Configure(Main.rand.Next(20, 30), 0.4f, 0.015f);
            }
            //超频档热度高企的预警细烟（个人读数）
            else if (mp.ModeIndex == 1 && mp.Heat > 0.7f && Main.GameUpdateCount % 9 == 0) {
                PRTLoader.NewParticle<PRT_Smoke>(muzzle, -Vector2.UnitY * 0.6f,
                    new Color(96, 84, 68), Main.rand.NextFloat(0.25f, 0.4f))?.Configure(Main.rand.Next(14, 22), 0.3f);
            }
        }

        internal override void GsGunHeldReset(Player player) => gearBiteTick = uint.MaxValue;
    }
}
