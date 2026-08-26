using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonSentries.Projectiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonSentries.Schemes
{
    /// <summary>
    /// 月亮传送门法杖「月门主宰」（经典不毁：无限贯穿扫射死光原样保留）：<br/>
    /// 联动枢纽 = 与月门成链的哨兵 +8%（图重算 HubLinked，与链边加成同封顶 20%），链线染月色；
    /// 充能 8，超频 240 帧「月相齐射」= 每次死光出膛补两侧 ±12° 伴束（0.45×），
    /// 并加开背门：绘制镜像门 + 反向 0.5× 伴束前后夹射
    /// </summary>
    internal class GsLunarPortalStaff : GsSentryScheme
    {
        public override int TargetItemID => ItemID.MoonlordTurretStaff;

        protected override int FamilyIdx => GsSentryFamilyIdx.LunarPortal;

        protected override string GsDescFallback =>
            "Deploy doctrine: the portal is a link hub, every linked sentry strikes harder\n" +
            "Hits charge it, right-click when full for lunar phase volley: side lances and a rear gate";

        private static readonly Color LunarTint = new(130, 200, 240);

        protected override SentryKit BuildKit() => new() {
            TowerTypes = [ProjectileID.MoonlordTurret],
            BoltTypes = [ProjectileID.MoonlordTurretLaser],
            ChargeMax = [8],
            OverdriveDuration = 240,
        };

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.06f;

        /// <summary>死光出膛方向：优先读主束速度，静止束回退为塔到最近敌方向</summary>
        private static Vector2 LaserAim(Projectile laser, Projectile tower) {
            if (laser.velocity.LengthSquared() > 0.25f) {
                return laser.velocity.SafeNormalize(Vector2.UnitX);
            }
            NPC target = null;
            float bestDist = 900f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(laser)) {
                    continue;
                }
                float dist = npc.Center.Distance(tower.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    target = npc;
                }
            }
            return target != null
                ? (target.Center - tower.Center).SafeNormalize(Vector2.UnitX)
                : new Vector2(tower.spriteDirection, 0f);
        }

        /// <summary>超频「月相齐射」：主束保留，补两侧伴束与背门反向束（owner 生成端）</summary>
        protected override void OnOverdriveBoltSpawn(Projectile bolt, Projectile tower, int tier) {
            Vector2 aim = LaserAim(bolt, tower);
            for (int i = -1; i <= 1; i += 2) {
                Vector2 vel = aim.RotatedBy(i * MathHelper.ToRadians(12f)) * 22f;
                Projectile.NewProjectile(SentrySource(tower), tower.Center, vel,
                    ModContent.ProjectileType<GsSentryBoltProj>(),
                    (int)(bolt.damage * 0.45f), bolt.knockBack,
                    tower.owner, GsSentryBoltProj.StyleLunarLance);
            }
            //背门夹射：反向半伤伴束
            Projectile.NewProjectile(SentrySource(tower), tower.Center, -aim * 22f,
                ModContent.ProjectileType<GsSentryBoltProj>(),
                (int)(bolt.damage * 0.5f), bolt.knockBack,
                tower.owner, GsSentryBoltProj.StyleLunarLance);
        }

        /// <summary>超频期背门：塔体水平镜像的月色重影（各端按超频态绘制，不生成第二座哨兵）</summary>
        protected override void DrawTowerExtra(Projectile tower, SentryKit kit, GsSentryLocal st, Color lightColor) {
            if (!SentryGrid.IsOverdriven(st)) {
                return;
            }
            Main.instance.LoadProjectile(tower.type);
            var tex = Terraria.GameContent.TextureAssets.Projectile[tower.type].Value;
            Rectangle frame = tex.Frame(1, Main.projFrames[tower.type], 0, tower.frame);
            //原门朝向取反即背门
            SpriteEffects fx = tower.spriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            float pulse = 0.32f + 0.10f * MathF.Sin(Main.GlobalTimeWrappedHourly * 3.1f + tower.identity * 0.67f);
            Color ghost = LunarTint * pulse;
            ghost.A = 0;
            Main.EntitySpriteDraw(tex, tower.Center + new Vector2(-tower.spriteDirection * 14f, 0f) - Main.screenPosition,
                frame, ghost, tower.rotation, frame.Size() * 0.5f, tower.scale * 0.92f, fx, 0);
        }
    }
}
