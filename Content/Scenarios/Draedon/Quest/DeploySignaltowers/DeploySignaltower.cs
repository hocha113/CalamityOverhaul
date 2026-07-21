using CalamityOverhaul.Content.Items.Placeable;
using CalamityOverhaul.Content.Scenarios.Draedon.Quest.DeploySignaltowers.SignalTower;
using InnoVault.TileProcessors;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Scenarios.Draedon.Quest.DeploySignaltowers
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
            return false;//破坏不掉落
        }
    }

    internal class DeploySignaltowerTP : TileProcessor
    {
        public override int TargetTileID => ModContent.TileType<DeploySignaltowerTile>();

        private bool hasMarkedCompletion;
        private int completedTargetIndex = -1;//-1无完成点
        private int connectionAnimTimer;
        private bool isPlayingConnectionAnim;
        private const int ConnectionAnimDuration = 180; //3秒 60帧

        public override void Update() {
            CheckAndMarkTargetCompletion();
            if (isPlayingConnectionAnim) {
                UpdateConnectionAnimation();
            }
        }

        private void CheckAndMarkTargetCompletion() {
            if (hasMarkedCompletion) {
                return;
            }

            if (!SignalTowerTargetManager.IsGenerated) {
                return;
            }

            Point tilePos = new(Position.X, Position.Y);

            int targetIndex = SignalTowerTargetManager.CheckAndMarkCompletionWithIndex(tilePos);
            if (targetIndex >= 0) {
                hasMarkedCompletion = true;
                completedTargetIndex = targetIndex;
                TriggerConnectionAnimation();
            }
        }

        private void TriggerConnectionAnimation() {
            isPlayingConnectionAnim = true;
            connectionAnimTimer = 0;

            SoundEngine.PlaySound(SoundID.Item4 with {
                Volume = 0.8f,
                Pitch = 0.3f,
                MaxInstances = 2
            }, new Vector2(Position.X * 16, Position.Y * 16));

            SoundEngine.PlaySound(SoundID.DD2_EtherianPortalSpawnEnemy with {
                Volume = 0.6f,
                Pitch = 0.5f,
                MaxInstances = 1
            }, new Vector2(Position.X * 16, Position.Y * 16));
        }

        private void UpdateConnectionAnimation() {
            connectionAnimTimer++;

            Vector2 towerTop = new Vector2(Position.X * 16 + 48, Position.Y * 16 + 32);//宽6格取中心

            //阶段1 能量聚集 0-30
            if (connectionAnimTimer <= 30) {
                SpawnEnergyGatherEffect(towerTop, connectionAnimTimer / 30f);
            }
            //阶段2 矩阵雨 30-120
            else if (connectionAnimTimer <= 120) {
                SpawnMatrixRainBurst(towerTop, (connectionAnimTimer - 30) / 90f);
            }
            //阶段3 能量脉冲 120-180
            else if (connectionAnimTimer <= ConnectionAnimDuration) {
                SpawnEnergyPulseRings(towerTop, (connectionAnimTimer - 120) / 60f);
            }

            if (connectionAnimTimer >= ConnectionAnimDuration) {
                isPlayingConnectionAnim = false;
            }
        }

        private static void SpawnEnergyGatherEffect(Vector2 position, float progress) {
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

        private static void SpawnMatrixRainBurst(Vector2 position, float progress) {
            int rainCount = (int)MathHelper.Lerp(3, 8, progress);
            for (int i = 0; i < rainCount; i++) {
                float horizontalSpread = Main.rand.NextFloat(-60f, 60f);
                Vector2 rainStart = position + new Vector2(horizontalSpread, -Main.rand.Next(20, 50));

                Vector2 velocity = new Vector2(
                    Main.rand.NextFloat(-0.5f, 0.5f),
                    Main.rand.NextFloat(-8f, -4f)//向上
                );

                SpawnMatrixCharacter(rainStart, velocity, progress);
            }

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

        private static void SpawnMatrixCharacter(Vector2 position, Vector2 velocity, float intensity) {
            Dust charDust = Dust.NewDustPerfect(position, DustID.Electric, velocity);
            charDust.noGravity = true;
            charDust.scale = Main.rand.NextFloat(0.8f, 1.4f);
            charDust.alpha = 50;

            float colorLerp = Main.rand.NextFloat();
            charDust.color = Color.Lerp(
                new Color(80, 200, 255),
                new Color(100, 255, 200),
                colorLerp
            );

            charDust.fadeIn = Main.rand.NextFloat(1.2f, 1.6f);
        }

        private static void SpawnEnergyPulseRings(Vector2 position, float progress) {
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

        /// <summary>信号塔破坏时取消目标点完成</summary>
        public override void OnKill() {
            if (hasMarkedCompletion && completedTargetIndex >= 0) {
                SignalTowerTargetManager.UnmarkCompletionByIndex(completedTargetIndex);
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
