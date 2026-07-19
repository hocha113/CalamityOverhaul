using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Stones.Granites
{
    /// <summary>花岗行走靴：跑速/加速度饰品；达到全速的瞬间"通电"迸发电弧，维持期脚下走火并拖曳青蓝残影</summary>
    internal class GraniteWalkingBoots : ModItem
    {
        public override void SetDefaults() {
            Item.width = Item.height = 30;
            Item.accessory = true;
            Item.value = Item.sellPrice(0, 0, 60, 0);
            Item.rare = ItemRarityID.Green;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            GraniteWalkingBootsPlayer modPlayer = player.GetModPlayer<GraniteWalkingBootsPlayer>();
            modPlayer.Equipped = true;
            modPlayer.VisualsHidden = hideVisual;
            player.moveSpeed += 0.12f;
            player.accRunSpeed = Math.Max(player.accRunSpeed, 6.85f);
            player.runAcceleration *= 1.7f;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.Granite, 16)
                .AddIngredient(ItemID.Aglet)
                .AddRecipeGroup(CWRCrafted.TinBarGroup, 6)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    internal class GraniteWalkingBootsPlayer : ModPlayer
    {
        /// <summary>残影个数与采样间隔（帧），供残影层取样</summary>
        public const int GhostCount = 3;
        private const int GhostSpacing = 4;
        private const int TrailLength = 16;
        //入态迸发的防抖冷却：崎岖地形上接地判定抖动时避免电弧音连响
        private const int BurstCooldownTicks = 30;

        public bool Equipped;
        /// <summary>饰品栏隐藏可见性时为 true：数值保留，粒子/照明/音效/残影全部关闭</summary>
        public bool VisualsHidden;
        /// <summary>本帧是否处于全速通电状态（接地且 |vx| ≥ accRunSpeed×0.85），残影层据此开关</summary>
        public bool FullSpeedNow { get; private set; }

        private bool wasFullSpeed;
        private int burstCooldown;

        //残影位置环形缓存：装备期间每帧写入一格
        private readonly Vector2[] trailPositions = new Vector2[TrailLength];
        private int trailHead;
        private int trailFilled;

        public override void ResetEffects() {
            Equipped = false;
            VisualsHidden = false;
        }

        public override void PostUpdate() {
            if (!Equipped || Player.dead || VaultUtils.isServer) {
                FullSpeedNow = wasFullSpeed = false;
                trailFilled = 0;
                return;
            }

            trailPositions[trailHead] = Player.position;
            trailHead = (trailHead + 1) % TrailLength;
            if (trailFilled < TrailLength) {
                trailFilled++;
            }

            bool fullSpeed = GraniteMarbleVFX.IsGrounded(Player)
                && Math.Abs(Player.velocity.X) >= Player.accRunSpeed * 0.85f;
            FullSpeedNow = fullSpeed;

            if (burstCooldown > 0) {
                burstCooldown--;
            }

            if (fullSpeed && !VisualsHidden) {
                Vector2 feet = Player.gravDir >= 0f
                    ? Player.Bottom + new Vector2(0f, -4f)
                    : Player.Top + new Vector2(0f, 4f);
                if (!wasFullSpeed && burstCooldown <= 0) {
                    burstCooldown = BurstCooldownTicks;
                    EntryBurst(feet);
                }
                SustainSparks(feet);
            }

            wasFullSpeed = fullSpeed;
        }

        /// <summary>取第 <paramref name="index"/> 个残影（1 为最新）的历史世界坐标；缓存尚未填够时返回 false</summary>
        public bool TryGetGhostPosition(int index, out Vector2 position) {
            position = default;
            int back = index * GhostSpacing;
            if (back >= trailFilled) {
                return false;
            }
            int i = (trailHead - 1 - back) % TrailLength;
            if (i < 0) {
                i += TrailLength;
            }
            position = trailPositions[i];
            return true;
        }

        //入态一次性通电迸发：三道微电弧 + 向后火花扇 + 脚下闪光 + 一声短促电弧音
        private void EntryBurst(Vector2 feet) {
            int dir = Math.Sign(Player.velocity.X);
            SoundEngine.PlaySound(SoundID.DD2_LightningBugZap with {
                Volume = 0.45f,
                Pitch = 0.45f,
                PitchVariance = 0.12f,
                MaxInstances = 3
            }, feet);

            for (int i = 0; i < 3; i++) {
                Vector2 arcVel = new Vector2(dir * Main.rand.NextFloat(1.5f, 4f), Main.rand.NextFloat(-1.2f, 0.4f));
                PRTLoader.NewParticle<PRT_GraniteVolt>(feet + new Vector2(Main.rand.Next(-10, 11), Main.rand.Next(-6, 3))
                    , arcVel, GraniteMarbleVFX.GraniteSpark
                    , Main.rand.NextFloat(0.3f, 0.5f)).Configure(Main.rand.Next(3, 7));
            }

            for (int i = 0; i < 8; i++) {
                Vector2 sparkVel = new Vector2(-dir * Main.rand.NextFloat(1.5f, 6f), Main.rand.NextFloat(-3.2f, -0.6f));
                PRTLoader.NewParticle<PRT_Spark>(feet + new Vector2(Main.rand.Next(-6, 7), 2f), sparkVel
                    , Main.rand.NextBool() ? GraniteMarbleVFX.GraniteSpark : GraniteMarbleVFX.GraniteCore
                    , Main.rand.NextFloat(0.6f, 1.05f)).Configure(true, Main.rand.Next(14, 24));
            }

            PRTLoader.NewParticle<PRT_Light>(feet, new Vector2(dir * 1.5f, 0f)
                , GraniteMarbleVFX.GraniteCore, 0.5f).Configure(12, 1f, 1.4f);
        }

        //维持期：脚下青蓝照明 + 低频率火花/光斑 + 偶发微电弧，不循环发声
        private void SustainSparks(Vector2 feet) {
            Lighting.AddLight(feet, GraniteMarbleVFX.GraniteCore.ToVector3() * 0.7f);

            Vector2 back = new Vector2(-Math.Sign(Player.velocity.X), 0f);
            if (Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_Spark>(feet + new Vector2(Main.rand.Next(-6, 6), 2f)
                    , back * Main.rand.NextFloat(2f, 5f) + new Vector2(0f, Main.rand.NextFloat(-2f, -0.5f))
                    , GraniteMarbleVFX.GraniteSpark, Main.rand.NextFloat(0.5f, 0.9f)).Configure(false, Main.rand.Next(10, 18));
            }
            if (Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_Light>(feet + new Vector2(Main.rand.Next(-8, 8), 0f)
                    , back * Main.rand.NextFloat(1f, 3f), GraniteMarbleVFX.GraniteCore
                    , Main.rand.NextFloat(0.25f, 0.45f)).Configure(14, 1f, 1.2f);
            }
            if (Main.rand.NextBool(10)) {
                PRTLoader.NewParticle<PRT_GraniteVolt>(feet + new Vector2(Main.rand.Next(-8, 9), Main.rand.Next(-4, 3))
                    , back * Main.rand.NextFloat(0.5f, 2f), GraniteMarbleVFX.GraniteSpark
                    , Main.rand.NextFloat(0.2f, 0.35f)).Configure(Main.rand.Next(2, 5));
            }
        }
    }

    /// <summary>
    /// 全速残影层：把当帧已积累的玩家 DrawData 以青蓝加法色平移复制到身后的缓存位置，
    /// 压入绘制队首使残影垫在本体之下；只在全速通电且未隐藏可见性时绘制
    /// </summary>
    internal class GraniteWalkingBootsGhostLayer : PlayerDrawLayer
    {
        //挂在最后一个固定位置的原版层之后，此时 DrawDataCache 已包含完整的躯体/护甲/翅膀数据
        public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.EyebrellaCloud);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) {
            if (Main.gameMenu || drawInfo.shadow != 0f) {
                return false;
            }
            GraniteWalkingBootsPlayer modPlayer = drawInfo.drawPlayer.GetModPlayer<GraniteWalkingBootsPlayer>();
            return modPlayer.Equipped && !modPlayer.VisualsHidden && modPlayer.FullSpeedNow;
        }

        protected override void Draw(ref PlayerDrawSet drawInfo) {
            List<DrawData> cache = drawInfo.DrawDataCache;
            int baseCount = cache.Count;
            if (baseCount == 0) {
                return;
            }

            GraniteWalkingBootsPlayer modPlayer = drawInfo.drawPlayer.GetModPlayer<GraniteWalkingBootsPlayer>();
            List<DrawData> ghosts = null;
            //旧→新遍历，使最旧的残影落在队列最前（画在最底层）
            for (int g = GraniteWalkingBootsPlayer.GhostCount; g >= 1; g--) {
                if (!modPlayer.TryGetGhostPosition(g, out Vector2 ghostPos)) {
                    continue;
                }
                Vector2 delta = ghostPos - drawInfo.drawPlayer.position;
                float distSQ = delta.LengthSquared();
                if (distSQ < 100f || distSQ > 200f * 200f) {
                    continue; //几乎重叠会加法叠亮本体；过远说明刚传送，历史位置作废
                }

                float t = (g - 1) / (float)(GraniteWalkingBootsPlayer.GhostCount - 1);
                Color tint = Color.Lerp(GraniteMarbleVFX.GraniteSpark, GraniteMarbleVFX.GraniteDeep, t)
                    * MathHelper.Lerp(0.55f, 0.2f, t);
                tint.A = 0; //预乘贴图下 A=0 即为加法发光

                ghosts ??= new List<DrawData>(baseCount * GraniteWalkingBootsPlayer.GhostCount);
                for (int i = 0; i < baseCount; i++) {
                    DrawData data = cache[i];
                    data.position += delta;
                    data.color = tint;
                    data.shader = 0; //残影为纯色剪影，不重放染料
                    ghosts.Add(data);
                }
            }

            if (ghosts != null) {
                cache.InsertRange(0, ghosts);
                //heldProj 的插绘索引指向缓存中的一个边界，前插残影后必须同步右移，否则手持弹幕绘制深度错位
                if (drawInfo.projectileDrawPosition >= 0) {
                    drawInfo.projectileDrawPosition += ghosts.Count;
                }
            }
        }
    }
}
