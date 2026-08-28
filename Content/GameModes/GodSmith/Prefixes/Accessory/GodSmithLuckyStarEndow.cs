using Microsoft.Xna.Framework.Graphics;
using System;
using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Prefixes.Accessory
{
    /// <summary>
    /// 【饰品·会心】幸运星芒：覆盖饰品暴击词缀（幸运/精确），
    /// 佩戴者打出暴击时，一枚金色幸运星坠向目标头顶补上一记。
    /// 星弹自身命中同帧压制再触发（私有 ModPlayer 记帧），防星生星
    /// </summary>
    internal class GodSmithLuckyStarEndow : GodSmithEndow
    {
        /// <summary>星坠伤害占触发伤害比（顶级档）</summary>
        internal const float BaseDamageRatio = 0.35f;

        /// <summary>触发冷却（帧）</summary>
        internal const int CooldownFrames = 120;

        public override int[] CoveredPrefixes => [PrefixID.Lucky, PrefixID.Precise];

        public override float TierScaleFor(int prefixId) => prefixId == PrefixID.Lucky ? 1f : 0.5f;

        protected override string EndowNameFallback => "Lucky Starfall";

        protected override string EndowDescFallback =>
            "Critical hits call down a lucky star dealing {0}% of that hit";

        public override object[] DescFormatArgs(Item item)
            => [(BaseDamageRatio * 100f * TierScaleFor(item.prefix)).ToString("0.#")];

        public override void OnWearerHitNPC(Item accessory, Player player, NPC target,
            in NPC.HitInfo hit, int damageDone, bool fromProjectile, float tierScale) {
            if (!hit.Crit || target.friendly || target.type == NPCID.TargetDummy) {
                return;
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            //星弹自己暴击的同帧回声不再生星
            if (player.GetModPlayer<GodSmithLuckyStarEndowPlayer>().SuppressedThisFrame) {
                return;
            }
            if (!player.GetModPlayer<GodSmithPlayer>().TryUseCooldown(
                -ModContent.ProjectileType<GodSmithLuckyStarBolt>(), CooldownFrames)) {
                return;
            }
            int damage = Math.Clamp((int)(damageDone * BaseDamageRatio * tierScale), 6, 500);
            Vector2 spawn = target.Center - Vector2.UnitY * 240f + Vector2.UnitX * Main.rand.NextFloat(-40f, 40f);
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithLuckyStarEndow"), spawn,
                Vector2.UnitY * 4f, ModContent.ProjectileType<GodSmithLuckyStarBolt>(), damage, 2f,
                player.whoAmI, target.whoAmI);
        }
    }

    /// <summary>幸运星的同帧压制记账：星弹命中先落 flag，佩戴钩子随后查询</summary>
    internal class GodSmithLuckyStarEndowPlayer : ModPlayer
    {
        private uint suppressFrame;

        internal bool SuppressedThisFrame => suppressFrame == Main.GameUpdateCount;

        internal void SuppressNow() => suppressFrame = Main.GameUpdateCount;
    }

    /// <summary>幸运星坠：自高处坠落加速，微微修向目标，砸中迸开一蓬金屑</summary>
    internal class GodSmithLuckyStarBolt : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FallingStar;

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 80;
            Projectile.tileCollide = false;
            Projectile.aiStyle = 0;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => Main.player[Projectile.owner].GetModPlayer<GodSmithLuckyStarEndowPlayer>().SuppressNow();

        public override void AI() {
            if (Projectile.timeLeft == 79 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.55f, Pitch = 0.5f }, Projectile.Center);
            }
            //坠落加速 + 朝目标轻微修向
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.55f, 20f);
            NPC target = Main.npc[(int)Projectile.ai[0]];
            if (target.active && target.CanBeChasedBy()) {
                float drift = Math.Sign(target.Center.X - Projectile.Center.X) * 0.35f;
                Projectile.velocity.X = MathHelper.Clamp(Projectile.velocity.X + drift, -8f, 8f);
            }
            Projectile.rotation += 0.3f;
            Lighting.AddLight(Projectile.Center, 0.5f, 0.42f, 0.15f);
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.YellowStarDust,
                    -Projectile.velocity * 0.1f, 90, default, 1f);
                dust.noGravity = true;
            }
        }

        public override Color? GetAlpha(Color lightColor) => new Color(255, 235, 140, 0) * Projectile.Opacity;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            //坠速拉出竖向星痕：金橙衬底 + 亮金星体
            float stretch = 1f + Math.Abs(Projectile.velocity.Y) * 0.04f;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(180, 120, 20, 0) * (0.7f * Projectile.Opacity), Projectile.rotation, origin,
                new Vector2(1f, stretch) * 1.1f, 0);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(255, 245, 190, 0) * Projectile.Opacity, Projectile.rotation, origin,
                new Vector2(0.7f, 0.7f * stretch), 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.5f, Pitch = 0.6f }, Projectile.Center);
            for (int i = 0; i < 10; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame,
                    Main.rand.NextVector2Circular(4f, 4f), 90, default, 1.2f);
                dust.noGravity = true;
            }
        }
    }
}
