using CalamityOverhaul.Common;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.DawnshatterAzures
{
    /// <summary>
    /// 苍穹破晓举枪突进<br/>
    /// 蓄力22t(压速+余烬汇聚+音高爬升+末2t预警闪)→释放软索敌(±12°锥内微调)→
    /// 冲刺速度赋形(首帧冲量→巡航复合微加速→末2t缓出+身体滑步),撞墙原地停,刹停即交还操控<br/>
    /// 火线世界锚定驻留在冲刺轨迹上,余烬痕迹活得比突进久;沿线空气拉扯复用 NeutronWarp.KamuiLine
    /// </summary>
    internal class DawnshatterDash : BaseHeldProj, IPrimitiveDrawable, IWarpDrawable
    {
        public override string Texture => CWRConstant.Item_Melee + "DawnshatterAzure";
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<DawnshatterAzure>();

        private const int FrameCount = 4;
        private const int ChargeTicks = 22;
        private const int DashTicks = 16;
        private const int StopTicks = 22;
        /// 冲刺期枪尖伸出距离
        private const float DashReach = 230f;
        /// 软索敌锥半角
        private const float AssistCone = 0.21f;

        private enum DashPhase : byte
        {
            Charging,
            Dashing,
            Stopping,
        }

        //==== 同步状态(NetHeldSend) ====
        private DashPhase phase;
        private Vector2 dashDir = Vector2.UnitX;
        private Vector2 dashStart;

        private int phaseTimer;
        private int stopHold;
        private bool hitAny;
        private float chargeProgress;
        private float heat;
        private float flashPulse;
        private float trailFade;
        private readonly List<VertexPositionColorTexture[]> stripSink = [];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 64;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = ChargeTicks + DashTicks + StopTicks + 30;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
            Projectile.ownerHitCheck = false;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
            Projectile.CWR().PierceResist = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void Initialize() {
            dashDir = UnitToMouseV;
            if (dashDir == Vector2.Zero) {
                dashDir = Vector2.UnitX * Owner.direction;
            }
        }

        public override void NetHeldSend(BinaryWriter writer) {
            writer.Write((byte)phase);
            writer.WriteVector2(dashDir);
            writer.WriteVector2(dashStart);
        }

        public override void NetHeldReceive(BinaryReader reader) {
            phase = (DashPhase)reader.ReadByte();
            dashDir = reader.ReadVector2();
            dashStart = reader.ReadVector2();
        }

        public override void AI() {
            VaultUtils.ClockFrame(ref Projectile.frame, 5, FrameCount - 1);
            flashPulse *= 0.55f;

            switch (phase) {
                case DashPhase.Charging:
                    UpdateCharging();
                    break;
                case DashPhase.Dashing:
                    UpdateDashing();
                    break;
                case DashPhase.Stopping:
                    UpdateStopping();
                    break;
            }

            UpdatePose();
            Lighting.AddLight(Projectile.Center, new Vector3(1.2f, 0.75f, 0.3f) * (0.4f + heat));
            phaseTimer++;
        }

        //==== 蓄力 ====

        private void UpdateCharging() {
            chargeProgress = VaultUtils.EaseOutCubic(phaseTimer / (float)ChargeTicks);
            heat = chargeProgress * 0.55f;
            trailFade = 0f;

            //蓄力压速,举枪对准;UnitToMouseV 框架跨端同步,远端有效
            Owner.velocity.X *= 0.82f;
            Vector2 aim = UnitToMouseV;
            if (aim != Vector2.Zero) {
                dashDir = aim;
            }

            //余烬向枪身汇聚
            if (!VaultUtils.isServer && phaseTimer % 2 == 0) {
                Vector2 tip = Owner.GetPlayerStabilityCenter() + dashDir * MathHelper.Lerp(60f, 92f, chargeProgress);
                Vector2 from = tip + Main.rand.NextVector2Unit() * Main.rand.NextFloat(50f, 120f) * (1.1f - chargeProgress);
                Vector2 vel = (tip - from) * 0.12f;
                PRTLoader.NewParticle<PRT_DawnEmber>(from, vel, default, Main.rand.NextFloat(0.7f, 1.1f))
                    .Configure(Main.rand.Next(10, 16), buoyancyStrength: 0.001f);
            }

            //音高爬升
            if (!VaultUtils.isServer && phaseTimer % 8 == 0) {
                SoundEngine.PlaySound(SoundID.DD2_EtherianPortalSpawnEnemy with {
                    Volume = 0.3f + chargeProgress * 0.2f, Pitch = chargeProgress * 0.6f
                }, Owner.Center);
            }

            //末2t金色预警闪
            if (phaseTimer == ChargeTicks - 2) {
                flashPulse = 1f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.6f, Pitch = -0.2f }, Owner.Center);
                }
            }

            if (phaseTimer >= ChargeTicks) {
                ReleaseDash();
            }
        }

        /// <summary>释放:软索敌只在 owner 端决策一次,经 NetHeldSend 广播</summary>
        private void ReleaseDash() {
            phase = DashPhase.Dashing;
            phaseTimer = -1;
            dashStart = Owner.Center;
            heat = 1f;
            trailFade = 1f;
            flashPulse = 1f;

            if (Projectile.IsOwnedByLocalPlayer()) {
                //±12°锥内最近可追踪目标,微调方向,大方向仍指哪打哪
                float best = 900f;
                NPC pick = null;
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (!npc.CanBeChasedBy(Projectile)) {
                        continue;
                    }
                    Vector2 to = npc.Center - Owner.Center;
                    float dist = to.Length();
                    if (dist > 900f || dist < 60f || dist >= best) {
                        continue;
                    }
                    float delta = MathF.Abs(MathHelper.WrapAngle(to.ToRotation() - dashDir.ToRotation()));
                    if (delta <= AssistCone) {
                        best = dist;
                        pick = npc;
                    }
                }
                if (pick != null) {
                    dashDir = (pick.Center - Owner.Center).SafeNormalize(dashDir);
                }
                Projectile.netUpdate = true;
            }

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack with { Volume = 0.75f, Pitch = 0.1f }, Owner.Center);
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.45f, Pitch = 0.3f }, Owner.Center);
                //起步喷发,余烬向后锥
                for (int i = 0; i < 14; i++) {
                    Vector2 vel = (-dashDir).RotatedByRandom(0.5f) * Main.rand.NextFloat(3f, 9f);
                    PRTLoader.NewParticle<PRT_DawnEmber>(Owner.Center + Main.rand.NextVector2Circular(14f, 14f)
                        , vel, default, Main.rand.NextFloat(0.9f, 1.4f)).Configure(Main.rand.Next(16, 26));
                }
            }
        }

        //==== 冲刺 ====

        /// 速度赋形:首帧冲量→巡航复合微加速→末2t缓出,全程约400px
        private static float DashSpeedAt(int tick) {
            if (tick <= 0) {
                return 30f;
            }
            if (tick >= DashTicks - 2) {
                return tick == DashTicks - 2 ? 12f : 6f;
            }
            return 23f * (1f + tick * 0.025f);
        }

        private void UpdateDashing() {
            float speed = DashSpeedAt(phaseTimer);
            heat = 1f;
            trailFade = 1f;

            if (Projectile.IsOwnedByLocalPlayer()) {
                //撞墙原地停,引擎扫掠碰撞防穿墙,禁橡皮筋
                Vector2 allowed = Collision.TileCollision(Owner.position, dashDir * speed
                    , Owner.width, Owner.height, false, false, (int)Owner.gravDir);
                if (allowed.Length() < speed * 0.4f) {
                    EnterStopping(wallSlam: true);
                    return;
                }
                Owner.velocity = dashDir * speed;
                Owner.GivePlayerImmuneState(3, false);
                Owner.noFallDmg = true;
            }

            //沿途余烬剥落
            if (!VaultUtils.isServer) {
                Vector2 perp = dashDir.RotatedBy(MathHelper.PiOver2);
                for (int i = 0; i < 2; i++) {
                    Vector2 pos = Owner.Center + perp * Main.rand.NextFloat(-20f, 20f) - dashDir * Main.rand.NextFloat(0f, 40f);
                    PRTLoader.NewParticle<PRT_DawnEmber>(pos, -dashDir * Main.rand.NextFloat(2f, 6f)
                        + perp * Main.rand.NextFloat(-1.5f, 1.5f), default, Main.rand.NextFloat(0.8f, 1.3f))
                        .Configure(Main.rand.Next(14, 24));
                }
                if (Main.rand.NextBool(3)) {
                    Vector2 outward = dashDir.RotatedBy(Main.rand.NextBool() ? MathHelper.PiOver2 : -MathHelper.PiOver2);
                    PRTLoader.NewParticle<PRT_DawnTongue>(Owner.Center + dashDir * Main.rand.NextFloat(40f, 180f)
                        , Vector2.Zero, default, Main.rand.NextFloat(0.6f, 0.9f))
                        .Configure(outward, Main.rand.NextFloat(0.5f, 0.85f), Main.rand.Next(3, 5));
                }
            }

            if (phaseTimer >= DashTicks) {
                EnterStopping(wallSlam: false);
            }
        }

        private void EnterStopping(bool wallSlam) {
            phase = DashPhase.Stopping;
            phaseTimer = -1;
            stopHold = hitAny || wallSlam ? 4 : 0;
            Projectile.netUpdate = true;

            if (Projectile.IsOwnedByLocalPlayer()) {
                //末段一寸滑步,之后交还操控
                Owner.velocity = dashDir * 4f;
                if (hitAny || wallSlam) {
                    Owner.CWR().ScreenShakeValue = 8f;
                }
            }
            if (!VaultUtils.isServer && (hitAny || wallSlam)) {
                flashPulse = 1f;
                SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.55f, Pitch = 0.15f }, Owner.Center);
                Vector2 tip = Owner.Center + dashDir * DashReach * 0.8f;
                for (int i = 0; i < 10; i++) {
                    PRTLoader.NewParticle<PRT_DawnEmber>(tip + Main.rand.NextVector2Circular(20f, 20f)
                        , dashDir.RotatedByRandom(0.8f) * Main.rand.NextFloat(3f, 10f)
                        , default, Main.rand.NextFloat(1f, 1.6f)).Configure(Main.rand.Next(18, 30));
                }
            }
        }

        private void UpdateStopping() {
            //顿帧驻留,姿态钉住
            if (stopHold > 0) {
                stopHold--;
                phaseTimer--;
                return;
            }
            heat *= 0.93f;
            //痕迹淡到近零才死,不许随弹幕消失
            trailFade *= 0.85f;

            //滑步衰减 3t 后不再写速度,操控交还玩家
            if (phaseTimer < 3 && Projectile.IsOwnedByLocalPlayer()) {
                Owner.velocity *= 0.5f;
            }

            if (phaseTimer >= StopTicks) {
                Projectile.Kill();
            }
        }

        private void UpdatePose() {
            Owner.heldProj = Projectile.whoAmI;
            int dir = MathF.Sign(dashDir.X) == 0 ? Owner.direction : Math.Sign(dashDir.X);
            Owner.direction = dir;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = (dashDir * dir).ToRotation();
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full
                , dashDir.ToRotation() - MathHelper.PiOver2);
            float holdout = phase == DashPhase.Charging ? MathHelper.Lerp(55f, 85f, chargeProgress) : 110f;
            Projectile.Center = Owner.GetPlayerStabilityCenter() + dashDir * holdout;
            Projectile.rotation = dashDir.ToRotation();
        }

        //==== 判定 ====

        public override bool? CanDamage() => phase == DashPhase.Dashing ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (phase != DashPhase.Dashing) {
                return false;
            }
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , hand - dashDir * 30f, hand + dashDir * DashReach, 44f, ref point);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = dashDir.X > 0 ? 1 : -1;
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.5f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            ItemLoader.OnHitNPC(Item, Owner, target, hit, damageDone);
            NPCLoader.OnHitByItem(target, Owner, Item, hit, damageDone);
            PlayerLoader.OnHitNPC(Owner, target, hit, damageDone);

            hitAny = true;
            flashPulse = 1f;
            target.AddBuff(BuffID.OnFire3, 600);
            target.AddBuff(BuffID.Daybreak, 480);
            target.AddBuff(BuffID.Ichor, 360);
            target.velocity += dashDir * 8f * target.knockBackResist;

            if (!VaultUtils.isServer) {
                bool steel = CWRLoad.NPCValue.ISTheofSteel(target);
                SoundEngine.PlaySound((steel ? SoundID.NPCHit4 : SoundID.NPCHit1) with {
                    Pitch = steel ? 0.1f : -0.1f, Volume = 0.75f
                }, target.Center);
                for (int i = 0; i < 7; i++) {
                    Vector2 vel = dashDir.RotatedByRandom(steel ? 0.9f : 0.5f) * Main.rand.NextFloat(4f, 11f);
                    PRTLoader.NewParticle<PRT_DawnEmber>(target.Center + Main.rand.NextVector2Circular(12f, 12f)
                        , vel, default, Main.rand.NextFloat(0.9f, 1.5f)).Configure(Main.rand.Next(16, 26));
                }
            }

            if (CWRServerConfig.Instance.ScreenVibration) {
                var modifier = new PunchCameraModifier(target.Center, dashDir, 4f, 5f, 8, 800f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }
        }

        //==== 绘制 ====

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureValue;
            Rectangle rect = tex.GetRectangle(Projectile.frame, FrameCount);
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            int dir = Owner.direction;
            //蓄力期枪略收,冲刺期全伸,枪根锚手后方
            float reach = phase == DashPhase.Charging
                ? MathHelper.Lerp(180f, 230f, chargeProgress) : DashReach;
            Vector2 spearVec = dashDir * reach;

            //冲刺残影,沿轨迹向后铺
            if (phase == DashPhase.Dashing) {
                for (int i = 1; i <= 3; i++) {
                    Color ghost = new Color(255, 176, 64) * (0.3f * (1f - i / 4f));
                    ghost.A = 0;
                    DawnshatterRenderer.DrawSpearQuad(tex, rect, hand - dashDir * (i * 30f), spearVec, dir, ghost);
                }
            }

            //蓄力升温辉光,依托枪身剪影不裸奔
            if (heat > 0.1f) {
                Color glow = new Color(255, 168, 60) * (heat * 0.55f);
                glow.A = 0;
                DawnshatterRenderer.DrawSpearQuad(tex, rect, hand, spearVec * 1.03f, dir, glow);
            }

            DawnshatterRenderer.DrawSpearQuad(tex, rect, hand, spearVec, dir, Projectile.GetAlpha(lightColor));
            return false;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (phase == DashPhase.Charging || trailFade <= 0.02f) {
                return;
            }
            float traveled = Vector2.Distance(dashStart, Owner.Center);
            float tip = traveled + DashReach;
            if (tip < 80f) {
                return;
            }
            stripSink.Clear();
            //火线世界锚定,rear=起点,驻留在冲刺轨迹上
            float halfWidth = 30f + heat * 8f;
            DawnshatterRenderer.CollectThrustStrips(stripSink, dashStart, dashDir, 0f, tip, halfWidth, heat, trailFade);
            DawnshatterRenderer.DrawStrips(false, trailFade, heat, flashPulse, 600f, stripSink);
        }

        /// <summary>沿冲刺线的空气拉扯,复用 NeutronWarp.KamuiLine;helper 只画轴对齐四边形,手动旋转对齐线轴</summary>
        void IWarpDrawable.Warp() {
            if (phase == DashPhase.Charging || EffectLoader.NeutronWarp?.Value is not Effect warpFx) {
                return;
            }
            float envelope = phase == DashPhase.Dashing ? 1f : trailFade;
            if (envelope <= 0.05f) {
                return;
            }
            Vector2 head = Owner.Center + dashDir * DashReach * 0.7f;
            float length = Vector2.Distance(dashStart, head);
            if (length < 60f) {
                return;
            }
            float angle = dashDir.ToRotation();

            warpFx.Parameters["uTime"]?.SetValue((float)Main.GameUpdateCount * 0.05f);
            warpFx.Parameters["uIntensity"]?.SetValue(0.18f);
            warpFx.Parameters["uProgress"]?.SetValue(envelope);
            warpFx.Parameters["uRotation"]?.SetValue(angle);
            warpFx.CurrentTechnique = warpFx.Techniques["KamuiLine"];

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState
                , DepthStencilState.None, RasterizerState.CullNone, warpFx, Main.GameViewMatrix.TransformationMatrix);
            warpFx.CurrentTechnique.Passes[0].Apply();

            //长度余量喂给两端羽化,线身全程实场;局部 +Y 旋到冲刺向
            Vector2 mid = (dashStart + head) * 0.5f - Main.screenPosition;
            Vector2 quad = new(240f, length * 1.5f + 120f);
            sb.Draw(VaultAsset.placeholder2.Value, mid, new Rectangle(0, 0, 1, 1), Color.White
                , angle - MathHelper.PiOver2, new Vector2(0.5f), quad, SpriteEffects.None, 0);

            sb.End();
            sb.Begin(0, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None
                , RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>日炎拉扯用中性色差,蓝移是中子星语言</summary>
        public bool DontUseBlueshiftEffect() => true;

        public void DrawCustom(SpriteBatch spriteBatch) { }
    }
}
