using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using System;
using Terraria;
using Terraria.Audio;
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

    /// <summary>处决斩：在目标身上闪现的短命处决判定。
    /// 出生源为 misc，不携带神赋打标，无级联</summary>
    internal class GodSmithExecutionSlash : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.NightBeam;

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
            }
        }
    }
}
