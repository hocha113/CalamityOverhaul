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

    /// <summary>威压爪痕：三道黑红爪影自上而下撕开，先撕出再淡收</summary>
    internal class GodSmithMenaceClaw : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.NightBeam;

        private float Seed => Projectile.whoAmI * 2.399f;

        private float LifeRatio => 1f - Projectile.timeLeft / 20f;

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
                for (int i = 0; i < 8; i++) {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke,
                        Main.rand.NextVector2Circular(3f, 3f), 170, Color.Black, 1.3f);
                    dust.noGravity = true;
                }
            }
            Lighting.AddLight(Projectile.Center, 0.35f, 0.05f, 0.08f);
        }

        public override Color? GetAlpha(Color lightColor) => new Color(220, 40, 60, 0) * Projectile.Opacity;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            float grow = LifeRatio < 0.3f ? LifeRatio / 0.3f : 1f - (LifeRatio - 0.3f) / 0.7f;
            float rot = Seed % 0.5f - 0.25f + MathHelper.PiOver4 * 0.5f;
            //三道平行爪影：横向错位，各自双层（黑衬底 + 血红面）
            for (int i = -1; i <= 1; i++) {
                Vector2 offset = new Vector2(i * 20f, 0f).RotatedBy(rot);
                Main.EntitySpriteDraw(tex, Projectile.Center + offset - Main.screenPosition, null,
                    new Color(30, 5, 10, 0) * (0.85f * grow), rot, origin,
                    new Vector2(1.2f, 1.5f * (0.5f + grow)), 0);
                Main.EntitySpriteDraw(tex, Projectile.Center + offset - Main.screenPosition, null,
                    new Color(210, 40, 55, 0) * grow, rot, origin,
                    new Vector2(0.55f, 1.3f * (0.5f + grow)), 0);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.CrimsonTorch,
                    Main.rand.NextVector2Circular(3f, 3f), 110, default, 1.1f);
                dust.noGravity = true;
            }
        }
    }
}
