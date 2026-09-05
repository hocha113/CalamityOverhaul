using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Guns
{
    /// <summary>
    /// 燧发手枪「双管齐鸣」：双膛快击。<br/>
    /// MagSize 2，两发之间射速 +40%；末发以 1 发弹药打出 V 形 2 弹（各 85%），
    /// 击退 ×2、后坐跳 2.5px。Reload 42t 前装。<br/>
    /// 账目：周期 16+max(11.4,29)=45t 打 2 发（射速比 0.71），末发均值 1.35，
    /// 伤害行 ×1.15 → 约 110%（待游戏内标定）
    /// </summary>
    internal class GsFlintlockPistol : GsMagazineScheme
    {
        public override int TargetItemID => ItemID.FlintlockPistol;

        protected override string GsDescFallback =>
            "Reforged: two chambers, the second shot comes 40% faster;\nthe last chamber fires a V-shaped double ball from a single bullet with doubled knockback.\nHit the sweet spot when reloading for +25% on the next shot";
        public override int MagSize => 2;
        public override int ReloadTicks => 42;
        public override GsReloadStyle Style => GsReloadStyle.Muzzle;
        protected override float GetRecoil(bool lastRound) => lastRound ? 2.5f : 1f;
        protected override int ReloadCueCount => 3;
        //燧发手枪：轻快小踢
        protected override float DefaultRecoilShift => 1.6f;
        protected override float DefaultRecoilKick => 0.07f;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) => damage *= 1.15f;

        /// <summary>两发间射速 +40%：首发打完（余 1）后下一击更快</summary>
        public override float GsUseSpeedMultiplier(Item item, Player player)
            => State(player).magLeft == 1 ? 1.4f : 1f;

        protected override bool? FireLastRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //双管齐鸣：1 发弹药化 V 形 2 弹，各 85%、击退 ×2
            pendingMark = 1f;
            int vDamage = (int)(damage * 0.85f);
            for (int i = 0; i < 2; i++) {
                Vector2 vel = velocity.RotatedBy(MathHelper.ToRadians(i == 0 ? -4f : 4f));
                Projectile.NewProjectile(source, position, vel, type, vDamage, knockback * 2f, player.whoAmI);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item36 with { Volume = 0.6f, Pitch = 0.25f }, position);
            }
            return false;
        }

        //==================== 前装三段 ====================

        protected override void OnReloadCue(Item item, Player player, GsGunsEarlyPlayer mp, int index, int total) {
            if (!VaultUtils.isServer) {
                float pitch = index switch { 1 => -0.45f, 2 => -0.1f, _ => 0.35f };
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.55f, Pitch = pitch }, player.Center);
            }
        }
    }
}
