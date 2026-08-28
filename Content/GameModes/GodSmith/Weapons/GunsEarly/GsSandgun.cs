using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsEarly
{
    /// <summary>
    /// 沙枪「熔沙成琉」：黄铜沙斗喷枪·石英观察窗。<br/>
    /// ①蓄热：连续喷沙给沙斗升温（枪口渐现热浪与烬点），停手缓冷；
    /// ②热满入「熔琉态」：沙弹在膛内烧成玻璃镖，更快更穿，命中迸溅棱片脆响；
    /// ③沙斗装填沙沙倒灌三拍；完美装填直接灌满热量。<br/>
    /// 普通沙弹保持原版（含落沙成块的老脾气）。后坐 2px + 角度踢。<br/>
    /// 账目：射速原版；玻璃镖 ×1.15 且穿透 +2，热态占空约 50%，
    /// 伤害行 ×1.0 → 约 110%（待游戏内标定）
    /// </summary>
    internal class GsSandgun : GsMagazineScheme
    {
        public override int TargetItemID => ItemID.Sandgun;

        protected override string GsDescFallback =>
            "Reforged: sustained fire heats the hopper; keep pouring and the sand melts in the chamber.\n" +
            "While molten, rounds leave as glass bolts that fly faster, pierce deeper, and shatter into razor facets.\n" +
            "Reload pours in three rustling beats; a sweet-spot pour ignites the hopper instantly";

        public override int MagSize => 12;
        public override int ReloadTicks => 46;
        public override GsReloadStyle Style => GsReloadStyle.Hopper;
        protected override int ReloadCueCount => 3;
        protected override bool EjectsShell => false;
        protected override float GetRecoil(bool lastRound) => 2f;

        /// <summary>热满值（发数积热）</summary>
        internal const int HeatMax = 8;
        /// <summary>熔琉阈值</summary>
        internal const int MoltenAt = 6;

        /// <summary>熔琉漂字</summary>
        internal static LocalizedText MoltenText;

        public override void GsSetStaticDefaults() {
            MoltenText = this.GetLocalization("Molten", () => "Molten!");
        }

        protected override void ModifyShot(Item item, Player player, GsGunsEarlyPlayer mp, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback, bool lastRound) {
            GsSandgunPlayer sp = player.GetModPlayer<GsSandgunPlayer>();
            if (sp.heat >= MoltenAt) {
                //熔琉态：置换玻璃镖
                type = ModContent.ProjectileType<GsSandgunGlassProj>();
                velocity *= 1.5f;
                damage = (int)(damage * 1.15f);
            }
        }

        protected override bool? FireNormalRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => HeatTick(player, position, velocity, false);

        protected override bool? FireLastRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            pendingMark = 1f;   //斗底弹：沙重一分
            return HeatTick(player, position, velocity, true);
        }

        /// <summary>每喷一口积热；跨过熔琉线时观察窗亮橙提示</summary>
        private bool? HeatTick(Player player, Vector2 position, Vector2 velocity, bool last) {
            GsSandgunPlayer sp = player.GetModPlayer<GsSandgunPlayer>();
            int before = sp.heat;
            sp.heat = Math.Min(HeatMax, sp.heat + 1);
            sp.coolDelay = 50;
            if (before < MoltenAt && sp.heat >= MoltenAt && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.6f, Pitch = 0.3f }, player.Center);
                CombatText.NewText(player.getRect(), new Color(255, 168, 80), MoltenText.Value);
            }
            if (!VaultUtils.isServer && sp.heat >= MoltenAt) {
                //熔琉枪口：热浪扭动的烬点
                Vector2 aim = velocity.SafeNormalize(Vector2.UnitX);
                PRTLoader.NewParticle<PRT_DefEmber>(position + aim * 8f,
                    aim * 1.5f - Vector2.UnitY * 0.4f, new Color(255, 176, 88),
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(12, 20));
            }
            return null;
        }

        //==================== 冷却与观察窗（本机手持每帧） ====================

        protected override void HoldTick(Item item, Player player, GsGunsEarlyPlayer mp) {
            GsSandgunPlayer sp = player.GetModPlayer<GsSandgunPlayer>();
            if (sp.coolDelay > 0) {
                sp.coolDelay--;
            }
            else if (sp.heat > 0 && Main.GameUpdateCount % 24 == 0) {
                sp.heat--;
            }
            //熔琉常态热浪（预算每 8 帧一粒）
            if (!VaultUtils.isServer && sp.heat >= MoltenAt && Main.GameUpdateCount % 8 == 0) {
                PRTLoader.NewParticle<PRT_Smoke>(player.Center + new Vector2(player.direction * 14f, -2f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.4f, 0.8f),
                    new Color(120, 100, 80), Main.rand.NextFloat(0.03f, 0.05f))
                    ?.Configure(Main.rand.Next(12, 18), 0.3f);
            }
        }

        /// <summary>完美奖励：沙斗直接烧满</summary>
        protected override void OnPerfectReload(Item item, Player player, GsGunsEarlyPlayer mp) {
            GsSandgunPlayer sp = player.GetModPlayer<GsSandgunPlayer>();
            sp.heat = HeatMax;
            sp.coolDelay = 60;
        }

        //==================== 沙斗倒灌 ====================

        protected override void OnReloadCue(Item item, Player player, GsGunsEarlyPlayer mp, int index, int total) {
            if (VaultUtils.isServer) {
                return;
            }
            //沙沙三拍：颗粒感的倒灌声 + 落沙尘
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.5f, Pitch = 0.3f + 0.1f * index }, player.Center);
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(player.Top + new Vector2(player.direction * 8f, 6f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(0.8f, 1.6f)),
                    new Color(212, 192, 140), Main.rand.NextFloat(0.02f, 0.04f))
                    ?.Configure(Main.rand.Next(10, 16), 0.25f);
            }
        }

        //==================== 后坐姿态 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            float progress = player.itemAnimation / (float)player.itemAnimationMax;
            player.itemLocation -= new Vector2(player.direction, 0f) * (2f * progress);
            player.itemRotation -= player.direction * 0.06f * progress;
        }

        //==================== 沙弹表现（普通沙弹的尾沙） ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (VaultUtils.isServer || proj.type == ModContent.ProjectileType<GsSandgunGlassProj>()) {
                return;
            }
            //斗底弹或普通沙弹：稀疏尾沙
            int interval = router.MarkData >= 1f ? 3 : 6;
            if (proj.timeLeft % interval == 0) {
                PRTLoader.NewParticle<PRT_Smoke>(proj.Center - proj.velocity * 0.3f,
                    -proj.velocity * 0.02f, new Color(206, 186, 136), Main.rand.NextFloat(0.02f, 0.04f))
                    ?.Configure(Main.rand.Next(8, 14), 0.25f);
            }
        }
    }

    /// <summary>
    /// 沙枪专属本地态：沙斗热量。只在 myPlayer 路径读写，不同步
    /// </summary>
    internal class GsSandgunPlayer : ModPlayer
    {
        public int heat;        //沙斗热量（0..8）
        public int coolDelay;   //停手冷却延迟

        public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource) {
            heat = 0;
            coolDelay = 0;
        }
    }

    /// <summary>
    /// 玻璃镖：熔琉态沙弹的膛内质变。飞快、穿透 3、命中与碎裂迸溅玻璃棱片。
    /// 镖体自绘：速度拉伸的琉璃亮条 + 橙芯余温（出生半秒内未冷透），identity 定相闪烁
    /// </summary>
    internal class GsSandgunGlassProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private static readonly Color GlassEdge = new(178, 226, 224);
        private static readonly Color GlassCore = new(235, 250, 250);
        private static readonly Color MoltenHint = new(255, 170, 84);

        private float Seed => Projectile.identity * 0.6180f % 1f;
        private float Age => 300f - Projectile.timeLeft;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 3;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 300;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            //琉璃镖是直簇快弹：远段微坠，不做匀速长直线
            if (Age > 60f) {
                Projectile.velocity.Y += 0.05f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, GlassEdge.ToVector3() * 0.2f);
            if (!VaultUtils.isServer && Projectile.timeLeft % 6 == 0) {
                //琉璃亮尘
                PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center - Projectile.velocity * 0.4f,
                    -Projectile.velocity * 0.03f, GlassCore, Main.rand.NextFloat(0.22f, 0.36f))
                    ?.Configure(GlassEdge, Main.rand.Next(8, 13), 0.15f, 0.6f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => ShatterBurst(target.Center, 5);

        public override void OnKill(int timeLeft) => ShatterBurst(Projectile.Center, 7);

        /// <summary>碎裂：棱片翻滚 + 玻璃脆响（个人反馈层）</summary>
        private void ShatterBurst(Vector2 at, int shards) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.4f, Pitch = 0.4f }, at);
            for (int i = 0; i < shards; i++) {
                PRTLoader.NewParticle<PRT_SHPCShardGlass>(at,
                    (-Projectile.velocity.SafeNormalize(Vector2.UnitX)).RotatedByRandom(1.1) * Main.rand.NextFloat(1.5f, 4.5f),
                    GlassCore, Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(GlassEdge, Main.rand.Next(20, 34));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (glow == null || star == null) {
                return false;
            }
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.07f, 0.4f, 1.1f);
            float glint = MathF.Pow(MathF.Abs(MathF.Sin(Main.GlobalTimeWrappedHourly * 15f + Seed * 12f)), 4f);

            //琉璃体：沿速度拉伸的亮条（边缘青 + 白芯）
            Color edge = GlassEdge * 0.75f;
            edge.A = 0;
            Main.EntitySpriteDraw(glow, drawPos, null, edge, Projectile.rotation,
                glow.Size() / 2f, new Vector2(0.24f * stretch, 0.055f), SpriteEffects.None, 0);
            Color core = GlassCore;
            core.A = 0;
            Main.EntitySpriteDraw(glow, drawPos, null, core, Projectile.rotation,
                glow.Size() / 2f, new Vector2(0.16f * stretch, 0.03f), SpriteEffects.None, 0);

            //出膛余温：前 30 帧橙芯未冷透
            if (Age < 30f) {
                Color hot = MoltenHint * (1f - Age / 30f);
                hot.A = 0;
                Main.EntitySpriteDraw(glow, drawPos, null, hot, Projectile.rotation,
                    glow.Size() / 2f, new Vector2(0.1f * stretch, 0.05f), SpriteEffects.None, 0);
            }
            //正对视线的尖闪
            if (glint > 0.5f) {
                Color flash = Color.White * (glint * 0.8f);
                flash.A = 0;
                Main.EntitySpriteDraw(star, drawPos, null, flash, Projectile.rotation + MathHelper.PiOver4,
                    star.Size() / 2f, 0.16f * glint, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
