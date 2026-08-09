using CalamityOverhaul.Common;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.DawnshatterAzures
{
    /// <summary>
    /// 苍穹破晓高空下砸:小跃举枪(6t)→拖玩家加速下坠(沿途气浪环)→落地爆发(火柱阵+日出冲击+震屏)→驻留收势<br/>
    /// owner 权威写位移,相位与落点经 NetHeldSend 广播,落地演出各端按相位切换自播;火柱弹幕仅 owner 生成<br/>
    /// 日出冲击画在本弹幕 PreDraw:半圆日轮沿地面线升起-悬停-沉没,地下部分按地面线裁剪
    /// </summary>
    internal class DawnshatterDive : BaseHeldProj, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Item_Melee + "DawnshatterAzure";
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<DawnshatterAzure>();

        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        internal static Asset<Texture2D> SunTex = null;

        private const int FrameCount = 4;
        private const int AimTicks = 6;
        /// 下坠兜底时长,掉虚空不无限坠
        private const int FallTimeout = 100;
        private const int ImpactTicks = 26;
        /// 判定线段长(自玩家中心沿坠向)
        private const float DiveReach = 260f;
        /// 日轮半径
        private const float SunRadius = 92f;

        private enum DivePhase : byte
        {
            Aim,
            Falling,
            Impact,
        }

        //==== 同步状态(NetHeldSend) ====
        private DivePhase phase;
        /// 落地点(枪尖触地),日出冲击与火柱阵的锚
        private Vector2 landPoint;
        /// 落地时是否真的砸中地(超时收尾则否,不播爆发)
        private bool landed;

        private int phaseTimer;
        private int stopHold;
        private float fallSpeed = 14f;
        private float heat;
        private float flashPulse;
        private float trailFade;
        /// 各端相位切换驱动的落地演出闩
        private bool impactFxDone;
        private Vector2 fallStartPos;
        private readonly List<VertexPositionColorTexture[]> stripSink = [];

        private Vector2 FallDir => new(0f, Owner.gravDir);

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 64;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = AimTicks + FallTimeout + ImpactTicks + 30;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;//一砸一敌一伤
            Projectile.ownerHitCheck = false;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
            Projectile.CWR().PierceResist = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void NetHeldSend(BinaryWriter writer) {
            writer.Write((byte)phase);
            writer.Write(landed);
            writer.WriteVector2(landPoint);
        }

        public override void NetHeldReceive(BinaryReader reader) {
            var newPhase = (DivePhase)reader.ReadByte();
            landed = reader.ReadBoolean();
            landPoint = reader.ReadVector2();
            if (newPhase != phase) {
                phase = newPhase;
                phaseTimer = 0;
                //远端相位落到 Impact 时由 AI 闩自播落地演出
            }
        }

        public override void AI() {
            VaultUtils.ClockFrame(ref Projectile.frame, 5, FrameCount - 1);
            flashPulse *= 0.55f;

            switch (phase) {
                case DivePhase.Aim:
                    UpdateAim();
                    break;
                case DivePhase.Falling:
                    UpdateFalling();
                    break;
                case DivePhase.Impact:
                    UpdateImpact();
                    break;
            }

            UpdatePose();
            Lighting.AddLight(Projectile.Center, new Vector3(1.2f, 0.72f, 0.28f) * (0.4f + heat));
            phaseTimer++;
        }

        //==== 起跳前摇 ====

        private void UpdateAim() {
            heat = phaseTimer / (float)AimTicks * 0.5f;
            trailFade = 0f;

            if (Projectile.IsOwnedByLocalPlayer()) {
                //蓄势小跃,砸之前先离地一寸
                if (phaseTimer == 0) {
                    Owner.velocity.Y = -5.5f * Owner.gravDir;
                }
                Owner.velocity.X *= 0.8f;
            }

            if (phaseTimer == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = -0.3f }, Owner.Center);
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 0.6f, Pitch = -0.2f }, Owner.Center);
            }

            if (phaseTimer >= AimTicks) {
                phase = DivePhase.Falling;
                phaseTimer = -1;
                fallStartPos = Owner.Center;
                heat = 1f;
                trailFade = 1f;
                flashPulse = 0.8f;
                Projectile.netUpdate = true;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack with { Volume = 0.8f, Pitch = -0.15f }, Owner.Center);
                }
            }
        }

        //==== 下坠 ====

        private void UpdateFalling() {
            heat = 1f;
            trailFade = 1f;
            fallSpeed = MathF.Min(fallSpeed * 1.06f, 46f);

            if (Projectile.IsOwnedByLocalPlayer()) {
                Vector2 step = FallDir * fallSpeed;
                //扫掠碰撞,平台也算落点(fallThrough=false)
                Vector2 allowed = Collision.TileCollision(Owner.position, step
                    , Owner.width, Owner.height, false, false, (int)Owner.gravDir);
                if (MathF.Abs(allowed.Y) < fallSpeed * 0.5f) {
                    LandImpact();
                    return;
                }
                Owner.velocity = step;
                Owner.GivePlayerImmuneState(3, false);
                Owner.noFallDmg = true;
                Owner.fallStart = (int)(Owner.position.Y / 16f);
            }

            //沿途:枪周余烬向上剥离+间隔气浪环
            if (!VaultUtils.isServer) {
                Vector2 perp = FallDir.RotatedBy(MathHelper.PiOver2);
                for (int i = 0; i < 2; i++) {
                    Vector2 pos = Owner.Center + perp * Main.rand.NextFloat(-22f, 22f)
                        + FallDir * Main.rand.NextFloat(-30f, 90f);
                    PRTLoader.NewParticle<PRT_DawnEmber>(pos, -FallDir * Main.rand.NextFloat(2f, 6f)
                        + perp * Main.rand.NextFloat(-1.5f, 1.5f), default, Main.rand.NextFloat(0.8f, 1.3f))
                        .Configure(Main.rand.Next(14, 24));
                }
                if (phaseTimer % 5 == 0) {
                    PRTLoader.NewParticle<PRT_DawnRing>(Owner.Center + FallDir * 40f, Vector2.Zero
                        , default, 1f).Configure(FallDir, 26f, 7f, 0.32f, 14);
                }
            }

            //掉虚空兜底:不爆发直接收势
            if (phaseTimer >= FallTimeout && Projectile.IsOwnedByLocalPlayer()) {
                landed = false;
                phase = DivePhase.Impact;
                phaseTimer = -1;
                impactFxDone = true;
                Projectile.netUpdate = true;
            }
        }

        /// <summary>owner 端落地结算:定落点、切相位、广播;演出由相位闩在各端 AI 里统一播</summary>
        private void LandImpact() {
            landed = true;
            phase = DivePhase.Impact;
            phaseTimer = -1;
            landPoint = Owner.Bottom + FallDir * 12f;
            Owner.velocity = Vector2.Zero;
            Owner.CWR().ScreenShakeValue = 11f;
            Projectile.netUpdate = true;

            //火柱阵仅 owner 生成,两侧贴地搜索,找不到地面的位置跳过
            for (int f = 0; f < 8; f++) {
                float xOff = (f - 3.5f) * 74f + Main.rand.NextFloat(-14f, 14f);
                Vector2 probe = landPoint + new Vector2(xOff, -70f * Owner.gravDir);
                if (!TryFindGround(probe, Owner.gravDir, 16, out Vector2 foot)) {
                    continue;
                }
                float tilt = MathHelper.Clamp(xOff / 900f, -0.35f, 0.35f)
                    + Main.rand.NextFloat(-0.12f, 0.12f);
                //柱向角在生成端算好(向上+倾角),Spike 本体不关心重力向
                float colAngle = (-FallDir).ToRotation() + tilt;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), foot, Vector2.Zero
                    , ModContent.ProjectileType<DawnshatterSpike>()
                    , (int)(Projectile.damage * Main.rand.NextFloat(0.85f, 1.15f))
                    , Projectile.knockBack, Owner.whoAmI
                    , ai0: Main.rand.NextFloat(76f, 132f), ai1: colAngle);
            }
        }

        /// <summary>自 from 沿重力向逐格找可站立面(实心或平台),返回贴地世界点</summary>
        internal static bool TryFindGround(Vector2 from, float gravDir, int maxTiles, out Vector2 foot) {
            foot = from;
            Point tile = from.ToTileCoordinates();
            if (!WorldGen.InWorld(tile.X, tile.Y, 4)) {
                return false;
            }
            int step = gravDir >= 0f ? 1 : -1;
            for (int i = 0; i < maxTiles; i++) {
                int y = tile.Y + i * step;
                if (!WorldGen.InWorld(tile.X, y, 4)) {
                    return false;
                }
                Tile probe = Framing.GetTileSafely(tile.X, y);
                if (probe.HasUnactuatedTile
                    && (Main.tileSolid[probe.TileType] || Main.tileSolidTop[probe.TileType])) {
                    foot = new Vector2(from.X, gravDir >= 0f ? y * 16f : y * 16f + 16f);
                    return true;
                }
            }
            return false;
        }

        //==== 落地驻留 ====

        private void UpdateImpact() {
            //落地演出闩:本地切相位与远端收包共用,谁先看到 Impact 谁播一次;顿帧也在这儿给,远端同样吃到
            if (!impactFxDone && landed) {
                impactFxDone = true;
                flashPulse = 1f;
                stopHold = 5;
                PlayImpactFX();
            }

            //顿帧驻留,姿态钉住
            if (stopHold > 0) {
                stopHold--;
                phaseTimer--;
                return;
            }

            heat *= 0.94f;
            trailFade *= 0.86f;
            if (phaseTimer >= ImpactTicks) {
                Projectile.Kill();
            }
        }

        /// <summary>落地爆发表现,各端自播:贴地火舌排+余烬喷泉+水平气浪环+分层爆音;碎砖读作大地被砸开</summary>
        private void PlayImpactFX() {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.9f, Pitch = -0.1f }, landPoint);
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.7f, Pitch = 0.1f }, landPoint);
            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.55f, Pitch = -0.25f }, landPoint);

            //落点碎砖,沿地面横向
            Collision.HitTiles(landPoint - new Vector2(40f, 8f), new Vector2(0f, 6f * Owner.gravDir), 80, 16);

            Vector2 up = -FallDir;
            //贴地火舌排,越远越矮
            for (int i = 0; i < 12; i++) {
                float side = i % 2 == 0 ? 1f : -1f;
                float dist = Main.rand.NextFloat(18f, 250f);
                Vector2 pos = landPoint + new Vector2(side * dist, 0f);
                if (!TryFindGround(pos - FallDir * 40f, Owner.gravDir, 8, out Vector2 foot)) {
                    continue;
                }
                float sizeK = 1.7f - dist / 250f;
                PRTLoader.NewParticle<PRT_DawnTongue>(foot, Vector2.Zero, default
                    , Main.rand.NextFloat(0.9f, 1.3f) * sizeK)
                    .Configure(up, Main.rand.NextFloat(0.8f, 1.3f) * sizeK, Main.rand.Next(4, 7));
            }
            //余烬喷泉,中心密两侧疏
            for (int i = 0; i < 24; i++) {
                float lane = Main.rand.NextFloat(-1f, 1f);
                Vector2 vel = (up * Main.rand.NextFloat(5f, 12f) * (1.2f - MathF.Abs(lane))
                    + new Vector2(lane * 7f, 0f));
                PRTLoader.NewParticle<PRT_DawnEmber>(landPoint + new Vector2(lane * 130f, 0f) - FallDir * 6f
                    , vel, default, Main.rand.NextFloat(1f, 1.6f)).Configure(Main.rand.Next(20, 32));
            }
            //水平气浪环贴地扩张,一大一小错帧感
            PRTLoader.NewParticle<PRT_DawnRing>(landPoint - FallDir * 14f, Vector2.Zero, default, 1f)
                .Configure(FallDir, 40f, 16f, 0.3f, 18);
            PRTLoader.NewParticle<PRT_DawnRing>(landPoint - FallDir * 22f, Vector2.Zero, default, 1f)
                .Configure(FallDir, 20f, 11f, 0.34f, 14);
        }

        private void UpdatePose() {
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            //举枪竖直向下,朝向维持原向
            Vector2 aim = FallDir;
            Owner.itemRotation = (aim * Owner.direction).ToRotation();
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full
                , aim.ToRotation() - MathHelper.PiOver2);
            float holdout = phase switch {
                DivePhase.Aim => MathHelper.Lerp(40f, 80f, phaseTimer / (float)AimTicks),
                DivePhase.Falling => 110f,
                _ => 96f,
            };
            Projectile.Center = Owner.GetPlayerStabilityCenter() + aim * holdout;
            Projectile.rotation = aim.ToRotation();
            Owner.noFallDmg = true;
        }

        //==== 判定 ====

        public override bool? CanDamage() => phase == DivePhase.Falling ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (phase != DivePhase.Falling) {
                return false;
            }
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , hand - FallDir * 20f, hand + FallDir * DiveReach, 46f, ref point);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //自上而下砸,击退向下压
            modifiers.HitDirectionOverride = target.Center.X >= Owner.Center.X ? 1 : -1;
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.5f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            ItemLoader.OnHitNPC(Item, Owner, target, hit, damageDone);
            NPCLoader.OnHitByItem(target, Owner, Item, hit, damageDone);
            PlayerLoader.OnHitNPC(Owner, target, hit, damageDone);

            flashPulse = 1f;
            //斩线横过目标:下砸是竖着落的,痕迹要横着切
            DawnshatterBrand.Strike(Owner, target, FallDir.RotatedBy(MathHelper.PiOver2));
            target.AddBuff(BuffID.OnFire3, 600);
            target.AddBuff(BuffID.Daybreak, 480);
            target.velocity += FallDir * 6f * target.knockBackResist;

            if (!VaultUtils.isServer) {
                bool steel = CWRLoad.NPCValue.ISTheofSteel(target);
                SoundEngine.PlaySound((steel ? SoundID.NPCHit4 : SoundID.NPCHit1) with {
                    Pitch = steel ? 0.1f : -0.1f, Volume = 0.8f
                }, target.Center);
                for (int i = 0; i < 8; i++) {
                    Vector2 vel = FallDir.RotatedByRandom(steel ? 0.9f : 0.5f) * Main.rand.NextFloat(4f, 11f);
                    PRTLoader.NewParticle<PRT_DawnEmber>(target.Center + Main.rand.NextVector2Circular(12f, 12f)
                        , vel, default, Main.rand.NextFloat(0.9f, 1.5f)).Configure(Main.rand.Next(16, 26));
                }
            }
        }

        //==== 绘制 ====

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureValue;
            Rectangle rect = tex.GetRectangle(Projectile.frame, FrameCount);
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            int dir = Owner.direction;
            float reach = phase == DivePhase.Aim
                ? MathHelper.Lerp(170f, 240f, phaseTimer / (float)AimTicks) : 250f;
            Vector2 spearVec = FallDir * reach;

            //下坠残影沿轨迹向上铺
            if (phase == DivePhase.Falling) {
                for (int i = 1; i <= 3; i++) {
                    Color ghost = new Color(255, 176, 64) * (0.3f * (1f - i / 4f));
                    ghost.A = 0;
                    DawnshatterRenderer.DrawSpearQuad(tex, rect, hand - FallDir * (i * 34f), spearVec, dir, ghost);
                }
            }

            if (heat > 0.1f) {
                Color glow = new Color(255, 168, 60) * (heat * 0.5f);
                glow.A = 0;
                DawnshatterRenderer.DrawSpearQuad(tex, rect, hand, spearVec * 1.03f, dir, glow);
            }
            DawnshatterRenderer.DrawSpearQuad(tex, rect, hand, spearVec, dir, Projectile.GetAlpha(lightColor));

            //日出冲击:落地后半圆日轮沿地面线升起-悬停-沉没
            if (phase == DivePhase.Impact && landed) {
                DrawSunrise();
            }
            return false;
        }

        /// <summary>
        /// 日轮时间线:0~7t 升起(EaseOutCubic) 7~13t 悬停微浮 13~22t 沉没转红;
        /// 圆心沿地面线上下移动,地下部分用源矩形裁掉,读作"太阳从砸点跳出地平线一瞬"
        /// </summary>
        private void DrawSunrise() {
            Texture2D sun = SunTex?.Value;
            if (sun == null) {
                return;
            }
            float t = MathF.Max(phaseTimer, 0f);
            float rise;
            float fade = 1f;
            if (t < 7f) {
                rise = VaultUtils.EaseOutCubic(t / 7f);
            }
            else if (t < 13f) {
                rise = 1f + MathF.Sin((t - 7f) * 0.9f) * 0.04f;
            }
            else {
                float k = MathHelper.Clamp((t - 13f) / 9f, 0f, 1f);
                rise = 1f - VaultUtils.EaseOutCubic(k) * 1.15f;
                fade = 1f - k * 0.4f;
            }

            float gravDir = Owner.gravDir;
            //圆心自地下 0.9R 升到地上 0.55R;gravDir 反转时整体镜像
            float lift = MathHelper.Lerp(-SunRadius * 0.9f, SunRadius * 0.55f, rise);
            float scale = SunRadius * 2f / sun.Width;
            float texR = sun.Height * 0.5f * scale;
            float centerY = landPoint.Y - lift * gravDir;
            float topY = centerY - texR * gravDir;
            //只画地面线以上的部分(重力反转时为以下)
            float visible = MathHelper.Clamp((landPoint.Y - topY) * gravDir, 0f, texR * 2f);
            if (visible <= 2f) {
                return;
            }
            var src = new Rectangle(0, gravDir >= 0f ? 0 : sun.Height - (int)(visible / scale)
                , sun.Width, (int)(visible / scale));
            //正常重力从贴图顶画到地面线;反转时从地面线向下画贴图下半
            Vector2 drawPos = new Vector2(landPoint.X, gravDir >= 0f ? topY : landPoint.Y) - Main.screenPosition;

            float heatK = MathHelper.Clamp(rise, 0f, 1f);
            Color body = Color.Lerp(new Color(255, 120, 40), new Color(255, 208, 96), heatK) * (0.85f * fade);
            body.A = 0;
            Color core = new Color(255, 240, 200) * (0.7f * fade * heatK);
            core.A = 0;

            var origin = new Vector2(sun.Width * 0.5f, 0f);
            Main.EntitySpriteDraw(sun, drawPos, src, body, 0f, origin, scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(sun, drawPos, src, core, 0f, origin, scale * 0.55f, SpriteEffects.None, 0);
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (phase == DivePhase.Aim || trailFade <= 0.02f) {
                return;
            }
            float traveled = Vector2.Distance(fallStartPos, Owner.Center);
            float tip = traveled + DiveReach;
            if (tip < 80f) {
                return;
            }
            stripSink.Clear();
            //火线世界锚定在下坠轨迹上,落地后驻留渐熄
            float halfWidth = 30f + heat * 8f;
            DawnshatterRenderer.CollectThrustStrips(stripSink, fallStartPos, FallDir, 0f, tip, halfWidth, heat, trailFade);
            //刺击条带本就在噪声原作刻度附近,传中性 600 保持既有观感
            DawnshatterRenderer.DrawStrips(false, trailFade, heat, flashPulse, 600f, 0f, stripSink);
        }
    }
}
