using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Guns
{
    /// <summary>
    /// 火枪「贯石重弹」：纯前装 MagSize 1，每发皆末发。<br/>
    /// 重弹：弹速 +40%、穿透 +1；Reload 55t 三段式（倒药/杵压/上膛三响）。后坐 3px，跳射位移可感知。<br/>
    /// 账目：周期 max(32ut,55t)=55t，射速比 0.58；伤害行 ×1.6 → 0.93，
    /// 穿透群体价值补足至约 105%（待游戏内标定）
    /// </summary>
    internal class GsMusket : GsMagazineScheme
    {
        public override int TargetItemID => ItemID.Musket;

        protected override string GsDescFallback =>
            "Reforged: a true muzzle-loader; every ball is a stone-piercer that flies faster and punches through one more foe.\nReload in three beats: pour, ram, prime";
        public override int MagSize => 1;
        public override int ReloadTicks => 55;
        public override GsReloadStyle Style => GsReloadStyle.Muzzle;
        protected override float GetRecoil(bool lastRound) => 3f;
        protected override int ReloadCueCount => 3;
        //前装长枪：黑火药重响，后挫深、上踢重
        protected override float DefaultRecoilShift => 3.2f;
        protected override float DefaultRecoilKick => 0.085f;

        /// <summary>伤害行：把装填空窗折回持续 DPS 的补偿，账目见类注释</summary>
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) => damage *= 1.6f;

        protected override void ModifyShot(Item item, Player player, GsGunsEarlyPlayer mp, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback, bool lastRound) {
            velocity *= 1.4f;   //贯石重弹：弹速 +40%
        }

        protected override bool? FireLastRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //重弹打 1 档标（穿透 +1）
            pendingMark = 1f;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item36 with { Volume = 0.5f, Pitch = -0.45f }, position);
            }
            return null;    //原版弹幕照常生成，交给路由打标
        }

        protected override void OnSpawnMarkedExtra(Projectile proj, GodSmithProjRouter router) {
            //穿透只在 owner 端生效即可（命中在 owner 端裁决）；>0 守卫防 -1 无限穿被写坏
            if (proj.penetrate > 0) {
                proj.penetrate += 1;
            }
        }

        //==================== 三段式前装 ====================

        protected override void OnReloadStart(Item item, Player player, GsGunsEarlyPlayer mp) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.5f, Pitch = -0.3f }, player.Center);
            }
        }

        protected override void OnReloadCue(Item item, Player player, GsGunsEarlyPlayer mp, int index, int total) {
            if (!VaultUtils.isServer) {
                //三响：倒药沙声、杵压闷响、上膛脆响
                float pitch = index switch { 1 => -0.5f, 2 => -0.15f, _ => 0.3f };
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.6f + 0.1f * index, Pitch = pitch }, player.Center);
            }
        }
    }
}
