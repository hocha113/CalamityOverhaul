using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Prefixes.Rescue
{
    /// <summary>
    /// 【救济池·藏锋】拙器藏锋：覆盖迟缓与短小的负词缀群（呆滞/缓慢/慢吞吞/笨拙/不幸/懒惰/极小/小型），
    /// 笨拙的兵刃暗藏真锋，每第五次出手放出一道雪白的藏锋一闪。
    /// 越迟钝的武器闪得越狠（档位反向：呆滞 = 1.0）。低权重逆境彩蛋
    /// </summary>
    internal class GodSmithHiddenEdgeEndow : GodSmithEndow
    {
        /// <summary>触发所需使用次数</summary>
        internal const int UsesPerProc = 5;

        /// <summary>藏锋一闪伤害占武器伤害比（最钝档）</summary>
        internal const float BaseDamageRatio = 1.30f;

        //救济彩蛋：低权重
        public override float RollWeight => 0.5f;

        public override int[] CoveredPrefixes => [
            PrefixID.Sluggish, PrefixID.Slow, PrefixID.Lethargic, PrefixID.Awkward,
            PrefixID.Unhappy, PrefixID.Lazy, PrefixID.Tiny, PrefixID.Small,
        ];

        //反向档位：越慢越小的武器，真锋越利
        public override float TierScaleFor(int prefixId) => prefixId switch {
            PrefixID.Sluggish => 1f,
            PrefixID.Slow => 0.85f,
            PrefixID.Lethargic => 0.8f,
            PrefixID.Awkward => 0.7f,
            PrefixID.Unhappy => 0.65f,
            PrefixID.Lazy => 0.6f,
            PrefixID.Tiny => 0.55f,
            _ => 0.4f,
        };

        protected override string EndowNameFallback => "Hidden Edge";

        protected override string EndowDescFallback =>
            "A clumsy weapon hides its true edge: every {0}th use flashes a white slash dealing {1}% weapon damage";

        public override object[] DescFormatArgs(Item item)
            => [UsesPerProc, (BaseDamageRatio * 100f * TierScaleFor(item.prefix)).ToString("0.#")];

        public override void OnUseAnimation(Item item, Player player, float tierScale) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            if (player.GetModPlayer<GodSmithHiddenEdgeEndowPlayer>().CountUse(item.type) < UsesPerProc) {
                return;
            }
            player.GetModPlayer<GodSmithHiddenEdgeEndowPlayer>().ResetUse();
            Vector2 aim = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX * player.direction);
            int damage = Math.Max(1, (int)(player.GetWeaponDamage(item) * BaseDamageRatio * tierScale));
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithHiddenEdgeEndow"),
                player.Center + aim * 10f, aim * 3f,
                ModContent.ProjectileType<GodSmithHiddenEdgeSlash>(), damage, item.knockBack, player.whoAmI);
        }
    }

    /// <summary>藏锋计数：换武器清零</summary>
    internal class GodSmithHiddenEdgeEndowPlayer : ModPlayer
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

    /// <summary>藏锋一闪：居合式的雪白刀光，滞出鞘、瞬全速、掠过即散，快得只留一条白线</summary>
    internal class GodSmithHiddenEdgeSlash : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.LightBeam;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 36;
            Projectile.tileCollide = false;
            Projectile.extraUpdates = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 18;
            Projectile.aiStyle = 0;
        }

        public override void AI() {
            if (Projectile.timeLeft == 35 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.65f, Pitch = 0.8f }, Projectile.Center);
            }
            //出鞘一瞬全速（居合：藏行程，露停顿）
            if (Projectile.timeLeft == 30) {
                Projectile.velocity *= 6f;
            }
            else if (Projectile.timeLeft < 30) {
                Projectile.velocity *= 0.97f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            if (Projectile.timeLeft < 10) {
                Projectile.alpha = Math.Min(255, Projectile.alpha + 26);
            }
            Lighting.AddLight(Projectile.Center, 0.35f, 0.35f, 0.4f);
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.SilverFlame,
                    -Projectile.velocity * 0.05f, 120, default, 0.8f);
                dust.noGravity = true;
            }
        }

        public override Color? GetAlpha(Color lightColor) => new Color(235, 240, 255, 0) * Projectile.Opacity;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            float stretch = 0.7f + Projectile.velocity.Length() * 0.07f;
            //雪白刀线：冷灰衬底 + 雪白刃光，速度全在拉伸里
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(90, 100, 130, 0) * Projectile.Opacity, Projectile.rotation, origin,
                new Vector2(1.1f, stretch * 1.2f), 0);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(255, 255, 255, 0) * Projectile.Opacity, Projectile.rotation, origin,
                new Vector2(0.45f, stretch), 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.SilverFlame,
                    Main.rand.NextVector2Circular(2f, 2f), 100, default, 0.9f);
                dust.noGravity = true;
            }
        }
    }
}
