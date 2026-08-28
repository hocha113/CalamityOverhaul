using Microsoft.Xna.Framework.Graphics;
using System;
using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Prefixes.Offense
{
    /// <summary>
    /// 【尺寸系·回响】剑风回响：覆盖近战尺寸词缀群（野蛮/巨大/笨重/危险/大型），
    /// 命中时刃风自落点荡开成青钢色环浪，斩击的余韵扫过周围其余敌人。
    /// 环浪不打原目标，纯范围收益，单体不超模
    /// </summary>
    internal class GodSmithEchoSlashEndow : GodSmithEndow
    {
        /// <summary>环浪伤害占触发伤害比（顶级档）</summary>
        internal const float BaseDamageRatio = 0.45f;

        /// <summary>触发冷却（帧），防止一次横扫多目标连爆</summary>
        internal const int CooldownFrames = 20;

        public override int[] CoveredPrefixes => [
            PrefixID.Savage, PrefixID.Massive, PrefixID.Bulky, PrefixID.Dangerous, PrefixID.Large,
        ];

        public override float TierScaleFor(int prefixId) => prefixId switch {
            PrefixID.Savage => 1f,
            PrefixID.Massive => 0.85f,
            PrefixID.Bulky => 0.75f,
            PrefixID.Dangerous => 0.7f,
            _ => 0.6f,
        };

        protected override string EndowNameFallback => "Blade Echo";

        protected override string EndowDescFallback =>
            "Hits echo outward as a blade-wind ring dealing {0}% of that hit to other nearby foes";

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
            //负键冷却：避开重铸饰品效果的正键（物品 type）约定，一物一神赋不会撞
            if (!player.GetModPlayer<GodSmithPlayer>().TryUseCooldown(
                -ModContent.ProjectileType<GodSmithEchoSlashWave>(), CooldownFrames)) {
                return;
            }
            int damage = Math.Clamp((int)(damageDone * BaseDamageRatio * tierScale), 8, 600);
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithEchoSlashEndow"), target.Center,
                Vector2.Zero, ModContent.ProjectileType<GodSmithEchoSlashWave>(), damage, 4f,
                player.whoAmI, target.whoAmI);
        }
    }

    /// <summary>剑风环浪：青钢色刃风自落点荡开，环沿由钢屑勾勒，扩张后散尽。
    /// ai[0] = 原目标 whoAmI，环浪对其免疫，只扫其余敌人</summary>
    internal class GodSmithEchoSlashWave : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        /// <summary>扩张终末半径（像素）</summary>
        internal const float MaxRadius = 150f;

        private float LifeRatio => 1f - Projectile.timeLeft / 20f;

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 20;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.aiStyle = 0;
        }

        public override bool? CanHitNPC(NPC target) => target.whoAmI == (int)Projectile.ai[0] ? false : null;

        public override void AI() {
            if (Projectile.timeLeft == 19 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.55f, Pitch = 0.3f }, Projectile.Center);
            }
            //判定盒随环浪扩张，减速式生长：前段快后段缓
            float radius = MaxRadius * (float)Math.Sqrt(LifeRatio);
            int size = (int)(radius * 2f);
            if (size > Projectile.width) {
                Projectile.Resize(size, size);
            }
            if (!VaultUtils.isServer) {
                //环沿钢屑：沿当前半径撒一圈，勾出可读的环形
                for (int i = 0; i < 6; i++) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    Dust dust = Dust.NewDustPerfect(Projectile.Center + ang.ToRotationVector2() * radius,
                        DustID.Platinum, ang.ToRotationVector2() * 1.6f, 100, default, 1.1f);
                    dust.noGravity = true;
                }
            }
            Lighting.AddLight(Projectile.Center, 0.2f, 0.35f, 0.4f);
        }

        public override Color? GetAlpha(Color lightColor) => new Color(140, 220, 235, 0) * Projectile.Opacity;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            float radius = MaxRadius * (float)Math.Sqrt(LifeRatio);
            float fade = 1f - LifeRatio;
            //软圆盘随扩张变薄，营造消散中的风环
            float scale = radius * 2.4f / tex.Width;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(60, 130, 150, 0) * (0.5f * fade), 0f, origin, scale, 0);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(170, 235, 245, 0) * (0.35f * fade), 0f, origin, scale * 0.75f, 0);
            return false;
        }
    }
}
