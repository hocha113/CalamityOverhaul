using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Guns
{
    /// <summary>
    /// 左轮手枪「轮盘速转」。<br/>
    /// 6 膛转轮（逐膛装填可打断）；末发固定 +40%；空匣正常 Reload 40t。<br/>
    /// 账目：周期 150t 打 6 发对原版 6.8 发（0.88），末发均值 1.067，伤害行 ×1.15 → 约 108%（待游戏内标定）
    /// </summary>
    internal class GsRevolver : GsMagazineScheme
    {
        public override int TargetItemID => ItemID.Revolver;

        protected override string GsDescFallback =>
            "Reforged: a six-round cylinder; the final chamber always hits +40% harder.";
        public override int MagSize => 6;
        public override int ReloadTicks => 40;
        public override GsReloadStyle Style => GsReloadStyle.Cylinder;
        protected override float GetRecoil(bool lastRound) => lastRound ? 1.5f : 1f;
        //转轮手枪：短后挫、腕部上翘明显
        protected override float DefaultRecoilShift => 1.8f;
        protected override float DefaultRecoilKick => 0.075f;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) => damage *= 1.15f;

        protected override void ModifyShot(Item item, Player player, GsGunsEarlyPlayer mp, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback, bool lastRound) {
            if (lastRound) {
                damage = (int)(damage * 1.4f);  //末发固定 +40%
            }
        }

        /// <summary>末发签名已由 ModifyShot 的 +40% 承载，弹幕照常走原版</summary>
        protected override bool? FireLastRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => null;

        //==================== 逐膛装填音效 ====================

        protected override void OnReloadStart(Item item, Player player, GsGunsEarlyPlayer mp) {
            if (!VaultUtils.isServer) {
                //甩轮
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.65f, Pitch = -0.3f }, player.Center);
            }
        }

        protected override void OnRoundLoaded(Item item, Player player, GsGunsEarlyPlayer mp, int roundIndex) {
            if (!VaultUtils.isServer) {
                //逐膛咔嗒，音阶上行
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.7f, Pitch = -0.25f + 0.09f * roundIndex }, player.Center);
            }
        }
    }
}
