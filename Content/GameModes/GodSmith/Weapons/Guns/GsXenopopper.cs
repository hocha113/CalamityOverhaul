using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Guns
{
    /// <summary>
    /// 外星泡泡枪重铸：泡爆身份保留，爆点变得可控。<br/>
    /// [簇泡]：5 泡向光标收敛到六成路程后齐爆，子弹从多角度聚焦打向光标点（十字火力）。<br/>
    /// 泡是自定义载体：弹药子弹 type 存 ai，爆点生成真子弹结算，特种弹药身份不灭；
    /// 一次 use 只耗 1 发弹药
    /// </summary>
    internal class GsXenopopper : GsFireModeScheme
    {
        public override int TargetItemID => ItemID.Xenopopper;

        public override string GsFamily => "Guns";

        protected override string GsDescFallback =>
            "Reforged: pops five bubbles that converge and burst in a crossfire on your cursor";
        /// <summary>本次射击的子弹出膛速度（打标窗口消费，写进 MarkData2）</summary>
        private float pendingBulletSpeed;

        public override GsFireMode[] Modes { get; } = [
            new GsFireMode {
                Key = "ModeCluster", EnName = "Cluster Pop",
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
            //簇泡：5 泡飞向目标点六成路程处的横向散点，18 tick 后齐爆
            Vector2 axis = toCursor.SafeNormalize(Vector2.UnitX * player.direction);
            Vector2 side = axis.RotatedBy(MathHelper.PiOver2);
            for (int i = -2; i <= 2; i++) {
                Vector2 burstAt = position + toCursor * 0.6f + side * (i * 26f);
                Vector2 vel = (burstAt - position) / GsXenoBubbleProj.ClusterFlightTicks;
                Projectile.NewProjectile(source, position, vel, bubbleType,
                    damage, knockback, player.whoAmI, type, target.X, target.Y);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item85 with { Volume = 0.6f, Pitch = 0.2f }, position);
            }
            return false;
        }

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            //第二槽携带子弹出膛速度，爆点结算真子弹时用
            router.MarkData2 = pendingBulletSpeed;
        }
    }

    /// <summary>
    /// 外星载体泡（ai0 = 子弹弹幕 type，ai1/ai2 = 目标点坐标）。
    /// 子弹速度从路由标记读取（随生成包同步）；泡自身无伤，
    /// 爆点由 owner 生成真子弹结算，damage/knockback 字段承载弹头预算
    /// </summary>
    internal class GsXenoBubbleProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Xenopopper;

        /// <summary>簇泡飞行时长（同 tick 出生同时长，天然齐爆）</summary>
        public const int ClusterFlightTicks = 18;

        private Vector2 TargetPoint => new(Projectile.ai[1], Projectile.ai[2]);

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
            Projectile.rotation += 0.02f * (Projectile.identity % 2 == 0 ? 1f : -1f);
            //簇泡：定时齐爆（同 tick 出生 + 相同飞行时长）
            Projectile.localAI[0]++;
            if (Projectile.localAI[0] >= ClusterFlightTicks) {
                Projectile.Kill();
            }
        }

        public override void OnKill(int timeLeft) {
            //爆点结算真子弹：只在 owner 端生成（OnKill 各端都跑，守门防翻倍）
            if (Projectile.owner == Main.myPlayer && Projectile.ai[0] > 0f) {
                int bulletType = (int)Projectile.ai[0];
                IEntitySource source = Projectile.GetSource_FromAI();
                //簇泡：单发朝目标点聚焦（五泡十字火力）
                Vector2 dir = (TargetPoint - Projectile.Center).SafeNormalize(
                    Projectile.velocity.SafeNormalize(Vector2.UnitX));
                Projectile.NewProjectile(source, Projectile.Center, dir * BulletSpeed,
                    bulletType, Projectile.damage, Projectile.knockBack, Projectile.owner);
            }
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item54 with {
                Volume = 0.4f,
                Pitch = 0.1f + (Projectile.identity % 5) * 0.06f,
            }, Projectile.Center);
        }
    }
}
