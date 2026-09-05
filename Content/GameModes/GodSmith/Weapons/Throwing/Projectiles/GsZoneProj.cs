using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Throwing.Projectiles
{
    /// <summary>
    /// 参数化领域(掷瓶三域 + 臭云)。判定圈与范围提示同半径。<br/>
    /// ai[0]=域类型;ai[1]=半径 px;ai[2]=覆盖 ≥3 个不同敌后返还的物品 ID(0=不返还)。<br/>
    /// 敌侧效果各端一致执行(服务器权威落地);玩家侧效果只处理本机玩家;
    /// 域增伤由 <see cref="DamageTakenMulFor"/> 在伤害结算端做几何查询,天然跨端一致
    /// </summary>
    internal class GsZoneProj : ModProjectile
    {
        /// <summary>圣辉域:敌受所有来源 +10%,域内玩家 1HP/s</summary>
        public const int KindHoly = 0;
        /// <summary>邪雾域:敌持续暗影焰 + 微滞</summary>
        public const int KindUnholy = 1;
        /// <summary>血雾域:域内玩家命中吸血(GsThrowPlayer 结算)</summary>
        public const int KindBlood = 2;
        /// <summary>臭云:敌受所有来源 +8%</summary>
        public const int KindStench = 3;

        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bubble;

        //owner 端点名(圣水返还):记覆盖过的敌编号
        private HashSet<int> touched;
        private bool refunded;

        private int Kind => (int)Projectile.ai[0];
        private float Radius => Projectile.ai[1];

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.netImportant = true;
        }

        public override bool? CanHitNPC(NPC target) => false;

        /// <summary>域时长表:各端 AI 首帧统一设定,不依赖 timeLeft 过线</summary>
        private static int LifeOf(int kind) => kind == KindStench ? 240 : 180;

        private static Color TintOf(int kind) => kind switch {
            KindHoly => new Color(255, 232, 150),
            KindUnholy => new Color(150, 92, 205),
            KindBlood => new Color(200, 52, 68),
            _ => new Color(158, 178, 62),
        };

        public override void AI() {
            //模式关闭:领域立即散场,行为退回原版
            if (!GameModeSystem.GodSmithActive) {
                Projectile.Kill();
                return;
            }
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = LifeOf(Kind);
            }
            Projectile.velocity = Vector2.Zero;
            float r = Radius;

            //敌侧效果:各端一致跑,服务器权威落地
            if (Kind == KindUnholy) {
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (!npc.active || !npc.CanBeChasedBy() || npc.Distance(Projectile.Center) > r) {
                        continue;
                    }
                    if (Projectile.timeLeft % 15 == 0) {
                        npc.AddBuff(BuffID.ShadowFlame, 40);
                    }
                    //邪雾微滞:轻阻尼(全端逻辑,服务器权威速度)
                    npc.velocity *= 0.985f;
                }
            }

            //玩家侧效果:只处理本机玩家
            if (Kind == KindHoly && !VaultUtils.isServer && Projectile.timeLeft % 60 == 0) {
                Player lp = Main.LocalPlayer;
                if (lp.active && !lp.dead && lp.statLife < lp.statLifeMax2
                    && lp.Distance(Projectile.Center) <= r) {
                    lp.statLife = Math.Min(lp.statLife + 1, lp.statLifeMax2);
                    lp.HealEffect(1);
                }
            }

            //圣水点名返还:owner 端记覆盖过的敌,凑满 3 个返还一瓶
            int refundItem = (int)Projectile.ai[2];
            if (Projectile.owner == Main.myPlayer && refundItem > 0 && !refunded) {
                touched ??= [];
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (npc.active && npc.CanBeChasedBy() && npc.Distance(Projectile.Center) <= r) {
                        touched.Add(npc.whoAmI);
                    }
                }
                if (touched.Count >= 3) {
                    refunded = true;
                    Player owner = Main.player[Projectile.owner];
                    owner.GiveItem(Projectile.GetSource_FromThis(), refundItem, 1);
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.6f }, Projectile.Center);
                    }
                }
            }
        }

        /// <summary>范围提示:原版气泡贴图按域半径缩放画一笔,按域色调色,起 12f 涨开、末 30f 收拢</summary>
        public override bool PreDraw(ref Color lightColor) {
            int life = LifeOf(Kind);
            float env = MathHelper.Clamp((life - Projectile.timeLeft) / 12f, 0f, 1f)
                * MathHelper.Clamp(Projectile.timeLeft / 30f, 0f, 1f);
            if (env <= 0.01f) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float scale = Radius * 2f / tex.Width;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                lightColor.MultiplyRGB(TintOf(Kind)) * env, 0f, tex.Size() / 2f, scale, SpriteEffects.None, 0);
            return false;
        }

        /// <summary>该 NPC 当前吃到的域增伤(几何查询,结算端一致;多域取最高不叠乘)</summary>
        public static float DamageTakenMulFor(NPC npc) {
            float mul = 1f;
            int type = ModContent.ProjectileType<GsZoneProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active || p.type != type) {
                    continue;
                }
                int kind = (int)p.ai[0];
                float bonus = kind == KindHoly ? 1.10f : kind == KindStench ? 1.08f : 1f;
                if (bonus > mul && npc.Distance(p.Center) <= p.ai[1]) {
                    mul = bonus;
                }
            }
            return mul;
        }

        /// <summary>指定玩家是否处于某类域内(调用方只对本机玩家使用)</summary>
        public static bool PlayerInZone(Player player, int kind) {
            int type = ModContent.ProjectileType<GsZoneProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == type && (int)p.ai[0] == kind
                    && player.Distance(p.Center) <= p.ai[1]) {
                    return true;
                }
            }
            return false;
        }
    }
}
