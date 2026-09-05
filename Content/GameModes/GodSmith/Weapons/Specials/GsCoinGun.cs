using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Specials
{
    /// <summary>
    /// 硬币枪重铸（L1 弹口层）：取币走原版自动顺序。<br/>
    /// 神匠弹道按币面分级：铜=3 连小币溅射；银=贯穿 +1；金=+10% 伤；铂=命中爆金光小 AoE。<br/>
    /// 「投资回报」：击杀返还 1 枚该面额（仅击杀、每秒封顶 3 枚，防线写死）
    /// </summary>
    internal class GsCoinGun : GodSmithScheme
    {
        public override int TargetItemID => ItemID.CoinGun;

        public override string GsFamily => "Specials";

        protected override string GsDescFallback =>
            "Reforged: Copper fires 3-coin sprays, Silver pierces one extra target, Gold hits 10% harder, Platinum bursts into golden light on hit\nReturn on Investment: kills refund 1 coin of that denomination, capped at 3 per second";
        /// <summary>面额对应的物品 ID（铜/银/金/铂）</summary>
        internal static readonly int[] CoinItemIDs = [ItemID.CopperCoin, ItemID.SilverCoin, ItemID.GoldCoin, ItemID.PlatinumCoin];

        //投资回报的每秒封顶窗口（owner 契约字段）
        private uint refundWindowStart;
        private int refundCount;

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
    /// 铂金爆金光：60px 一帧结算的小 AoE
    /// </summary>
    internal class GsCoinBurstProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.PlatinumCoin;

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
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //区域一笔：原版铂金币贴图（取其首帧，原版币是竖排帧带）按判定箱缩放画在爆心，给出判定范围提示
            Main.instance.LoadProjectile(Projectile.type);
            Texture2D tex = TextureAssets.Projectile[Projectile.type].Value;
            Rectangle frame = tex.Frame(1, Main.projFrames[ProjectileID.PlatinumCoin]);
            float scale = Projectile.width / (float)frame.Width;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, frame, lightColor, 0f,
                frame.Size() / 2f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
