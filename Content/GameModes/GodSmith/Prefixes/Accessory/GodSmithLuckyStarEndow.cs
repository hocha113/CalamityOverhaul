using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using System;
using Terraria;
using Terraria.Audio;
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

    /// <summary>幸运星坠：自高处坠落加速，微微修向目标</summary>
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
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.5f, Pitch = 0.6f }, Projectile.Center);
        }
    }
}
