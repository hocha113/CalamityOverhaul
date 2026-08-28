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
    /// 【伤害系·处决】处决斩痕：覆盖通用伤害词缀群（神圣/恶魔/无情/精良/锋利/致命/凶残/尖锐/危害/污秽），
    /// 命中生命低于斩杀线的敌人时追出一道绯黑处决十字斩。阈值与追伤随档位回缩（神圣 = 1.0 基准）
    /// </summary>
    internal class GodSmithExecutionEndow : GodSmithEndow
    {
        /// <summary>顶级档斩杀线（生命比例）</summary>
        internal const float BaseThreshold = 0.20f;

        /// <summary>处决斩伤害占触发伤害比（顶级档）</summary>
        internal const float BaseDamageRatio = 0.45f;

        public override int[] CoveredPrefixes => [
            PrefixID.Godly, PrefixID.Demonic, PrefixID.Ruthless, PrefixID.Superior,
            PrefixID.Sharp, PrefixID.Deadly2, PrefixID.Murderous, PrefixID.Pointy,
            PrefixID.Hurtful, PrefixID.Nasty,
        ];

        public override float TierScaleFor(int prefixId) => prefixId switch {
            PrefixID.Godly => 1f,
            PrefixID.Demonic => 0.95f,
            PrefixID.Ruthless => 0.85f,
            PrefixID.Superior => 0.75f,
            PrefixID.Sharp => 0.7f,
            PrefixID.Deadly2 => 0.7f,
            PrefixID.Murderous => 0.6f,
            PrefixID.Nasty => 0.5f,
            _ => 0.55f,
        };

        protected override string EndowNameFallback => "Executioner's Line";

        protected override string EndowDescFallback =>
            "Striking a foe below {0}% life looses an executioner's slash dealing {1}% of that hit";

        public override object[] DescFormatArgs(Item item) {
            float tier = TierScaleFor(item.prefix);
            return [(BaseThreshold * 100f * tier).ToString("0.#"), (BaseDamageRatio * 100f * tier).ToString("0.#")];
        }

        public override void OnHitNPC(Player player, Item sourceItem, Projectile sourceProj, NPC target,
            in NPC.HitInfo hit, int damageDone, float tierScale) {
            if (target.friendly || target.type == NPCID.TargetDummy || target.lifeMax <= 5) {
                return;
            }
            //只处决还活着且踩进斩杀线的目标；已死目标不补刀
            if (target.life <= 0 || target.life > target.lifeMax * BaseThreshold * tierScale) {
                return;
            }
            //权威动作只在 owner 端；misc 出生源不带神赋打标，处决斩命中不会再触发本钩子
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            int damage = Math.Clamp((int)(damageDone * BaseDamageRatio * tierScale), 8, 800);
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithExecutionEndow"), target.Center,
                Vector2.Zero, ModContent.ProjectileType<GodSmithExecutionSlash>(), damage, 2f, player.whoAmI);
        }
    }

    /// <summary>处决十字斩：绯黑双痕在目标身上交错闪现，短促张开后收拢熄灭。
    /// 出生源为 misc，不携带神赋打标，无级联</summary>
    internal class GodSmithExecutionSlash : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.NightBeam;

        /// <summary>逐实例确定性种子（禁 Main.rand 进绘制）</summary>
        private float Seed => Projectile.whoAmI * 2.399f;

        private float LifeRatio => 1f - Projectile.timeLeft / 22f;

        public override void SetDefaults() {
            Projectile.width = 56;
            Projectile.height = 56;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 22;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 22;
            Projectile.aiStyle = 0;
        }

        public override void AI() {
            if (Projectile.timeLeft == 21 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.7f, Pitch = -0.35f }, Projectile.Center);
                for (int i = 0; i < 10; i++) {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.CrimsonTorch,
                        Main.rand.NextVector2Circular(4f, 4f), 120, default, 1.4f);
                    dust.noGravity = true;
                }
            }
            Lighting.AddLight(Projectile.Center, 0.5f, 0.08f, 0.1f);
        }

        public override Color? GetAlpha(Color lightColor) => new Color(255, 60, 70, 0) * Projectile.Opacity;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            //张开-收拢的长度生命周期：前 1/3 弹出，后 2/3 收敛
            float grow = LifeRatio < 0.35f ? LifeRatio / 0.35f : 1f - (LifeRatio - 0.35f) / 0.65f;
            float len = 0.6f + 2.2f * grow;
            float fade = Math.Clamp(grow + 0.15f, 0f, 1f);
            for (int i = 0; i < 2; i++) {
                float rot = Seed + MathHelper.PiOver4 + i * MathHelper.PiOver2;
                //底层暗红宽痕 + 面层亮绯窄痕，双层交错成十字
                Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                    new Color(120, 10, 30, 0) * (0.8f * fade), rot, origin, new Vector2(1.5f, len * 1.15f), 0);
                Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                    new Color(255, 90, 100, 0) * fade, rot, origin, new Vector2(0.7f, len), 0);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke,
                    Main.rand.NextVector2Circular(2f, 2f), 160, Color.DarkRed, 1.1f);
                dust.noGravity = true;
            }
        }
    }
}
