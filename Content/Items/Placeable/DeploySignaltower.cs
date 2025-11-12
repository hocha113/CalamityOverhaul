using CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest.DeploySignaltowers.SignalTower;
using InnoVault.TileProcessors;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Items.Placeable
{
    internal class DeploySignaltower : ModItem
    {
        public override string Texture => CWRConstant.Item + "Placeable/DeploySignaltower";

        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 48;
            Item.maxStack = 99;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.buyPrice(gold: 5);
            Item.rare = ItemRarityID.LightPurple;
            Item.createTile = ModContent.TileType<DeploySignaltowerTile>();
        }
    }

    internal class DeploySignaltowerTile : ModTile
    {
        public override string Texture => CWRConstant.Item + "Placeable/DeploySignaltowerTile";

        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileSolid[Type] = false;

            TileID.Sets.DisableSmartCursor[Type] = true;

            AddMapEntry(new Color(100, 150, 255), VaultUtils.GetLocalizedItemName<DeploySignaltower>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 6;
            TileObjectData.newTile.Height = 14;
            TileObjectData.newTile.Origin = new Point16(2, 13);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16];
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.newTile.LavaDeath = false;
            TileObjectData.addTile(Type);
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            r = 0.2f;
            g = 0.3f;
            b = 0.5f;
        }

        public override bool CreateDust(int i, int j, ref int type) {
            type = DustID.TreasureSparkle;
            return true;
        }

        public override bool CanDrop(int i, int j) {
            return false;//被破坏后不会掉落物品
        }
    }

    internal class DeploySignaltowerTP : TileProcessor
    {
        public override int TargetTileID => ModContent.TileType<DeploySignaltowerTile>();

        /// <summary>
        /// 是否已标记目标点完成
        /// </summary>
        private bool hasMarkedCompletion;

        /// <summary>
        /// 已完成的目标点索引（-1表示未完成任何点）
        /// </summary>
        private int completedTargetIndex = -1;

        /// <summary>
        /// 连接动画进度计时器
        /// </summary>
        private int connectionAnimTimer;

        /// <summary>
        /// 是否正在播放连接动画
        /// </summary>
        private bool isPlayingConnectionAnim;

        /// <summary>
        /// 连接动画持续时间（帧数）
        /// </summary>
        private const int ConnectionAnimDuration = 180; //3秒

        public override void Update() {
            //持续检查是否在目标点范围内
            if (!hasMarkedCompletion) {
                CheckAndMarkTargetCompletion();
            }

            //更新连接动画
            if (isPlayingConnectionAnim) {
                UpdateConnectionAnimation();
            }
        }

        private void CheckAndMarkTargetCompletion() {
            if (VaultUtils.isClient || hasMarkedCompletion) {
                return;
            }

            if (!SignalTowerTargetManager.IsGenerated) {
                return;
            }

            //将Point16转换为Point
            Point tilePos = new(Position.X, Position.Y);

            //检查信号塔位置是否在任何目标点范围内，并获取索引
            int targetIndex = SignalTowerTargetManager.CheckAndMarkCompletionWithIndex(tilePos);
            if (targetIndex >= 0) {
                hasMarkedCompletion = true;
                completedTargetIndex = targetIndex;
                //触发连接动画
                TriggerConnectionAnimation();
            }
        }

        /// <summary>
        /// 触发信号塔连接动画
        /// </summary>
        private void TriggerConnectionAnimation() {
            isPlayingConnectionAnim = true;
            connectionAnimTimer = 0;

            //播放连接成功音效
            SoundEngine.PlaySound(SoundID.Item4 with {
                Volume = 0.8f,
                Pitch = 0.3f,
                MaxInstances = 2
            }, new Vector2(Position.X * 16, Position.Y * 16));

            //额外的科技感音效
            SoundEngine.PlaySound(SoundID.DD2_EtherianPortalSpawnEnemy with {
                Volume = 0.6f,
                Pitch = 0.5f,
                MaxInstances = 1
            }, new Vector2(Position.X * 16, Position.Y * 16));
        }

        /// <summary>
        /// 更新连接动画
        /// </summary>
        private void UpdateConnectionAnimation() {
            connectionAnimTimer++;

            //获取信号塔顶部位置（世界坐标）
            Vector2 towerTop = new Vector2(Position.X * 16 + 48, Position.Y * 16 + 32); //信号塔宽6格（96像素），取中心点

            //阶段1：初始能量聚集（0-30帧）
            if (connectionAnimTimer <= 30) {
                SpawnEnergyGatherEffect(towerTop, connectionAnimTimer / 30f);
            }
            //阶段2：矩阵雨爆发（30-120帧）
            else if (connectionAnimTimer <= 120) {
                SpawnMatrixRainBurst(towerTop, (connectionAnimTimer - 30) / 90f);
            }
            //阶段3：能量脉冲扩散（120-180帧）
            else if (connectionAnimTimer <= ConnectionAnimDuration) {
                SpawnEnergyPulseRings(towerTop, (connectionAnimTimer - 120) / 60f);
            }

            //动画结束
            if (connectionAnimTimer >= ConnectionAnimDuration) {
                isPlayingConnectionAnim = false;
            }
        }

        /// <summary>
        /// 生成能量聚集特效
        /// </summary>
        private static void SpawnEnergyGatherEffect(Vector2 position, float progress) {
            //在信号塔周围生成向中心聚集的粒子
            if (Main.rand.NextBool(2)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float distance = MathHelper.Lerp(200f, 50f, progress);
                Vector2 spawnPos = position + angle.ToRotationVector2() * distance;
                Vector2 velocity = (position - spawnPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(3f, 6f);

                Dust dust = Dust.NewDustPerfect(spawnPos, DustID.Electric, velocity);
                dust.noGravity = true;
                dust.scale = Main.rand.NextFloat(1.2f, 1.8f);
                dust.color = new Color(80, 200, 255);
                dust.alpha = 100;
            }
        }

        /// <summary>
        /// 生成矩阵雨爆发效果
        /// </summary>
        private static void SpawnMatrixRainBurst(Vector2 position, float progress) {
            //每帧生成多条矩阵雨
            int rainCount = (int)MathHelper.Lerp(3, 8, progress);
            for (int i = 0; i < rainCount; i++) {
                //在信号塔上方区域随机生成矩阵雨起始点
                float horizontalSpread = Main.rand.NextFloat(-60f, 60f);
                Vector2 rainStart = position + new Vector2(horizontalSpread, -Main.rand.Next(20, 50));

                //向上发射的速度（模拟数据流向天空）
                Vector2 velocity = new Vector2(
                    Main.rand.NextFloat(-0.5f, 0.5f),
                    Main.rand.NextFloat(-8f, -4f) //负值表示向上
                );

                //创建矩阵雨字符粒子
                SpawnMatrixCharacter(rainStart, velocity, progress);
            }

            //额外的能量波纹
            if (Main.rand.NextBool(5)) {
                for (int i = 0; i < 8; i++) {
                    float angle = MathHelper.TwoPi * i / 8f;
                    Vector2 waveVelocity = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 4f);

                    Dust dust = Dust.NewDustPerfect(position, DustID.TreasureSparkle, waveVelocity);
                    dust.noGravity = true;
                    dust.scale = Main.rand.NextFloat(1f, 1.5f);
                    dust.color = new Color(100, 220, 255);
                    dust.fadeIn = 1.2f;
                }
            }
        }

        /// <summary>
        /// 生成矩阵字符粒子
        /// </summary>
        private static void SpawnMatrixCharacter(Vector2 position, Vector2 velocity, float intensity) {
            //使用Dust模拟字符效果
            Dust charDust = Dust.NewDustPerfect(position, DustID.Electric, velocity);
            charDust.noGravity = true;
            charDust.scale = Main.rand.NextFloat(0.8f, 1.4f);
            charDust.alpha = 50;

            //科技蓝绿色渐变
            float colorLerp = Main.rand.NextFloat();
            charDust.color = Color.Lerp(
                new Color(80, 200, 255),
                new Color(100, 255, 200),
                colorLerp
            );

            //添加轻微的拖尾效果
            charDust.fadeIn = Main.rand.NextFloat(1.2f, 1.6f);
        }

        /// <summary>
        /// 生成能量脉冲环
        /// </summary>
        private static void SpawnEnergyPulseRings(Vector2 position, float progress) {
            //生成向外扩散的能量环
            if (Main.rand.NextBool(3)) {
                int ringSegments = 16;
                float ringRadius = MathHelper.Lerp(50f, 150f, progress);

                for (int i = 0; i < ringSegments; i++) {
                    float angle = MathHelper.TwoPi * i / ringSegments;
                    Vector2 ringPos = position + angle.ToRotationVector2() * ringRadius;
                    Vector2 ringVelocity = angle.ToRotationVector2() * Main.rand.NextFloat(1f, 2f);

                    Dust dust = Dust.NewDustPerfect(ringPos, DustID.Electric, ringVelocity);
                    dust.noGravity = true;
                    dust.scale = Main.rand.NextFloat(0.8f, 1.2f);
                    dust.color = new Color(80, 200, 255) * (1f - progress);
                    dust.alpha = 100;
                }
            }

            //向上发射的余波粒子
            if (Main.rand.NextBool(2)) {
                Vector2 upwardVelocity = new Vector2(
                    Main.rand.NextFloat(-1f, 1f),
                    Main.rand.NextFloat(-6f, -3f)
                );

                Dust dust = Dust.NewDustPerfect(position, DustID.TreasureSparkle, upwardVelocity);
                dust.noGravity = true;
                dust.scale = Main.rand.NextFloat(1f, 1.5f);
                dust.color = new Color(100, 220, 255) * (1f - progress);
            }
        }

        /// <summary>
        /// 当TileProcessor被移除时调用（信号塔被破坏）
        /// </summary>
        public override void OnKill() {
            //如果该信号塔已完成某个目标点，则取消该目标点的完成状态
            if (hasMarkedCompletion && completedTargetIndex >= 0) {
                SignalTowerTargetManager.UnmarkCompletionByIndex(completedTargetIndex);
                //播放失效音效
                SoundEngine.PlaySound(SoundID.Item8 with {
                    Volume = 0.6f,
                    Pitch = -0.3f,
                    MaxInstances = 2
                }, new Vector2(Position.X * 16, Position.Y * 16));
            }
            if (!VaultUtils.isClient) {
                VaultUtils.SpwanItem(this.FromObjectGetParent(), HitBox
                    , new Item(ModContent.ItemType<StarflowPlatedBlock>(), Main.rand.Next(32, 42)));
            }
        }
    }
}
