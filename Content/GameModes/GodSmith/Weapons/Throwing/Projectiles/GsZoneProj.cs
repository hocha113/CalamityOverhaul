using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Throwing.Projectiles
{
    /// <summary>
    /// 参数化领域(掷瓶三域 + 臭云)。判定圈与可见雾体同半径。<br/>
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

        public override string Texture => CWRConstant.VaultPlaceholder;

        [VaultLoaden(CWRConstant.Masking + "Extra_98")]
        internal static Asset<Texture2D> FogTex = null;

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
            Projectile.alpha = 255;
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
            Color tint = TintOf(Kind);
            Lighting.AddLight(Projectile.Center, tint.ToVector3() * 0.32f);

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

            //域内漂浮粒子(客户端)
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(r * 0.9f, r * 0.9f);
                PRTLoader.NewParticle<PRT_Spark>(pos,
                    -Vector2.UnitY * Main.rand.NextFloat(0.2f, 0.7f),
                    tint, Main.rand.NextFloat(0.16f, 0.3f))?.Configure(false, Main.rand.Next(12, 20));
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //散场:一圈淡尘
            Color tint = TintOf(Kind);
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Center + Main.rand.NextVector2Circular(Radius * 0.6f, Radius * 0.6f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.8f),
                    tint, Main.rand.NextFloat(0.18f, 0.3f))?.Configure(false, 16);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (FogTex == null) {
                return false;
            }
            Texture2D fog = FogTex.Value;
            float r = Radius;
            //生命周期:起 12f 涨开,末 30f 收拢
            int life = LifeOf(Kind);
            float grow = MathHelper.Clamp((life - Projectile.timeLeft) / 12f, 0f, 1f);
            float fade = MathHelper.Clamp(Projectile.timeLeft / 30f, 0f, 1f);
            float baseScale = r * 2f / fog.Width * grow;
            //呼吸与自转用 identity 定种,不掷随机
            float breath = 1f + 0.06f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.3f + Projectile.identity * 0.71f);
            float spin = Main.GlobalTimeWrappedHourly * 0.35f + Projectile.identity * 1.13f;
            Color tint = TintOf(Kind);
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = fog.Size() / 2f;
            //真 alpha 雾片双层对转,判定圈与可见体同半径
            Main.EntitySpriteDraw(fog, pos, null, tint * (0.4f * fade), spin,
                origin, baseScale * breath, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(fog, pos, null, tint * (0.26f * fade), -spin * 0.7f,
                origin, baseScale * 0.72f * breath, SpriteEffects.FlipHorizontally, 0);
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
