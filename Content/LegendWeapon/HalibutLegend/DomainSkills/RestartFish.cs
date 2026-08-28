using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Tools;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.DomainSkills.Restarts;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Resurrections;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.DomainSkills
{
    internal static class RestartFish
    {
        public static int ID = 5;
        private const int ToggleCD = 20;
        internal const int RestartCooldown = 60 * 60 * 3; //3分钟冷却

        public static void AltUse(Player player) {
            if (!player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                return;
            }
            if (halibutPlayer.RestartFishToggleCD > 0 || halibutPlayer.RestartFishCooldown > 0) return;

            //七眼起兑现文本承诺的大范围重启：潮汐吞没+时间倒带，
            //冷却与交互锁定由演出确立/结算时挂上（请求可能被权威端拒绝，不预扣三分钟）
            if (halibutPlayer.SeaDomainLayers >= HalibutReset.UnlockLayers) {
                HalibutReset.TryReset(player);
                halibutPlayer.RestartFishToggleCD = ToggleCD;
                return;
            }

            Activate(player);
            halibutPlayer.IsInteractionLockedTime = (int)(60 * ((10 - MathHelper.Clamp(halibutPlayer.CrashesLevel() - 5, 0, 10)) * 3));
            halibutPlayer.RestartFishToggleCD = ToggleCD;
            halibutPlayer.RestartFishCooldown = RestartCooldown;
        }

        public static void Activate(Player player) {
            if (Main.myPlayer == player.whoAmI) {
                SpawnRestartEffect(player);
            }
        }

        internal static void SpawnRestartEffect(Player player) {
            var source = player.GetSource_Misc("RestartFishSkill");
            Projectile.NewProjectile(source, player.Center, Vector2.Zero
                , ModContent.ProjectileType<RestartEffectProj>(), 0, 0, player.whoAmI);
        }

        /// <summary>清除全部 buff（增益减益一并重启）；DelBuff 会压缩数组，倒序遍历不漏删</summary>
        internal static void ClearAllBuffs(Player player) {
            for (int i = Player.MaxBuffs - 1; i >= 0; i--) {
                if (player.buffType[i] > 0) {
                    player.DelBuff(i);
                }
            }
        }

        internal static void ExecuteRestart(Player player) {
            //调整最大生命值，避免类似削弱生命上限的情况影响重启效果
            if (player.TryGetHalibutPlayer(out var halibutPlayer)) {
                player.statLifeMax2 = (int)MathHelper.Clamp(player.statLifeMax2, halibutPlayer.PlayerLifeMax, int.MaxValue - 1);
            }

            //满血
            player.Heal(player.statLifeMax2);

            //清除所有buff
            ClearAllBuffs(player);

            player.SetResurrectionValue(0);//复苏进度归零

            if (player.TryGetModPlayer<SirenMusicalBoxPlayer>(out var sirenMusicalBoxPlayer) && sirenMusicalBoxPlayer.IsCursed) {
                SirenMusicalBoxPlayer.StopAllMusicBoxes(player);
            }

            //生成大量恢复粒子
            for (int i = 0; i < 50; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = player.Center + angle.ToRotationVector2() * Main.rand.NextFloat(100f);
                int dust = Dust.NewDust(pos, 1, 1, DustID.HealingPlus, 0, 0, 0, default, 2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = (player.Center - pos).SafeNormalize(Vector2.Zero) * 5f;
            }
        }
    }

    #region 重启鱼群
    internal class RestartFishBoid
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public Vector2 TargetPosition;
        public float Scale;
        public float Frame;
        public int FishType;
        public Color TintColor;
        public float LifeProgress; //0-1生命周期
        public float MaxLife;
        public float Life;
        private float rotationSpeed;
        private float spiralAngle;
        private float spiralRadius;
        public readonly List<Vector2> TrailPositions = new();
        private const int MaxTrailLength = 12;

        public RestartFishBoid(Vector2 spawnPos, Vector2 targetPos) {
            var rand = Main.rand;
            Position = spawnPos;
            TargetPosition = targetPos;
            Velocity = (targetPos - spawnPos).SafeNormalize(Vector2.Zero) * rand.NextFloat(15f, 25f);

            Scale = 0.5f + rand.NextFloat() * 0.4f;
            Frame = rand.NextFloat(10f);
            FishType = rand.Next(3);
            Life = 0f;
            MaxLife = 120f;
            LifeProgress = 0f;

            rotationSpeed = rand.NextFloat(0.05f, 0.1f) * (rand.NextBool() ? 1 : -1);
            spiralAngle = rand.NextFloat(MathHelper.TwoPi);
            spiralRadius = rand.NextFloat(30f, 60f);

            TintColor = new Color(100 + rand.Next(50), 200 + rand.Next(55), 255);
        }

        public void Update(Vector2 playerCenter) {
            Life++;
            LifeProgress = Life / MaxLife;

            //阶段性运动
            if (LifeProgress < 0.6f) {
                //阶段1、冲向玩家中心+螺旋
                spiralAngle += rotationSpeed;
                Vector2 spiralOffset = new Vector2(
                    (float)Math.Cos(spiralAngle) * spiralRadius * (1f - LifeProgress),
                    (float)Math.Sin(spiralAngle) * spiralRadius * (1f - LifeProgress)
                );

                Vector2 toTarget = (playerCenter - Position).SafeNormalize(Vector2.Zero);
                Velocity = Vector2.Lerp(Velocity, (toTarget * 20f) + spiralOffset * 0.3f, 0.15f);
            }
            else {
                //阶段2、环绕玩家快旋
                float orbitAngle = LifeProgress * MathHelper.TwoPi * 3f + spiralAngle;
                float orbitRadius = 50f * (1f - (LifeProgress - 0.6f) / 0.4f);
                Vector2 orbitPos = playerCenter + orbitAngle.ToRotationVector2() * orbitRadius;
                Velocity = (orbitPos - Position) * 0.3f;
            }

            Position += Velocity;
            Frame += 0.4f;

            TrailPositions.Insert(0, Position);
            if (TrailPositions.Count > MaxTrailLength) {
                TrailPositions.RemoveAt(TrailPositions.Count - 1);
            }
        }

        public bool ShouldRemove() => Life >= MaxLife;

        public void DrawTrail(float globalAlpha) {
            if (TrailPositions.Count < 2) return;
            Texture2D tex = VaultAsset.placeholder2.Value;

            for (int i = 0; i < TrailPositions.Count - 1; i++) {
                float progress = i / (float)TrailPositions.Count;
                float trailAlpha = (1f - progress) * globalAlpha * (1f - LifeProgress) * 0.55f;
                float width = Scale * (5f - progress * 3f);

                Vector2 start = TrailPositions[i];
                Vector2 end = TrailPositions[i + 1];
                Vector2 diff = end - start;
                float rot = diff.ToRotation();
                float len = diff.Length();

                //越靠尾越沉向深水色：亮痕有暗底，不再整条泛白
                Color c = Color.Lerp(TintColor, RestartEffectProj.DeepColor, progress * 0.8f) * trailAlpha;
                Main.spriteBatch.Draw(tex, start - Main.screenPosition, new Rectangle(0, 0, 1, 1),
                    c, rot, Vector2.Zero, new Vector2(len, width), SpriteEffects.None, 0f);
            }
        }

        public void Draw(float globalAlpha) {
            int itemType = FishType switch {
                0 => ItemID.Tuna,
                1 => ItemID.Bass,
                2 => ItemID.Trout,
                _ => ItemID.Tuna
            };

            Main.instance.LoadItem(itemType);
            Texture2D fishTex = TextureAssets.Item[itemType].Value;
            Rectangle rect = fishTex.Bounds;
            SpriteEffects effects = Velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            float rot = Velocity.ToRotation() + (Velocity.X > 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4);
            Vector2 origin = rect.Size() * 0.5f;

            float fadeAlpha = globalAlpha * (1f - LifeProgress * 0.5f);
            Vector2 drawPos = Position - Main.screenPosition;

            //暗体剪影垫底：略大一圈的深水色底，亮色骑在暗体上（治旧版四向重影拷贝的发虚）
            Color silhouette = RestartEffectProj.DeepColor * (fadeAlpha * 0.85f);
            Main.spriteBatch.Draw(fishTex, drawPos, rect, silhouette, rot, origin, Scale * 1.12f, effects, 0f);

            //速度拖影：沿速度反向两枚渐淡残影，读作水中冲刺（moving=velocity-stretched）
            Vector2 back = -Velocity.SafeNormalize(Vector2.Zero);
            float speed = Velocity.Length();
            for (int i = 1; i <= 2; i++) {
                Vector2 offset = back * (speed * 0.45f * i);
                //A=0 加色只在预乘 AlphaBlend 批里成立，本绘制正处于实体批
                Color ghost = new Color(TintColor.R, TintColor.G, TintColor.B, (byte)0)
                    * (fadeAlpha * (0.34f - i * 0.11f));
                Main.spriteBatch.Draw(fishTex, drawPos + offset, rect, ghost, rot, origin,
                    Scale * (1f - i * 0.08f), effects, 0f);
            }

            //清晰本体
            Main.spriteBatch.Draw(fishTex, drawPos, rect, TintColor * fadeAlpha, rot, origin, Scale, effects, 0f);
        }
    }
    #endregion

    internal class RestartPlayer : ModPlayer
    {
        public override void OnRespawn() {
            if (!Player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                return;
            }
            if (halibutPlayer.IsInteractionLockedTime > 3 * 60) {
                halibutPlayer.IsInteractionLockedTime = 3 * 60;//重生后锁定时间不超过3秒
            }
        }

        /// <summary>
        /// 大范围重启的无敌前置顶位：Apply 落地当帧要到 PostUpdateEverything 才补 immune，
        /// 这里在玩家更新最前面先顶住，堵起始帧的空窗
        /// </summary>
        public override void PreUpdate() {
            if (Restarts.HalibutReset.IsPlayerAffected(Player.whoAmI) && !Player.dead) {
                Player.immune = true;
                Player.immuneTime = Math.Max(Player.immuneTime, 2);
            }
        }

        /// <summary>immune 被其他系统消耗或绕开时的末道免伤：倒带期间任何 Hurt 一律无效</summary>
        public override bool FreeDodge(Player.HurtInfo info)
            => Restarts.HalibutReset.IsPlayerAffected(Player.whoAmI);

        /// <summary>
        /// 全程无敌的语义覆盖 DoT：immune 只挡碰撞与弹幕，
        /// 中毒/灼烧走 lifeRegen：倒带途中坏再生钳零，防被烧死在潮水里
        /// </summary>
        public override void UpdateBadLifeRegen() {
            if (Restarts.HalibutReset.IsPlayerAffected(Player.whoAmI) && Player.lifeRegen < 0) {
                Player.lifeRegen = 0;
            }
        }
    }

    internal class RestartEffectProj : BaseHeldProj
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //深海色板：与领域鱼群同一海蓝语系
        internal static readonly Color DeepColor = new(10, 26, 48);
        internal static readonly Color BodyColor = new(30, 90, 160);
        internal static readonly Color FilmColor = new(70, 170, 230);
        internal static readonly Color GlowColor = new(120, 210, 255);
        internal static readonly Color FoamColor = new(185, 232, 250);

        private List<RestartFishBoid> fishSwarms;
        private enum RestartState { Gathering, Wrapping, Restarting, Dispersing }
        private RestartState currentState = RestartState.Gathering;
        private int stateTimer = 0;
        private const int GatherDuration = 40;
        private const int WrapDuration = 30;
        private const int RestartDuration = 20;
        private const int DisperseDuration = 30;
        private float effectAlpha = 0f;
        private int particleTimer = 0;
        private readonly float seed = Main.rand.NextFloat(1000f);

        //收束环计时：跨越聚拢与包裹两段的向心水环
        private int convergeTimer;
        //深渊水球包络（包裹段长成、待发绷紧、重启拍破膜）
        private float bubbleRadius;
        private float bubbleFade;
        private float bubbleArm;
        private float bubbleBurst;
        //暗水纱：亮层下的深水底，治发虚的根
        private float veilAlpha;
        //空化塌缩进度（<0 未开始）与重启闪（快衰减，白热只活两三帧）
        private float collapseProgress = -1f;
        private float flashGlow;

        public override void SetDefaults() {
            Projectile.width = 400;
            Projectile.height = 400;
            Projectile.timeLeft = GatherDuration + WrapDuration + RestartDuration + DisperseDuration;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.friendly = false;
        }

        public override void AI() {
            if (!Owner.active) { Projectile.Kill(); return; }

            Projectile.Center = Owner.Center;
            stateTimer++;
            if (currentState <= RestartState.Wrapping) {
                convergeTimer++;
            }

            switch (currentState) {
                case RestartState.Gathering:
                    UpdateGathering();
                    break;
                case RestartState.Wrapping:
                    UpdateWrapping();
                    break;
                case RestartState.Restarting:
                    UpdateRestarting();
                    break;
                case RestartState.Dispersing:
                    UpdateDispersing();
                    break;
            }

            //更新鱼群
            if (fishSwarms != null) {
                for (int i = fishSwarms.Count - 1; i >= 0; i--) {
                    fishSwarms[i].Update(Owner.Center);
                    if (fishSwarms[i].ShouldRemove()) {
                        fishSwarms.RemoveAt(i);
                    }
                }
            }

            //重启闪逐帧快衰：白热核只在头两三帧可读
            flashGlow *= 0.72f;
        }

        private void UpdateGathering() {
            float progress = stateTimer / (float)GatherDuration;
            effectAlpha = MathHelper.Clamp(progress, 0f, 1f);
            veilAlpha = progress * 0.5f;

            if (stateTimer == 1) {
                InitializeFishSwarms();
                SoundEngine.PlaySound(SoundID.Item8, Owner.Center); //召唤音效
            }

            //生成聚集粒子
            particleTimer++;
            if (particleTimer % 3 == 0) {
                SpawnGatherParticle();
            }

            if (stateTimer >= GatherDuration) {
                currentState = RestartState.Wrapping;
                stateTimer = 0;
                SoundEngine.PlaySound(SoundID.DD2_WitherBeastAuraPulse, Owner.Center);
            }
        }

        private void UpdateWrapping() {
            effectAlpha = 1f;
            veilAlpha = MathHelper.Lerp(veilAlpha, 0.62f, 0.12f);

            //水球快速吹起（回弹缓动），末段待发绷紧、膜面张力压平
            bubbleFade = MathHelper.Clamp(stateTimer / 8f, 0f, 1f);
            float growT = MathHelper.Clamp(stateTimer / (WrapDuration * 0.6f), 0f, 1f);
            bubbleRadius = VaultUtils.EaseOutBack(growT) * 66f;
            const int armLead = 12;
            bubbleArm = MathHelper.Clamp((stateTimer - (WrapDuration - armLead)) / (float)armLead, 0f, 1f);

            //生成包裹效果
            particleTimer++;
            if (particleTimer % 2 == 0) {
                SpawnWrapParticle();
            }

            if (stateTimer >= WrapDuration) {
                currentState = RestartState.Restarting;
                stateTimer = 0;
                SoundEngine.PlaySound(SoundID.Item4, Owner.Center); //爆炸音效
                SoundEngine.PlaySound(SoundID.Item29, Owner.Center); //恢复音效
            }
        }

        private void UpdateRestarting() {
            float progress = stateTimer / (float)RestartDuration;
            veilAlpha = MathHelper.Lerp(veilAlpha, 0.5f, 0.1f);

            //破膜：头 6 帧膜蚀散，随后膜体退场；空化塌缩环接管冲击表达
            bubbleBurst = MathHelper.Clamp(stateTimer / 6f, 0f, 1f);
            bubbleFade = 1f - MathHelper.Clamp((stateTimer - 4) / 8f, 0f, 1f);
            collapseProgress = progress;

            //白闪拍在破膜瞬间，快衰减
            if (stateTimer == 1) {
                flashGlow = 1f;
            }

            //执行重启效果
            if (stateTimer == 5) {
                RestartFish.ExecuteRestart(Owner);
            }

            //密集粒子爆发
            if (stateTimer < 10) {
                for (int i = 0; i < 3; i++) {
                    SpawnRestartParticle();
                }
            }

            if (stateTimer >= RestartDuration) {
                currentState = RestartState.Dispersing;
                stateTimer = 0;
            }
        }

        private void UpdateDispersing() {
            float progress = stateTimer / (float)DisperseDuration;
            effectAlpha = 1f - MathHelper.Clamp(progress, 0f, 1f);
            veilAlpha *= 0.9f;
            collapseProgress = -1f;

            if (stateTimer % 4 == 0) {
                SpawnDisperseParticle();
            }

            if (stateTimer >= DisperseDuration) {
                Projectile.Kill();
            }
        }

        private void InitializeFishSwarms() {
            fishSwarms = new List<RestartFishBoid>();
            int fishCount = 150; //大量鱼群

            for (int i = 0; i < fishCount; i++) {
                //从屏幕四周生成
                Vector2 spawnPos;
                float side = Main.rand.NextFloat(4f);
                if (side < 1f) { //上方
                    spawnPos = Owner.Center + new Vector2(Main.rand.NextFloat(-400, 400), -600);
                }
                else if (side < 2f) { //下方
                    spawnPos = Owner.Center + new Vector2(Main.rand.NextFloat(-400, 400), 600);
                }
                else if (side < 3f) { //左侧
                    spawnPos = Owner.Center + new Vector2(-600, Main.rand.NextFloat(-400, 400));
                }
                else { //右侧
                    spawnPos = Owner.Center + new Vector2(600, Main.rand.NextFloat(-400, 400));
                }

                fishSwarms.Add(new RestartFishBoid(spawnPos, Owner.Center));
            }
        }

        private void SpawnGatherParticle() {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 pos = Owner.Center + angle.ToRotationVector2() * Main.rand.NextFloat(200f, 400f);
            int dust = Dust.NewDust(pos, 1, 1, DustID.Water, 0, 0, 100, new Color(100, 200, 255), 1.5f);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].velocity = (Owner.Center - pos).SafeNormalize(Vector2.Zero) * 6f;
        }

        private void SpawnWrapParticle() {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float dist = Main.rand.NextFloat(50f, 100f);
            Vector2 pos = Owner.Center + angle.ToRotationVector2() * dist;
            int dust = Dust.NewDust(pos, 1, 1, DustID.DungeonSpirit, 0, 0, 120, new Color(120, 220, 255), 1.2f);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].velocity = angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 3f;
        }

        private void SpawnRestartParticle() {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 pos = Owner.Center + Main.rand.NextVector2Circular(30, 30);
            int dustType = Main.rand.NextBool() ? DustID.Electric : DustID.BlueFairy;
            int dust = Dust.NewDust(pos, 1, 1, dustType, 0, 0, 0, default, 2.5f);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].velocity = angle.ToRotationVector2() * Main.rand.NextFloat(8f, 15f);
        }

        private void SpawnDisperseParticle() {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 pos = Owner.Center + Main.rand.NextVector2Circular(50, 50);
            int dust = Dust.NewDust(pos, 1, 1, DustID.Water, 0, 0, 100, new Color(150, 220, 255), 1.3f);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].velocity = angle.ToRotationVector2() * 5f;

            //水散成泡：余下的海在原地上浮，读作潮水退去
            if (Main.rand.NextBool(2)) {
                Vector2 bubblePos = Owner.Center + Main.rand.NextVector2Circular(70, 70);
                Dust bubble = Dust.NewDustPerfect(bubblePos, DustID.BreatheBubble,
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(1.5f, 3.5f)),
                    120, default, Main.rand.NextFloat(0.8f, 1.2f));
                bubble.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //暗水纱垫底：亮层全部骑在这层深水底上
            DrawVeil();

            //向心收束环：聚拢与包裹段的"水在朝这里赶"
            DrawConvergeRings();

            //空化塌缩：重启拍的冲击表达（自管批次）
            if (collapseProgress > 0f && collapseProgress < 1f) {
                SeaShrimpVFX.DrawCollapse(Owner.Center, 250f, collapseProgress, seed,
                    MathHelper.Clamp(effectAlpha, 0f, 1f));
            }

            //绘制鱼群拖尾
            if (fishSwarms != null) {
                foreach (var fish in fishSwarms) {
                    fish.DrawTrail(effectAlpha);
                }
            }

            //绘制鱼群主体
            if (fishSwarms != null) {
                foreach (var fish in fishSwarms) {
                    fish.Draw(effectAlpha);
                }
            }

            //深渊水球：包裹段吹起、待发绷紧、重启拍破膜（自管批次）
            DrawBubble();

            //重启闪：软辉+白热核，A=0 加色进预乘批
            DrawFlash();
            return false;
        }

        /// <summary>暗水纱：Extra_98 真 alpha 暗底，加色批与 A=0 物理上压不暗画面，只有它能</summary>
        private void DrawVeil() {
            if (veilAlpha <= 0.01f) {
                return;
            }
            Texture2D veil = CWRAsset.Extra_98?.Value;
            if (veil == null) {
                return;
            }
            Vector2 drawPos = Owner.Center - Main.screenPosition;
            Color c = DeepColor * (veilAlpha * MathHelper.Clamp(effectAlpha, 0f, 1f));
            float breathe = 1f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.2f + seed) * 0.05f;
            Main.spriteBatch.Draw(veil, drawPos, null, c, 0f,
                veil.Size() / 2f, 380f / veil.Width * breathe, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(veil, drawPos, null, c * 0.6f, MathHelper.PiOver2,
                veil.Size() / 2f, 300f / veil.Width, SpriteEffects.None, 0f);
        }

        /// <summary>向心收束环：两道错拍循环、自外向内收拢的水环（ShockRingDraw 自管批次）</summary>
        private void DrawConvergeRings() {
            if (currentState > RestartState.Wrapping || convergeTimer <= 0) {
                return;
            }
            const int ringLife = 46;
            const int stagger = 22;
            for (int i = 0; i < 2; i++) {
                int local = convergeTimer - i * stagger;
                if (local <= 0) {
                    continue;
                }
                float t = local % ringLife / (float)ringLife;
                //向心：半径由外收内，收拢越近越快
                float radius = MathHelper.Lerp(330f, 72f, t * t);
                float alpha = (float)Math.Sin(t * MathHelper.Pi) * 0.35f * effectAlpha;
                ShockRingDraw.Draw(Main.spriteBatch, Owner.Center, radius, 9f,
                    FoamColor, FilmColor, DeepColor, alpha,
                    tearPx: 12f, squish: 1f, innerGlow: 0f, timeSeed: seed + i * 3.7f);
            }
        }

        /// <summary>深渊水球：FishronBubble 换深海色板，包住玩家的那口"重启舱"</summary>
        private void DrawBubble() {
            if (bubbleFade <= 0.01f || bubbleRadius < 4f) {
                return;
            }
            Effect fx = EffectLoader.FishronBubble?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            float fade = bubbleFade * MathHelper.Clamp(effectAlpha, 0f, 1f);
            if (fx == null || noise == null || pixel == null) {
                //着色器缺失：参数化冲击环顶一口膜圈，不拿灰度堆假膜
                ShockRingDraw.Draw(Main.spriteBatch, Owner.Center, bubbleRadius, 6f,
                    FoamColor, FilmColor, DeepColor, fade * 0.8f, timeSeed: seed);
                return;
            }

            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + seed * 0.61f);
            fx.Parameters["uSeed"]?.SetValue(seed * 0.173f);
            fx.Parameters["uWobble"]?.SetValue(0.62f * (1f - bubbleArm * 0.5f));
            fx.Parameters["uArm"]?.SetValue(bubbleArm);
            fx.Parameters["uBurst"]?.SetValue(bubbleBurst);
            fx.Parameters["uFade"]?.SetValue(fade);
            fx.Parameters["uTint"]?.SetValue(FilmColor.ToVector3());
            fx.Parameters["uDeepColor"]?.SetValue(DeepColor.ToVector3());

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);

            //盘径契约：可见半径 = 画布半宽 × 0.42
            float quad = bubbleRadius / 0.42f * 2f;
            Main.spriteBatch.Draw(pixel, Owner.Center - Main.screenPosition, null, Color.White, 0f,
                pixel.Size() * 0.5f, new Vector2(quad / pixel.Width, quad / pixel.Height), SpriteEffects.None, 0f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>重启闪：软辉铺场 + 白热核只活头两三帧（≤2f 过曝纪律）</summary>
        private void DrawFlash() {
            if (flashGlow <= 0.02f) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = Owner.Center - Main.screenPosition;
            Vector2 origin = glow.Size() / 2f;

            Color soft = new Color(GlowColor.R, GlowColor.G, GlowColor.B, (byte)0) * (flashGlow * 0.8f);
            float scale = 3.0f + (1f - flashGlow) * 1.6f;
            Main.spriteBatch.Draw(glow, drawPos, null, soft, 0f, origin, scale, SpriteEffects.None, 0f);

            if (flashGlow > 0.6f) {
                float hot = (flashGlow - 0.6f) / 0.4f;
                Color core = new Color(232, 250, 255, 0) * hot;
                Main.spriteBatch.Draw(glow, drawPos, null, core, 0f, origin, 1.5f, SpriteEffects.None, 0f);
            }
        }
    }
}
