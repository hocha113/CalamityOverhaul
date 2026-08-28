using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Items.Melee.Abyssrends;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Summon.Deepclaws
{
    /// <summary>
    /// 钳渊龙虾。ai0 状态 ai1 计时 ai2 目标 whoAmI(-1 无)。
    /// 钳击命中三次后收势引爆空化钳鸣(<see cref="DeepclawSnapBurst"/>)，
    /// 尾链为参数化装配,见 <see cref="DeepclawVFX"/>
    /// </summary>
    internal class DeepclawLobster : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Projectile_Summon + "DeepclawLobster";

        //客户端 PostSetupContent 加载,服务端为空,绘制侧判空
        [VaultLoaden(CWRConstant.Projectile_Summon + "DeepclawLobsterSegment")]
        public static Asset<Texture2D> SegmentTex = null;
        [VaultLoaden(CWRConstant.Projectile_Summon + "DeepclawLobsterFan")]
        public static Asset<Texture2D> FanTex = null;

        private const int StIdle = 0;
        private const int StAim = 1;
        private const int StPinch = 2;
        private const int StSnap = 3;
        private const int StFlip = 4;
        private const int StRecover = 5;

        private const int AimTime = 10;
        private const int PinchTime = 20;
        private const int SnapTime = 18;
        private const int FlipTime = 14;
        private const int RecoverTime = 18;

        private const float RestRadius = 116f;
        private const float IdleLeash = 280f;
        private const float PinchLeash = 620f;
        private const float DetectRange = 560f;
        /// <summary>攒满多少次钳击命中换一次钳鸣</summary>
        private const int SnipsPerSnap = 3;
        private const int WakeLen = 12;

        private float State { get => Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private float Timer { get => Projectile.ai[1]; set => Projectile.ai[1] = value; }
        private float TargetIndex { get => Projectile.ai[2]; set => Projectile.ai[2] = value; }

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>已攒的钳击命中数,owner 侧决策,无需入网</summary>
        private int snips;
        /// <summary>朝向角(世界),贴图朝上所以绘制时 +PiOver2</summary>
        private float facing = -MathHelper.PiOver2;
        /// <summary>尾链节点,索引 0 为尾根</summary>
        private readonly Vector2[] tailNodes = new Vector2[DeepclawVFX.TailNodes];
        private bool tailInit;
        /// <summary>甩尾增幅,冲刺/尾弹时拉高</summary>
        private float whip;
        /// <summary>冲刺热度,驱动暗流尾迹</summary>
        private float dashHeat;
        private float appear;
        /// <summary>钳鸣蓄势进度 0~1,驱动开钳姿态</summary>
        private float clawOpen;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.MinionSacrificable[Type] = true;
            ProjectileID.Sets.MinionTargettingFeature[Type] = true;
            ProjectileID.Sets.TrailCacheLength[Type] = WakeLen;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 36;
            Projectile.height = 36;
            Projectile.minion = true;
            Projectile.minionSlots = 1f;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.netImportant = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 18000;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 16;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.ai[2] = -1f;
        }

        public override bool? CanDamage() => State == StPinch;

        public override bool MinionContactDamage() => State == StPinch;

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }
            //增益在场才续命：取消增益后寿命自然耗尽即解散。
            //禁止在这里反向 AddBuff，否则玩家永远取消不掉召唤
            if (Owner.HasBuff(ModContent.BuffType<DeepclawBuff>())) {
                Projectile.timeLeft = 2;
            }

            if (appear < 1f) {
                appear = MathHelper.Clamp(appear + 0.07f, 0f, 1f);
            }

            if (Projectile.Distance(Owner.Center) > 2400f) {
                Projectile.Center = RestPosition();
                Projectile.velocity *= 0.1f;
                tailInit = false;
                Enter(StIdle);
            }

            switch ((int)State) {
                case StAim:
                    AimAI();
                    break;
                case StPinch:
                    PinchAI();
                    break;
                case StSnap:
                    SnapAI();
                    break;
                case StFlip:
                    FlipAI();
                    break;
                case StRecover:
                    RecoverAI();
                    break;
                default:
                    IdleAI();
                    break;
            }

            float leash = State is StPinch or StSnap or StFlip ? PinchLeash : IdleLeash;
            ClampLeash(leash);
            UpdateVisual();
            Lighting.AddLight(Projectile.Center, 0.06f, 0.2f, 0.3f);
        }

        #region 状态
        private void CountFlock(out int slot, out int total) {
            slot = 0;
            total = 0;
            int selfId = Projectile.identity;
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.owner != Projectile.owner || p.type != Type) {
                    continue;
                }
                total++;
                if (p.identity < selfId) {
                    slot++;
                }
            }
            if (total < 1) {
                total = 1;
            }
        }

        private Vector2 RestPosition() {
            CountFlock(out int slot, out int total);
            float t = Main.GameUpdateCount * 0.01f + Projectile.identity * 0.83f;
            float ang = MathHelper.TwoPi * slot / total + t + MathF.Sin(t * 0.7f + slot) * 0.24f;
            float rad = RestRadius + (Projectile.identity % 4) * 9f + MathF.Sin(t * 1.3f) * 12f;
            Vector2 orbit = ang.ToRotationVector2() * rad;
            orbit.Y = orbit.Y * 0.58f - 30f * Owner.gravDir;
            return Owner.Center + orbit;
        }

        private void IdleAI() {
            Vector2 rest = RestPosition();
            float wt = Main.GameUpdateCount * 0.028f + Projectile.identity * 0.5f;
            Vector2 wind = new(MathF.Sin(wt) * 8f, MathF.Cos(wt * 0.7f) * 7f);

            Vector2 want = rest + wind;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, (want - Projectile.Center) * 0.08f, 0.16f);
            Projectile.velocity *= 0.96f;

            if (Projectile.owner == Main.myPlayer && Main.GameUpdateCount % 8 == (uint)(Projectile.identity % 8)) {
                NPC target = PickTarget();
                if (target != null) {
                    TargetIndex = target.whoAmI;
                    Enter(StAim);
                    Projectile.netUpdate = true;
                }
            }
        }

        private void AimAI() {
            Timer++;
            NPC target = ResolveTarget();
            if (target == null) {
                Enter(StRecover);
                return;
            }
            //刹住并盯准,尾巴先甩起来做前摇
            Projectile.velocity *= 0.82f;
            whip = MathHelper.Clamp(whip + 0.16f, 0f, 0.9f);
            if (Timer >= AimTime) {
                Vector2 aim = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                Projectile.velocity = aim * 14.5f;
                Enter(StPinch);
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.7f, Pitch = 0.35f }, Projectile.Center);
                }
                if (Projectile.owner == Main.myPlayer) {
                    Projectile.netUpdate = true;
                }
            }
        }

        private void PinchAI() {
            Timer++;
            NPC target = ResolveTarget();
            Vector2 aim = target != null ? target.Center : Projectile.Center + Projectile.velocity;
            Vector2 accel = (aim - Projectile.Center).SafeNormalize(Vector2.UnitX) * 2.3f;
            //前几帧猛加速,后半自然衰减,不匀速爬行
            float gain = Timer < 4f ? 1.3f : Timer < 10f ? 1.06f : 0.97f;
            Projectile.velocity = (Projectile.velocity + accel) * gain;
            float cap = MathHelper.Lerp(13f, 24f, MathHelper.Clamp(Timer / 7f, 0f, 1f));
            if (Projectile.velocity.Length() > cap) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * cap;
            }

            dashHeat = MathHelper.Clamp(dashHeat + 0.2f, 0f, 1f);
            whip = MathHelper.Clamp(whip + 0.1f, 0f, 1.1f);
            if (!Main.dedServ && Timer % 2 == 0) {
                DeepclawVFX.DashSpray(Projectile.Center, Projectile.velocity);
            }

            bool overLeash = Vector2.Distance(Projectile.Center, Owner.Center) > PinchLeash - 40f;
            if (Timer >= PinchTime || overLeash || target == null) {
                BeginFlip();
            }
        }

        private void SnapAI() {
            Timer++;
            clawOpen = MathHelper.Clamp(Timer / (SnapTime - 2f), 0f, 1f);
            NPC target = ResolveTarget();
            //贴住目标缓漂,钳口对准
            if (target != null) {
                Vector2 hold = target.Center - (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 52f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, (hold - Projectile.Center) * 0.1f, 0.25f) * 0.9f;
            }
            else {
                Projectile.velocity *= 0.85f;
            }

            if (!Main.dedServ) {
                DeepclawVFX.SnapGather(ClawPoint(), clawOpen);
            }

            if (Timer >= SnapTime) {
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item96 with { Volume = 0.75f, Pitch = 0.3f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.6f, Pitch = -0.2f }, Projectile.Center);
                }
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), ClawPoint(), Vector2.Zero
                        , ModContent.ProjectileType<DeepclawSnapBurst>()
                        , (int)(Projectile.damage * 1.5f), Projectile.knockBack * 1.5f, Projectile.owner);
                    Owner.CWR().ScreenShakeValue = Math.Max(Owner.CWR().ScreenShakeValue, 3f);
                }
                clawOpen = 0f;
                Enter(StRecover);
                if (Projectile.owner == Main.myPlayer) {
                    Projectile.netUpdate = true;
                }
            }
        }

        private void BeginFlip() {
            //龙虾式尾弹:朝反方向弹开,眼睛仍盯着目标
            Vector2 back = -Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Projectile.velocity = back * 19f + new Vector2(0f, -1.5f * Owner.gravDir);
            whip = 1.2f;
            Enter(StFlip);
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.55f, Pitch = -0.1f }, Projectile.Center);
            }
            if (Projectile.owner == Main.myPlayer) {
                Projectile.netUpdate = true;
            }
        }

        private void FlipAI() {
            Timer++;
            if (Timer > 3f) {
                Projectile.velocity *= 0.88f;
            }
            dashHeat = MathHelper.Clamp(dashHeat + 0.12f, 0f, 1f);
            if (Timer >= FlipTime) {
                Enter(StRecover);
            }
        }

        private void RecoverAI() {
            Timer++;
            Vector2 rest = RestPosition();
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, (rest - Projectile.Center) * 0.1f, 0.18f);
            Projectile.velocity *= 0.92f;
            if (Timer >= RecoverTime) {
                Enter(StIdle);
            }
        }

        private void ClampLeash(float maxLen) {
            Vector2 delta = Projectile.Center - Owner.Center;
            float dist = delta.Length();
            if (dist > maxLen) {
                Projectile.Center = Owner.Center + delta.SafeNormalize(Vector2.Zero) * maxLen;
                Vector2 inward = (Owner.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                if (Vector2.Dot(Projectile.velocity, inward) < 0f) {
                    Projectile.velocity = Vector2.Reflect(Projectile.velocity, inward) * 0.35f + inward * 5f;
                }
            }
        }

        private void Enter(int state) {
            State = state;
            Timer = 0f;
        }

        private NPC PickTarget() {
            if (Owner.HasMinionAttackTargetNPC) {
                NPC tagged = Main.npc[Owner.MinionAttackTargetNPC];
                if (tagged.CanBeChasedBy(Projectile) && tagged.Distance(Owner.Center) < DetectRange + 200f) {
                    return tagged;
                }
            }
            return Projectile.Center.FindClosestNPC(DetectRange);
        }

        private NPC ResolveTarget() {
            int idx = (int)TargetIndex;
            if (idx >= 0 && idx < Main.maxNPCs) {
                NPC npc = Main.npc[idx];
                if (npc.active && npc.CanBeChasedBy(Projectile)) {
                    return npc;
                }
            }
            return null;
        }
        #endregion

        #region 命中与视觉
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!Main.dedServ) {
                DeepclawVFX.HitSplat(target.Center, Projectile.velocity);
            }
            if (State != StPinch) {
                return;
            }
            snips++;
            if (snips >= SnipsPerSnap) {
                snips = 0;
                TargetIndex = target.whoAmI;
                Enter(StSnap);
            }
            else {
                BeginFlip();
                return;
            }
            if (Projectile.owner == Main.myPlayer) {
                Projectile.netUpdate = true;
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (State == StPinch) {
                modifiers.Knockback += 1.2f;
                modifiers.HitDirectionOverride = Math.Sign(Projectile.velocity.X);
            }
        }

        /// <summary>钳口世界坐标,朝向前方一小段</summary>
        private Vector2 ClawPoint() => Projectile.Center + facing.ToRotationVector2() * 30f;

        private void UpdateVisual() {
            //期望朝向按状态取,最短路平滑防跳变
            Vector2 face;
            float turn;
            switch ((int)State) {
                case StAim:
                case StSnap: {
                    NPC target = ResolveTarget();
                    face = target != null ? target.Center - Projectile.Center : facing.ToRotationVector2();
                    turn = 0.3f;
                    break;
                }
                case StPinch:
                    face = Projectile.velocity;
                    turn = 0.42f;
                    break;
                case StFlip:
                    //尾巴先行退开,钳口仍朝目标
                    face = -Projectile.velocity;
                    turn = 0.28f;
                    break;
                default:
                    face = Projectile.velocity.LengthSquared() > 2f
                        ? Projectile.velocity
                        : new Vector2(Projectile.Center.X - Owner.Center.X >= 0 ? 1f : -1f, -0.2f);
                    turn = 0.09f;
                    break;
            }
            float desired = face.SafeNormalize(Vector2.UnitX).ToRotation();
            facing += MathHelper.WrapAngle(desired - facing) * turn;

            whip *= 0.9f;
            dashHeat *= State is StPinch or StFlip ? 1f : 0.86f;

            //尾链装配
            float drawRot = facing + MathHelper.PiOver2;
            Vector2 anchor = Projectile.Center + DeepclawVFX.TailAnchorOffset.RotatedBy(drawRot) * Projectile.scale;
            float restAngle = facing + MathHelper.Pi;
            if (!tailInit) {
                DeepclawVFX.ResetTail(tailNodes, anchor, restAngle);
                tailInit = true;
            }
            float swim = 0.14f + MathHelper.Clamp(Projectile.velocity.Length() * 0.018f, 0f, 0.3f);
            float time = Main.GlobalTimeWrappedHourly * MathHelper.TwoPi * 0.5f + Projectile.identity * 0.6f;
            DeepclawVFX.BuildTail(tailNodes, anchor, restAngle, time, swim, whip, 0.34f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Color col = lightColor * appear;

            DeepclawVFX.DrawTail(tailNodes, lightColor, appear, Projectile.scale);

            float drawRot = facing + MathHelper.PiOver2;
            float speed = Projectile.velocity.Length();
            //高速时沿前进轴拉伸(贴图前方是 -Y,即 scale.Y 轴)
            float stretch = MathHelper.Clamp((speed - 5f) * 0.02f, 0f, 0.24f);
            //钳鸣蓄势时鼓开身位
            float openPulse = clawOpen > 0f ? 1f + MathF.Sin(clawOpen * MathHelper.Pi) * 0.1f : 1f;
            Vector2 scale = new Vector2(1f - stretch * 0.35f, 1f + stretch) * Projectile.scale
                * MathHelper.Lerp(0.5f, 1f, appear) * openPulse;

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, col
                , drawRot, tex.Size() * 0.5f, scale, SpriteEffects.None, 0);

            //蓄势末段钳口聚一点亮芯
            if (clawOpen > 0.55f) {
                Texture2D glow = CWRAsset.SoftGlow.Value;
                float t = (clawOpen - 0.55f) / 0.45f;
                Main.EntitySpriteDraw(glow, ClawPoint() - Main.screenPosition, null
                    , new Color(AbyssrendFX.Cyan.R, AbyssrendFX.Cyan.G, AbyssrendFX.Cyan.B, 0) * (0.7f * t)
                    , 0f, glow.Size() * 0.5f, 0.24f * t, SpriteEffects.None, 0);
            }
            return false;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (dashHeat < 0.08f) {
                return;
            }
            Vector2[] path = new Vector2[WakeLen];
            int count = 0;
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                path[count++] = Projectile.oldPos[i] + Projectile.Size * 0.5f;
            }
            if (count < 2) {
                return;
            }
            path[count - 1] = Projectile.Center;
            float fade = dashHeat * appear;
            AbyssrendFX.DrawPathStrip(path, count, i => {
                float t = i / (float)Math.Max(count - 1, 1);
                return MathHelper.Lerp(5f, 15f, t) * fade;
            }, fade * 0.8f);
        }
        #endregion
    }

    /// <summary>
    /// 空化钳鸣。ai0 半径倍率。伤害窗对准崩开段,窗内把非 Boss 敌人向爆心拖拽
    /// </summary>
    internal class DeepclawSnapBurst : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int Lifetime = 32;
        private const float BaseRadius = 132f;

        private float SizeMul => Projectile.ai[0] > 0.05f ? Projectile.ai[0] : 1f;
        private int Age => Lifetime - Projectile.timeLeft;
        private float Progress => MathHelper.Clamp(Age / (float)Lifetime, 0f, 1f);
        private float VisibleRadius => BaseRadius * SizeMul * MathHelper.Lerp(0.35f, 1f, 1f - (1f - Progress) * (1f - Progress));

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            if (Progress > 0.45f && Progress < 0.85f) {
                Lighting.AddLight(Projectile.Center, 0.22f, 0.7f, 0.8f);
            }
            else {
                Lighting.AddLight(Projectile.Center, 0.07f, 0.2f, 0.28f);
            }

            //崩开段向心拖拽,NPC 权威端结算
            if (!VaultUtils.isClient && CanDamage() == true) {
                float pullR = BaseRadius * SizeMul * 1.25f;
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (npc.boss || npc.friendly || npc.knockBackResist <= 0f || npc.immortal) {
                        continue;
                    }
                    float dist = npc.Distance(Projectile.Center);
                    if (dist > pullR || dist < 12f) {
                        continue;
                    }
                    npc.velocity += (Projectile.Center - npc.Center).SafeNormalize(Vector2.Zero)
                        * 0.65f * npc.knockBackResist;
                }
            }

            if (VaultUtils.isServer) {
                return;
            }
            if (Age == 15) {
                int count = (int)(11 * SizeMul);
                for (int i = 0; i < count; i++) {
                    Vector2 dir = Main.rand.NextVector2Unit();
                    PRTLoader.NewParticle<PRT_AbyssGlob>(Projectile.Center + dir * 8f
                        , dir * Main.rand.NextFloat(3f, 7f)
                        , Color.Lerp(AbyssrendFX.Deep, AbyssrendFX.Body, Main.rand.NextFloat())
                        , Main.rand.NextFloat(0.45f, 0.8f))
                        .Configure(Main.rand.Next(14, 24), 1.5f);
                }
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_AbyssSpark>(Projectile.Center
                        , Main.rand.NextVector2Circular(5f, 5f)
                        , AbyssrendFX.Foam, Main.rand.NextFloat(0.8f, 1.2f))
                        .Configure(12);
                }
            }
        }

        public override bool? CanDamage() => Progress >= 0.45f && Progress <= 0.82f;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (CanDamage() != true) {
                return false;
            }
            float boomR = BaseRadius * SizeMul * MathHelper.Lerp(0.2f, 1f, (Progress - 0.45f) / 0.37f);
            return targetHitbox.Distance(Projectile.Center) <= boomR;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Wet, 240);
            target.AddBuff(ModContent.BuffType<AbyssalPressure>(), 180);
        }

        public override bool PreDraw(ref Color lightColor) {
            float fade = Progress < 0.12f ? Progress / 0.12f : MathHelper.Clamp((1f - Progress) / 0.18f, 0f, 1f);
            fade = MathF.Max(fade, Progress > 0.35f && Progress < 0.9f ? 1f : fade);
            AbyssrendFX.DrawCanvasTech("TechBurst", Projectile.Center, AbyssrendFX.QuadPx(VisibleRadius)
                , Progress, fade);
            return false;
        }
    }
}
