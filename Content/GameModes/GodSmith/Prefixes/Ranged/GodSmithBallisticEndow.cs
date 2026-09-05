using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Prefixes.Ranged
{
    /// <summary>
    /// 【远程系·曳光】穿云曳光：覆盖远程词缀群（虚幻/致命/精准/强大/坚决/迅速/急促/威吓），
    /// 每第四次射击附送一发金白曳光弹，撕开一条贯穿队列的弹道
    /// </summary>
    internal class GodSmithBallisticEndow : GodSmithEndow
    {
        /// <summary>触发所需射击次数</summary>
        internal const int UsesPerProc = 4;

        /// <summary>曳光弹伤害占武器伤害比（顶级档）</summary>
        internal const float BaseDamageRatio = 0.50f;

        public override int[] CoveredPrefixes => [
            PrefixID.Unreal, PrefixID.Deadly, PrefixID.Sighted, PrefixID.Powerful,
            PrefixID.Staunch, PrefixID.Rapid, PrefixID.Hasty, PrefixID.Intimidating,
        ];

        public override float TierScaleFor(int prefixId) => prefixId switch {
            PrefixID.Unreal => 1f,
            PrefixID.Deadly => 0.85f,
            PrefixID.Sighted => 0.75f,
            PrefixID.Powerful => 0.75f,
            PrefixID.Staunch => 0.7f,
            PrefixID.Rapid => 0.65f,
            PrefixID.Hasty => 0.65f,
            _ => 0.55f,
        };

        protected override string EndowNameFallback => "Piercing Tracer";

        protected override string EndowDescFallback =>
            "Every {0}th shot adds a piercing tracer round dealing {1}% weapon damage";

        public override object[] DescFormatArgs(Item item)
            => [UsesPerProc, (BaseDamageRatio * 100f * TierScaleFor(item.prefix)).ToString("0.#")];

        public override void OnUseAnimation(Item item, Player player, float tierScale) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            if (player.GetModPlayer<GodSmithBallisticEndowPlayer>().CountUse(item.type) < UsesPerProc) {
                return;
            }
            player.GetModPlayer<GodSmithBallisticEndowPlayer>().ResetUse();
            Vector2 aim = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX * player.direction);
            int damage = Math.Max(1, (int)(player.GetWeaponDamage(item) * BaseDamageRatio * tierScale));
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithBallisticEndow"),
                player.Center + aim * 16f, aim * 9f,
                ModContent.ProjectileType<GodSmithBallisticTracer>(), damage, 1.5f, player.whoAmI);
        }
    }

    /// <summary>曳光计数：换武器清零</summary>
    internal class GodSmithBallisticEndowPlayer : ModPlayer
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

    /// <summary>金白曳光弹：出膛即全速，穿透中缓缓减速失能</summary>
    internal class GodSmithBallisticTracer : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BulletHighVelocity;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 5;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = true;
            Projectile.extraUpdates = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
            Projectile.aiStyle = 0;
        }

        public override void AI() {
            if (Projectile.timeLeft == 89 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item40 with { Volume = 0.5f, Pitch = 0.5f }, Projectile.Center);
            }
            //贯穿途中逐渐降速失能，不做匀速直飞
            Projectile.velocity *= 0.995f;
            //原版子弹贴图竖向朝上，旋转补四分之一圈
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            if (Projectile.timeLeft < 16) {
                Projectile.alpha = Math.Min(255, Projectile.alpha + 18);
            }
        }
    }
}
