using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Prefixes.Accessory
{
    /// <summary>
    /// 【饰品·伤害】威压杀意：覆盖饰品伤害词缀链（威吓/愤怒/尖刺/锯齿），
    /// 命中累积杀意，第六击撕出三道黑红爪痕。爪痕命中同帧压制，防自喂
    /// </summary>
    internal class GodSmithMenaceEndow : GodSmithEndow
    {
        /// <summary>触发所需命中数</summary>
        internal const int HitsPerProc = 6;

        /// <summary>爪痕伤害占触发伤害比（顶级档）</summary>
        internal const float BaseDamageRatio = 0.40f;

        public override int[] CoveredPrefixes => [
            PrefixID.Menacing, PrefixID.Angry, PrefixID.Spiked, PrefixID.Jagged,
        ];

        public override float TierScaleFor(int prefixId) => prefixId switch {
            PrefixID.Menacing => 1f,
            PrefixID.Angry => 0.75f,
            PrefixID.Spiked => 0.5f,
            _ => 0.25f,
        };

        protected override string EndowNameFallback => "Killing Intent";

        protected override string EndowDescFallback =>
            "Hits build killing intent; the {0}th strike rends the foe for {1}% of that hit";

        public override object[] DescFormatArgs(Item item)
            => [HitsPerProc, (BaseDamageRatio * 100f * TierScaleFor(item.prefix)).ToString("0.#")];

        public override void OnWearerHitNPC(Item accessory, Player player, NPC target,
            in NPC.HitInfo hit, int damageDone, bool fromProjectile, float tierScale) {
            if (target.friendly || target.type == NPCID.TargetDummy) {
                return;
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            GodSmithMenaceEndowPlayer intent = player.GetModPlayer<GodSmithMenaceEndowPlayer>();
            //爪痕自己的命中不计数
            if (intent.SuppressedThisFrame) {
                return;
            }
            if (intent.CountHit() < HitsPerProc) {
                return;
            }
            intent.ResetHits();
            int damage = Math.Clamp((int)(damageDone * BaseDamageRatio * tierScale), 6, 500);
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithMenaceEndow"), target.Center,
                Vector2.Zero, ModContent.ProjectileType<GodSmithMenaceClaw>(), damage, 3f, player.whoAmI);
        }
    }

    /// <summary>杀意记账：命中计数 + 爪痕同帧压制</summary>
    internal class GodSmithMenaceEndowPlayer : ModPlayer
    {
        private int hits;
        private uint suppressFrame;

        internal bool SuppressedThisFrame => suppressFrame == Main.GameUpdateCount;

        internal void SuppressNow() => suppressFrame = Main.GameUpdateCount;

        internal int CountHit() => ++hits;

        internal void ResetHits() => hits = 0;
    }

    /// <summary>威压爪痕：一记自上而下撕开的短命爪击判定</summary>
    internal class GodSmithMenaceClaw : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.NightBeam;

        public override void SetDefaults() {
            Projectile.width = 62;
            Projectile.height = 62;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 20;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.aiStyle = 0;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => Main.player[Projectile.owner].GetModPlayer<GodSmithMenaceEndowPlayer>().SuppressNow();

        public override void AI() {
            if (Projectile.timeLeft == 19 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.6f, Pitch = -0.5f }, Projectile.Center);
            }
        }
    }
}
