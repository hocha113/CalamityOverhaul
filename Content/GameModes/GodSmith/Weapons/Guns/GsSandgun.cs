using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Guns
{
    /// <summary>
    /// 沙枪「熔沙成琉」：黄铜沙斗喷枪·石英观察窗。<br/>
    /// ①蓄热：连续喷沙给沙斗升温，停手缓冷；
    /// ②热满入「熔琉态」：沙弹在膛内烧成玻璃镖，更快更穿，命中脆响；
    /// ③沙斗装填沙沙倒灌三拍。<br/>
    /// 普通沙弹保持原版（含落沙成块的老脾气）。后坐 2px + 角度踢。<br/>
    /// 账目：射速原版；玻璃镖 ×1.15 且穿透 +2，热态占空约 50%，
    /// 伤害行 ×1.0 → 约 110%（待游戏内标定）
    /// </summary>
    internal class GsSandgun : GsMagazineScheme
    {
        public override int TargetItemID => ItemID.Sandgun;

        protected override string GsDescFallback =>
            "Reforged: sustained fire heats the hopper; keep pouring and the sand melts in the chamber.\nWhile molten, rounds leave as glass bolts that fly faster, pierce deeper, and shatter into razor facets.\nReload pours in three rustling beats; a sweet-spot pour ignites the hopper instantly";
        public override int MagSize => 12;
        public override int ReloadTicks => 46;
        public override GsReloadStyle Style => GsReloadStyle.Hopper;
        protected override int ReloadCueCount => 3;
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
            => HeatTick(player);

        protected override bool? FireLastRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            pendingMark = 1f;   //斗底弹：沙重一分
            return HeatTick(player);
        }

        /// <summary>每喷一口积热；跨过熔琉线时漂字提示</summary>
        private bool? HeatTick(Player player) {
            GsSandgunPlayer sp = player.GetModPlayer<GsSandgunPlayer>();
            int before = sp.heat;
            sp.heat = Math.Min(HeatMax, sp.heat + 1);
            sp.coolDelay = 50;
            if (before < MoltenAt && sp.heat >= MoltenAt && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.6f, Pitch = 0.3f }, player.Center);
                CombatText.NewText(player.getRect(), new Color(255, 168, 80), MoltenText.Value);
            }
            return null;
        }

        //==================== 冷却（本机手持每帧） ====================

        protected override void HoldTick(Item item, Player player, GsGunsEarlyPlayer mp) {
            GsSandgunPlayer sp = player.GetModPlayer<GsSandgunPlayer>();
            if (sp.coolDelay > 0) {
                sp.coolDelay--;
            }
            else if (sp.heat > 0 && Main.GameUpdateCount % 24 == 0) {
                sp.heat--;
            }
        }

        //==================== 沙斗倒灌 ====================

        protected override void OnReloadCue(Item item, Player player, GsGunsEarlyPlayer mp, int index, int total) {
            if (!VaultUtils.isServer) {
                //沙沙三拍：颗粒感的倒灌声
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.5f, Pitch = 0.3f + 0.1f * index }, player.Center);
            }
        }

        //==================== 后坐姿态（差分，见 GsGunRecoil） ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame)
            => GunKickStyle(player, 2f, 0.06f);
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
    /// 玻璃镖：熔琉态沙弹的膛内质变。飞快、穿透 3、命中与碎裂脆响。借原版水晶碎片贴图默认绘制
    /// </summary>
    internal class GsSandgunGlassProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.CrystalShard;

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
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => ShatterSound(target.Center);

        public override void OnKill(int timeLeft) => ShatterSound(Projectile.Center);

        /// <summary>碎裂：玻璃脆响（个人反馈层）</summary>
        private static void ShatterSound(Vector2 at) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.4f, Pitch = 0.4f }, at);
            }
        }
    }
}
