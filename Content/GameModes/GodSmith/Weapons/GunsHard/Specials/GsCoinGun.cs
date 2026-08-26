using CalamityOverhaul.Common;
using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsHard.Specials
{
    /// <summary>
    /// 硬币枪重铸（L1 弹口层 + 面额选择器）：右键循环 铜/银/金/铂 面额（仅持有该币时可选），
    /// 面额过滤经 <see cref="GsCoinGunAmmoFilter"/>（本地玩家专属，模式关闭零足迹）。<br/>
    /// 神匠弹道按币面分级：铜=3 连小币溅射；银=贯穿 +1；金=+10% 伤；铂=命中爆金光小 AoE。<br/>
    /// 「投资回报」：击杀返还 1 枚该面额（仅击杀、每秒封顶 3 枚，防线写死）
    /// </summary>
    internal class GsCoinGun : GodSmithScheme
    {
        public override int TargetItemID => ItemID.CoinGun;

        public override string GsFamily => "GunsSpecial";

        protected override string GsDescFallback =>
            "Reforged: right click cycles the denomination (only coins you carry). Copper spits 3-coin sprays, Silver pierces one extra, Gold hits 10% harder, Platinum bursts into golden light"
            + "\nReturn on Investment: kills refund 1 coin of that denomination, capped at 3 per second";

        /// <summary>面额对应的物品 ID（铜/银/金/铂）</summary>
        internal static readonly int[] CoinItemIDs = [ItemID.CopperCoin, ItemID.SilverCoin, ItemID.GoldCoin, ItemID.PlatinumCoin];

        /// <summary>面额名（[0..3]=铜银金铂）与无币提示</summary>
        internal static LocalizedText[] DenomNames;
        internal static LocalizedText NoCoinsHint;

        /// <summary>当前选中的面额索引；-1=未选（原版自动取币）。只在本地玩家路径读写</summary>
        internal int denomIndex = -1;
        private int switchCd;
        //投资回报的每秒封顶窗口（owner 契约字段）
        private uint refundWindowStart;
        private int refundCount;

        public override void GsSetStaticDefaults() {
            DenomNames = [
                this.GetLocalization("Denom0", () => "Copper Spray"),
                this.GetLocalization("Denom1", () => "Silver Pierce"),
                this.GetLocalization("Denom2", () => "Gold Weight"),
                this.GetLocalization("Denom3", () => "Platinum Burst"),
            ];
            NoCoinsHint = this.GetLocalization("NoCoins", () => "No coins to load!");
        }

        public override bool? GsAltFunctionUse(Item item, Player player) => true;

        public override bool? GsCanUseItem(Item item, Player player) {
            if (player.altFunctionUse == 2) {
                if (player.whoAmI == Main.myPlayer && switchCd <= 0) {
                    switchCd = 12;
                    CycleDenomination(player);
                }
                return false;
            }
            return null;
        }

        /// <summary>从当前面额起顺位循环，跳过背包里没有的面额；一枚币都没有时漂提示</summary>
        private void CycleDenomination(Player player) {
            for (int step = 1; step <= 4; step++) {
                int candidate = (denomIndex + step + 4) % 4;
                if (player.HasItem(CoinItemIDs[candidate])) {
                    denomIndex = candidate;
                    GsGunPose.ModeSwitchFeedback(player, DenomNames[candidate].Value);
                    return;
                }
            }
            if (!VaultUtils.isServer) {
                CombatText.NewText(player.getRect(), Color.Gray, NoCoinsHint.Value);
            }
        }

        public override void GsHoldItem(Item item, Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            if (switchCd > 0) {
                switchCd--;
            }
        }

        public override void GsPickAmmo(Item weapon, Item ammo, Player player,
            ref int type, ref float speed, ref StatModifier damage, ref float knockback) {
            //金币的分量：+10% 伤
            if (ammo.type == ItemID.GoldCoin) {
                damage *= 1.10f;
            }
        }

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //铜币溅射：补 2 枚小角度侧币（同源打标，弹药只耗原本那 1 枚）
            if (type == ProjectileID.CopperCoin) {
                for (int i = -1; i <= 1; i += 2) {
                    Vector2 vel = velocity.RotatedBy(i * MathHelper.ToRadians(7f));
                    Projectile.NewProjectile(source, position, vel, type,
                        Math.Max(1, (int)(damage * 0.8f)), knockback, player.whoAmI);
                }
            }
            return null;
        }

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            //银币贯穿 +1，带 >0 守卫
            if (proj.type == ProjectileID.SilverCoin && proj.penetrate > 0) {
                proj.penetrate++;
            }
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (VaultUtils.isServer) {
                return;
            }
            //金铂币的贵金属流光（稀疏，identity 错拍）
            if (proj.type == ProjectileID.GoldCoin || proj.type == ProjectileID.PlatinumCoin) {
                bool platinum = proj.type == ProjectileID.PlatinumCoin;
                Lighting.AddLight(proj.Center, new Vector3(0.4f, 0.36f, platinum ? 0.42f : 0.14f) * 0.6f);
                if ((proj.timeLeft + proj.identity) % 6 == 0) {
                    PRTLoader.NewParticle<PRT_Sparkle>(proj.Center, -proj.velocity * 0.05f,
                        platinum ? new Color(220, 235, 255) : new Color(255, 220, 110),
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(
                            platinum ? new Color(180, 210, 255) : new Color(255, 236, 160),
                            Main.rand.Next(10, 16), 0.08f);
                }
            }
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            //铂金币命中处爆金光小 AoE（owner 权威生成）
            if (proj.type != ProjectileID.PlatinumCoin || proj.owner != Main.myPlayer) {
                return;
            }
            Projectile.NewProjectile(proj.GetSource_FromAI(), proj.Center, Vector2.Zero,
                ModContent.ProjectileType<GsCoinBurstProj>(),
                Math.Max(1, (int)(proj.damage * 0.5f)), 2f, proj.owner);
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            //投资回报：只认击杀，且每秒封顶 3 枚（只在攻击方端执行，owner 契约成立）
            int denom = Array.IndexOf(
                new[] { ProjectileID.CopperCoin, ProjectileID.SilverCoin, ProjectileID.GoldCoin, ProjectileID.PlatinumCoin },
                proj.type);
            if (denom < 0 || target.life > 0 || target.type == NPCID.TargetDummy) {
                return;
            }
            Player player = Main.player[proj.owner];
            if (Main.GameUpdateCount - refundWindowStart >= 60) {
                refundWindowStart = Main.GameUpdateCount;
                refundCount = 0;
            }
            if (refundCount >= 3) {
                return;
            }
            refundCount++;
            player.QuickSpawnItem(player.GetSource_Misc("GsCoinGunRefund"), CoinItemIDs[denom], 1);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.CoinPickup with { Volume = 0.5f, Pitch = 0.3f }, target.Center);
            }
        }
    }

    /// <summary>
    /// 硬币枪的本地弹药过滤：神匠模式开启且本地玩家选定面额时，
    /// 只放行该面额的币（背包没有所选面额时整体放行防锁死）。模式关闭返回 null，零足迹
    /// </summary>
    internal class GsCoinGunAmmoFilter : GlobalItem
    {
        public override bool? CanChooseAmmo(Item weapon, Item ammo, Player player) {
            if (weapon.type != ItemID.CoinGun || !GameModeSystem.GodSmithActive
                || player.whoAmI != Main.myPlayer) {
                return null;
            }
            if (!GodSmithScheme.TryGetScheme(ItemID.CoinGun, out GodSmithScheme scheme)
                || scheme is not GsCoinGun coinGun || coinGun.denomIndex < 0) {
                return null;
            }
            int wanted = GsCoinGun.CoinItemIDs[coinGun.denomIndex];
            if (ammo.type == wanted) {
                return null;//所选面额照原版判定
            }
            bool isCoin = ammo.type == ItemID.CopperCoin || ammo.type == ItemID.SilverCoin
                || ammo.type == ItemID.GoldCoin || ammo.type == ItemID.PlatinumCoin;
            if (!isCoin) {
                return null;
            }
            //其他面额：只有背包里还有所选面额时才屏蔽，打光了自动放行防卡死
            return player.HasItem(wanted) ? false : null;
        }
    }

    /// <summary>
    /// 铂金爆金光：60px 一帧结算的小 AoE，金环 + 碎金闪演出
    /// </summary>
    internal class GsCoinBurstProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private static readonly Color GoldBright = new(255, 240, 190);
        private static readonly Color GoldMain = new(255, 200, 90);
        private static readonly Color GoldDeep = new(150, 96, 30);

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 120;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 14;
        }

        /// <summary>伤害窗只开前 3 帧，其余时间纯演出</summary>
        public override bool? CanDamage() => Projectile.timeLeft > 11 ? null : false;

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            if (Projectile.timeLeft == 13 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.CoinPickup with { Volume = 0.7f, Pitch = -0.3f, MaxInstances = 3 }, Projectile.Center);
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center,
                        Main.rand.NextVector2Circular(3.5f, 3.5f) - Vector2.UnitY * 1.5f,
                        GoldBright, Main.rand.NextFloat(0.5f, 0.9f))?.Configure(GoldMain, Main.rand.Next(16, 26), 0.12f);
                }
            }
            Lighting.AddLight(Projectile.Center, GoldMain.ToVector3() * (Projectile.timeLeft / 14f));
        }

        public override bool PreDraw(ref Color lightColor) {
            //扩张金环：共享冲击环参数化复用，调用点已处于实体批
            float t = 1f - Projectile.timeLeft / 14f;
            float radius = MathHelper.Lerp(10f, 62f, MathF.Sqrt(t));
            float alpha = (1f - t) * 0.85f;
            ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center, radius, 7f,
                GoldBright, GoldMain, GoldDeep, alpha,
                squish: 1f, innerGlow: 0.25f, timeSeed: Projectile.identity * 0.37f);
            return false;
        }
    }
}
