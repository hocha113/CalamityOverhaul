using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Prefixes.Offense
{
    /// <summary>
    /// 【击退系·震波】冲击震波：覆盖击退词缀群（强力/强劲/难受/坚决/威吓/沉重），
    /// 命中时自落点轰出土黄色震荡波，把周围敌人齐齐掀开。控场为主，伤害为辅
    /// </summary>
    internal class GodSmithShockwaveEndow : GodSmithEndow
    {
        /// <summary>震波伤害占触发伤害比（顶级档）</summary>
        internal const float BaseDamageRatio = 0.30f;

        /// <summary>触发冷却（帧）</summary>
        internal const int CooldownFrames = 90;

        public override int[] CoveredPrefixes => [
            PrefixID.Forceful, PrefixID.Strong, PrefixID.Unpleasant,
            PrefixID.Staunch, PrefixID.Intimidating, PrefixID.Heavy,
        ];

        public override float TierScaleFor(int prefixId) => prefixId switch {
            PrefixID.Forceful => 1f,
            PrefixID.Strong => 1f,
            PrefixID.Unpleasant => 0.9f,
            PrefixID.Staunch => 0.85f,
            PrefixID.Intimidating => 0.8f,
            _ => 0.7f,
        };

        protected override string EndowNameFallback => "Concussive Wave";

        protected override string EndowDescFallback =>
            "Every 1.5s, a hit slams out a shockwave knocking nearby foes away and dealing {0}% of that hit";

        public override object[] DescFormatArgs(Item item)
            => [(BaseDamageRatio * 100f * TierScaleFor(item.prefix)).ToString("0.#")];

        public override void OnHitNPC(Player player, Item sourceItem, Projectile sourceProj, NPC target,
            in NPC.HitInfo hit, int damageDone, float tierScale) {
            if (target.friendly || target.type == NPCID.TargetDummy) {
                return;
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            //震波自身命中不再触发，防连环
            if (sourceProj != null && sourceProj.type == ModContent.ProjectileType<GodSmithShockwavePulse>()) {
                return;
            }
            //负键冷却：避开重铸饰品效果的正键约定
            if (!player.GetModPlayer<GodSmithPlayer>().TryUseCooldown(
                -ModContent.ProjectileType<GodSmithShockwavePulse>(), CooldownFrames)) {
                return;
            }
            int damage = Math.Clamp((int)(damageDone * BaseDamageRatio * tierScale), 6, 500);
            float knock = 8f * tierScale;
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithShockwaveEndow"), target.Center,
                Vector2.Zero, ModContent.ProjectileType<GodSmithShockwavePulse>(), damage, knock, player.whoAmI);
        }
    }

    /// <summary>震荡波：一记闷响的土黄冲击环，扬尘四溅，把挨到的敌人推开</summary>
    internal class GodSmithShockwavePulse : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        /// <summary>扩张终末半径（像素）</summary>
        internal const float MaxRadius = 130f;

        private float LifeRatio => 1f - Projectile.timeLeft / 16f;

        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 50;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 16;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 16;
            Projectile.aiStyle = 0;
        }

        public override void AI() {
            if (Projectile.timeLeft == 15 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.4f, Pitch = 0.35f }, Projectile.Center);
                for (int i = 0; i < 10; i++) {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke,
                        Main.rand.NextVector2Circular(3f, 2f) - Vector2.UnitY * 1.5f, 150, Color.Tan, 1.4f);
                    dust.noGravity = false;
                }
            }
            float radius = MaxRadius * (float)Math.Sqrt(LifeRatio);
            int size = (int)(radius * 2f);
            if (size > Projectile.width) {
                Projectile.Resize(size, size);
            }
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 4; i++) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    Dust dust = Dust.NewDustPerfect(Projectile.Center + ang.ToRotationVector2() * radius,
                        DustID.Sand, ang.ToRotationVector2() * 2.2f, 120, default, 1.2f);
                    dust.noGravity = true;
                }
            }
            Lighting.AddLight(Projectile.Center, 0.35f, 0.3f, 0.18f);
        }

        public override Color? GetAlpha(Color lightColor) => new Color(230, 200, 140, 0) * Projectile.Opacity;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            float radius = MaxRadius * (float)Math.Sqrt(LifeRatio);
            float fade = 1f - LifeRatio;
            float scale = radius * 2.4f / tex.Width;
            //闷土色冲击盘：外圈暗、内圈亮，快速消散
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(120, 95, 50, 0) * (0.55f * fade), 0f, origin, scale, 0);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(240, 215, 160, 0) * (0.4f * fade), 0f, origin, scale * 0.7f, 0);
            return false;
        }
    }
}
