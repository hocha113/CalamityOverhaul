using CalamityOverhaul.Common;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Guns
{
    /// <summary>
    /// 四管霰弹枪「四管齐鸣」：本族后坐位移轴的招牌件。<br/>
    /// 4 管弹巢：单击放 1 管 2 粒精散（节拍 14t，用速桥把 55ut 压到 14ut）；
    /// 第 4 管齐鸣：8 粒全散 + 玩家反冲 6px（空中 ×1.5 即 9px，向下打=火箭跳）+ 震屏。
    /// Reload 52t 逐管折装四响。<br/>
    /// 账目：周期 94t 打 14 粒对原版 94t 内 13.7 粒（×1.02），精散粒 ×1.25、齐鸣粒 ×1.0，
    /// 均值 ×1.11，合计约 112%（含装填空窗，待游戏内标定）
    /// </summary>
    internal class GsQuadBarrelShotgun : GsMagazineScheme
    {
        public override int TargetItemID => ItemID.QuadBarrelShotgun;

        protected override string GsDescFallback =>
            "Reforged: fire one barrel at a time in a rapid four-count;\nthe fourth barrel roars all at once with 8 pellets and hurls you backward.\nAim down to rocket-jump. Break open to reload; nail the sweet spot for +2 pellets";
        public override int MagSize => 4;
        public override int ReloadTicks => 52;
        public override GsReloadStyle Style => GsReloadStyle.Break;
        protected override float GetRecoil(bool lastRound) => lastRound ? 6f : 1f;
        protected override int ReloadCueCount => 4;
        //霰弹枪：枪身重挫，颠动大
        protected override float DefaultRecoilShift => 4f;
        protected override float DefaultRecoilKick => 0.1f;

        /// <summary>55ut 压到每管 14t 的节拍</summary>
        public override float GsUseSpeedMultiplier(Item item, Player player) => 55f / 14f;

        protected override bool? FireNormalRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //精散管：2 粒 ±2°，粒伤 ×1.25（奖励点准）
            pendingMark = 0f;
            SpawnPellets(player, source, position, velocity, type, (int)(damage * 1.25f), knockback,
                count: 2, spreadDeg: 2f);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item36 with { Volume = 0.55f, Pitch = 0.3f }, position);
            }
            return false;
        }

        protected override bool? FireLastRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //四管齐鸣：8 粒全散，仍只吃本次 use 的 1 发弹药
            pendingMark = 1f;
            SpawnPellets(player, source, position, velocity, type, damage, knockback * 1.4f,
                count: 8, spreadDeg: 12f);

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item36 with { Volume = 1f, Pitch = -0.25f }, position);
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.45f, Pitch = 0.2f }, position);
                Vector2 aim = velocity.SafeNormalize(Vector2.UnitX * player.direction);
                //震屏走本机配置门
                if (CWRClientConfig.Instance.ScreenVibration) {
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(position,
                        aim, 3f, 6f, 10, 900f, "GsQuadSalvo"));
                }
            }
            return false;
        }

        private static void SpawnPellets(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position,
            Vector2 velocity, int type, int damage, float knockback, int count, float spreadDeg) {
            for (int i = 0; i < count; i++) {
                Vector2 vel = velocity.RotatedByRandom(MathHelper.ToRadians(spreadDeg))
                    * Main.rand.NextFloat(0.95f, 1.05f);
                Projectile.NewProjectile(source, position, vel, type, damage, knockback, player.whoAmI);
            }
        }

        //==================== 折管四响装填 ====================

        protected override void OnReloadStart(Item item, Player player, GsGunsEarlyPlayer mp) {
            if (!VaultUtils.isServer) {
                //折开
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.7f, Pitch = -0.4f }, player.Center);
            }
        }

        protected override void OnReloadCue(Item item, Player player, GsGunsEarlyPlayer mp, int index, int total) {
            if (!VaultUtils.isServer) {
                //逐管落膛四响，音阶渐升
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.75f, Pitch = -0.35f + 0.18f * index }, player.Center);
            }
        }

        protected override void OnReloadComplete(Item item, Player player, GsGunsEarlyPlayer mp) {
            if (!VaultUtils.isServer) {
                //合膛重响
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.8f, Pitch = 0.25f }, player.Center);
            }
        }
    }
}
