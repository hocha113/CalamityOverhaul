using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Prefixes.Speed
{
    /// <summary>
    /// 【攻速系·残影】疾影残击：与连击迸发同池竞争，覆盖同一组攻速词缀。
    /// 出手太快，残影跟不上本体：每第五次挥击，一道苍白残影朝准星追斩一记
    /// </summary>
    internal class GodSmithAfterimageEndow : GodSmithEndow
    {
        /// <summary>触发所需使用次数</summary>
        internal const int UsesPerProc = 5;

        /// <summary>残影伤害占武器伤害比（顶级档）</summary>
        internal const float BaseDamageRatio = 0.55f;

        //与连击迸发同池，权重略低
        public override float RollWeight => 0.8f;

        public override int[] CoveredPrefixes => [
            PrefixID.Agile, PrefixID.Quick, PrefixID.Hasty, PrefixID.Rapid,
            PrefixID.Light, PrefixID.Frenzying, PrefixID.Nimble,
        ];

        public override float TierScaleFor(int prefixId) => prefixId switch {
            PrefixID.Agile => 1f,
            PrefixID.Quick => 0.9f,
            PrefixID.Hasty => 0.85f,
            PrefixID.Rapid => 0.8f,
            PrefixID.Light => 0.8f,
            PrefixID.Frenzying => 0.7f,
            _ => 0.55f,
        };

        protected override string EndowNameFallback => "Afterimage Strike";

        protected override string EndowDescFallback =>
            "Every {0}th use, a pale afterimage lashes toward the cursor dealing {1}% weapon damage";

        public override object[] DescFormatArgs(Item item)
            => [UsesPerProc, (BaseDamageRatio * 100f * TierScaleFor(item.prefix)).ToString("0.#")];

        public override void OnUseAnimation(Item item, Player player, float tierScale) {
            //计数与生成都只在 owner 端；残影弹随 NewProjectile 自动同步
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            if (player.GetModPlayer<GodSmithAfterimageEndowPlayer>().CountUse(item.type) < UsesPerProc) {
                return;
            }
            player.GetModPlayer<GodSmithAfterimageEndowPlayer>().ResetUse();
            Vector2 aim = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX * player.direction);
            int damage = Math.Max(1, (int)(player.GetWeaponDamage(item) * BaseDamageRatio * tierScale));
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithAfterimageEndow"),
                player.Center - aim * 24f, aim * 5f,
                ModContent.ProjectileType<GodSmithAfterimageBolt>(), damage, item.knockBack, player.whoAmI);
        }
    }

    /// <summary>残影计数：换武器清零，跨帧保留</summary>
    internal class GodSmithAfterimageEndowPlayer : ModPlayer
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

    /// <summary>苍白残影：从持有者身后掠出，先蓄后掠的追斩，掠速远快于出速，尾迹是幽蓝魂尘</summary>
    internal class GodSmithAfterimageBolt : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.LightBeam;

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 40;
            Projectile.tileCollide = false;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.aiStyle = 0;
        }

        public override void AI() {
            if (Projectile.timeLeft == 39 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.5f, Pitch = 0.6f }, Projectile.Center);
            }
            //前 10 更新帧猛加速（蓄势后掠出），随后缓收
            if (Projectile.timeLeft > 30) {
                Projectile.velocity *= 1.16f;
            }
            else {
                Projectile.velocity *= 0.985f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            if (Projectile.timeLeft < 10) {
                Projectile.alpha = Math.Min(255, Projectile.alpha + 26);
            }
            Lighting.AddLight(Projectile.Center, 0.2f, 0.3f, 0.45f);
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.DungeonSpirit,
                    -Projectile.velocity * 0.1f, 120, default, 0.9f);
                dust.noGravity = true;
            }
        }

        public override Color? GetAlpha(Color lightColor) => new Color(150, 200, 255, 0) * Projectile.Opacity;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            float stretch = 0.9f + Projectile.velocity.Length() * 0.05f;
            //残影双层：暗青底 + 苍白面，速度越快拉得越长
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(50, 90, 140, 0) * Projectile.Opacity, Projectile.rotation, origin,
                new Vector2(1.3f, stretch * 1.25f), 0);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(190, 225, 255, 0) * Projectile.Opacity, Projectile.rotation, origin,
                new Vector2(0.6f, stretch), 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.DungeonSpirit,
                    Main.rand.NextVector2Circular(2.5f, 2.5f), 120, default, 1f);
                dust.noGravity = true;
            }
        }
    }
}
