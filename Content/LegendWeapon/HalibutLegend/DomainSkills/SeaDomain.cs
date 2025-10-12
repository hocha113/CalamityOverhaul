using CalamityOverhaul.Common;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.DomainSkills
{
    //十层鬼域，开，无限叠加，无限重启，封锁过去，截断未来，
    //你的层次太低，永远无法理解我现在的状态
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

            // 播放领域开启音效：瀑布+深海氛围+水流涌动
            if (Main.myPlayer == player.whoAmI) {
                // 主要水流音效（瀑布声）
                SoundEngine.PlaySound(SoundID.Waterfall with {
                    Volume = 0.8f,
                    Pitch = -0.3f,  // 降低音调，营造深海感
                    MaxInstances = 1
                }, player.Center);

                // 叠加雷鸣音效（营造威严感）
                SoundEngine.PlaySound(SoundID.Thunder with {
                    Volume = 0.4f,
                    Pitch = -0.5f,  // 低沉的雷鸣
                    MaxInstances = 1
                }, player.Center);

                // 深海回响音效
                SoundEngine.PlaySound(SoundID.Item29 with {  // 水晶音效
                    Volume = 0.6f,
                    Pitch = -0.8f,  // 极低音调
                    MaxInstances = 1
                }, player.Center);

                SpawnDomain(player);
            }
        }

        public static void Deactivate(Player player) {
            var hp = player.GetOverride<HalibutPlayer>();
            hp.SeaDomainActive = false;

            // 播放领域关闭音效：水流消退
            if (Main.myPlayer == player.whoAmI) {
                SoundEngine.PlaySound(SoundID.Splash with {
                    Volume = 0.6f,
                    Pitch = -0.4f,  // 低沉的水流消退声
                    MaxInstances = 1
                }, player.Center);

                // 气泡破裂音效
                SoundEngine.PlaySound(SoundID.Item54 with {  // 柔和的破裂声
                    Volume = 0.4f,
                    Pitch = 0.2f,
                    MaxInstances = 1
                }, player.Center);
            }
        }

        internal static void SpawnDomain(Player player) {
            var hp = player.GetOverride<HalibutPlayer>();
            int layers = Math.Clamp(hp.SeaDomainLayers, 1, 10);
            var source = player.GetSource_Misc("SeaDomainSkill");
            //通过 ai[0] 传递层数信息
            Projectile.NewProjectile(source, player.Center, Vector2.Zero
                , ModContent.ProjectileType<SeaDomainProj>(), 0, 0, player.whoAmI, layers);
        }
    }

    internal class SeaDomainProj : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;

        private List<DomainLayer> layers;
        private int layerCount;
        private List<BubbleChain> bubbles;
        private int particleTimer;
        private int bubbleTimer;
        private float maxDomainRadius; //最外层半径

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

        //领域中心平滑跟随
        private Vector2 domainCenter;
        private Vector2 targetCenter;
        private float smoothLerpSpeed = 0.1f; //缓动速度

        // 音效相关
        private SlotId ambientLoopSlot; // 环境循环音效槽
        private int ambientSoundTimer;
        private int bubbleSoundTimer;

        // 移动时的内容淡出（除边界线外）
        private float movementFadeFactor = 1f; // 0-1 越低越淡
        private const float MoveThreshold = 3f; // 玩家判定移动速度
        private const float TargetMoveFade = 0.18f; // 移动时目标透明度

        /// <summary>获取当前领域最大半径（供瞬移等技能使用）</summary>
        public float GetMaxRadius() {
            return maxDomainRadius;
        }

        public override void SetDefaults() {
            Projectile.width = 2400; //扩大碰撞箱以支持10层
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

            //首帧初始化
            if (Projectile.localAI[0] == 0f) {
                layerCount = (int)Projectile.ai[0];
                if (layerCount <= 0) layerCount = 1;
                layerCount = Math.Clamp(layerCount, 1, 10); //支持10层
                InitializeLayers();
                domainCenter = Owner.Center;
                targetCenter = Owner.Center;
                Projectile.localAI[0] = 1f;

                // 初始化环境音效循环
                if (Main.myPlayer == Owner.whoAmI) {
                    ambientLoopSlot = SoundEngine.PlaySound(SoundID.Waterfall with {
                        Volume = 0.3f,
                        Pitch = -0.6f,
                        IsLooped = true,
                        MaxInstances = 1
                    }, domainCenter);
                }
            }

            if (hp == null || !hp.SeaDomainActive) {
                if (currentState != DomainState.Collapsing) {
                    StartCollapse();
                }
            }

            Projectile.timeLeft = 2;
            Projectile.Center = domainCenter; //使用缓动后的中心

            //平滑跟随玩家
            targetCenter = Owner.Center;
            float distance = Vector2.Distance(domainCenter, targetCenter);

            //距离越远，缓动速度越快（避免玩家瞬移后领域跟不上）
            float dynamicSpeed = smoothLerpSpeed;
            if (distance > 500f) {
                dynamicSpeed = 0.2f; //远距离加速
            }
            else if (distance > 200f) {
                dynamicSpeed = 0.15f; //中距离
            }

            if (Owner.TryGetOverride<HalibutPlayer>(out var halibutPlayer) && halibutPlayer.FishTeleportCooldown > 0) {
                domainCenter = Vector2.Lerp(domainCenter, targetCenter, dynamicSpeed / 2f);
            }
            else {
                domainCenter = targetCenter;
            }

            // 更新循环音效位置
            if (SoundEngine.TryGetActiveSound(ambientLoopSlot, out var sound)) {
                sound.Position = domainCenter;
            }

            // 根据玩家移动速度调整非边界元素透明度
            float speed = Owner.velocity.Length();
            bool moving = speed > MoveThreshold;
            float targetFade = moving ? TargetMoveFade : 1f;
            // 加速淡出/淡入：移动时快 -> 静止时更快恢复
            float lerpSpeed = moving ? 0.25f : 0.35f;
            movementFadeFactor = MathHelper.Lerp(movementFadeFactor, targetFade, lerpSpeed);

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

            //全帧更新所有层的鱼群
            if (layers != null) {
                foreach (var layer in layers) {
                    foreach (var fish in layer.Fish) {
                        fish.Update(domainCenter, layer.Radius, domainAlpha); // 行为仍使用原始领域透明度
                    }
                }
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
                UpdateAmbientSounds();
            }
        }

        private void UpdateAmbientSounds() {
            if (Main.myPlayer != Owner.whoAmI) return;

            // 定期播放深海氛围音效
            ambientSoundTimer++;

            // 随机的鱼群游动音效
            if (ambientSoundTimer % 120 == 0 && Main.rand.NextBool(3)) {
                SoundEngine.PlaySound(SoundID.Splash with {
                    Volume = 0.15f,
                    Pitch = Main.rand.NextFloat(-0.3f, 0.3f),
                    MaxInstances = 3
                }, domainCenter + Main.rand.NextVector2Circular(maxDomainRadius * 0.7f, maxDomainRadius * 0.7f));
            }

            // 气泡上浮音效
            bubbleSoundTimer++;
            if (bubbleSoundTimer % 40 == 0 && Main.rand.NextBool(2)) {
                SoundEngine.PlaySound(SoundID.Drip with {
                    Volume = 0.1f,
                    Pitch = 0.6f,
                    MaxInstances = 4
                }, domainCenter);
            }
        }

        private void InitializeLayers() {
            layers = new List<DomainLayer>();
            for (int i = 0; i < layerCount; i++) {
                layers.Add(new DomainLayer(i, layerCount));
            }
            maxDomainRadius = layers[^1].Radius;
        }

        /// <summary>判断是否为弱小可束缚生物</summary>
        public static bool IsWeakEntity(NPC npc) {
            if (npc.boss || npc.defense > 20 || npc.lifeMax > 500) {
                return false;
            }

            if (npc.knockBackResist <= 0.3f) {
                return false;
            }

            if (NPCID.Sets.ProjectileNPC[npc.type]) {
                return false;
            }

            return npc.friendly == false && npc.damage > 0;
        }

        private void UpdateDomainEffects() {
            var toRemove = enemyEffects.Where(kvp => !Main.npc[kvp.Key].active).Select(kvp => kvp.Key).ToList();
            foreach (var key in toRemove) {
                enemyEffects.Remove(key);
                boundNPCs.Remove(key);
            }

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly) continue;

                float dist = Vector2.Distance(npc.Center, domainCenter); //使用领域中心
                bool inDomain = dist < maxDomainRadius;

                if (inDomain) {
                    if (!enemyEffects.TryGetValue(i, out WaterPressureEffect value)) {
                        value = new WaterPressureEffect(i);
                        enemyEffects[i] = value;
                    }

                    float npcRadius = (npc.width + npc.height) * 0.25f;
                    value.Update(npc.Center, npcRadius);

                    if (IsWeakEntity(npc)) {
                        if (!boundNPCs.Contains(i)) {
                            //播放束缚音效
                            if (Main.myPlayer == Owner.whoAmI) {
                                SoundEngine.PlaySound(SoundID.Item20 with {  //钩爪音效
                                    Volume = 0.3f,
                                    Pitch = -0.3f,
                                    MaxInstances = 5
                                }, npc.Center);

                                // 叠加水压音效
                                SoundEngine.PlaySound(SoundID.Splash with {
                                    Volume = 0.25f,
                                    Pitch = -0.6f,
                                    MaxInstances = 5
                                }, npc.Center);
                            }

                            boundNPCs.Add(i);
                        }

                        float dragStrength = 0.25f;
                        Vector2 desiredPos = domainCenter + (npc.Center - domainCenter).SafeNormalize(Vector2.Zero) * Math.Min(dist, maxDomainRadius * 0.8f);
                        Vector2 dragForce = (desiredPos - npc.Center) * dragStrength;
                        npc.velocity += dragForce;
                        Lighting.AddLight(npc.Center, TorchID.Blue);

                        if (dist > maxDomainRadius * 0.9f) {
                            Vector2 pullBack = (domainCenter - npc.Center).SafeNormalize(Vector2.Zero) * 5f;
                            npc.velocity += pullBack;
                        }
                    }
                }
                else {
                    enemyEffects.Remove(i);
                    boundNPCs.Remove(i);
                }
            }
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
                ripples.Add(new WaterRipple(domainCenter, randomRadius));

                // 扩张过程中的水波音效
                if (Main.myPlayer == Owner.whoAmI && stateTimer % 24 == 0) {
                    SoundEngine.PlaySound(SoundID.Item85 with {  // 魔法音效
                        Volume = 0.2f,
                        Pitch = -0.4f + (progress * 0.3f),  // 音调随扩张提升
                        MaxInstances = 3
                    }, domainCenter);
                }
            }

            if (stateTimer >= ExpandDuration) {
                currentState = DomainState.Active;
                stateTimer = 0;
                domainAlpha = 1f;
                if (layers != null) {
                    foreach (var layer in layers) {
                        layer.Radius = layer.TargetRadius;
                        layer.InitializeFish(domainCenter);
                    }
                }

                // 领域完全展开音效
                if (Main.myPlayer == Owner.whoAmI) {
                    SoundEngine.PlaySound(SoundID.Item29 with {
                        Volume = 0.5f,
                        Pitch = -0.2f,
                        MaxInstances = 1
                    }, domainCenter);
                }
            }
        }

        private void UpdateActive() {
            domainAlpha = 1f;

            particleTimer++;
            if (particleTimer % 8 == 0) {
                SpawnDomainParticle();
            }

            //鱼发光粒子：保持固定频率
            if (particleTimer % 4 == 0 && layers != null) {
                //随机选择2-3层生成粒子
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
            //气泡频率：根据层数适当调整但不过度降低
            int bubbleInterval = Math.Max(10, 25 - layerCount * 2);
            if (bubbleTimer % bubbleInterval == 0 && layers != null) {
                var randomLayer = layers[Main.rand.Next(layers.Count)];
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = Main.rand.NextFloat(randomLayer.Radius * 0.4f, randomLayer.Radius * 0.9f);
                Vector2 pos = domainCenter + angle.ToRotationVector2() * dist;
                bubbles.Add(new BubbleChain(pos));
            }

            rippleTimer++;
            if (rippleTimer % 35 == 0 && layers != null) {
                //多层时生成多圈水纹
                int rippleCount = Math.Min(3, (layerCount + 2) / 3);
                for (int i = 0; i < rippleCount; i++) {
                    var randomLayer = layers[Main.rand.Next(layers.Count)];
                    ripples.Add(new WaterRipple(domainCenter, randomLayer.Radius * Main.rand.NextFloat(0.5f, 0.9f)));
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

            // 停止循环音效
            if (SoundEngine.TryGetActiveSound(ambientLoopSlot, out var sound)) {
                sound.Volume = (1f - progress) * 0.3f;  // 淡出
            }

            if (stateTimer >= CollapseDuration) {
                if (SoundEngine.TryGetActiveSound(ambientLoopSlot, out var sound2)) {
                    sound2.Stop();
                }
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
            Vector2 pos = domainCenter + angle.ToRotationVector2() * randomLayer.Radius * Main.rand.NextFloat(0.6f, 1f);
            int dust = Dust.NewDust(pos, 1, 1, DustID.Water, 0, 0, 100, new Color(100, 200, 255), 1.5f);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].velocity = (pos - domainCenter).SafeNormalize(Vector2.Zero) * 2.5f;
        }

        private void SpawnDomainParticle() {
            if (layers == null || layers.Count == 0) return;
            var randomLayer = layers[Main.rand.Next(layers.Count)];
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float dist = Main.rand.NextFloat(randomLayer.Radius * 0.3f, randomLayer.Radius);
            Vector2 pos = domainCenter + angle.ToRotationVector2() * dist;
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
            Vector2 pos = domainCenter + angle.ToRotationVector2() * randomLayer.Radius;
            int dust = Dust.NewDust(pos, 1, 1, DustID.Water, 0, 0, 100, new Color(100, 200, 255), 1.3f);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].velocity = (domainCenter - pos).SafeNormalize(Vector2.Zero) * 3.5f;
        }

        private static float EaseOutCubic(float t) => 1f - MathF.Pow(1f - t, 3f);
        private static float EaseInCubic(float t) => t * t * t;

        public override bool PreDraw(ref Color lightColor) {
            if (layers == null) {
                return false;
            }

            // 计算移动后的内容透明度（边界始终使用 domainAlpha）
            float contentAlpha = domainAlpha * movementFadeFactor;

            //从内到外绘制各层边界
            foreach (var layer in layers) {
                if (domainAlpha > 0.01f) {
                    DrawLayerBorder(layer);
                }
            }

            if (CWRServerConfig.Instance.HalibutDomainConciseDisplay) {
                return false;
            }

            //水纹
            foreach (var ripple in ripples) {
                ripple.Draw(domainCenter, contentAlpha);
            }

            //气泡
            if (bubbles != null) {
                foreach (var bubble in bubbles) {
                    bubble.Draw(contentAlpha);
                }
            }

            //水压效果
            foreach (var effect in enemyEffects.Values) {
                effect.Draw(contentAlpha);
            }

            //绘制所有鱼拖尾
            foreach (var layer in layers) {
                foreach (var fish in layer.Fish) {
                    fish.DrawTrail(contentAlpha);
                }
            }

            //绘制所有鱼主体
            foreach (var layer in layers) {
                foreach (var fish in layer.Fish) {
                    DrawFish(fish, contentAlpha);
                }
            }

            DrawBoundIndicators(contentAlpha);

            return false;
        }

        private void DrawLayerBorder(DomainLayer layer) {
            Texture2D tex = TextureAssets.MagicPixel.Value;
            int segments = 120; //固定高精度分段
            float angleStep = MathHelper.TwoPi / segments;

            for (int i = 0; i < segments; i++) {
                float angle1 = i * angleStep;
                float angle2 = (i + 1) * angleStep;
                Vector2 p1 = domainCenter + angle1.ToRotationVector2() * layer.Radius;
                Vector2 p2 = domainCenter + angle2.ToRotationVector2() * layer.Radius;

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

        private void DrawBoundIndicators(float contentAlpha) {
            if (boundNPCs.Count == 0) return;

            Texture2D chainTex = TextureAssets.Chain12.Value;
            foreach (int npcIndex in boundNPCs) {
                NPC npc = Main.npc[npcIndex];
                if (!npc.active) continue;

                Vector2 start = domainCenter;
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

                    Color c = new Color(100, 200, 255, 0) * 0.4f * contentAlpha; // 使用淡出因子
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

    #region 领域层数据
    internal class DomainLayer
    {
        public float Radius;
        public float TargetRadius;
        public List<DomainFishBoid> Fish;
        public int FishCount;
        public Color BorderColor;
        public float RotationSpeed;
        public float WaveAmplitude;
        public int LayerIndex;

        public DomainLayer(int layerIndex, int totalLayers) {
            LayerIndex = layerIndex;

            //优化半径计算：前3层密集，后续层逐渐扩大
            float baseRadius = 220f;
            float radiusStep;
            if (layerIndex < 3) {
                //前3层：紧密布局
                radiusStep = 130f;
                TargetRadius = baseRadius + (layerIndex * radiusStep);
            }
            else {
                //3层之后：渐进扩大
                float base3LayerRadius = baseRadius + (2 * 130f); //第3层的半径
                radiusStep = 100f + ((layerIndex - 2) * 15f); //逐渐增大间距
                TargetRadius = base3LayerRadius + radiusStep * (layerIndex - 2);
            }
            Radius = TargetRadius;

            //鱼数量：前5层线性增长，后续层增长放缓
            if (layerIndex < 5) {
                FishCount = 20 + (layerIndex * 10);
            }
            else {
                FishCount = 60 + ((layerIndex - 4) * 6);
            }

            //颜色渐变：多层时使用更丰富的色谱
            float colorProgress = layerIndex / (float)Math.Max(1, totalLayers - 1);
            if (totalLayers <= 3) {
                //1-3层：深蓝到浅蓝
                BorderColor = Color.Lerp(new Color(60, 160, 255), new Color(120, 220, 255), colorProgress);
            }
            else if (totalLayers <= 7) {
                //4-7层：深蓝到青绿
                BorderColor = Color.Lerp(new Color(50, 140, 255), new Color(100, 230, 240), colorProgress);
            }
            else {
                //8-10层：深蓝到亮青
                BorderColor = Color.Lerp(new Color(40, 120, 255), new Color(120, 255, 255), colorProgress);
            }

            //旋转速度：外层更慢
            RotationSpeed = MathHelper.Lerp(1f, 0.3f, colorProgress);

            //波动幅度：外层更大
            WaveAmplitude = MathHelper.Lerp(6f, 14f, colorProgress);

            Fish = new List<DomainFishBoid>();
        }

        public void InitializeFish(Vector2 center) {
            Fish.Clear();
            for (int i = 0; i < FishCount; i++) {
                float angle = (i / (float)FishCount) * MathHelper.TwoPi;
                Fish.Add(new DomainFishBoid(center, Radius, angle));
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
            Texture2D tex = TextureAssets.Extra[ExtrasID.SharpTears].Value; //气泡贴图
            Color c = new Color(150, 220, 255, 0) * alpha;
            Main.spriteBatch.Draw(tex, Position - Main.screenPosition, null, c, 0f, tex.Size() / 2f, Scale, SpriteEffects.None, 0f);
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

        public void Draw(float alpha) {
            foreach (var p in Particles) {
                p.Draw(alpha);
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

        public void Draw(float globalAlpha) {
            float progress = Life / MaxLife;
            float alpha = (1f - progress) * 0.8f * globalAlpha;
            if (alpha <= 0.01f) return;
            Texture2D tex = TextureAssets.Extra[ExtrasID.SharpTears].Value;
            Color c = color * alpha;
            Main.spriteBatch.Draw(tex, Position - Main.screenPosition, null, c, Rotation,
                tex.Size() / 2f, Scale * (0.3f + progress * 0.2f), SpriteEffects.None, 0f);
        }
    }
    #endregion

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
