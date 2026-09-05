using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Guns
{
    /// <summary>
    /// 发条突击步枪重铸：点射件。<br/>
    /// [齿轮点射]：原版三连点射原样保留，每个动画链的第 3 发咬合齿轮 +30% 伤。<br/>
    /// 弹药经济原样：原版「三连只首发耗弹」词条不动
    /// </summary>
    internal class GsClockworkAssaultRifle : GsFireModeScheme
    {
        public override int TargetItemID => ItemID.ClockworkAssaultRifle;

        public override string GsFamily => "Guns";

        protected override string GsDescFallback =>
            "Reforged: keeps the classic three round burst, the third round bites 30% harder";
        /// <summary>本次射击为咬合弹的世界帧（出手音消费）；只在 owner 射击链读写</summary>
        private uint gearBiteTick = uint.MaxValue;

        public override GsFireMode[] Modes { get; } = [
            new GsFireMode {
                Key = "ModeGearBurst", EnName = "Gear Burst",
            },
        ];

        //==================== 射击：动画链第 3 发咬合 ====================

        protected override void GsGunModifyShoot(Item item, Player player, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback,
            GsFireMode mode, GsGunsHardPlayer mp) {
            //原版三连 = 一个 useAnimation 内 3 次 Shoot；CurAnimShot 为动画链内序号
            if (mp.CurAnimShot % 3 == 2) {
                gearBiteTick = Main.GameUpdateCount;
                damage = (int)(damage * 1.30f);
            }
        }

        protected override bool? GsGunShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback,
            GsFireMode mode, GsGunsHardPlayer mp) {
            if (gearBiteTick == Main.GameUpdateCount && !VaultUtils.isServer) {
                //咬合发出手：齿轮咔哒（owner 个人反馈）
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.5f, Pitch = 0.7f }, position);
            }
            return null;
        }

        internal override void GsGunHeldReset(Player player) => gearBiteTick = uint.MaxValue;
    }
}
