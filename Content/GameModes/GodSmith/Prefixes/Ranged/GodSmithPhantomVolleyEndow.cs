using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Prefixes.Ranged
{
    /// <summary>
    /// 【远程系·签名】幻影齐射：只覆盖虚幻词缀的顶级签名神赋，与曳光/跳弹三池并立。
    /// 每第五次射击，两发幽蓝幻影弹呈扇形随行齐射，如同枪械的鬼影在替你补枪
    /// </summary>
    internal class GodSmithPhantomVolleyEndow : GodSmithEndow
    {
        /// <summary>触发所需射击次数</summary>
        internal const int UsesPerProc = 5;

        /// <summary>单发幻影弹伤害占武器伤害比</summary>
        internal const float BaseDamageRatio = 0.35f;

        /// <summary>齐射发数</summary>
        internal const int VolleyCount = 2;

        //签名彩蛋：池内偏稀有
        public override float RollWeight => 0.6f;

        public override int[] CoveredPrefixes => [PrefixID.Unreal];

        protected override string EndowNameFallback => "Phantom Volley";

        protected override string EndowDescFallback =>
            "Every {0}th shot conjures {1} phantom bolts, each dealing {2}% weapon damage";

        public override object[] DescFormatArgs(Item item)
            => [UsesPerProc, VolleyCount, (BaseDamageRatio * 100f * TierScaleFor(item.prefix)).ToString("0.#")];

        public override void OnUseAnimation(Item item, Player player, float tierScale) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            if (player.GetModPlayer<GodSmithPhantomVolleyEndowPlayer>().CountUse(item.type) < UsesPerProc) {
                return;
            }
            player.GetModPlayer<GodSmithPhantomVolleyEndowPlayer>().ResetUse();
            Vector2 aim = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX * player.direction);
            int damage = Math.Max(1, (int)(player.GetWeaponDamage(item) * BaseDamageRatio * tierScale));
            for (int i = 0; i < VolleyCount; i++) {
                float spread = (i - (VolleyCount - 1) * 0.5f) * 0.16f;
                Projectile.NewProjectile(player.GetSource_Misc("GodSmithPhantomVolleyEndow"),
                    player.Center + aim * 12f, aim.RotatedBy(spread) * 8f,
                    ModContent.ProjectileType<GodSmithPhantomBolt>(), damage, 1f, player.whoAmI);
            }
        }
    }

    /// <summary>幻影齐射计数：换武器清零</summary>
    internal class GodSmithPhantomVolleyEndowPlayer : ModPlayer
    {
        private int uses;
        private int weaponType;

        internal int CountUse(int itemType) {
            if (itemType != weaponType) {
                uses = 0;
                weaponType = itemType;
            }
            return ++uses;
        }

        internal void ResetUse() => uses = 0;
    }

    /// <summary>幽蓝幻影弹：虚体出膛，加速时渐渐凝实，中途最亮，末段散回虚无</summary>
    internal class GodSmithPhantomBolt : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.LightBeam;

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.aiStyle = 0;
            Projectile.alpha = 180;
        }

        public override void AI() {
            if (Projectile.timeLeft == 59 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item72 with { Volume = 0.45f, Pitch = 0.3f }, Projectile.Center);
            }
            //出膛虚影渐凝实（alpha 降），过中点再散逸（alpha 升）
            if (Projectile.timeLeft > 40) {
                Projectile.velocity *= 1.05f;
                Projectile.alpha = Math.Max(0, Projectile.alpha - 14);
            }
            else if (Projectile.timeLeft < 20) {
                Projectile.alpha = Math.Min(255, Projectile.alpha + 14);
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }
    }
}
