using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories
{
    /// <summary>忍法克苏鲁闪耀靴:克苏鲁闪耀靴加上忍者大师装备,巨眼系上了头巾</summary>
    internal class NinjaCthulsparkBoots : ModItem
    {
        public override string Texture => CWRConstant.Item_Accessorie + "NinjaCthulsparkBoots";

        //忍者装备放在靴与盾之前:闪避与爬墙照常生效,冲刺归属在下方统一定型
        internal static readonly int[] FuseSources = [
            ItemID.AmphibianBoots, ItemID.MasterNinjaGear, ItemID.TerrasparkBoots,
            ItemID.HorseshoeBundle, ItemID.EoCShield];

        public override void SetDefaults() {
            Item.width = 28;
            Item.height = 38;
            Item.accessory = true;
            Item.value = Item.sellPrice(0, 10, 0, 0);
            Item.rare = ItemRarityID.Yellow;
            //穿戴外观与克苏鲁闪耀靴一致,忍者装备无可见部件
            Item.shoeSlot = new Item(ItemID.TerrasparkBoots).shoeSlot;
            Item.balloonSlot = new Item(ItemID.HorseshoeBundle).balloonSlot;
            Item.shieldSlot = new Item(ItemID.EoCShield).shieldSlot;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            FrogsparkBoots.ApplyFuse(player, hideVisual, FuseSources);
            //足袋与盾的原版冲刺全部让位(含灾厄冲刺接管),双击冲刺由赤影冲刺统一承担
            player.dashType = 0;
            player.SetPlayerDashID(string.Empty);
            NinjaCthulsparkPlayer modPlayer = player.GetModPlayer<NinjaCthulsparkPlayer>();
            modPlayer.Equipped = true;
            modPlayer.VisualsHidden = hideVisual;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<CthulsparkBoots>()
                .AddIngredient(ItemID.MasterNinjaGear)
                .AddTile(TileID.TinkerersWorkbench)
                .Register();
        }
    }

    /// <summary>
    /// 赤影冲刺:克苏鲁之盾撞击冲刺的忍法加长版。<br/>
    /// 冲刺与盾撞结算仅在本机(owner)推进,位移经原版玩家同步跨端;
    /// 残影采样与冲刺演出由各客户端按水平速度自导,多人无需网络包
    /// </summary>
    internal class NinjaCthulsparkPlayer : ModPlayer
    {
        #region 数值
        /// <summary>满速阶段速度(px/帧),原版盾冲起步为 14.5</summary>
        public const float DashSpeed = 18.5f;
        /// <summary>满速保持帧数</summary>
        public const int DashHoldFrames = 16;
        /// <summary>急刹帧数</summary>
        public const int DashBrakeFrames = 6;
        public const int DashTotalFrames = DashHoldFrames + DashBrakeFrames;
        /// <summary>冷却(帧),收招起算,对齐原版盾冲</summary>
        public const int DashCooldownFrames = 30;
        /// <summary>盾撞基础伤害,吃近战总伤加成,与原版克苏鲁之盾同值</summary>
        public const int BashBaseDamage = 30;
        /// <summary>盾撞击退</summary>
        public const float BashKnockback = 9f;
        /// <summary>各端视觉判定的水平速度阈值,略高于原版盾冲起步速度</summary>
        public const float DashVisualSpeedGate = 14f;
        /// <summary>残影个数与采样间隔(帧)</summary>
        public const int GhostCount = 5;
        private const int GhostSpacing = 3;
        private const int TrailLength = 24;
        #endregion

        #region 状态
        /// <summary>本帧是否装备,物品钩子逐帧点亮</summary>
        public bool Equipped;
        /// <summary>隐藏可见性时关残影与冲刺粒子,盾撞反馈保留</summary>
        public bool VisualsHidden;
        /// <summary>残影热度 0~1,由水平速度推导,冲刺结束后自然衰减</summary>
        public float DashHeat;

        //冲刺状态机仅 owner 有效
        private int dashTimer;
        private int dashCooldown;
        private int dashDir;
        /// <summary>本段冲刺已撞过的敌人,一段冲刺每个敌人只撞一次</summary>
        private readonly HashSet<int> bashedNpcs = [];

        //残影采样各客户端本地自采
        private readonly Vector2[] trailPositions = new Vector2[TrailLength];
        private int trailHead;
        private int trailFilled;
        private bool wasDashVisual;
        #endregion

        public override void ResetEffects() {
            if (dashCooldown > 0) {
                dashCooldown--;
            }
            Equipped = false;
            VisualsHidden = false;
        }

        //死亡期间 ResetEffects 不跑,冷却照常流逝;死亡同时打断冲刺
        public override void UpdateDead() {
            if (dashCooldown > 0) {
                dashCooldown--;
            }
            dashTimer = 0;
            bashedNpcs.Clear();
            DashHeat = 0f;
            trailFilled = 0;
        }

        #region 冲刺
        //双击检测必须放在 PostUpdateEquips:原版在 Update 中段就把 releaseLeft/Right 改写为"按住即 false",
        //到 PreUpdateMovement 时按键沿已不可见;SolarCoreFist 的下双击在此钩子可用是既证
        public override void PostUpdateEquips() {
            if (Player.whoAmI != Main.myPlayer || !Equipped || Player.dead) {
                return;
            }
            TryStartDash();
        }

        public override void PreUpdateMovement() {
            if (Player.whoAmI != Main.myPlayer || !Equipped || Player.dead) {
                dashTimer = 0;
                return;
            }

            if (dashTimer <= 0) {
                return;
            }

            //上马/抓钩/被控/微光中断冲刺
            if (Player.mount.Active || Player.grapCount > 0 || Player.pulley
                || Player.CCed || Player.shimmering) {
                EndDash();
                return;
            }

            int elapsed = DashTotalFrames - dashTimer;
            if (elapsed > 0 && elapsed < DashHoldFrames
                && Math.Abs(Player.velocity.X) < DashSpeed * 0.35f) {
                //撞墙:上一帧速度被物块碰撞吃掉,提前收招
                EndDash();
                return;
            }

            if (elapsed < DashHoldFrames) {
                //满速保持,微衰减避免匀速僵直;竖直方向交还重力
                Player.velocity.X = dashDir * DashSpeed * (1f - elapsed * 0.008f);
            }
            else {
                Player.velocity.X *= 0.78f;
            }

            //盾撞判定全程有效
            BashContact();

            Player.fallStart = (int)(Player.position.Y / 16f);

            dashTimer--;
            if (dashTimer == 0) {
                EndDash();
            }
        }

        private void EndDash() {
            dashTimer = 0;
            dashCooldown = DashCooldownFrames;
            bashedNpcs.Clear();
        }

        /// <summary>双击窗口:首按当帧被置 15,二按时必然小于 15 且大于 0</summary>
        private bool TapWindow(int index)
            => Player.doubleTapCardinalTimer[index] > 0 && Player.doubleTapCardinalTimer[index] < 15;

        /// <summary>本帧能否接住左右双击并起步。仪轨集环纱幕步据此让位，避免空消费闩把冲刺绑死</summary>
        internal bool CanAcceptDash() {
            return Equipped && !Player.dead && Player.whoAmI == Main.myPlayer
                && dashTimer <= 0 && dashCooldown <= 0
                && !Player.mount.Active && Player.grapCount <= 0 && !Player.pulley
                && !Player.CCed && !Player.shimmering && !Player.setSolar;
        }

        private void TryStartDash() {
            if (!CanAcceptDash()) {
                return;
            }

            int dir = 0;
            int tapIndex = -1;
            if (Player.controlRight && Player.releaseRight && TapWindow(2)) {
                dir = 1;
                tapIndex = 2;
            }
            else if (Player.controlLeft && Player.releaseLeft && TapWindow(3)) {
                dir = -1;
                tapIndex = 3;
            }
            if (dir == 0) {
                return;
            }
            //同帧同方向双击位移技消费闩:被别家抢走则本帧静默放弃
            if (!Player.CWR().TryConsumeRelicDoubleTap(tapIndex)) {
                return;
            }

            dashTimer = DashTotalFrames;
            dashDir = dir;
            bashedNpcs.Clear();
            Player.ChangeDir(dir);
            //压制别家冲刺状态机在同窗口起步
            Player.dashDelay = Math.Max(Player.dashDelay, DashTotalFrames + 4);
        }

        /// <summary>盾撞:原版克苏鲁之盾撞击的复刻,撞开敌人并给自己盾反瞬间的短暂无敌</summary>
        private void BashContact() {
            Rectangle bashRect = new((int)(Player.position.X + Player.velocity.X * 0.5f - 4f),
                (int)(Player.position.Y + Player.velocity.Y * 0.5f - 4f),
                Player.width + 8, Player.height + 8);

            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.dontTakeDamage || npc.friendly || bashedNpcs.Contains(npc.whoAmI)
                    || !bashRect.Intersects(npc.getRect())
                    || !Player.CanNPCBeHitByPlayerOrPlayerProjectile(npc)) {
                    continue;
                }

                bashedNpcs.Add(npc.whoAmI);
                int damage = (int)Player.GetTotalDamage(DamageClass.Melee).ApplyTo(BashBaseDamage);
                Player.ApplyDamageToNPC(npc, damage, BashKnockback, dashDir, false);
                Player.GivePlayerImmuneState(Player.longInvince ? 12 : 8);

                SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.75f, Pitch = -0.15f }, npc.Center);
                EocMotion.Shake(npc.Center, 4f, 9, new Vector2(dashDir, 0f));
                if (!VisualsHidden) {
                    EocMotion.BloodBurst(npc.Center, 0.62f, false);
                }
            }
        }
        #endregion

        #region 视觉
        public override void PostUpdate() {
            if (VaultUtils.isServer) {
                return;
            }
            if (!Equipped || Player.dead) {
                DashHeat = 0f;
                wasDashVisual = false;
                trailFilled = 0;
                return;
            }

            //残影采样持续进行,冲刺爆发时才被读取
            trailPositions[trailHead] = Player.position;
            trailHead = (trailHead + 1) % TrailLength;
            if (trailFilled < TrailLength) {
                trailFilled++;
            }

            //视觉状态由水平速度自导,各端一致,无需网络包
            bool dashVisual = !Player.mount.Active
                && Math.Abs(Player.velocity.X) >= DashVisualSpeedGate;
            DashHeat = dashVisual ? 1f : DashHeat * 0.86f;
            if (DashHeat < 0.03f) {
                DashHeat = 0f;
            }

            if (dashVisual && !VisualsHidden) {
                //原版盾冲的护盾残像顺路点亮
                Player.armorEffectDrawShadowEOCShield = true;
                if (!wasDashVisual) {
                    StartBurst();
                }
                SustainTrailFX();
            }
            wasDashVisual = dashVisual;
        }

        /// <summary>第 index 个残影(1=最新);未填够返回 false</summary>
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

        /// <summary>起步演出:破风声 + 横拉血环 + 红雾破膛 + 向后火星扇面</summary>
        private void StartBurst() {
            int dir = Math.Sign(Player.velocity.X);
            Vector2 center = Player.Center;
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 0.9f, Pitch = 0.35f }, center);

            EocMotion.MistPuff(center, 3, 1.05f, 0.42f);
            PRT_DWave wave = PRTLoader.NewParticle<PRT_DWave>(center, Vector2.Zero, EocMotion.Arterial, 0.16f);
            wave?.Configure(new Vector2(1f, 0.6f), 0f, 0.85f, 14);

            for (int i = 0; i < 14; i++) {
                Vector2 sparkVel = new(-dir * Main.rand.NextFloat(2.5f, 8f), Main.rand.NextFloat(-2.4f, 2.4f));
                PRTLoader.NewParticle<PRT_Spark>(center + Main.rand.NextVector2Circular(10f, 14f), sparkVel,
                    Main.rand.NextBool() ? EocMotion.BrightBlood : EocMotion.IrisRed,
                    Main.rand.NextFloat(0.7f, 1.2f))?.Configure(false, Main.rand.Next(14, 24));
            }
        }

        /// <summary>冲刺途中的持续排焰:反向火星 + 稀疏血雾 + 红光</summary>
        private void SustainTrailFX() {
            int dir = Math.Sign(Player.velocity.X);
            Vector2 back = new(-dir, 0f);
            Lighting.AddLight(Player.Center, EocMotion.BrightBlood.ToVector3() * 0.6f);

            for (int i = 0; i < 2; i++) {
                Vector2 sparkVel = back * Main.rand.NextFloat(1.5f, 5f)
                    + new Vector2(0f, Main.rand.NextFloat(-1.6f, 1.6f));
                PRTLoader.NewParticle<PRT_Spark>(Player.Center + Main.rand.NextVector2Circular(8f, 16f), sparkVel,
                    Main.rand.NextBool() ? EocMotion.BrightBlood : EocMotion.Arterial,
                    Main.rand.NextFloat(0.5f, 0.95f))?.Configure(false, Main.rand.Next(10, 18));
            }
            if (Main.rand.NextBool(4)) {
                EocMotion.MistPuff(Player.Center + back * 8f, 1, 0.8f, 0.3f);
            }
        }
        #endregion
    }

    /// <summary>赤影残影层,加法猩红剪影垫在本体之下,随冲刺热度淡出</summary>
    internal class NinjaCthulsparkGhostLayer : PlayerDrawLayer
    {
        //EyebrellaCloud 之后,DrawDataCache 已齐
        public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.EyebrellaCloud);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) {
            if (Main.gameMenu || drawInfo.shadow != 0f) {
                return false;
            }
            NinjaCthulsparkPlayer modPlayer = drawInfo.drawPlayer.GetModPlayer<NinjaCthulsparkPlayer>();
            return modPlayer.Equipped && !modPlayer.VisualsHidden && modPlayer.DashHeat > 0.08f;
        }

        protected override void Draw(ref PlayerDrawSet drawInfo) {
            List<DrawData> cache = drawInfo.DrawDataCache;
            int baseCount = cache.Count;
            if (baseCount == 0) {
                return;
            }

            NinjaCthulsparkPlayer modPlayer = drawInfo.drawPlayer.GetModPlayer<NinjaCthulsparkPlayer>();
            List<DrawData> ghosts = null;
            for (int g = NinjaCthulsparkPlayer.GhostCount; g >= 1; g--) {
                if (!modPlayer.TryGetGhostPosition(g, out Vector2 ghostPos)) {
                    continue;
                }
                Vector2 delta = ghostPos - drawInfo.drawPlayer.position;
                float distSQ = delta.LengthSquared();
                if (distSQ < 64f || distSQ > 420f * 420f) {
                    continue; //过近叠亮本体,过远当传送作废
                }

                float t = (g - 1f) / (NinjaCthulsparkPlayer.GhostCount - 1f);
                Color tint = Color.Lerp(EocMotion.BrightBlood, EocMotion.VenousDark, t)
                    * (MathHelper.Lerp(0.62f, 0.16f, t) * modPlayer.DashHeat);
                tint.A = 0; //预乘下 A=0 加法发光

                ghosts ??= new List<DrawData>(baseCount * NinjaCthulsparkPlayer.GhostCount);
                for (int i = 0; i < baseCount; i++) {
                    DrawData data = cache[i];
                    data.position += delta;
                    data.color = tint;
                    data.shader = 0; //纯色剪影,不重放染料
                    ghosts.Add(data);
                }
            }

            if (ghosts != null) {
                cache.InsertRange(0, ghosts);
                //前插后同步 heldProj 插绘索引
                if (drawInfo.projectileDrawPosition >= 0) {
                    drawInfo.projectileDrawPosition += ghosts.Count;
                }
            }
        }
    }
}
