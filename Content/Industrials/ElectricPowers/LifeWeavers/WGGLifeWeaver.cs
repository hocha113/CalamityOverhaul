using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.LifeWeavers
{
    internal class WGGLifeWeaver : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/WGGLifeWeaver";
        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 9999;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.buyPrice(0, 1, 50, 0);
            Item.rare = ItemRarityID.Orange;
            Item.createTile = ModContent.TileType<WGGLifeWeaverTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 300;
        }
    }

    internal class WGGLifeWeaverTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/WGGLifeWeaverTile";
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            AddMapEntry(new Color(100, 140, 80), VaultUtils.GetLocalizedItemName<WGGLifeWeaver>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 2;
            TileObjectData.newTile.Height = 2;
            TileObjectData.newTile.Origin = new Point16(0, 1);
            TileObjectData.newTile.CoordinateHeights = [16, 16];
            TileObjectData.newTile.StyleWrapLimit = 36;
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile
                | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.addTile(Type);
            //敌对建筑设置
            HitSound = SoundID.Item14;
            MineResist = 2.5f;
        }

        public override bool CanExplode(int i, int j) => false;

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Grass);
            return false;
        }

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void NumDust(int i, int j, bool fail, ref int num) {
            num = fail ? 1 : 3;
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out WGGLifeWeaverTP lifeWeaver)) {
                return;
            }
            lifeWeaver.byHitSyncopeTime = 45;
            lifeWeaver.SendData();
        }

        public override bool CanDrop(int i, int j) => false;

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out WGGLifeWeaverTP lifeWeaver)) {
                return false;
            }

            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;
            Texture2D tex = TextureAssets.Tile[Type].Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color drawColor = Lighting.GetColor(i, j);

            if (!t.IsHalfBlock && t.Slope == 0) {
                spriteBatch.Draw(tex, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            else if (t.IsHalfBlock) {
                spriteBatch.Draw(tex, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            return false;
        }
    }

    internal class WGGLifeWeaverTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<WGGLifeWeaverTile>();
        public override int TargetItem => ModContent.ItemType<WGGLifeWeaver>();
        public override bool ReceivedEnergy => true;
        public override bool CanDrop => false;
        public override float MaxUEValue => 300;

        //攻击范围
        internal const int attackDistance = 600;
        //发射间隔
        private const int ShootInterval = 90;
        //能耗
        internal int consumeUE = 8;

        //状态
        private int shootTimer;
        private int textIdleTime;
        internal int byHitSyncopeTime;
        internal bool BatteryPrompt;

        public override void SetBattery() {
            Efficiency = 0;//敌对建筑不具备电力传输能力
            IdleDistance = 1000;
            PlaceNet = true;
            DrawExtendMode = 800;
        }

        public override void Initialize() {
            MachineData.UEvalue = MaxUEValue;
        }

        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write(byHitSyncopeTime);
            data.Write(BatteryPrompt);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            byHitSyncopeTime = reader.ReadInt32();
            BatteryPrompt = reader.ReadBoolean();
        }

        /// <summary>
        /// 获取发射位置
        /// </summary>
        private Vector2 GetLaunchPosition() {
            return CenterInWorld + new Vector2(0, -16);
        }

        /// <summary>
        /// 计算向玩家发射的抛物线轨迹
        /// </summary>
        private bool TryCalculateTrajectoryToPlayer(Player target, out Vector2 velocity, out float flightTime) {
            velocity = Vector2.Zero;
            flightTime = 0f;

            Vector2 start = GetLaunchPosition();
            Vector2 targetPos = target.Center;

            //轻微预判玩家移动，但不要太准确
            Vector2 playerVel = target.velocity;
            float predictTime = 20f;
            targetPos += playerVel * predictTime * 0.3f;

            Vector2 diff = targetPos - start;
            float horizontalDist = Math.Abs(diff.X);
            float gravity = WGGLifeWeaverAcorn.Gravity;

            //根据水平距离计算基础飞行时间，确保足够的抛物线高度
            //距离越近飞行时间越长，产生更高的弧线
            float minFlightTime = 70f + (400f - Math.Min(horizontalDist, 400f)) * 0.15f;
            float maxFlightTime = 140f;

            //尝试不同的飞行时间，优先选择较长的飞行时间以产生更高弧线
            for (float testTime = minFlightTime; testTime <= maxFlightTime; testTime += 10f) {
                float vx = diff.X / testTime;

                //限制水平速度，让玩家有时间反应
                float maxHorizontalSpeed = 6f;
                if (Math.Abs(vx) > maxHorizontalSpeed) continue;

                //计算垂直初速度
                float vy = (diff.Y - 0.5f * gravity * testTime * testTime) / testTime;

                //确保有足够的向上初速度产生明显弧线
                //即使目标在同一水平线，也要有向上抛射的感觉
                float minUpwardVelocity = -4f;//至少要有向上的初速度
                if (vy > minUpwardVelocity) {
                    //如果初速度不够向上，增加飞行时间重新计算
                    continue;
                }

                //限制最大向上速度
                if (vy < -14f) continue;

                //计算抛物线最高点，确保有足够的弧度
                float peakTime = -vy / gravity;
                float peakHeight = start.Y + vy * peakTime + 0.5f * gravity * peakTime * peakTime;
                float arcHeight = start.Y - peakHeight;

                //确保弧线高度至少有80像素，增加可见性和躲避时间
                if (arcHeight < 80f && horizontalDist < 300f) {
                    continue;
                }

                //验证轨迹
                if (ValidateTrajectoryPath(start, new Vector2(vx, vy), testTime)) {
                    velocity = new Vector2(vx, vy);
                    flightTime = testTime;
                    return true;
                }
            }

            //如果常规方法失败，使用高弧线备选方案
            return TryHighArcFallback(start, targetPos, gravity, out velocity, out flightTime);
        }

        /// <summary>
        /// 高弧线备选方案，用于近距离或复杂地形
        /// </summary>
        private bool TryHighArcFallback(Vector2 start, Vector2 targetPos, float gravity, out Vector2 velocity, out float flightTime) {
            velocity = Vector2.Zero;
            flightTime = 0f;

            Vector2 diff = targetPos - start;
            float horizontalDist = Math.Abs(diff.X);

            //使用固定的高弧线
            flightTime = 100f + horizontalDist * 0.1f;
            float vx = diff.X / flightTime;

            //限制水平速度
            if (Math.Abs(vx) > 5f) {
                vx = Math.Sign(vx) * 5f;
                flightTime = diff.X / vx;
            }

            float vy = (diff.Y - 0.5f * gravity * flightTime * flightTime) / flightTime;

            //确保向上发射
            vy = Math.Min(vy, -6f);

            if (ValidateTrajectoryPath(start, new Vector2(vx, vy), flightTime)) {
                velocity = new Vector2(vx, vy);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 验证轨迹路径
        /// </summary>
        private static bool ValidateTrajectoryPath(Vector2 start, Vector2 velocity, float totalTime) {
            float gravity = WGGLifeWeaverAcorn.Gravity;
            int checkCount = Math.Max((int)(totalTime / 8f), 6);

            for (int i = 1; i < checkCount; i++) {
                float t = totalTime * i / checkCount;

                float x = start.X + velocity.X * t;
                float y = start.Y + velocity.Y * t + 0.5f * gravity * t * t;

                int tileX = (int)(x / 16);
                int tileY = (int)(y / 16);

                if (!WorldGen.InWorld(tileX, tileY, 5)) return false;

                Tile tile = Main.tile[tileX, tileY];
                if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 发射攻击橡子
        /// </summary>
        private void LaunchAttackAcorn(Player target) {
            if (VaultUtils.isClient) return;

            if (!TryCalculateTrajectoryToPlayer(target, out Vector2 velocity, out float flightTime)) {
                return;
            }

            Vector2 startPos = GetLaunchPosition();

            //生成敌对橡子弹幕
            int dmg = 12;
            if (Main.masterMode || Main.expertMode) {
                dmg = 10;
            }

            Projectile.NewProjectile(this.FromObjectGetParent(), startPos, velocity,
                ModContent.ProjectileType<WGGLifeWeaverAcorn>(), dmg, 2f, -1, flightTime);

            //播放发射音效
            SoundEngine.PlaySound(SoundID.Item1 with {
                Volume = 0.6f,
                Pitch = 0.2f
            }, startPos);

            //发射粒子效果
            for (int i = 0; i < 5; i++) {
                Vector2 dustVel = velocity * 0.3f + Main.rand.NextVector2Circular(2f, 2f);
                Dust dust = Dust.NewDustDirect(startPos, 8, 8, DustID.Grass, dustVel.X, dustVel.Y, 100, default, 1.2f);
                dust.noGravity = true;
            }
        }

        public override void UpdateMachine() {
            if (textIdleTime > 0) {
                textIdleTime--;
            }

            if (byHitSyncopeTime > 0) {
                byHitSyncopeTime--;
                return;//被攻击时停止运作
            }

            //检查能量
            BatteryPrompt = MachineData.UEvalue < consumeUE;
            if (BatteryPrompt) {
                if (textIdleTime <= 0) {
                    CombatText.NewText(HitBox, new Color(111, 247, 200), CWRLocText.Instance.Turret_Text1.Value);
                    textIdleTime = 300;
                }
                return;
            }

            //寻找玩家目标
            Player target = CenterInWorld.FindClosestPlayer(attackDistance);
            if (target == null) {
                shootTimer = 0;
                return;
            }

            //发射计时
            if (++shootTimer >= ShootInterval) {
                shootTimer = 0;

                //消耗能量
                MachineData.UEvalue -= consumeUE;

                //发射橡子攻击玩家
                LaunchAttackAcorn(target);

                SendData();
            }
        }

        public override void MachineKill() {
            if (!VaultUtils.isClient) {
                DropItem(ModContent.ItemType<LifeWeaver>());
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            DrawChargeBar();
        }
    }

    /// <summary>
    /// 荒野植树者发射的敌对橡子弹幕
    /// </summary>
    internal class WGGLifeWeaverAcorn : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public const float Gravity = 0.22f;//降低重力让弧线更明显

        //物理参数
        private Vector2 initialVelocity;
        private Vector2 startPosition;
        private float expectedFlightTime;
        private float currentFlightTime;

        //视觉效果
        private float rotationSpeed;
        private int particleTimer;
        private float glowIntensity;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1000;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.timeLeft = 600;
            Projectile.penetrate = 1;
            Projectile.ignoreWater = false;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                initialVelocity = Projectile.velocity;
                startPosition = Projectile.Center;
                expectedFlightTime = Projectile.ai[0];
                rotationSpeed = Main.rand.NextFloat(0.08f, 0.12f) * (initialVelocity.X > 0 ? 1 : -1);
                glowIntensity = 0f;
                Projectile.localAI[0] = 1f;

                //发射时的明显视觉提示
                SpawnLaunchEffect();
            }

            currentFlightTime++;

            //使用抛物线公式计算位置
            float t = currentFlightTime;
            float x = startPosition.X + initialVelocity.X * t;
            float y = startPosition.Y + initialVelocity.Y * t + 0.5f * Gravity * t * t;
            Projectile.Center = new Vector2(x, y);

            //计算当前速度用于碰撞检测
            float vx = initialVelocity.X;
            float vy = initialVelocity.Y + Gravity * t;
            Projectile.velocity = new Vector2(vx, vy);

            //旋转速度随飞行时间略微变化
            Projectile.rotation += rotationSpeed * (1f + vy * 0.02f);

            //光晕强度随时间脉动
            glowIntensity = 0.3f + (float)Math.Sin(currentFlightTime * 0.15f) * 0.2f;

            //轨迹粒子
            SpawnTrailParticles();

            //超时检查
            if (currentFlightTime > expectedFlightTime + 120f) {
                Projectile.Kill();
            }
        }

        /// <summary>
        /// 发射时的视觉效果
        /// </summary>
        private void SpawnLaunchEffect() {
            if (Main.netMode == NetmodeID.Server) return;

            //明显的发射粒子爆发
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f;
                Vector2 dustVel = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 3f;
                dustVel += initialVelocity * 0.5f;
                Dust dust = Dust.NewDustDirect(Projectile.Center, 6, 6, DustID.GreenFairy, dustVel.X, dustVel.Y, 100, default, 1.2f);
                dust.noGravity = true;
            }

            //上升的光点
            for (int i = 0; i < 4; i++) {
                Vector2 dustVel = new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-3f, -1f));
                Dust dust = Dust.NewDustDirect(Projectile.Center, 4, 4, DustID.FireworksRGB, dustVel.X, dustVel.Y, 150, new Color(100, 255, 100), 0.8f);
                dust.noGravity = true;
            }
        }

        private void SpawnTrailParticles() {
            if (Main.netMode == NetmodeID.Server) return;

            particleTimer++;

            //绿色轨迹，频率降低让轨迹更清晰
            if (particleTimer % 3 == 0) {
                Vector2 dustVel = -Projectile.velocity * 0.05f;
                Dust dust = Dust.NewDustDirect(Projectile.Center - Vector2.One * 4, 8, 8, DustID.CursedTorch, dustVel.X, dustVel.Y, 100, default, 0.9f);
                dust.noGravity = true;
                dust.fadeIn = 0.8f;
            }

            //偶尔的闪光粒子
            if (particleTimer % 10 == 0) {
                Dust sparkle = Dust.NewDustDirect(Projectile.Center, 4, 4, DustID.GreenFairy, 0, 0, 150, default, 0.7f);
                sparkle.noGravity = true;
                sparkle.velocity *= 0.1f;
            }

            //下落阶段增加警告粒子
            if (Projectile.velocity.Y > 2f && particleTimer % 4 == 0) {
                Vector2 dustVel = Main.rand.NextVector2Circular(1f, 1f);
                Dust dust = Dust.NewDustDirect(Projectile.Center, 6, 6, DustID.YellowTorch, dustVel.X, dustVel.Y, 100, default, 0.6f);
                dust.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            //爆炸效果
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);

            if (Main.netMode != NetmodeID.Server) {
                //毒性爆炸粒子
                for (int i = 0; i < 12; i++) {
                    Vector2 dustVel = Main.rand.NextVector2Circular(4f, 4f);
                    Dust dust = Dust.NewDustDirect(Projectile.Center - Vector2.One * 6, 12, 12, DustID.CursedTorch, dustVel.X, dustVel.Y, 100, default, 1.3f);
                    dust.noGravity = true;
                }

                //碎片粒子
                for (int i = 0; i < 6; i++) {
                    Vector2 dustVel = Main.rand.NextVector2Circular(3f, 3f);
                    Dust.NewDustDirect(Projectile.Center - Vector2.One * 4, 8, 8, DustID.WoodFurniture, dustVel.X, dustVel.Y, 100, default, 1f);
                }
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            return true;//碰到地面消失
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadItem(ItemID.Acorn);
            Texture2D texture = TextureAssets.Item[ItemID.Acorn].Value;
            Vector2 origin = texture.Size() / 2f;

            //绘制拖影，让轨迹更明显
            int trailCount = 5;
            for (int i = 1; i <= trailCount; i++) {
                Vector2 trailPos = Projectile.Center - Projectile.velocity * i * 0.4f;
                float trailAlpha = 1f - i / (float)(trailCount + 1);
                float trailScale = 1f - i * 0.08f;
                Color trailColor = Color.Lerp(lightColor, new Color(100, 200, 100), 0.3f) * trailAlpha * 0.5f;

                Main.EntitySpriteDraw(texture, trailPos - Main.screenPosition, null, trailColor,
                    Projectile.rotation - rotationSpeed * i * 1.5f, origin, trailScale, SpriteEffects.None, 0);
            }

            //主体绘制
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation, origin, 1f, SpriteEffects.None, 0);

            //毒性光晕，脉动效果
            Color glowColor = new Color(100, 255, 100) * glowIntensity;
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, glowColor,
                Projectile.rotation, origin, 1.2f, SpriteEffects.None, 0);

            //下落时额外的警告光晕
            if (Projectile.velocity.Y > 1f) {
                float warningIntensity = Math.Min(Projectile.velocity.Y * 0.1f, 0.4f);
                Color warningColor = new Color(255, 200, 100) * warningIntensity;
                Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, warningColor,
                    Projectile.rotation, origin, 1.3f, SpriteEffects.None, 0);
            }

            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) {
            behindProjectiles.Add(index);
        }
    }
}
