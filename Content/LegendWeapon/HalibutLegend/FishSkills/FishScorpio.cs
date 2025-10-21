using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishScorpio : FishSkill
    {
        public override int UnlockFishID => ItemID.ScorpioFish;
        public override int DefaultCooldown => 120 - HalibutData.GetDomainLayer() * 8; //2秒冷却
        public override int ResearchDuration => 60 * 18;

        private static int MaxScorpionSentries => 1 + HalibutData.GetDomainLayer() / 3; //最多1-4只蝎子

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            if (!Active(player)) {
                return base.Shoot(item, player, source, position, velocity, type, damage, knockback);
            }

            int existingCount = player.CountProjectilesOfID<ScorpionSentry>();
            int maxCount = MaxScorpionSentries;

            if (existingCount < maxCount) {
                SetCooldown();
                SpawnScorpionSentry(player, source, damage, knockback);
            }

            return base.Shoot(item, player, source, position, velocity, type, damage, knockback);
        }

        private void SpawnScorpionSentry(Player player, EntitySource_ItemUse_WithAmmo source, int damage, float knockback) {
            //寻找附近敌人作为参考方向
            NPC target = player.Center.FindClosestNPC(1200f);

            Vector2 spawnPos = FindValidGroundPosition(player, target);
            if (spawnPos == Vector2.Zero) {
                //如果找不到合适位置，在玩家脚下生成
                spawnPos = player.Bottom + new Vector2(0, -8);
            }

            if (player.whoAmI == Main.myPlayer) {
                Projectile.NewProjectile(
                source,
                spawnPos,
                Vector2.Zero,
                ModContent.ProjectileType<ScorpionSentry>(),
                (int)(damage * (1.6f + HalibutData.GetDomainLayer() * 0.35f)),
                knockback * 0.7f,
                player.whoAmI,
                ai0: target?.whoAmI ?? -1
                );
            }

            //召唤特效
            SpawnSummonEffect(spawnPos);
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.6f, Pitch = 0.2f }, spawnPos);
        }

        private static Vector2 FindValidGroundPosition(Player player, NPC target) {
            Vector2 dirToTarget;
            if (target != null) {
                dirToTarget = (target.Center - player.Center).SafeNormalize(Vector2.Zero);
            }
            else {
                //没有目标时在玩家移动方向生成
                dirToTarget = new Vector2(player.direction, 0);
            }

            //尝试在玩家前方寻找地面
            for (int attempt = 0; attempt < 8; attempt++) {
                float distance = Main.rand.NextFloat(80f, 200f);
                float angleOffset = Main.rand.NextFloat(-0.5f, 0.5f);
                Vector2 testDir = dirToTarget.RotatedBy(angleOffset);
                Vector2 testPos = player.Center + testDir * distance;

                //向下搜索地面
                for (int y = 0; y < 40; y++) {
                    Vector2 checkPos = testPos + new Vector2(0, y * 16);
                    Point tilePos = checkPos.ToTileCoordinates();

                    if (WorldGen.InWorld(tilePos.X, tilePos.Y)) {
                        Tile tile = Main.tile[tilePos.X, tilePos.Y];
                        if (tile.HasSolidTile()) {
                            return new Vector2(checkPos.X, tilePos.Y * 16 - 16);
                        }
                    }
                }
            }

            return Vector2.Zero;
        }

        private static void SpawnSummonEffect(Vector2 position) {
            //沙尘效果
            for (int i = 0; i < 18; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4f, 3f);
                vel.Y -= Main.rand.NextFloat(1f, 3f); //向上飞散
                Dust d = Dust.NewDustPerfect(position, DustID.Sand, vel, 100,
                    new Color(200, 180, 140), Main.rand.NextFloat(1.2f, 2f));
                d.noGravity = false;
            }

            //黄色沙尘
            for (int i = 0; i < 12; i++) {
                Dust d = Dust.NewDustDirect(position - new Vector2(16), 32, 16,
                    DustID.YellowTorch, Scale: Main.rand.NextFloat(0.8f, 1.4f));
                d.velocity.Y -= 2f;
                d.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 蝎子哨兵，从地面爬出，向敌人发射沙丘弹幕
    /// </summary>
    internal class ScorpionSentry : BaseHeldProj
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.Scorpion;

        private ref float TargetIndex => ref Projectile.ai[0];
        private ref float AttackTimer => ref Projectile.ai[2];

        private NPC target;
        private int direction = 1; //朝向
        private bool isEmerging = true; //正在从地面爬出
        private float emergeProgress = 0f;

        private const int LifeTime = 60 * 15; //15秒存在时间
        private const int AttackInterval = 80; //攻击间隔（随层数减少）

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 4; //蝎子有4帧动画
        }

        public override void SetDefaults() {
            Projectile.width = 42;
            Projectile.height = 20;
            Projectile.friendly = false; //哨兵本体不造成伤害
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTime;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            return false;
        }

        public override bool? CanDamage() => false;

        public override void AI() {
            if (!Owner.active || Owner.dead || !FishSkill.GetT<FishScorpio>().Active(Owner)) {
                Projectile.Kill();
                return;
            }

            int layer = HalibutData.GetDomainLayer(Owner);

            //爬出地面动画
            if (isEmerging) {
                emergeProgress += 0.05f;
                if (emergeProgress >= 1f) {
                    emergeProgress = 1f;
                    isEmerging = false;
                }
                Projectile.alpha = (int)MathHelper.Lerp(255, 0, emergeProgress);
            }

            //寻找目标
            if (TargetIndex >= 0 && TargetIndex < Main.maxNPCs && Main.npc[(int)TargetIndex].active && Main.npc[(int)TargetIndex].CanBeChasedBy()) {
                target = Main.npc[(int)TargetIndex];
            }
            else {
                target = Projectile.Center.FindClosestNPC(800f);
                if (target != null) TargetIndex = target.whoAmI;
            }

            //面向目标
            if (target != null) {
                direction = target.Center.X > Projectile.Center.X ? 1 : -1;
            }

            if (Math.Abs(Projectile.Center.X - target.Center.X) > 6) {
                Projectile.velocity.X = direction * 3;
            }
            else {
                Projectile.velocity.X = direction * 0.01f;
            }

            if (Projectile.velocity.Y < 16) {
                Projectile.velocity.Y += 2;
            }

            //攻击逻辑
            AttackTimer++;
            int adjustedInterval = Math.Clamp(AttackInterval - layer * 8, 35, AttackInterval);

            if (!isEmerging && AttackTimer >= adjustedInterval && target != null) {
                AttackTimer = 0;
                if (Projectile.IsOwnedByLocalPlayer()) {
                    ShootAtTarget(target, layer);
                }
            }

            //帧动画
            if (++Projectile.frameCounter >= 8) {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame >= 4) Projectile.frame = 0;
            }

            //消失前淡出
            if (Projectile.timeLeft < 60) {
                Projectile.alpha = (int)MathHelper.Lerp(0, 255, 1f - Projectile.timeLeft / 60f);
            }

            //沙尘粒子
            if (Main.rand.NextBool(10) && !isEmerging) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Sand, 0, -1f, 100, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.velocity.X *= 0.3f;
            }

            Lighting.AddLight(Projectile.Center, 0.3f, 0.25f, 0.15f);
        }

        private void ShootAtTarget(NPC target, int layer) {
            Vector2 toTarget = target.Center - Projectile.Center;
            float distance = toTarget.Length();

            //预判目标移动
            Vector2 predictedPos = target.Center + target.velocity * (distance / 15f);
            Vector2 shootDir = (predictedPos - Projectile.Center).SafeNormalize(Vector2.Zero);

            //发射沙丘弹幕
            int projectileType = ProjectileID.SandnadoFriendly; //友好沙尘龙卷
            float speed = 12f + layer * 0.6f;
            int numShots = 1 + layer / 5; //高层数多发

            for (int i = 0; i < numShots; i++) {
                float angleOffset = numShots > 1 ? MathHelper.Lerp(-0.15f, 0.15f, i / (float)(numShots - 1)) : 0f;
                Vector2 vel = shootDir.RotatedBy(angleOffset) * speed;

                Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                    Projectile.Center + shootDir * 20f,
                    vel,
                    projectileType,
                    Projectile.damage,
                    Projectile.knockBack,
                    Owner.whoAmI);
            }

            //攻击音效与特效
            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.6f, Pitch = 0.3f }, Projectile.Center);

            //沙尘爆发
            for (int i = 0; i < 12; i++) {
                Vector2 vel = shootDir.RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f)) * Main.rand.NextFloat(3f, 6f);
                Dust d = Dust.NewDustPerfect(Projectile.Center + shootDir * 15f, DustID.Sand, vel,
                    100, new Color(220, 200, 160), Main.rand.NextFloat(1.2f, 1.8f));
                d.noGravity = true;
            }

            //黄色闪光
            for (int i = 0; i < 6; i++) {
                Vector2 vel = shootDir.RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f)) * Main.rand.NextFloat(3f, 6f);
                Dust flash = Dust.NewDustDirect(Projectile.Center + shootDir * 10f, 8, 8,
                    DustID.YellowTorch, Scale: Main.rand.NextFloat(1f, 1.5f));
                flash.velocity = vel * 0.4f;
                flash.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            //消失时沙尘效果
            for (int i = 0; i < 20; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Sand, vel,
                    100, new Color(200, 180, 140), Main.rand.NextFloat(1f, 1.6f));
                d.noGravity = false;
            }

            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 0.5f, Pitch = 0.4f }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            //加载原版蝎子纹理
            Main.instance.LoadNPC(NPCID.Scorpion);
            Texture2D texture = TextureAssets.Npc[NPCID.Scorpion].Value;

            int frameHeight = texture.Height / 4;
            Rectangle source = new Rectangle(0, Projectile.frame * frameHeight, texture.Width, frameHeight);
            Vector2 origin = source.Size() / 2f;

            //蝎子正面朝左，根据方向翻转
            SpriteEffects effects = direction > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            float fade = 1f - Projectile.alpha / 255f;

            //爬出地面时从下往上显示
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            if (isEmerging) {
                drawPos.Y += (1f - emergeProgress) * Projectile.height;
            }

            //阴影
            Vector2 shadowPos = drawPos + new Vector2(2, 4);
            Main.EntitySpriteDraw(texture, shadowPos, source, Color.Black * 0.4f * fade,
                0f, origin, Projectile.scale, effects, 0);

            //主体
            Main.EntitySpriteDraw(texture, drawPos, source, lightColor * fade,
                0f, origin, Projectile.scale, effects, 0);

            //轻微发光
            if (AttackTimer > AttackInterval - 30 && target != null) {
                float glowIntensity = (AttackTimer - (AttackInterval - 30)) / 30f;
                Color glow = new Color(255, 200, 100, 0) * glowIntensity * 0.5f * fade;
                Main.EntitySpriteDraw(texture, drawPos, source, glow,
                    0f, origin, Projectile.scale * 1.05f, effects, 0);
            }

            return false;
        }

        public override Color? GetAlpha(Color lightColor) =>
            new Color(255, 255, 255, 200) * (1f - Projectile.alpha / 255f);
    }
}
