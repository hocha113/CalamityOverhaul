using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Prefixes.Accessory
{
    /// <summary>
    /// 【饰品·移速】奔行风势：覆盖饰品移速词缀链（迅捷/轻快/急速 Hasty2/敏捷 Brisk），
    /// 持续跑动蓄满风势，下一次命中放出贯穿风刃。
    /// 蓄势读数只对佩戴者本端可见，风刃实体跨端同步
    /// </summary>
    internal class GodSmithSwiftFootEndow : GodSmithEndow
    {
        /// <summary>蓄满所需奔行距离（像素，35 格）</summary>
        internal const float ChargeDistance = 560f;

        /// <summary>风刃伤害占触发伤害比（顶级档）</summary>
        internal const float BaseDamageRatio = 0.40f;

        public override int[] CoveredPrefixes => [
            PrefixID.Quick2, PrefixID.Hasty2, PrefixID.Fleeting, PrefixID.Brisk,
        ];

        public override float TierScaleFor(int prefixId) => prefixId switch {
            PrefixID.Quick2 => 1f,
            PrefixID.Hasty2 => 0.75f,
            PrefixID.Fleeting => 0.5f,
            _ => 0.25f,
        };

        protected override string EndowNameFallback => "Gale Stride";

        protected override string EndowDescFallback =>
            "Running about {0} tiles charges a gale; your next hit looses a piercing wind blade dealing {1}% of that hit";

        public override object[] DescFormatArgs(Item item)
            => [(int)(ChargeDistance / 16f), (BaseDamageRatio * 100f * TierScaleFor(item.prefix)).ToString("0.#")];

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state, float tierScale) {
            //本帧佩戴登记：距离累计放私有 ModPlayer 的 PostUpdate
            player.GetModPlayer<GodSmithSwiftFootEndowPlayer>().MarkWornThisFrame();
        }

        public override void OnWearerHitNPC(Item accessory, Player player, NPC target,
            in NPC.HitInfo hit, int damageDone, bool fromProjectile, float tierScale) {
            if (target.friendly || target.type == NPCID.TargetDummy) {
                return;
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            GodSmithSwiftFootEndowPlayer gale = player.GetModPlayer<GodSmithSwiftFootEndowPlayer>();
            if (!gale.TryConsumeCharge()) {
                return;
            }
            int damage = Math.Clamp((int)(damageDone * BaseDamageRatio * tierScale), 6, 500);
            Vector2 dir = (target.Center - player.Center).SafeNormalize(Vector2.UnitX * player.direction);
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithSwiftFootEndow"),
                player.Center + dir * 12f, dir * 6f,
                ModContent.ProjectileType<GodSmithSwiftWindBlade>(), damage, 2f, player.whoAmI);
        }
    }

    /// <summary>风势记账：佩戴帧标记 + 奔行距离累计 + 蓄满就绪；就绪闪光只本端可见</summary>
    internal class GodSmithSwiftFootEndowPlayer : ModPlayer
    {
        private bool wornThisFrame;
        private float distance;
        private bool charged;

        internal void MarkWornThisFrame() => wornThisFrame = true;

        internal bool TryConsumeCharge() {
            if (!charged) {
                return false;
            }
            charged = false;
            return true;
        }

        public override void ResetEffects() => wornThisFrame = false;

        public override void PostUpdate() {
            if (!GameModeSystem.GodSmithActive || !wornThisFrame || charged) {
                return;
            }
            distance += Player.velocity.Length();
            if (distance < GodSmithSwiftFootEndow.ChargeDistance) {
                return;
            }
            distance = 0f;
            charged = true;
            //就绪读数：只佩戴者本端可见
            if (Player.whoAmI == Main.myPlayer && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item24 with { Volume = 0.4f, Pitch = 0.6f }, Player.Center);
                for (int i = 0; i < 10; i++) {
                    Dust dust = Dust.NewDustPerfect(Player.Center, DustID.Cloud,
                        Main.rand.NextVector2Circular(3f, 3f), 130, default, 1.1f);
                    dust.noGravity = true;
                }
            }
        }
    }

    /// <summary>贯穿风刃：一泓青白气刃破风而出，越飞越薄，途中留下气旋涟漪</summary>
    internal class GodSmithSwiftWindBlade : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.LightBeam;

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 5;
            Projectile.timeLeft = 45;
            Projectile.tileCollide = false;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.aiStyle = 0;
        }

        public override void AI() {
            if (Projectile.timeLeft == 44 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item39 with { Volume = 0.45f, Pitch = 0.55f }, Projectile.Center);
            }
            //破风加速后缓收
            if (Projectile.timeLeft > 34) {
                Projectile.velocity *= 1.12f;
            }
            else {
                Projectile.velocity *= 0.99f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            if (Projectile.timeLeft < 12) {
                Projectile.alpha = Math.Min(255, Projectile.alpha + 22);
            }
            Lighting.AddLight(Projectile.Center, 0.2f, 0.35f, 0.35f);
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Cloud,
                    -Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.5f, 0.5f), 140, default, 0.9f);
                dust.noGravity = true;
            }
        }

        public override Color? GetAlpha(Color lightColor) => new Color(170, 240, 230, 0) * Projectile.Opacity;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            //气刃随飞行变薄拉长
            float age = 1f - Projectile.timeLeft / 45f;
            float stretch = (0.9f + Projectile.velocity.Length() * 0.05f) * (1f + age * 0.5f);
            float width = 0.8f - age * 0.35f;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(50, 120, 110, 0) * Projectile.Opacity, Projectile.rotation, origin,
                new Vector2(width * 1.6f, stretch * 1.15f), 0);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(200, 255, 245, 0) * Projectile.Opacity, Projectile.rotation, origin,
                new Vector2(width * 0.7f, stretch), 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Cloud,
                    Main.rand.NextVector2Circular(2.5f, 2.5f), 130, default, 1f);
                dust.noGravity = true;
            }
        }
    }
}
