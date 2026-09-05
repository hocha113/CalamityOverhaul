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

    /// <summary>震荡波：一记闷响的扩张冲击环，把挨到的敌人推开；绘制只有一笔按当前半径缩放的原版气泡贴图</summary>
    internal class GodSmithShockwavePulse : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bubble;

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
            }
            float radius = MaxRadius * (float)Math.Sqrt(LifeRatio);
            int size = (int)(radius * 2f);
            if (size > Projectile.width) {
                Projectile.Resize(size, size);
            }
        }

        /// <summary>区域弹一笔：原版气泡贴图按当前判定直径缩放画在中心，随生命淡出</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * (1f - LifeRatio), 0f,
                tex.Size() * 0.5f, Projectile.width / (float)tex.Width, SpriteEffects.None, 0);
            return false;
        }
    }
}
