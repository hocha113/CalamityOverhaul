using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Guns
{
    /// <summary>
    /// 雷筒「獠牙合口」：丛林硬木双管·兽牙锁扣。<br/>
    /// ①收束弹道：每管 4 粒铅弹朝准星处收拢交汇，散布随距离闭合而非散开；
    /// ②交汇点「咬合」：弹粒在准星处合口成兽口一击（延时小范围咬合爆，瞄得准就多咬一口）；
    /// ③折管装填：逐管落膛两响。<br/>
    /// 后坐 2px（末管 3.5px），带角度上踢。<br/>
    /// 账目：每管 4 粒 ×0.9 对原版均值 3.5 粒（×1.03），咬合 ×0.55 为瞄准奖励，
    /// 弹匣占空比 0.71 → 合计约 108%（瞄准满收益 115%，待游戏内标定）
    /// </summary>
    internal class GsBoomstick : GsMagazineScheme
    {
        public override int TargetItemID => ItemID.Boomstick;

        protected override string GsDescFallback =>
            "Reforged: each barrel throws 4 slugs that converge on your cursor instead of spraying.\nWhere they cross, the jaws snap shut: a delayed bite tears whatever stands at the aim point.\nBreak open to reload both shells; nail the sweet spot for +1 slug per barrel";
        public override int MagSize => 2;
        public override int ReloadTicks => 40;
        public override GsReloadStyle Style => GsReloadStyle.Break;
        protected override int ReloadCueCount => 2;
        protected override float GetRecoil(bool lastRound) => lastRound ? 3.5f : 2f;

        protected override bool? FireNormalRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => FireBarrel(player, mp, source, position, velocity, type, damage, knockback, last: false);

        protected override bool? FireLastRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => FireBarrel(player, mp, source, position, velocity, type, damage, knockback, last: true);

        /// <summary>
        /// 一管收束弹：弹粒从枪口两侧错位出膛、弹道朝准星闭合；
        /// 同时在交汇点埋一记延时咬合（末管咬得更狠）
        /// </summary>
        private bool? FireBarrel(Player player, GsGunsEarlyPlayer mp, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback, bool last) {
            pendingMark = last ? 2f : 1f;
            float speed = velocity.Length();
            Vector2 aimUnit = velocity.SafeNormalize(Vector2.UnitX * player.direction);
            Vector2 sideUnit = new(-aimUnit.Y, aimUnit.X);

            //交汇点=准星，夹到 120~640px 的合口行程
            Vector2 focus = Main.MouseWorld;
            float dist = MathHelper.Clamp(Vector2.Distance(position, focus), 120f, 640f);
            focus = position + aimUnit * dist;

            int count = last ? 5 : 4;
            int pelletDamage = Math.Max(1, (int)(damage * 0.9f));
            for (int i = 0; i < count; i++) {
                //两侧错位出膛，弹道向焦点闭合
                float lane = (i - (count - 1) * 0.5f) * 7f;
                Vector2 spawn = position + sideUnit * lane;
                Vector2 vel = (focus - spawn).SafeNormalize(aimUnit) * speed * Main.rand.NextFloat(0.96f, 1.04f);
                Projectile.NewProjectile(source, spawn, vel, type, pelletDamage,
                    knockback * (last ? 1.5f : 1f), player.whoAmI);
            }

            //咬合：延时 = 弹粒抵达焦点的帧数
            int delay = Math.Max(4, (int)(dist / Math.Max(4f, speed)));
            Projectile.NewProjectile(source, focus, Vector2.Zero,
                ModContent.ProjectileType<GsBoomstickBiteProj>(),
                Math.Max(1, (int)(damage * (last ? 0.75f : 0.55f))), knockback,
                player.whoAmI, delay, last ? 1f : 0f);

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item36 with {
                    Volume = last ? 0.95f : 0.7f,
                    Pitch = last ? -0.3f : -0.05f
                }, position);
            }
            return false;
        }

        //==================== 折管两响装填 ====================

        protected override void OnReloadStart(Item item, Player player, GsGunsEarlyPlayer mp) {
            if (!VaultUtils.isServer) {
                //折开
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.7f, Pitch = -0.35f }, player.Center);
            }
        }

        protected override void OnReloadCue(Item item, Player player, GsGunsEarlyPlayer mp, int index, int total) {
            if (!VaultUtils.isServer) {
                //逐管落膛两响
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.75f, Pitch = -0.3f + 0.2f * index }, player.Center);
            }
        }

        protected override void OnReloadComplete(Item item, Player player, GsGunsEarlyPlayer mp) {
            if (!VaultUtils.isServer) {
                //合膛重扣
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.8f, Pitch = 0.3f }, player.Center);
            }
        }

        //==================== 后坐姿态：位移 + 角度上踢（差分，见 GsGunRecoil） ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame)
            => GunKickStyle(player, 3f, 0.09f);
    }

    /// <summary>
    /// 咬合弹：埋伏在准星交汇点的延时兽口。ai[0]=咬合延时帧，ai[1]=末管重咬旗标。<br/>
    /// 等待期驻留准星处，到点合口：小范围判定。借原版骨头贴图默认绘制
    /// </summary>
    internal class GsBoomstickBiteProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bone;

        private int Delay => (int)Projectile.ai[0];
        private bool Heavy => Projectile.ai[1] > 0f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 60;
        }

        /// <summary>合口前不判定，合口帧一次性张开判定圈</summary>
        public override bool? CanDamage() => Projectile.localAI[0] >= Delay ? null : false;

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            Projectile.localAI[0]++;
            if (Projectile.localAI[0] == Delay) {
                //合口：撑开判定 + 咬合声
                int size = Heavy ? 84 : 64;
                Projectile.Resize(size, size);
                Projectile.timeLeft = 6;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.7f, Pitch = -0.25f }, Projectile.Center);
                }
            }
            else if (Projectile.localAI[0] > Delay + 6) {
                Projectile.Kill();
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Heavy) {
                target.AddBuff(BuffID.Bleeding, 180);
            }
        }
    }
}
