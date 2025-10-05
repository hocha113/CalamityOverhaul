using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Skills
{
    internal static class SeaDomain
    {
        internal static int ID = 4;
        private const int ToggleCD = 15;

        public static void AltUse(Item item, Player player) {
            var hp = player.GetOverride<HalibutPlayer>();
            if (hp.SeaDomainToggleCD > 0) return;
            if (!hp.SeaDomainActive) {
                Activate(player);
            }
            else {
                Deactivate(player);
            }
            hp.SeaDomainToggleCD = ToggleCD;
        }

        public static void Activate(Player player) {
            var hp = player.GetOverride<HalibutPlayer>();
            if (hp.SeaDomainActive) return;
            hp.SeaDomainActive = true;
            if (Main.myPlayer == player.whoAmI) {
                SpawnDomain(player);
            }
        }

        public static void Deactivate(Player player) {
            var hp = player.GetOverride<HalibutPlayer>();
            hp.SeaDomainActive = false;
        }

        internal static void SpawnDomain(Player player) {
            var hp = player.GetOverride<HalibutPlayer>();
            int layers = Math.Clamp(hp.SeaDomainLayers, 1, 10);
            var source = player.GetSource_Misc("SeaDomainSkill");
            // 通过 ai[0] 传递层数信息
            Projectile.NewProjectile(source, player.Center, Vector2.Zero
                , ModContent.ProjectileType<SeaDomainProj>(), 0, 0, player.whoAmI, layers);
        }

        /// <summary>判断是否为弱小可束缚生物</summary>
        public static bool IsWeakEntity(NPC npc) {
            if (npc.boss || npc.defense > 20 || npc.lifeMax > 500)
                return false;
            if (npc.knockBackResist <= 0.3f)
                return false;
            return npc.friendly == false && npc.damage > 0;
        }
    }

    #region 领域层数据
    internal class DomainLayer
    {
        public float Radius;
        public float TargetRadius;
        public List<DomainFishBoid> Fish;
        public List<VolumetricRay> Rays;
        public int FishCount;
        public Color BorderColor;
        public float RotationSpeed;
        public float WaveAmplitude;
        public int LayerIndex;

        public DomainLayer(int layerIndex, int totalLayers) {
            LayerIndex = layerIndex;
            
            // 优化半径计算：前3层密集，后续层逐渐扩大间距
            float baseRadius = 180f;
            float radiusStep;
            if (layerIndex < 3) {
                // 前3层：紧密布局
                radiusStep = 120f;
                TargetRadius = baseRadius + (layerIndex * radiusStep);
            }
            else {
                // 3层之后：渐进扩大
                float base3LayerRadius = baseRadius + (2 * 120f); // 第3层的半径
                radiusStep = 100f + ((layerIndex - 2) * 15f); // 逐渐增大间距
                TargetRadius = base3LayerRadius + radiusStep * (layerIndex - 2);
            }
            Radius = TargetRadius;

            // 鱼数量：前5层线性增长，后续层增长放缓
            if (layerIndex < 5) {
                FishCount = 20 + (layerIndex * 10);
            }
            else {
                FishCount = 60 + ((layerIndex - 4) * 6);
            }

            // 颜色渐变：多层时使用更丰富的色谱
            float colorProgress = layerIndex / (float)Math.Max(1, totalLayers - 1);
            if (totalLayers <= 3) {
                // 1-3层：深蓝到浅蓝
                BorderColor = Color.Lerp(new Color(60, 160, 255), new Color(120, 220, 255), colorProgress);
            }
            else if (totalLayers <= 7) {
                // 4-7层：深蓝到青绿
                BorderColor = Color.Lerp(new Color(50, 140, 255), new Color(100, 230, 240), colorProgress);
            }
            else {
                // 8-10层：深蓝到亮青
                BorderColor = Color.Lerp(new Color(40, 120, 255), new Color(120, 255, 255), colorProgress);
            }

            // 旋转速度：外层更慢
            RotationSpeed = MathHelper.Lerp(1f, 0.3f, colorProgress);

            // 波动幅度：外层更大
            WaveAmplitude = MathHelper.Lerp(6f, 14f, colorProgress);

            Fish = new List<DomainFishBoid>();
            Rays = new List<VolumetricRay>();
        }

        public void InitializeFish(Vector2 center) {
            Fish.Clear();
            for (int i = 0; i < FishCount; i++) {
                float angle = (i / (float)FishCount) * MathHelper.TwoPi;
                Fish.Add(new DomainFishBoid(center, Radius, angle));
            }
        }

        public void InitializeRays(Vector2 center) {
            Rays.Clear();
            // 光束数量：内层少，外层多
            int rayCount = 4 + Math.Min(12, (int)(Radius / 80f));
            for (int i = 0; i < rayCount; i++) {
                Rays.Add(new VolumetricRay(center, Radius));
            }
        }
    }
    #endregion

    #region 领域鱼群系统
    internal enum FishSize { Small, Medium, Large }
    
    internal class DomainFishBoid
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Scale;
        public float Frame;
        public FishSize Size;
        public int FishType;
        public Color TintColor;
        private float OrbitAngle;
        private float OrbitSpeed;
        private float RadiusOffset;
        private float NoiseSeed;
        private float VerticalWave;
        private float VerticalPhase;
        private float DashTimer;
        private float DashCooldown;
        private Vector2 dashVelocity;
        public readonly List<Vector2> TrailPositions = new();
        private const int MaxTrailLength = 8;

        public DomainFishBoid(Vector2 center, float baseRadius, float angle) {
            var rand = Main.rand;
            OrbitAngle = angle;
            
            float sizeRoll = rand.NextFloat();
            if (sizeRoll < 0.5f) {
                Size = FishSize.Small;
                Scale = 0.4f + rand.NextFloat() * 0.25f;
                OrbitSpeed = 0.018f + rand.NextFloat() * 0.025f;
                VerticalWave = rand.NextFloat(10f, 20f);
            }
            else if (sizeRoll < 0.85f) {
                Size = FishSize.Medium;
                Scale = 0.65f + rand.NextFloat() * 0.3f;
                OrbitSpeed = 0.012f + rand.NextFloat() * 0.015f;
                VerticalWave = rand.NextFloat(20f, 35f);
            }
            else {
                Size = FishSize.Large;
                Scale = 1.0f + rand.NextFloat() * 0.4f;
                OrbitSpeed = 0.008f + rand.NextFloat() * 0.01f;
                VerticalWave = rand.NextFloat(25f, 45f);
            }
            
            FishType = rand.Next(3);
            RadiusOffset = rand.NextFloat(-40f, 40f);
            Frame = rand.NextFloat(10f);
            NoiseSeed = rand.NextFloat(1000f);
            VerticalPhase = rand.NextFloat(MathHelper.TwoPi);
            Position = center + OrbitAngle.ToRotationVector2() * (baseRadius + RadiusOffset);
            Velocity = Vector2.Zero;
            DashCooldown = rand.NextFloat(180f, 360f);
            
            TintColor = Size switch {
                FishSize.Small => new Color(120, 220, 255, 255),
                FishSize.Medium => new Color(100, 200, 240, 255),
                FishSize.Large => new Color(80, 180, 230, 255),
                _ => Color.White
            };
        }

        public void Update(Vector2 center, float baseRadius, float domainAlpha) {
            OrbitAngle += OrbitSpeed * (0.8f + domainAlpha * 0.4f);
            float time = Main.GameUpdateCount * 0.05f + NoiseSeed;
            float wobble = (float)Math.Sin(time * 1.2f) * 8f;
            float verticalOffset = (float)Math.Sin(time * 0.8f + VerticalPhase) * VerticalWave * domainAlpha;
            
            float targetRadius = baseRadius + RadiusOffset + wobble;
            Vector2 targetPos = center + OrbitAngle.ToRotationVector2() * targetRadius;
            targetPos.Y += verticalOffset;

            DashCooldown--;
            if (DashCooldown <= 0 && DashTimer <= 0) {
                DashTimer = 25f;
                DashCooldown = Main.rand.NextFloat(200f, 400f);
                float dashAngle = OrbitAngle + Main.rand.NextFloat(-0.5f, 0.5f);
                dashVelocity = dashAngle.ToRotationVector2() * (6f + Scale * 3f);
            }

            if (DashTimer > 0) {
                DashTimer--;
                targetPos += dashVelocity * (DashTimer / 25f);
                dashVelocity *= 0.92f;
            }

            Position = Vector2.Lerp(Position, targetPos, 0.15f);
            Velocity = (targetPos - Position) * 0.1f;
            Frame += 0.3f + OrbitSpeed * 8f + (DashTimer > 0 ? 0.5f : 0f);

            TrailPositions.Insert(0, Position);
            if (TrailPositions.Count > MaxTrailLength) {
                TrailPositions.RemoveAt(TrailPositions.Count - 1);
            }
        }

        public void DrawTrail(float alpha) {
            if (TrailPositions.Count < 2) return;
            Texture2D tex = TextureAssets.MagicPixel.Value;
            
            for (int i = 0; i < TrailPositions.Count - 1; i++) {
                float progress = i / (float)TrailPositions.Count;
                float trailAlpha = (1f - progress) * alpha * 0.5f;
                float width = Scale * (3f - progress * 2f);
                
                Vector2 start = TrailPositions[i];
                Vector2 end = TrailPositions[i + 1];
                Vector2 diff = end - start;
                float rot = diff.ToRotation();
                float len = diff.Length();
                
                Color c = TintColor * trailAlpha * 0.6f;
                Main.spriteBatch.Draw(tex, start - Main.screenPosition, new Rectangle(0, 0, 1, 1), 
                    c, rot, Vector2.Zero, new Vector2(len, width), SpriteEffects.None, 0f);
            }
        }
    }
    #endregion

    #region 深度气泡链
    internal class BubbleChain
    {
        public Vector2 Position;
        public float Life;
        public float MaxLife;
        public float Speed;
        public float Scale;
        private float wobblePhase;

        public BubbleChain(Vector2 pos) {
            Position = pos;
            Life = 0f;
            MaxLife = Main.rand.NextFloat(120f, 200f);
            Speed = Main.rand.NextFloat(0.8f, 1.5f);
            Scale = Main.rand.NextFloat(0.6f, 1.2f);
            wobblePhase = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public void Update() {
            Life++;
            Position.Y -= Speed;
            float wobble = (float)Math.Sin(Life * 0.1f + wobblePhase) * 2f;
            Position.X += wobble * 0.05f;
        }

        public bool ShouldRemove() => Life >= MaxLife;

        public void Draw(float domainAlpha) {
            float progress = Life / MaxLife;
            float alpha = (1f - progress) * domainAlpha * 0.7f;
            Texture2D tex = TextureAssets.Extra[ExtrasID.SharpTears].Value; // 气泡贴图
            Color c = new Color(150, 220, 255, 0) * alpha;
            Main.spriteBatch.Draw(tex, Position - Main.screenPosition, null, c, 0f, tex.Size() / 2f, Scale, SpriteEffects.None, 0f);
        }
    }
    #endregion

    #region 光束效果
    internal class VolumetricRay
    {
        public Vector2 StartPos;
        public float Angle;
        public float Length;
        public float Width;
        public float Rotation;
        public float Alpha;
        private float phaseOffset;

        public VolumetricRay(Vector2 center, float radius) {
            var rand = Main.rand;
            Angle = rand.NextFloat(MathHelper.TwoPi);
            StartPos = center + Angle.ToRotationVector2() * radius * rand.NextFloat(0.2f, 0.6f);
            Length = rand.NextFloat(80f, 180f);
            Width = rand.NextFloat(15f, 35f);
            Rotation = Angle + rand.NextFloat(-0.3f, 0.3f);
            phaseOffset = rand.NextFloat(10f);
        }

        public void Update(Vector2 center) {
            float time = Main.GameUpdateCount * 0.02f + phaseOffset;
            Alpha = 0.3f + (float)Math.Sin(time) * 0.2f;
            Rotation += 0.003f;
        }

        public void Draw(Vector2 center, float domainAlpha) {
            Texture2D tex = CWRAsset.StarTexture.Value;
            Vector2 endPos = StartPos + Rotation.ToRotationVector2() * Length;
            Vector2 diff = endPos - StartPos;
            float rot = diff.ToRotation();
            float len = diff.Length();
            
            Color c = new Color(100, 200, 255, 0) * Alpha * domainAlpha * 0.25f;
            Main.spriteBatch.Draw(tex, StartPos - Main.screenPosition, null, 
                c, rot, new Vector2(0, 0.5f), new Vector2(len, 1), SpriteEffects.None, 0f);
        }
    }
    #endregion

    #region 水压侵蚀效果
    internal class WaterPressureEffect
    {
        public int NPCIndex;
        public int Life;
        public readonly List<WaterPressureParticle> Particles = new();
        private int particleSpawnTimer;

        public WaterPressureEffect(int npcIndex) {
            NPCIndex = npcIndex;
            Life = 0;
        }

        public void Update(Vector2 npcCenter, float npcRadius) {
            Life++;
            particleSpawnTimer++;
            
            if (particleSpawnTimer % 3 == 0) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = npcCenter + angle.ToRotationVector2() * npcRadius * Main.rand.NextFloat(0.8f, 1.2f);
                Particles.Add(new WaterPressureParticle(pos, npcCenter));
            }

            for (int i = Particles.Count - 1; i >= 0; i--) {
                Particles[i].Update();
                if (Particles[i].ShouldRemove()) {
                    Particles.RemoveAt(i);
                }
            }
        }

        public void Draw() {
            foreach (var p in Particles) {
                p.Draw();
            }
        }
    }

    internal class WaterPressureParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Life;
        public float MaxLife;
        public float Scale;
        public float Rotation;
        private Color color;

        public WaterPressureParticle(Vector2 pos, Vector2 npcCenter) {
            Position = pos;
            Life = 0f;
            MaxLife = Main.rand.NextFloat(20f, 40f);
            Scale = Main.rand.NextFloat(0.8f, 1.5f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            
            Vector2 toCenter = (npcCenter - pos).SafeNormalize(Vector2.Zero);
            Velocity = toCenter * Main.rand.NextFloat(2f, 4f) + Main.rand.NextVector2Circular(1f, 1f);
            
            color = Main.rand.NextBool() ? new Color(100, 200, 255) : new Color(80, 180, 255);
        }

        public void Update() {
            Life++;
            Position += Velocity;
            Velocity *= 0.95f;
            Rotation += 0.1f;
        }

        public bool ShouldRemove() => Life >= MaxLife;

        public void Draw() {
            float progress = Life / MaxLife;
            float alpha = (1f - progress) * 0.8f;
            Texture2D tex = TextureAssets.Extra[ExtrasID.SharpTears].Value;
            Color c = color * alpha;
            Main.spriteBatch.Draw(tex, Position - Main.screenPosition, null, c, Rotation, 
                tex.Size() / 2f, Scale * (0.3f + progress * 0.2f), SpriteEffects.None, 0f);
        }
    }
    #endregion

    internal class SeaDomainProj : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;

        private List<DomainLayer> layers;
        private int layerCount;
        private List<BubbleChain> bubbles;
        private int particleTimer;
        private int bubbleTimer;
        private float maxDomainRadius; // 最外层半径

        private enum DomainState { Expanding, Active, Collapsing }
        private DomainState currentState = DomainState.Expanding;
        private int stateTimer = 0;
        private const int ExpandDuration = 50;
        private const int CollapseDuration = 40;
        private float domainAlpha = 0f;

        private readonly List<WaterRipple> ripples = new();
        private int rippleTimer;

        private readonly Dictionary<int, WaterPressureEffect> enemyEffects = new();
        private readonly HashSet<int> boundNPCs = new();
        private int effectUpdateTimer;

        public override void SetDefaults() {
            Projectile.width = 2400; // 扩大碰撞箱以支持10层
            Projectile.height = 2400;
            Projectile.timeLeft = 2;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.friendly = false;
            Projectile.hostile = false;
        }

        public override void AI() {
            if (!Owner.active) { Projectile.Kill(); return; }
            var hp = Owner.GetOverride<HalibutPlayer>();

            // 首帧初始化
            if (Projectile.localAI[0] == 0f) {
                layerCount = (int)Projectile.ai[0];
                if (layerCount <= 0) layerCount = 1;
                layerCount = Math.Clamp(layerCount, 1, 10); // 支持10层
                InitializeLayers();
                Projectile.localAI[0] = 1f;
            }

            if (hp == null || !hp.SeaDomainActive) {
                if (currentState != DomainState.Collapsing) {
                    StartCollapse();
                }
            }

            Projectile.timeLeft = 2;
            Projectile.Center = Owner.Center;

            switch (currentState) {
                case DomainState.Expanding:
                    UpdateExpanding();
                    break;
                case DomainState.Active:
                    UpdateActive();
                    break;
                case DomainState.Collapsing:
                    UpdateCollapsing();
                    break;
            }

            // 性能优化：分帧更新鱼群
            if (layers != null) {
                int updateBatch = Math.Max(1, layers.Count / 3); // 每帧更新1/3
                int startLayer = (int)(Projectile.localAI[1] % layers.Count);
                for (int i = 0; i < updateBatch && i < layers.Count; i++) {
                    int layerIndex = (startLayer + i) % layers.Count;
                    var layer = layers[layerIndex];
                    
                    foreach (var fish in layer.Fish) {
                        fish.Update(Owner.Center, layer.Radius, domainAlpha);
                    }
                    foreach (var ray in layer.Rays) {
                        ray.Update(Owner.Center);
                    }
                }
                Projectile.localAI[1]++;
            }

            bubbles ??= new List<BubbleChain>();
            for (int i = bubbles.Count - 1; i >= 0; i--) {
                bubbles[i].Update();
                if (bubbles[i].ShouldRemove()) bubbles.RemoveAt(i);
            }

            for (int i = ripples.Count - 1; i >= 0; i--) {
                ripples[i].Update();
                if (ripples[i].Life >= ripples[i].MaxLife) {
                    ripples.RemoveAt(i);
                }
            }

            if (currentState == DomainState.Active) {
                UpdateDomainEffects();
            }
        }

        private void InitializeLayers() {
            layers = new List<DomainLayer>();
            for (int i = 0; i < layerCount; i++) {
                layers.Add(new DomainLayer(i, layerCount));
            }
            maxDomainRadius = layers[^1].Radius;
        }

        private void UpdateDomainEffects() {
            effectUpdateTimer++;
            
            var toRemove = enemyEffects.Where(kvp => !Main.npc[kvp.Key].active).Select(kvp => kvp.Key).ToList();
            foreach (var key in toRemove) {
                enemyEffects.Remove(key);
                boundNPCs.Remove(key);
            }

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly) continue;

                float dist = Vector2.Distance(npc.Center, Owner.Center);
                bool inDomain = dist < maxDomainRadius;

                if (inDomain) {
                    if (!enemyEffects.ContainsKey(i)) {
                        enemyEffects[i] = new WaterPressureEffect(i);
                    }
                    
                    float npcRadius = (npc.width + npc.height) * 0.25f;
                    enemyEffects[i].Update(npc.Center, npcRadius);

                    if (SeaDomain.IsWeakEntity(npc)) {
                        if (!boundNPCs.Contains(i)) {
                            boundNPCs.Add(i);
                        }
                        
                        float dragStrength = 0.25f;
                        Vector2 desiredPos = Owner.Center + (npc.Center - Owner.Center).SafeNormalize(Vector2.Zero) * Math.Min(dist, maxDomainRadius * 0.8f);
                        Vector2 dragForce = (desiredPos - npc.Center) * dragStrength;
                        npc.velocity += dragForce;
                        Lighting.AddLight(npc.Center, TorchID.Blue);
                        
                        if (dist > maxDomainRadius * 0.9f) {
                            Vector2 pullBack = (Owner.Center - npc.Center).SafeNormalize(Vector2.Zero) * 5f;
                            npc.velocity += pullBack;
                        }
                    }
                }
                else {
                    if (enemyEffects.ContainsKey(i)) {
                        enemyEffects.Remove(i);
                    }
                    boundNPCs.Remove(i);
                }
            }

            ApplyProjectileHoming();
        }

        private void ApplyProjectileHoming() {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || !proj.friendly || proj.hostile) continue;
                if (proj.owner != Owner.whoAmI) continue;

                float distToCenter = Vector2.Distance(proj.Center, Owner.Center);
                if (distToCenter > maxDomainRadius) continue;

                NPC target = FindNearestEnemy(proj.Center, maxDomainRadius);
                if (target != null) {
                    float homingStrength = 0.25f;
                    Vector2 toTarget = (target.Center - proj.Center).SafeNormalize(Vector2.Zero);
                    proj.velocity = Vector2.Lerp(proj.velocity, toTarget * proj.velocity.Length(), homingStrength);
                }
            }
        }

        private NPC FindNearestEnemy(Vector2 position, float maxRange) {
            NPC closest = null;
            float closestDist = maxRange;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage) continue;

                float dist = Vector2.Distance(npc.Center, position);
                if (dist < closestDist && Vector2.Distance(npc.Center, Owner.Center) < maxDomainRadius) {
                    closest = npc;
                    closestDist = dist;
                }
            }
            return closest;
        }

        private void UpdateExpanding() {
            stateTimer++;
            float progress = stateTimer / (float)ExpandDuration;
            domainAlpha = MathHelper.Clamp(progress, 0f, 1f);

            if (layers != null) {
                foreach (var layer in layers) {
                    float minRadius = 80f;
                    layer.Radius = MathHelper.Lerp(minRadius, layer.TargetRadius, EaseOutCubic(progress));
                }
            }

            if (stateTimer % 6 == 0) {
                SpawnExpandParticle();
            }

            if (stateTimer % 12 == 0 && layers != null) {
                float randomRadius = layers[Main.rand.Next(layers.Count)].Radius * Main.rand.NextFloat(0.6f, 1f);
                ripples.Add(new WaterRipple(Owner.Center, randomRadius));
            }

            if (stateTimer >= ExpandDuration) {
                currentState = DomainState.Active;
                stateTimer = 0;
                domainAlpha = 1f;
                if (layers != null) {
                    foreach (var layer in layers) {
                        layer.Radius = layer.TargetRadius;
                        layer.InitializeFish(Owner.Center);
                        layer.InitializeRays(Owner.Center);
                    }
                }
            }
        }

        private void UpdateActive() {
            domainAlpha = 1f;

            particleTimer++;
            if (particleTimer % 8 == 0) {
                SpawnDomainParticle();
            }

            // 粒子生成频率优化：层数多时降低频率
            int fishGlowInterval = Math.Max(4, 8 - (layerCount / 3));
            if (particleTimer % fishGlowInterval == 0 && layers != null) {
                // 随机选择2-3层生成粒子
                int layerSamples = Math.Min(3, layers.Count);
                for (int s = 0; s < layerSamples; s++) {
                    var randomLayer = layers[Main.rand.Next(layers.Count)];
                    if (randomLayer.Fish.Count > 0) {
                        var randomFish = randomLayer.Fish[Main.rand.Next(randomLayer.Fish.Count)];
                        if (randomFish.Size == FishSize.Large || Main.rand.NextBool(4)) {
                            SpawnFishGlowParticle(randomFish.Position, randomFish.TintColor);
                        }
                    }
                }
            }

            bubbleTimer++;
            // 气泡频率：基于层数动态调整
            int bubbleInterval = Math.Max(5, 20 - layerCount);
            if (bubbleTimer % bubbleInterval == 0 && layers != null) {
                var randomLayer = layers[Main.rand.Next(layers.Count)];
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = Main.rand.NextFloat(randomLayer.Radius * 0.4f, randomLayer.Radius * 0.9f);
                Vector2 pos = Owner.Center + angle.ToRotationVector2() * dist;
                bubbles.Add(new BubbleChain(pos));
            }

            rippleTimer++;
            if (rippleTimer % 35 == 0 && layers != null) {
                // 多层时生成多圈水纹
                int rippleCount = Math.Min(3, (layerCount + 2) / 3);
                for (int i = 0; i < rippleCount; i++) {
                    var randomLayer = layers[Main.rand.Next(layers.Count)];
                    ripples.Add(new WaterRipple(Owner.Center, randomLayer.Radius * Main.rand.NextFloat(0.5f, 0.9f)));
                }
            }
        }

        private void UpdateCollapsing() {
            stateTimer++;
            float progress = stateTimer / (float)CollapseDuration;
            domainAlpha = 1f - MathHelper.Clamp(progress, 0f, 1f);

            if (layers != null) {
                foreach (var layer in layers) {
                    layer.Radius = MathHelper.Lerp(layer.TargetRadius, 80f, EaseInCubic(progress));
                }
            }

            if (stateTimer % 4 == 0) {
                SpawnCollapseParticle();
            }

            if (stateTimer >= CollapseDuration) {
                Projectile.Kill();
            }
        }

        private void StartCollapse() {
            currentState = DomainState.Collapsing;
            stateTimer = 0;
            enemyEffects.Clear();
            boundNPCs.Clear();
        }

        private void SpawnExpandParticle() {
            if (layers == null || layers.Count == 0) return;
            var randomLayer = layers[Main.rand.Next(layers.Count)];
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 pos = Owner.Center + angle.ToRotationVector2() * randomLayer.Radius * Main.rand.NextFloat(0.6f, 1f);
            int dust = Dust.NewDust(pos, 1, 1, DustID.Water, 0, 0, 100, new Color(100, 200, 255), 1.5f);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].velocity = (pos - Owner.Center).SafeNormalize(Vector2.Zero) * 2.5f;
        }

        private void SpawnDomainParticle() {
            if (layers == null || layers.Count == 0) return;
            var randomLayer = layers[Main.rand.Next(layers.Count)];
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float dist = Main.rand.NextFloat(randomLayer.Radius * 0.3f, randomLayer.Radius);
            Vector2 pos = Owner.Center + angle.ToRotationVector2() * dist;
            int dust = Dust.NewDust(pos, 1, 1, DustID.DungeonSpirit, 0, 0, 120, new Color(80, 180, 255), 1.1f);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].velocity = Main.rand.NextVector2Circular(1.2f, 1.2f);
        }

        private void SpawnFishGlowParticle(Vector2 pos, Color color) {
            int dust = Dust.NewDust(pos, 1, 1, DustID.GlowingMushroom, 0, 0, 100, color, 0.9f);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].velocity = Main.rand.NextVector2Circular(1.5f, 1.5f);
            Main.dust[dust].fadeIn = 1.2f;
        }

        private void SpawnCollapseParticle() {
            if (layers == null || layers.Count == 0) return;
            var randomLayer = layers[Main.rand.Next(layers.Count)];
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 pos = Owner.Center + angle.ToRotationVector2() * randomLayer.Radius;
            int dust = Dust.NewDust(pos, 1, 1, DustID.Water, 0, 0, 100, new Color(100, 200, 255), 1.3f);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].velocity = (Owner.Center - pos).SafeNormalize(Vector2.Zero) * 3.5f;
        }

        private static float EaseOutCubic(float t) => 1f - MathF.Pow(1f - t, 3f);
        private static float EaseInCubic(float t) => t * t * t;

        public override bool PreDraw(ref Color lightColor) {
            if (layers == null) return false;

            // 性能优化：层数过多时降低绘制精度
            bool highDetail = layerCount <= 5;
            int segmentDivisor = highDetail ? 1 : 2; // 高层数时减少边界分段

            // 从内到外绘制各层
            foreach (var layer in layers) {
                // 光束（只绘制部分层以优化性能）
                if (domainAlpha > 0.3f && (layer.LayerIndex % 2 == 0 || layerCount <= 5)) {
                    foreach (var ray in layer.Rays) {
                        ray.Draw(Owner.Center, domainAlpha * 0.7f);
                    }
                }

                // 边界
                if (domainAlpha > 0.01f) {
                    DrawLayerBorder(layer, segmentDivisor);
                }
            }

            // 水纹
            foreach (var ripple in ripples) {
                ripple.Draw(Owner.Center, domainAlpha);
            }

            // 气泡
            if (bubbles != null) {
                foreach (var bubble in bubbles) {
                    bubble.Draw(domainAlpha);
                }
            }

            // 水压效果
            foreach (var effect in enemyEffects.Values) {
                effect.Draw();
            }

            // 各层鱼拖尾（高层数时跳帧绘制）
            int fishDrawInterval = highDetail ? 1 : 2;
            foreach (var layer in layers) {
                for (int i = 0; i < layer.Fish.Count; i += fishDrawInterval) {
                    layer.Fish[i].DrawTrail(domainAlpha);
                }
            }

            // 各层鱼主体
            foreach (var layer in layers) {
                for (int i = 0; i < layer.Fish.Count; i += fishDrawInterval) {
                    DrawFish(layer.Fish[i], domainAlpha);
                }
            }

            DrawBoundIndicators();

            return false;
        }

        private void DrawLayerBorder(DomainLayer layer, int segmentDivisor) {
            Texture2D tex = TextureAssets.MagicPixel.Value;
            int segments = 120 / segmentDivisor; // 动态调整分段数
            float angleStep = MathHelper.TwoPi / segments;
            
            for (int i = 0; i < segments; i++) {
                float angle1 = i * angleStep;
                float angle2 = (i + 1) * angleStep;
                Vector2 p1 = Owner.Center + angle1.ToRotationVector2() * layer.Radius;
                Vector2 p2 = Owner.Center + angle2.ToRotationVector2() * layer.Radius;
                
                float wave = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f * layer.RotationSpeed + angle1 * 2f) * layer.WaveAmplitude;
                p1 += angle1.ToRotationVector2() * wave;
                p2 += angle2.ToRotationVector2() * wave;

                Vector2 diff = p2 - p1;
                float rotation = diff.ToRotation();
                float length = diff.Length();
                
                float brightness = 0.65f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + angle1 * 3f) * 0.3f;
                Color color = layer.BorderColor * brightness * domainAlpha * 0.9f;
                
                Main.spriteBatch.Draw(tex, p1 - Main.screenPosition, new Rectangle(0, 0, 1, 1), color, rotation, Vector2.Zero, new Vector2(length, 4f), SpriteEffects.None, 0f);
            }
        }

        private void DrawBoundIndicators() {
            if (boundNPCs.Count == 0) return;
            
            Texture2D chainTex = TextureAssets.Chain12.Value;
            foreach (int npcIndex in boundNPCs) {
                NPC npc = Main.npc[npcIndex];
                if (!npc.active) continue;

                Vector2 start = Owner.Center;
                Vector2 end = npc.Center;
                Vector2 diff = end - start;
                float length = diff.Length();
                float rotation = diff.ToRotation() - MathHelper.PiOver2;
                int segments = (int)(length / 16f);

                for (int i = 0; i < segments; i++) {
                    float progress = i / (float)segments;
                    Vector2 pos = Vector2.Lerp(start, end, progress);
                    float wave = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + progress * MathHelper.TwoPi) * 3f;
                    pos += diff.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2) * wave;
                    
                    Color c = new Color(100, 200, 255, 0) * 0.4f * domainAlpha;
                    Main.spriteBatch.Draw(chainTex, pos - Main.screenPosition, null, c, rotation, 
                        chainTex.Size() / 2f, 0.6f, SpriteEffects.None, 0f);
                }
            }
        }

        private void DrawFish(DomainFishBoid fish, float alpha) {
            int itemType = fish.FishType switch {
                0 => ItemID.Tuna,
                1 => ItemID.Bass,
                2 => ItemID.Trout,
                _ => ItemID.Tuna
            };
            
            Main.instance.LoadItem(itemType);
            Texture2D fishTex = TextureAssets.Item[itemType].Value;
            Rectangle rect = fishTex.Bounds;
            SpriteEffects spriteEffects = fish.Velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            float rot = fish.Velocity.ToRotation() + (fish.Velocity.X > 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4);
            Vector2 origin = rect.Size() * 0.5f;
            float fade = 0.75f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 5f + fish.Frame) * 0.2f;

            if (fish.Size == FishSize.Large) {
                for (int i = 0; i < 4; i++) {
                    Vector2 offset = (i * MathHelper.PiOver2).ToRotationVector2() * 2f;
                    Color glowColor = fish.TintColor * 0.4f * fade * alpha;
                    Main.spriteBatch.Draw(fishTex, fish.Position + offset - Main.screenPosition, rect, 
                        glowColor, rot, origin, fish.Scale * 0.72f, spriteEffects, 0f);
                }
            }
            
            Color c = fish.TintColor * fade * alpha;
            Main.spriteBatch.Draw(fishTex, fish.Position - Main.screenPosition, rect, c, rot, origin, fish.Scale * 0.7f, spriteEffects, 0f);
        }
    }

    #region 水纹效果
    internal class WaterRipple
    {
        public Vector2 Center;
        public float Radius;
        public int Life;
        public int MaxLife;
        private float initialRadius;

        public WaterRipple(Vector2 center, float radius) {
            Center = center;
            initialRadius = radius;
            Radius = radius;
            Life = 0;
            MaxLife = 80;
        }

        public void Update() {
            Life++;
            float progress = Life / (float)MaxLife;
            Radius = initialRadius + progress * 70f;
        }

        public void Draw(Vector2 domainCenter, float alpha) {
            float progress = Life / (float)MaxLife;
            float fadeAlpha = (1f - progress) * alpha * 0.45f;
            if (fadeAlpha < 0.01f) return;

            Texture2D tex = CWRAsset.LightShot.Value;
            int segments = 60;
            float angleStep = MathHelper.TwoPi / segments;

            for (int i = 0; i < segments; i++) {
                float angle1 = i * angleStep;
                float angle2 = (i + 1) * angleStep;
                Vector2 p1 = domainCenter + angle1.ToRotationVector2() * Radius;
                Vector2 p2 = domainCenter + angle2.ToRotationVector2() * Radius;

                Vector2 diff = p2 - p1;
                float rotation = diff.ToRotation();
                float length = diff.Length();

                Color color = new Color(120, 200, 255, 0) * fadeAlpha;
                Main.spriteBatch.Draw(tex, p1 - Main.screenPosition, null, color, rotation, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            }
        }
    }
    #endregion
}
