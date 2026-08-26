using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.SkeletronPrime
{
    /// <summary>
    /// 过载四臂虚影：ai[0]=臂型(0锯 1钳 2炮 3激光)，ai[1]=0活跃/1消散（owner 写+netUpdate）。<br/>
    /// 真实弹幕经原版同步全端可见；出招节拍各端本地推演（远端以 itemAnimation 近似，
    /// 伤害只在 owner 端解算故无碍）。挥击=臂体线判定，点射=owner 端出离子弹
    /// </summary>
    internal class OverloadArmProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        //臂型 → 原版机械骷髅王臂 NPC 贴图
        private static readonly int[] HandNpcIds = [NPCID.PrimeSaw, NPCID.PrimeVice, NPCID.PrimeCannon, NPCID.PrimeLaser];
        //对应 Main.npcFrameCount：锯2 钳2 炮1 激光1
        private static readonly int[] HandFrameCounts = [2, 2, 1, 1];

        /// <summary>出招总帧</summary>
        internal const int StrikeFrames = 18;
        /// <summary>出招后冷却帧（逐臂错相形成轮转）</summary>
        internal const int StrikeCooldown = 22;
        /// <summary>挥击伤害＝武器面板伤害比例</summary>
        internal const float MeleeDamageMul = 0.35f;
        /// <summary>离子弹伤害比例</summary>
        internal const float BoltDamageMul = 0.32f;
        /// <summary>展开动画帧数</summary>
        internal const int MaterializeFrames = 16;
        /// <summary>消散帧数</summary>
        internal const int DissolveFrames = 14;
        /// <summary>臂体线判定宽 px</summary>
        internal const float SweepHitWidth = 52f;

        private int ArmIndex => (int)Projectile.ai[0];
        /// <summary>锯/钳＝大弧挥臂，炮/激光＝短程突刺</summary>
        private bool IsPhysicalArm => ArmIndex <= 1;
        private Player Owner => Main.player[Projectile.owner];

        private ref float StrikeTimer => ref Projectile.localAI[0];
        private ref float CooldownTimer => ref Projectile.localAI[1];
        private ref float LifeTimer => ref Projectile.localAI[2];

        private float strikeAim;
        private float sweepDir = 1f;
        private bool strikeIsMelee;
        private Vector2 handPos;
        private float handRot;
        private int dissolveTimer;
        private int handFrame;
        //挥击拖影：最近几帧手端位置角度
        private readonly float[] smearAngles = new float[3];
        private int smearCount;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 800;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 36;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = OverloadCommandCore.OverloadFrames + 90;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 14;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Player owner = Owner;
            if (!owner.Alives()) {
                Projectile.Kill();
                return;
            }
            OverloadCorePlayer mp = owner.GetModPlayer<OverloadCorePlayer>();

            //生灭权威在 owner：窗口结束/卸下装备 → 进入消散并广播
            if (Projectile.IsOwnedByLocalPlayer() && !mp.OverloadActive && Projectile.ai[1] == 0f) {
                Projectile.ai[1] = 1f;
                Projectile.netUpdate = true;
            }

            LifeTimer++;

            //消散：反向成形，各端本地推演，owner 到点销毁
            if (Projectile.ai[1] >= 1f) {
                dissolveTimer++;
                if (dissolveTimer == 1 && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item93 with {
                        Volume = 0.25f, Pitch = -0.4f + ArmIndex * 0.08f, MaxInstances = 4
                    }, Projectile.Center);
                }
                if (dissolveTimer > DissolveFrames && Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.Kill();
                    return;
                }
            }

            UpdatePose(owner, mp);

            Projectile.Center = handPos;

            if (!VaultUtils.isServer) {
                Lighting.AddLight(handPos, OverloadCommandCore.IonCyan.ToVector3() * 0.3f * MaterializeT);
            }
        }

        /// <summary>展开进度 0~1（逐臂错帧），消散时反向回落</summary>
        private float MaterializeT {
            get {
                float t = MathHelper.Clamp((LifeTimer - ArmIndex * 3f) / MaterializeFrames, 0f, 1f);
                if (dissolveTimer > 0) {
                    t *= MathHelper.Clamp(1f - dissolveTimer / (float)DissolveFrames, 0f, 1f);
                }
                return t;
            }
        }

        /// <summary>驻位槽角：面向反侧的背后扇形</summary>
        private float SlotAngle(Player owner)
            => -MathHelper.PiOver2 - owner.direction * MathHelper.Lerp(0.30f, 1.72f, ArmIndex / 3f);

        private Vector2 SlotPos(Player owner) {
            float bob = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.1f + ArmIndex * 1.7f) * 3f;
            //展开带 6% 过冲：机械骨架"弹开"一拍
            float t = MaterializeT;
            float radius = (52f + bob) * (t + (float)Math.Sin(t * MathHelper.Pi) * 0.06f);
            return owner.MountedCenter + SlotAngle(owner).ToRotationVector2() * radius;
        }

        private void UpdatePose(Player owner, OverloadCorePlayer mp) {
            Vector2 slot = SlotPos(owner);
            if (LifeTimer <= 1f) {
                handPos = owner.MountedCenter;
            }

            bool ready = MaterializeT >= 1f && dissolveTimer == 0;
            //攻击热度：itemAnimation 全端可见；RecentComboHit 仅 owner 有值（持械弹幕型武器信号）
            bool attackActive = owner.itemAnimation > 0
                || (Projectile.IsOwnedByLocalPlayer() && mp.RecentComboHit);

            if (CooldownTimer > 0f) {
                CooldownTimer--;
            }

            if (ready && StrikeTimer <= 0f && CooldownTimer <= 0f && attackActive) {
                TryStartStrike(owner);
            }

            if (StrikeTimer > 0f) {
                UpdateStrike(owner);
                StrikeTimer--;
                if (StrikeTimer <= 0f) {
                    CooldownTimer = StrikeCooldown;
                    smearCount = 0;
                }
            }
            else {
                //驻位：缓动归位 + 手端指向瞄准方向
                handPos = Vector2.Lerp(handPos, slot, 0.28f);
                handRot = AimAngle(owner) + MathHelper.PiOver2;
                handFrame = 0;
            }
        }

        /// <summary>owner 用光标，远端/服务器退最近敌人或朝向水平（纯表现）</summary>
        private float AimAngle(Player owner) {
            if (Projectile.IsOwnedByLocalPlayer()) {
                return owner.MountedCenter.To(Main.MouseWorld).ToRotation();
            }
            NPC target = owner.Center.FindClosestNPC(600f);
            if (target != null) {
                return owner.MountedCenter.To(target.Center).ToRotation();
            }
            return owner.direction >= 0 ? 0f : MathHelper.Pi;
        }

        private static bool IsMeleeItem(Item item) {
            if (item == null || item.IsAir) {
                return false;
            }
            return item.DamageType.CountsAsClass(DamageClass.Melee)
                || item.DamageType == CWRRef.GetTrueMeleeDamageClass()
                || item.DamageType == CWRRef.GetTrueMeleeNoSpeedDamageClass();
        }

        private void TryStartStrike(Player owner) {
            Item held = owner.HeldItem;
            if (held == null || held.IsAir || held.damage <= 0) {
                return;
            }

            strikeIsMelee = IsMeleeItem(held);
            strikeAim = AimAngle(owner);
            sweepDir = -sweepDir;
            StrikeTimer = StrikeFrames;
            smearCount = 0;

            //伤害只在 owner 端结算，面板伤经 GetWeaponDamage 已含职业加成
            if (Projectile.IsOwnedByLocalPlayer()) {
                float mul = strikeIsMelee ? MeleeDamageMul : BoltDamageMul;
                Projectile.damage = Math.Max((int)(owner.GetWeaponDamage(held) * mul), 1);
            }

            if (!VaultUtils.isServer && strikeIsMelee) {
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with {
                    Volume = 0.4f, Pitch = 0.1f + ArmIndex * 0.07f, MaxInstances = 4
                }, Projectile.Center);
            }
        }

        private void UpdateStrike(Player owner) {
            int elapsed = StrikeFrames - (int)StrikeTimer;
            Vector2 root = RootPos(owner);
            Vector2 slot = SlotPos(owner);

            if (strikeIsMelee) {
                UpdateMeleeStrike(owner, elapsed, root, slot);
            }
            else {
                UpdateRangedStrike(owner, elapsed, root, slot);
            }
        }

        /// <summary>挥击三相：4帧后拉蓄势 → 6帧爆发弧扫（先快后缓）→ 归位</summary>
        private void UpdateMeleeStrike(Player owner, int elapsed, Vector2 root, Vector2 slot) {
            float arcHalf = IsPhysicalArm ? 0.85f : 0.5f;
            float radius = IsPhysicalArm ? 94f : 82f;

            if (elapsed <= 4) {
                float wt = elapsed / 4f;
                Vector2 cocked = owner.MountedCenter
                    + (strikeAim - sweepDir * (arcHalf + 0.45f)).ToRotationVector2() * (radius * 0.7f);
                handPos = Vector2.Lerp(slot, Vector2.Lerp(cocked, root, 0.18f), wt * wt);
                handRot = strikeAim - sweepDir * (arcHalf + 0.45f) * wt + MathHelper.PiOver2;
            }
            else if (elapsed <= 10) {
                float ts = (elapsed - 5) / 5f;
                float ang = strikeAim + sweepDir * MathHelper.Lerp(-arcHalf, arcHalf, VaultUtils.EaseOutCubic(ts));
                handPos = owner.MountedCenter + ang.ToRotationVector2() * radius;
                handRot = ang + MathHelper.PiOver2;

                //旋转拖影记账
                if (smearCount < smearAngles.Length) {
                    smearAngles[smearCount++] = ang;
                }
                else {
                    smearAngles[0] = smearAngles[1];
                    smearAngles[1] = smearAngles[2];
                    smearAngles[2] = ang;
                }
                //锯刃转帧 + 刃端火花
                if (HandFrameCounts[ArmIndex] > 1) {
                    handFrame = elapsed / 2 % HandFrameCounts[ArmIndex];
                }
                if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                    PRTLoader.NewParticle<PRT_Spark>(handPos + Main.rand.NextVector2Circular(8f, 8f),
                        ang.ToRotationVector2().RotatedBy(sweepDir * MathHelper.PiOver2) * Main.rand.NextFloat(2f, 5f),
                        OverloadCommandCore.IonCyan, Main.rand.NextFloat(0.6f, 1f))
                        ?.Configure(false, Main.rand.Next(8, 14), owner);
                }
            }
            else {
                float rt = (elapsed - 11) / 7f;
                handPos = Vector2.Lerp(handPos, slot, rt * 0.5f);
                handRot = AimAngle(owner) + MathHelper.PiOver2;
                handFrame = 0;
            }
        }

        /// <summary>点射三相：4帧后坐蓄势 → 第5帧出弹+反冲 → 归位</summary>
        private void UpdateRangedStrike(Player owner, int elapsed, Vector2 root, Vector2 slot) {
            Vector2 aimDir = strikeAim.ToRotationVector2();
            handRot = strikeAim + MathHelper.PiOver2;

            if (elapsed <= 4) {
                float wt = elapsed / 4f;
                handPos = Vector2.Lerp(slot, slot - aimDir * 10f, wt);
            }
            else if (elapsed == 5) {
                handPos = slot - aimDir * 16f;
                FireBolt(owner, aimDir);
            }
            else {
                float rt = (elapsed - 6) / 12f;
                handPos = Vector2.Lerp(handPos, slot, MathHelper.Clamp(rt, 0f, 1f) * 0.4f);
            }
        }

        private void FireBolt(Player owner, Vector2 aimDir) {
            //出膛口感各端都播；弹幕只在 owner 端生成（netcode 契约）
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound((ArmIndex % 2 == 0 ? SoundID.Item11 : SoundID.Item12) with {
                    Volume = 0.35f, Pitch = 0.3f + ArmIndex * 0.05f, MaxInstances = 4
                }, handPos);
                PRTLoader.NewParticle<PRT_Light>(handPos + aimDir * 12f, aimDir * 2f,
                    OverloadCommandCore.IonCyan, 0.3f)
                    ?.Configure(10, opacity: 1.2f, squishStrenght: 2.5f);
            }
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            Vector2 vel = aimDir.RotatedBy(Main.rand.NextFloat(-0.045f, 0.045f)) * 17f;
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), handPos + aimDir * 14f, vel,
                ModContent.ProjectileType<OverloadIonBolt>(), Projectile.damage, 1.2f, Projectile.owner);
        }

        private Vector2 RootPos(Player owner)
            => owner.MountedCenter + new Vector2(-owner.direction * 10f, -4f);

        //==================== 判定：只有挥击的爆发弧扫段咬人 ====================

        private bool SweepActive {
            get {
                //判定跑在 AI 递减之后，此处 elapsed 比 AI 内视角大 1：弧扫动作帧 5..10 → 判定窗 6..11
                int elapsed = StrikeFrames - (int)StrikeTimer;
                return StrikeTimer > 0f && strikeIsMelee && elapsed is >= 6 and <= 11;
            }
        }

        public override bool? CanDamage() => SweepActive ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!SweepActive) {
                return false;
            }
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Owner.MountedCenter, handPos, SweepHitWidth, ref _);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.4f, Pitch = 0.3f, MaxInstances = 3 }, target.Center);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center + Main.rand.NextVector2Circular(10f, 10f),
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 6f),
                    Color.Lerp(OverloadCommandCore.IonCyan, OverloadCommandCore.IonHot, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.7f, 1.1f))?.Configure(false, Main.rand.Next(10, 18));
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //余韵：骨架散作上浮离子屑
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(
                    Vector2.Lerp(RootPos(Owner), handPos, Main.rand.NextFloat()),
                    new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-2.2f, -0.8f)),
                    OverloadCommandCore.IonCyan, Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(false, Main.rand.Next(12, 22));
            }
        }

        //==================== 绘制：BoneArm 双骨节 + 原版臂贴图，BRelicIonArm 全息化 ====================

        public override bool PreDraw(ref Color lightColor) {
            Player owner = Owner;
            if (!owner.Alives() || MaterializeT <= 0.01f) {
                return false;
            }

            Main.instance.LoadNPC(HandNpcIds[ArmIndex]);
            Texture2D handTex = TextureAssets.Npc[HandNpcIds[ArmIndex]].Value;
            Texture2D boneTex = TextureAssets.BoneArm.Value;

            Vector2 root = RootPos(owner);
            Vector2 hand = handPos;
            Vector2 mid = (root + hand) * 0.5f;
            Vector2 along = hand - root;
            float len = along.Length();
            //肘弯取离玩家中心更远的一侧，骨架始终外拱
            Vector2 perp = along.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2) * len * 0.2f;
            Vector2 elbow = Vector2.DistanceSquared(mid + perp, owner.MountedCenter)
                > Vector2.DistanceSquared(mid - perp, owner.MountedCenter) ? mid + perp : mid - perp;

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Effect shader = EffectLoader.BRelicIonArm?.Value;
            float ghost = dissolveTimer > 0 ? MaterializeT : Math.Min(MaterializeT * 1.35f, 1f);
            //窗口尾声全息不稳定（远端 mp 值为 0 → 无闪烁，纯表现差异）
            float flicker = 0f;
            OverloadCorePlayer mp = owner.GetModPlayer<OverloadCorePlayer>();
            if (mp.OverloadActive && mp.OverloadTimer < 60) {
                flicker = 1f - mp.OverloadTimer / 60f;
            }

            //骨节两段
            DrawSegment(sb, shader, boneTex, root, elbow, ghost, flicker, 0.9f);
            DrawSegment(sb, shader, boneTex, elbow, hand, ghost, flicker, 0.8f);

            //挥击旋转拖影（残angle重画手端，读出"在转"而非"在平移"）
            int frames = HandFrameCounts[ArmIndex];
            Rectangle frameRec = handTex.Frame(1, frames, 0, handFrame);
            for (int i = 0; i < smearCount; i++) {
                float ang = smearAngles[i];
                Vector2 ghostPos = owner.MountedCenter + ang.ToRotationVector2()
                    * (IsPhysicalArm ? 94f : 82f);
                DrawHand(sb, shader, handTex, frameRec, frames, ghostPos,
                    ang + MathHelper.PiOver2, ghost, flicker, 0.22f * (i + 1) / smearCount);
            }

            //手端本体
            DrawHand(sb, shader, handTex, frameRec, frames, hand, handRot, ghost, flicker, 0.95f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        /// <summary>骨节：BoneArm 沿 from→to 拉伸，rotation 契约同原版臂绘制</summary>
        private void DrawSegment(SpriteBatch sb, Effect shader, Texture2D tex,
            Vector2 from, Vector2 to, float ghost, float flicker, float alpha) {
            Vector2 along = to - from;
            float dist = along.Length();
            if (dist < 4f) {
                return;
            }
            ApplyShader(shader, tex, new Vector2(0f, 1f), ghost, flicker, alpha);
            sb.Draw(tex, from - Main.screenPosition, null,
                shader == null ? OverloadCommandCore.IonCyan * (0.7f * alpha * ghost) : Color.White,
                along.ToRotation() - MathHelper.PiOver2,
                new Vector2(tex.Width * 0.5f, 0f),
                new Vector2(0.9f, dist / tex.Height), SpriteEffects.None, 0f);
        }

        private void DrawHand(SpriteBatch sb, Effect shader, Texture2D tex, Rectangle frameRec,
            int frameCount, Vector2 pos, float rot, float ghost, float flicker, float alpha) {
            ApplyShader(shader, tex,
                new Vector2(handFrame / (float)frameCount, 1f / frameCount), ghost, flicker, alpha);
            sb.Draw(tex, pos - Main.screenPosition, frameRec,
                shader == null ? OverloadCommandCore.IonCyan * (0.8f * alpha * ghost) : Color.White,
                rot, frameRec.Size() * 0.5f, 1f, SpriteEffects.None, 0f);
        }

        private void ApplyShader(Effect shader, Texture2D tex, Vector2 uvRow,
            float ghost, float flicker, float alpha) {
            if (shader == null) {
                return;
            }
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["uColor"]?.SetValue(OverloadCommandCore.IonCyan.ToVector3());
            shader.Parameters["uSecondaryColor"]?.SetValue(OverloadCommandCore.IonHot.ToVector3());
            shader.Parameters["uGhost"]?.SetValue(ghost);
            shader.Parameters["uSeed"]?.SetValue(ArmIndex * 0.37f + 0.13f);
            shader.Parameters["uAlpha"]?.SetValue(alpha);
            shader.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            shader.Parameters["uFlicker"]?.SetValue(flicker);
            shader.Parameters["uUvRow"]?.SetValue(uvRow);
            shader.CurrentTechnique.Passes[0].Apply();
        }
    }

    /// <summary>四臂点射离子弹：快直点射，飞行复利续力，命中电闪</summary>
    internal class OverloadIonBolt : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        public override void SetStaticDefaults() {
            //先注册再消费 oldPos，否则拖尾静默缺席
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 70;
            Projectile.extraUpdates = 1;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
        }

        public override void AI() {
            //飞行段复利续力：越飞越急，不做匀速贴图平移
            if (Projectile.velocity.Length() < 26f) {
                Projectile.velocity *= 1.012f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (!VaultUtils.isServer) {
                Lighting.AddLight(Projectile.Center, OverloadCommandCore.IonCyan.ToVector3() * 0.35f);
                if (Projectile.timeLeft % 6 == 0) {
                    PRTLoader.NewParticle<PRT_Light>(Projectile.Center,
                        -Projectile.velocity * 0.05f, OverloadCommandCore.IonCyan, 0.16f)
                        ?.Configure(10, opacity: 0.9f, squishStrenght: 1.6f);
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with {
                Volume = 0.3f, Pitch = 0.5f, MaxInstances = 3
            }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //撞击余韵：电火花+滞留光斑，痕迹活过弹体
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f),
                    OverloadCommandCore.IonCyan, Main.rand.NextFloat(0.6f, 1f))
                    ?.Configure(false, Main.rand.Next(10, 16));
            }
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero,
                OverloadCommandCore.IonHot, 0.3f)?.Configure(14, opacity: 1.1f, squishStrenght: 1f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98.Value;
            Vector2 origin = tex.Size() * 0.5f;
            float speedT = MathHelper.Clamp(Projectile.velocity.Length() / 26f, 0.4f, 1f);

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            //残影链：段带式衰减
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float fade = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 dpos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                sb.Draw(tex, dpos, null, OverloadCommandCore.IonDeep * (0.4f * fade),
                    Projectile.rotation + MathHelper.PiOver2, origin,
                    new Vector2(0.16f, 0.5f * speedT) * fade, SpriteEffects.None, 0f);
            }

            //弹体：速度拉伸双层梭形（宽青鞘+白热芯）
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            sb.Draw(tex, drawPos, null, OverloadCommandCore.IonCyan * 0.9f,
                Projectile.rotation + MathHelper.PiOver2, origin,
                new Vector2(0.22f, 0.9f * speedT + 0.25f), SpriteEffects.None, 0f);
            sb.Draw(tex, drawPos, null, OverloadCommandCore.IonHot,
                Projectile.rotation + MathHelper.PiOver2, origin,
                new Vector2(0.11f, 0.62f * speedT + 0.18f), SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}
