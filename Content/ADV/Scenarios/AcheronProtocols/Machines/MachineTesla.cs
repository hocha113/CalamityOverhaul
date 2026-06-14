using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.Machines
{
    /// <summary>
    /// 科尔托三号星世界闪电 机械废墟中的残余星流能量放电，视觉上为冷色电弧
    /// </summary>
    internal class MachineTesla : Lightning
    {
        #region 世界闪电特化属性
        public override int MaxBranches => 1; //世界闪电分叉适中
        public override float BranchProbability => 0.38f; //适中的分叉概率
        public override float BranchLengthRatio => 1f;
        public override int LingerTime => 28; //稍长的停留时间
        public override int FadeTime => 18; //较快消失
        public override float BaseWidth => 42f; //世界闪电较粗
        public override float MinBranchWidthRatio => 0.45f;
        public override float MaxBranchWidthRatio => 0.75f;

        //高伤害常量
        public const int MassiveStrikeDamage = short.MaxValue;//32767
        private const int PlayerExtraInvulnerabilityFrames = 20; //额外硬直保护
        private const float GroundResidualRadius = 160f; //地面余辉作用半径

        //导电地面接触扩散
        private const int ConductiveActiveTicks = 14;
        private const int ConductiveDamageBase = MassiveStrikeDamage / 2;
        private const float ConductiveKnockPower = 5.2f;

        private struct ConductiveTileHit
        {
            public Point pos;
            public int activateTick;
            public float intensity;
        }

        private static readonly List<ConductiveTileHit> conductiveTiles = new();
        private static readonly Dictionary<int, int> npcHitTick = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> playerHitTick = new Dictionary<int, int>();
        #endregion

        #region 金属材质定义
        private static readonly HashSet<ushort> MetalTiles = new() {
            TileID.Copper, TileID.Tin, TileID.Iron, TileID.Lead, TileID.Silver, TileID.Tungsten, TileID.Gold, TileID.Platinum,
            TileID.Demonite, TileID.Crimtane, TileID.Meteorite, TileID.Hellstone, TileID.Cobalt, TileID.Palladium,
            TileID.Mythril, TileID.Orichalcum, TileID.Adamantite, TileID.Titanium, TileID.Chlorophyte
        };

        private static float GetConductivity(ushort tileType) {
            if (MetalTiles.Contains(tileType)) return 1.0f;
            if (tileType == TileID.Stone || tileType == TileID.Dirt || tileType == TileID.Mud ||
                tileType == TileID.Sand || tileType == TileID.ClayBlock) return 0.55f;
            return 0.35f;
        }
        #endregion

        public override void SetLightningDefaults() {
            Projectile.hostile = true;  //环境危害
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
            Projectile.damage = MassiveStrikeDamage;

            //世界闪电强度稍高
            Intensity = 1.0f;
        }

        public override Color GetLightningColor(float factor) => new Color(103, 255, 255);

        /// <summary>

        /// 世界闪电的宽度函数 - 使用优化的曲线

        /// </summary>
        public override float GetLightningWidth(float factor) {
            //更平滑的宽度曲线，避免过粗
            float curve = MathF.Sin(factor * MathHelper.Pi);
            //中部略粗，但整体更细
            float shapeFactor = curve * (0.55f + 0.35f * MathF.Pow(MathF.Sin(factor * MathHelper.Pi), 1.5f));
            return ThunderWidth * shapeFactor * Intensity * 0.9f; //额外缩小10%
        }

        #region 寻找目标算法（保持原有的复杂算法）
        public override Vector2 FindTargetPosition() {
            return FindHighestSolidInAreaBelow(Projectile.Center, 60, 1000);
        }

        /// <summary>

        /// 物理/逻辑要点：<br/> 1.闪电优先击中“最高 + 导电性好 + 突出地形”的点<br/> 2.支持实体(玩家/NPC)作为优先落点：需足够突出(minEntityProtrusion)，并在水平搜索范围内<br/> 3.建立局部高度图，对每个X找到最上层可导实体方块(实心且非平台)<br/> 4.对金属类方块给予高度偏置(等效“更容易引雷”)<br/> 5.对局部凸起(比相邻更高)给予额外偏置<br/> 6.若存在视线遮挡(Line Of Sight)则降低优先级（选取一条较清晰通路）<br/> 7.在候选高度相近时引入轻微随机扰动，避免落点过于机械<br/> 8.若无任何合格候选，落到最大搜索深度的垂直下方<br/> 评分规则(数值越小越优)：score = worldY - materialBias - peakBias - entityBias + losPenalty<br/> 其中：<br/> materialBias：金属类 ~14 像素 等效抬高<br/> peakBias：局部凸起奖励 8<br/> entityBias：实体（玩家/NPC）基准 18 ~ 26（玩家略高）<br/> losPenalty：无直视 +12<br/> 终选：score 最小；若差距 小于 6 再比较真实高度 - 更高(更小 Y)优先<br/>

        /// </summary>
        internal static Vector2 FindHighestSolidInAreaBelow(
            Vector2 startPosition,
            int searchWidth = 10,
            int maxSearchHeight = 60) {
            Point startTile = startPosition.ToTileCoordinates();
            int halfWidth = searchWidth / 2;
            int minX = Math.Max(0, startTile.X - halfWidth);
            int maxX = Math.Min(Main.maxTilesX - 1, startTile.X + halfWidth);
            int maxY = Math.Min(Main.maxTilesY - 1, startTile.Y + maxSearchHeight);

            Span<int> columnTopY = stackalloc int[maxX - minX + 1];
            for (int i = 0; i < columnTopY.Length; i++) columnTopY[i] = -1;

            //扫描：建立高度图
            for (int x = minX; x <= maxX; x++) {
                for (int y = startTile.Y; y <= maxY; y++) {
                    Tile tile = Framing.GetTileSafely(x, y);
                    if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType]) {
                        columnTopY[x - minX] = y;
                        break;
                    }
                }
            }

            //记录最佳
            bool foundCandidate = false;
            float bestScore = float.MaxValue;
            Vector2 bestPos = startPosition + new Vector2(0, maxSearchHeight * 16);
            float bestRealY = float.MaxValue;

            //工具函数：Line Of Sight 简化
            bool HasLineOfSight(Vector2 from, Vector2 to) {
                return Collision.CanHitLine(from, 1, 1, to, 1, 1);
            }

            //评估一个候选
            void TryAccept(Vector2 worldPos, bool isEntity, bool isPlayerEntity, ushort tileType = 0, bool hasTile = false, bool isPeak = false) {
                float worldY = worldPos.Y;

                float materialBias = 0f;
                if (hasTile) {
                    if (MetalTiles.Contains(tileType))
                        materialBias = 14f;
                    else if (tileType == TileID.Stone || tileType == TileID.Dirt)
                        materialBias = 4f;
                }

                float peakBias = isPeak ? 8f : 0f;
                float entityBias = 0f;
                if (isEntity) {
                    entityBias = isPlayerEntity ? 26f : 18f;
                }

                bool los = HasLineOfSight(startPosition, worldPos);
                float losPenalty = los ? 0f : 12f;
                float score = worldY - materialBias - peakBias - entityBias + losPenalty;
                score += Main.rand.NextFloat(-1.2f, 1.2f);

                if (!foundCandidate ||
                    score < bestScore - 0.5f ||
                    Math.Abs(score - bestScore) < 0.5f && worldY < bestRealY) {
                    foundCandidate = true;
                    bestScore = score;
                    bestPos = worldPos;
                    bestRealY = worldY;
                }
            }

            //先处理地形
            for (int x = minX; x <= maxX; x++) {
                int topY = columnTopY[x - minX];
                if (topY == -1) continue;

                //局部峰判断
                bool peak = false;
                int leftIdx = x - 1 - minX;
                int rightIdx = x + 1 - minX;
                int selfY = topY;
                int leftY = leftIdx >= 0 ? columnTopY[leftIdx] : int.MaxValue;
                int rightY = rightIdx < columnTopY.Length ? columnTopY[rightIdx] : int.MaxValue;
                if (selfY != -1 && selfY < leftY && selfY < rightY)
                    peak = true;

                Tile tile = Framing.GetTileSafely(x, topY);
                Vector2 surface = new Vector2(x * 16 + 8, topY * 16 + 8);

                TryAccept(surface, false, false, tile.TileType, true, peak);
            }

            //再处理实体
            const float minEntityProtrusion = 16f;
            const float entityHorizontalRange = 16f * 6;
            for (int n = 0; n < Main.maxNPCs; n++) {
                NPC npc = Main.npc[n];
                if (!npc.active || npc.dontTakeDamage) continue;

                float dx = Math.Abs(npc.Center.X - startPosition.X);
                if (dx > searchWidth * 16 / 2f + entityHorizontalRange) continue;

                Point entTile = npc.Center.ToTileCoordinates();
                if (entTile.X < minX || entTile.X > maxX) continue;

                int groundY = columnTopY[entTile.X - minX];
                float topY = npc.Hitbox.Top;
                if (groundY != -1) {
                    float groundWorldY = groundY * 16 + 8;
                    if (groundWorldY - topY < minEntityProtrusion) continue;
                }

                Vector2 apex = new Vector2(npc.position.X + npc.width / 2f, npc.Hitbox.Top + 6f);
                TryAccept(apex, true, false);
            }

            if (foundCandidate) {
                bestPos += Main.rand.NextVector2Circular(2.2f, 2.2f);
                return bestPos;
            }

            return startPosition + new Vector2(0, maxSearchHeight * 16);
        }
        #endregion

        #region 重写基类方法
        public override void OnStrike() {
            //播放雷声
            SoundStyle sound = CWRSound.Thunder;
            sound.PitchRange = (-0.2f, 0.1f);
            sound.Volume = 0.6f;
            sound.MaxInstances = 6;
            SoundEngine.PlaySound(sound, TargetPosition);

            //生成导电效果
            GenerateConductiveDischarge(TargetPosition.ToTileCoordinates(), 520, 56);

            //地面残留效果
            SpawnGroundResidual(TargetPosition);

            //触发天空闪光效果：根据与玩家的距离计算闪光强度
            Player player = Main.LocalPlayer;
            if (player != null && player.active) {
                float dist = Vector2.Distance(TargetPosition, player.Center);
                //1200像素内为最强闪光，3000像素外无闪光
                float flashStrength = MathHelper.Clamp(1f - (dist - 1200f) / 1800f, 0f, 1f);
                if (flashStrength > 0.05f) {
                    MachineWorldSky.TriggerLightningFlash(TargetPosition, flashStrength);
                }
            }
        }

        /// <summary>

        /// 重写分叉创建 - 世界闪电的特殊分叉逻辑

        /// </summary>
        protected override void CreateBranch() {
            //首先调用基类的优化分叉算法
            base.CreateBranch();

            //世界闪电的额外特殊分叉（概率较低，避免过多）
            if (BranchTrails.Count < MaxBranches - 1 && Main.rand.NextFloat() < 0.15f) {
                CreateSpecialBranch();
            }
        }

        /// <summary>

        /// 创建特殊的世界闪电分叉

        /// </summary>
        private void CreateSpecialBranch() {
            if (LightningTexture == null || TrailPoints.Count < 8) return;

            var points = TrailPoints.ToArray();
            //从主干前半段选择
            int startIndex = Main.rand.Next(points.Length / 4, points.Length / 2);
            Vector2 branchStart = points[startIndex];

            List<Vector2> branchPoints = new List<Vector2> { branchStart };

            //主干方向
            Vector2 mainDirection = (TargetPosition - Projectile.Center).SafeNormalize(Vector2.UnitY);

            //更大的偏离角度
            float sideSign = Main.rand.NextBool() ? 1 : -1;
            float branchAngle = mainDirection.ToRotation() + sideSign * Main.rand.NextFloat(0.7f, 1.2f);

            //较短的分叉
            int branchLength = Main.rand.Next(12, 22);
            Vector2 currentPos = branchStart;

            for (int i = 0; i < branchLength; i++) {
                float progressFactor = i / (float)branchLength;

                //角度逐渐变化
                branchAngle += Main.rand.NextFloat(-0.15f, 0.15f);
                Vector2 branchDirection = branchAngle.ToRotationVector2();

                //向下偏移
                branchDirection.Y += 0.08f * progressFactor;
                branchDirection = branchDirection.SafeNormalize(Vector2.UnitY);

                float randomOffset = Main.rand.NextFloat(-15f, 15f) * (1f - progressFactor * 0.6f);
                Vector2 perpendicular = branchDirection.RotatedBy(MathHelper.PiOver2);

                currentPos += branchDirection * Main.rand.NextFloat(11f, 15f) + perpendicular * randomOffset;
                branchPoints.Add(currentPos);

                //提前结束概率
                if (Main.rand.NextFloat() < 0.05f + progressFactor * 0.08f) break;
            }

            if (branchPoints.Count > 3) {
                float widthRatio = Main.rand.NextFloat(0.35f, 0.6f);

                var branch = new ThunderTrail(LightningTexture,
                    factor => GetLightningWidth(factor) * widthRatio,
                    factor => GetLightningColor(factor) * Main.rand.NextFloat(0.7f, 0.9f),
                    GetAlpha) {
                    CanDraw = true,
                    UseNonOrAdd = true,
                    PartitionPointCount = 2,
                    BasePositions = branchPoints.ToArray()
                };
                branch.SetRange((0, Main.rand.Next(3, 6)));
                branch.SetExpandWidth(Main.rand.Next(2, 4));
                branch.RandomThunder();

                BranchTrails.Add(branch);
            }
        }
        #endregion

        #region 伤害处理
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.FinalDamage.Base = MassiveStrikeDamage;
            if (target.boss) {
                modifiers.FinalDamage *= 0.65f;
            }
            target.AddBuff(BuffID.Electrified, 300);
            target.AddBuff(BuffID.OnFire3, 240);
        }

        public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers) {
            modifiers.FinalDamage.Base = MassiveStrikeDamage;
            modifiers.FinalDamage *= 0.85f;
            target.AddBuff(BuffID.Electrified, 300);
            target.AddBuff(BuffID.OnFire3, 180);
            target.hurtCooldowns[0] = Math.Max(target.hurtCooldowns[0], PlayerExtraInvulnerabilityFrames);
        }
        #endregion

        #region 导电效果系统（保持原有复杂算法）
        private struct ConductionNode
        {
            public Point pos;
            public float intensity;
            public int layer;
        }

        /// <summary>

        /// 最大电弧跳跃距离（穿越空气格数），模拟电弧可以跨越小段空气

        /// </summary>
        private const int MaxArcJumpDistance = 3;
        /// <summary>
        /// 初始种子搜索半径，在落点附近播种多个起始节点保证扩散覆盖
        /// </summary>
        private const int SeedSearchRadius = 4;
        /// <summary>
        /// 前N层不进行随机淘汰，保证初始扩散稳定
        /// </summary>
        private const int GuaranteedSpreadLayers = 5;

        public static void GenerateConductiveDischarge(Point startTile, int maxNodes = 480, int maxRadius = 48) {
            if (!WorldGen.InWorld(startTile.X, startTile.Y)) return;

            Queue<ConductionNode> q = new Queue<ConductionNode>();
            Dictionary<Point, ConductionNode> accepted = new Dictionary<Point, ConductionNode>();

            //在落点附近搜索所有实心方块作为初始种子，解决尖塔尖端只有单点的问题
            int seedCount = 0;
            for (int dy = -1; dy <= SeedSearchRadius; dy++) {
                for (int dx = -SeedSearchRadius; dx <= SeedSearchRadius; dx++) {
                    int nx = startTile.X + dx;
                    int ny = startTile.Y + dy;
                    if (!WorldGen.InWorld(nx, ny)) continue;
                    Tile t = Framing.GetTileSafely(nx, ny);
                    if (!t.HasTile || !Main.tileSolid[t.TileType] || Main.tileSolidTop[t.TileType]) continue;

                    Point sp = new Point(nx, ny);
                    if (accepted.ContainsKey(sp)) continue;

                    //距离越远初始强度越低
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    float seedIntensity = MathHelper.Clamp(1f - dist * 0.12f, 0.5f, 1f);
                    ConductionNode seed = new ConductionNode { pos = sp, intensity = seedIntensity, layer = 0 };
                    accepted[sp] = seed;
                    q.Enqueue(seed);
                    seedCount++;
                }
            }

            //如果附近连一个实心方块都没有，放弃
            if (seedCount == 0) return;

            int processed = 0;

            Point[] dirs = [
                new Point(1,0), new Point(-1,0), new Point(0,1), new Point(0,-1),
                new Point(2,0), new Point(-2,0), new Point(1,1), new Point(-1,1),
                new Point(1,-1), new Point(-1,-1)
            ];

            int currentTick = (int)Main.GameUpdateCount;
            while (q.Count > 0 && processed < maxNodes) {
                var node = q.Dequeue();
                processed++;
                Tile tile = Framing.GetTileSafely(node.pos.X, node.pos.Y);
                if (!tile.HasTile || !Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType])
                    continue;

                if (Math.Abs(node.pos.X - startTile.X) > maxRadius ||
                    Math.Abs(node.pos.Y - startTile.Y) > maxRadius)
                    continue;

                Vector2 center = new Vector2(node.pos.X * 16 + 8, node.pos.Y * 16 + 8);
                float fade = node.intensity;
                PRT_TileHightlightByWorld p = PRTLoader.NewParticle<PRT_TileHightlightByWorld>(center.Floor() / 16 * 16, Vector2.Zero, Color.White);
                p.idleTime = node.layer;
                p.Opacity = fade;

                conductiveTiles.Add(new ConductiveTileHit {
                    pos = node.pos,
                    activateTick = currentTick + node.layer,
                    intensity = node.intensity
                });

                if (node.intensity < 0.06f) continue;

                for (int i = 0; i < dirs.Length; i++) {
                    //尝试直接邻居，若为空气则电弧跳跃搜索更远的实心方块
                    Point dir = dirs[i];
                    Point np = new Point(node.pos.X + dir.X, node.pos.Y + dir.Y);
                    int jumpSteps = 1;

                    if (WorldGen.InWorld(np.X, np.Y) && !accepted.ContainsKey(np)) {
                        Tile ntile = Framing.GetTileSafely(np.X, np.Y);
                        if (!ntile.HasTile || !Main.tileSolid[ntile.TileType] || Main.tileSolidTop[ntile.TileType]) {
                            //邻居是空气，尝试电弧跳跃：沿同方向继续探测
                            Point jumpTarget = np;
                            bool found = false;
                            for (int j = 2; j <= MaxArcJumpDistance; j++) {
                                jumpTarget = new Point(node.pos.X + dir.X * j, node.pos.Y + dir.Y * j);
                                if (!WorldGen.InWorld(jumpTarget.X, jumpTarget.Y) || accepted.ContainsKey(jumpTarget)) break;
                                Tile jt = Framing.GetTileSafely(jumpTarget.X, jumpTarget.Y);
                                if (jt.HasTile && Main.tileSolid[jt.TileType] && !Main.tileSolidTop[jt.TileType]) {
                                    np = jumpTarget;
                                    jumpSteps = j;
                                    found = true;
                                    break;
                                }
                            }
                            if (!found) continue;
                        }
                    }

                    if (!WorldGen.InWorld(np.X, np.Y) || accepted.ContainsKey(np)) continue;

                    Tile finalTile = Framing.GetTileSafely(np.X, np.Y);
                    if (!finalTile.HasTile || !Main.tileSolid[finalTile.TileType] || Main.tileSolidTop[finalTile.TileType])
                        continue;

                    float cond = GetConductivity(finalTile.TileType);
                    float nextIntensity = node.intensity * (0.88f + 0.10f * cond);

                    //跳跃越远衰减越大
                    if (jumpSteps > 1) nextIntensity *= MathF.Pow(0.78f, jumpSteps - 1);
                    if (Math.Abs(dir.X) == 2) nextIntensity *= 0.85f;
                    if (dir.Y < 0) nextIntensity *= 0.80f;

                    //前几层保证扩散，不随机淘汰
                    if (node.layer >= GuaranteedSpreadLayers) {
                        float surviveChance = 0.55f * cond + 0.45f;
                        if (Main.rand.NextFloat() > surviveChance) continue;
                    }

                    if (nextIntensity < 0.04f) continue;

                    ConductionNode next = new ConductionNode {
                        pos = np,
                        intensity = nextIntensity,
                        layer = node.layer + jumpSteps
                    };
                    accepted[np] = next;
                    q.Enqueue(next);
                }
            }
        }

        private void SpawnGroundResidual(Vector2 center) {
            for (int i = 0; i < 18; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4f, 4f) * Main.rand.NextFloat(0.6f, 1.2f);
                int d1 = Dust.NewDust(center - new Vector2(8), 16, 16, DustID.Electric, vel.X, vel.Y, 150, default, Main.rand.NextFloat(0.8f, 1.4f));
                Main.dust[d1].noGravity = true;
            }
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(2.2f, 2.2f) * Main.rand.NextFloat(0.5f, 1.1f);
                int smoke = Dust.NewDust(center - new Vector2(12), 24, 24, DustID.Smoke, vel.X, vel.Y, 180, default, Main.rand.NextFloat(1.1f, 1.8f));
                Main.dust[smoke].velocity *= 0.7f;
            }

            int radiusSq = (int)(GroundResidualRadius * GroundResidualRadius);
            for (int n = 0; n < Main.maxNPCs; n++) {
                NPC npc = Main.npc[n];
                if (npc.active && !npc.friendly && !npc.dontTakeDamage && npc.DistanceSQ(center) <= radiusSq) {
                    npc.AddBuff(BuffID.Electrified, 240);
                    npc.AddBuff(BuffID.OnFire3, 180);
                }
            }
        }

        public static void UpdateConductiveContacts() {
            if (conductiveTiles.Count == 0) return;
            int tick = (int)Main.GameUpdateCount;
            Rectangle tileRect = new(0, 0, 16, 16);

            for (int i = conductiveTiles.Count - 1; i >= 0; i--) {
                var ct = conductiveTiles[i];
                if (tick < ct.activateTick) continue;
                if (tick > ct.activateTick + ConductiveActiveTicks) {
                    conductiveTiles.RemoveAt(i);
                    continue;
                }

                tileRect.X = ct.pos.X * 16;
                tileRect.Y = ct.pos.Y * 16;
                Vector2 center = new(tileRect.X + 8, tileRect.Y + 8);

                //NPC伤害处理
                for (int n = 0; n < Main.maxNPCs; n++) {
                    NPC npc = Main.npc[n];
                    if (!npc.active || npc.friendly || npc.dontTakeDamage || !npc.Hitbox.Intersects(tileRect))
                        continue;
                    if (npcHitTick.TryGetValue(n, out int last) && last == tick) continue;

                    npcHitTick[n] = tick;
                    int dmg = (int)(ConductiveDamageBase * (0.65f + 0.55f * ct.intensity));
                    if (dmg < 1) dmg = 1;

                    npc.StrikeNPCNoInteraction(dmg, 0f, 0);
                    npc.AddBuff(BuffID.Electrified, 120);
                    Vector2 kb = (npc.Center - center).SafeNormalize(Vector2.UnitY) * ConductiveKnockPower * (0.4f + ct.intensity * 0.6f);
                    npc.velocity += kb;
                }
            }
        }
        #endregion

        #region 绘制相关
        protected override void DrawLightningCore(Color lightColor) {
            Texture2D mainTex = TextureAssets.Projectile[Type].Value;
            Color c = Lighting.GetColor(Projectile.Center.ToTileCoordinates(), new Color(103, 255, 255));
            c.A = 0;
            c *= ThunderAlpha;

            Vector2 position = Projectile.Center - Main.screenPosition;
            Vector2 origin = mainTex.Size() / 2;

            Main.spriteBatch.Draw(mainTex, position, null, c, 0, origin, 0.15f, 0, 0);
            Main.spriteBatch.Draw(mainTex, position, null, c, 0, origin, 0.5f, 0, 0);

            c = lightColor;
            c.A = 0;
            c *= ThunderAlpha;
            Main.spriteBatch.Draw(mainTex, position, null, c, 0, origin, 0.2f, 0, 0);
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 5; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Electric, VaultUtils.RandVr(5));
                d.noGravity = true;
            }
        }
        #endregion
    }

    //来自珊瑚石，谢谢你瓶中微光
    internal class PRT_TileHightlightByWorld : BasePRT
    {
        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Lifetime = 22;
        }
        public int idleTime;
        public int originalIdleTime;
        public override void AI() {
            if (ai[0] == 0) {
                ai[0] = 1f;
                originalIdleTime = idleTime;
            }
            if (--idleTime > 0) {
                return;
            }

            Opacity++;
            if (Opacity > Lifetime) {
                active = false;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (idleTime > 0) {
                return false;
            }
            spriteBatch.Draw(TexValue, Position - Main.screenPosition, null
                , Color * (1f - LifetimeCompletion), Rotation, TexValue.Size() / 2, Scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
