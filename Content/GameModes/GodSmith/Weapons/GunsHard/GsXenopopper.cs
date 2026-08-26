using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsHard
{
    /// <summary>
    /// 外星泡泡枪重铸：泡爆身份保留，爆点变得可控。<br/>
    /// [簇泡]：5 泡向光标收敛到六成路程后齐爆，子弹从多角度聚焦打向光标点（十字火力）。<br/>
    /// [真空泡]：单个大泡缓飞到光标处停驻半秒，爆出本次全部 5 发弹头锥形集火（单点爆发）。<br/>
    /// 泡是自定义载体：弹药子弹 type 存 ai，爆点生成真子弹结算，特种弹药身份不灭；
    /// 两档一次 use 都只耗 1 发弹药
    /// </summary>
    internal class GsXenopopper : GsFireModeScheme
    {
        public override int TargetItemID => ItemID.Xenopopper;

        public override string GsFamily => "GunsHard";

        protected override string GsDescFallback =>
            "Reforged: right click to switch bubble\n" +
            "Cluster pops five bubbles that converge and burst in a crossfire on your cursor\n" +
            "Vacuum floats one big bubble to the cursor, holds, then vents all five rounds at once";

        /// <summary>本次射击的子弹出膛速度（打标窗口消费，写进 MarkData2）</summary>
        private float pendingBulletSpeed;

        public override GsFireMode[] Modes { get; } = [
            new GsFireMode {
                Key = "ModeCluster", EnName = "Cluster Pop",
            },
            new GsFireMode {
                Key = "ModeVacuum", EnName = "Vacuum Bubble",
            },
        ];

        protected override bool? GsGunShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback,
            GsFireMode mode, GsGunsHardPlayer mp) {
            pendingBulletSpeed = Math.Max(6f, velocity.Length());
            //目标点 = 光标（限程 900px），存泡 ai1/ai2 随生成包过线
            Vector2 toCursor = Main.MouseWorld - position;
            if (toCursor.Length() > 900f) {
                toCursor = toCursor.SafeNormalize(Vector2.UnitX) * 900f;
            }
            Vector2 target = position + toCursor;
            int bubbleType = ModContent.ProjectileType<GsXenoBubbleProj>();
            if (mp.ModeIndex == 0) {
                //簇泡：5 泡飞向目标点六成路程处的横向散点，18 tick 后齐爆
                Vector2 axis = toCursor.SafeNormalize(Vector2.UnitX * player.direction);
                Vector2 side = axis.RotatedBy(MathHelper.PiOver2);
                for (int i = -2; i <= 2; i++) {
                    Vector2 burstAt = position + toCursor * 0.6f + side * (i * 26f);
                    Vector2 vel = (burstAt - position) / GsXenoBubbleProj.ClusterFlightTicks;
                    Projectile.NewProjectile(source, position, vel, bubbleType,
                        damage, knockback, player.whoAmI, type, target.X, target.Y);
                }
            }
            else {
                //真空泡：单大泡缓飞（到点停驻由泡自理）
                Vector2 vel = toCursor.SafeNormalize(Vector2.UnitX * player.direction) * 7f;
                Projectile.NewProjectile(source, position, vel, bubbleType,
                    damage, knockback, player.whoAmI, type, target.X, target.Y);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item85 with { Volume = 0.6f, Pitch = 0.2f }, position);
            }
            return false;
        }

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            GsGunsHardPlayer mp = Main.player[proj.owner].GetModPlayer<GsGunsHardPlayer>();
            router.MarkData = PackMark(mp.ModeIndex);
            //第二槽携带子弹出膛速度，爆点结算真子弹时用
            router.MarkData2 = pendingBulletSpeed;
        }
    }

    /// <summary>
    /// 外星载体泡（ai0 = 子弹弹幕 type，ai1/ai2 = 目标点坐标）。
    /// 档位与子弹速度从路由标记读取（随生成包同步）；泡自身无伤，
    /// 爆点由 owner 生成真子弹结算，damage/knockback 字段承载弹头预算
    /// </summary>
    internal class GsXenoBubbleProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Xenopopper;

        /// <summary>簇泡飞行时长（同 tick 出生同时长，天然齐爆）</summary>
        public const int ClusterFlightTicks = 18;

        /// <summary>真空泡停驻时长</summary>
        public const int VacuumHoldTicks = 18;

        /// <summary>外星青绿</summary>
        private static readonly Color XenoGreen = new(132, 240, 176);

        private Vector2 TargetPoint => new(Projectile.ai[1], Projectile.ai[2]);

        /// <summary>档位：0 簇泡 / 1 真空泡（从路由标记读，各端一致）</summary>
        private int ModeOfBubble
            => Projectile.TryGetGlobalProjectile(out GodSmithProjRouter router) ? (int)router.MarkData % 16 : 0;

        /// <summary>爆点子弹速度（MarkData2 随包）</summary>
        private float BulletSpeed {
            get {
                if (Projectile.TryGetGlobalProjectile(out GodSmithProjRouter router) && router.MarkData2 > 0f) {
                    return router.MarkData2;
                }
                return 10f;
            }
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 240;
        }

        public override void AI() {
            int mode = ModeOfBubble;
            Projectile.scale = mode == 1 ? 1.55f : 1f;
            Projectile.rotation += 0.02f * (Projectile.identity % 2 == 0 ? 1f : -1f);
            Lighting.AddLight(Projectile.Center, XenoGreen.ToVector3() * 0.25f);
            if (mode == 0) {
                //簇泡：定时齐爆（同 tick 出生 + 相同飞行时长）
                Projectile.localAI[0]++;
                if (Projectile.localAI[0] >= ClusterFlightTicks) {
                    Projectile.Kill();
                }
                return;
            }
            //真空泡：缓飞到点，停驻后放气爆发
            if (Projectile.localAI[1] > 0f) {
                Projectile.velocity = Vector2.Zero;
                Projectile.localAI[1]++;
                if (!VaultUtils.isServer && Main.GameUpdateCount % 3 == 0) {
                    //停驻吸附演出：周围敌人身上向泡飘青粒（拉扯的视觉化，不写 NPC 速度）
                    foreach (NPC npc in Main.ActiveNPCs) {
                        if (npc.friendly || Vector2.DistanceSquared(npc.Center, Projectile.Center) > 90f * 90f) {
                            continue;
                        }
                        Vector2 pull = (Projectile.Center - npc.Center).SafeNormalize(Vector2.UnitY);
                        PRTLoader.NewParticle<PRT_Spark>(npc.Center + Main.rand.NextVector2Circular(10f, 10f),
                            pull * Main.rand.NextFloat(2f, 4f), XenoGreen,
                            Main.rand.NextFloat(0.2f, 0.32f))?.Configure(false, Main.rand.Next(8, 13));
                    }
                }
                if (Projectile.localAI[1] >= VacuumHoldTicks) {
                    Projectile.Kill();
                }
                return;
            }
            //记住来向供爆发锥定向（rotation 不同步，但爆发生成只在 owner 端）
            if (Projectile.velocity.LengthSquared() > 0.1f) {
                Projectile.localAI[2] = Projectile.velocity.ToRotation();
            }
            if (Vector2.DistanceSquared(Projectile.Center, TargetPoint) < 14f * 14f) {
                Projectile.localAI[1] = 1f;
            }
        }

        public override void OnKill(int timeLeft) {
            int mode = ModeOfBubble;
            //爆点结算真子弹：只在 owner 端生成（OnKill 各端都跑，守门防翻倍）
            if (Projectile.owner == Main.myPlayer && Projectile.ai[0] > 0f) {
                int bulletType = (int)Projectile.ai[0];
                IEntitySource source = Projectile.GetSource_FromAI();
                if (mode == 0) {
                    //簇泡：单发朝目标点聚焦（五泡十字火力）
                    Vector2 dir = (TargetPoint - Projectile.Center).SafeNormalize(
                        Projectile.velocity.SafeNormalize(Vector2.UnitX));
                    Projectile.NewProjectile(source, Projectile.Center, dir * BulletSpeed,
                        bulletType, Projectile.damage, Projectile.knockBack, Projectile.owner);
                }
                else {
                    //真空泡：5 发锥形沿来向集火
                    Vector2 axis = Projectile.localAI[2].ToRotationVector2();
                    for (int i = -2; i <= 2; i++) {
                        Vector2 dir = axis.RotatedBy(0.12f * i);
                        Projectile.NewProjectile(source, Projectile.Center, dir * BulletSpeed,
                            bulletType, Projectile.damage, Projectile.knockBack, Projectile.owner);
                    }
                }
            }
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item54 with {
                Volume = mode == 1 ? 0.7f : 0.4f,
                Pitch = 0.1f + (Projectile.identity % 5) * 0.06f,
            }, Projectile.Center);
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_CampfireBubble>(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    Main.rand.NextVector2Circular(1.5f, 1.5f) - Vector2.UnitY * 0.8f,
                    XenoGreen, Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(14, 24));
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Circular(3f, 3f), XenoGreen,
                    Main.rand.NextFloat(0.24f, 0.4f))?.Configure(false, Main.rand.Next(8, 14));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //原版泡贴图 + 呼吸浮动（identity 定相，禁随机）
            Main.instance.LoadProjectile(Projectile.type);
            var tex = Terraria.GameContent.TextureAssets.Projectile[Projectile.type].Value;
            float bob = MathF.Sin(Main.GlobalTimeWrappedHourly * 3.4f + Projectile.identity * 0.77f) * 3f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition + new Vector2(0f, bob);
            Main.EntitySpriteDraw(tex, drawPos, null, lightColor * 0.95f, Projectile.rotation,
                tex.Size() / 2f, Projectile.scale, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
            //泡膜青光泽
            Color sheen = XenoGreen * 0.4f;
            sheen.A = 0;
            Main.EntitySpriteDraw(tex, drawPos, null, sheen, Projectile.rotation,
                tex.Size() / 2f, Projectile.scale * 1.08f, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
            return false;
        }
    }
}
