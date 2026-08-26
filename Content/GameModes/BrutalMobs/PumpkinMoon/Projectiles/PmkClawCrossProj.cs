using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.PumpkinMoon.Projectiles
{
    /// <summary>
    /// 南瓜王爪击十字：ai[0]=锚定南瓜王索引 ai[1]/ai[2]=锁定坐标（0,0=未锁定）。
    /// 追踪期各端直读目标坐标确定性跟随，锁定帧冻结（预告即承诺），权威端写 ai[1/2] 作纠偏；
    /// 迟入端首帧见非零锁定坐标即快进相位（不重放追踪）。四臂沿正十字爪现，
    /// 臂半宽/臂长具名且判定循环直接读取（公平阀门），对角象限恒为安全区。
    /// 判定窗=爪现可见窗；锁定前锚死亡即取消
    /// </summary>
    internal class PmkClawCrossProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "MaskLaserLine";

        /// <summary>追踪帧+锁定帧=预告总时长（小 Boss 契约 ≥40）</summary>
        internal const int TrackFrames = 20;
        internal const int LockFrames = 26;
        internal const int StrikeFrames = 16;
        /// <summary>爪臂撑满臂长的帧数</summary>
        private const int ReachFrames = 8;
        internal const int FadeFrames = 14;
        internal const int TotalFrames = TrackFrames + LockFrames + StrikeFrames + FadeFrames;
        /// <summary>臂长与臂半宽（判定与绘制共用，对角象限恒安全的具名边界）</summary>
        private const float ArmLength = 196f;
        private const float ArmHalfWidth = 22f;

        private static readonly Color CrossWarn = new Color(255, 140, 44, 0);
        private static readonly Color CrossHot = new Color(255, 232, 190, 0);

        private int AnchorIndex => (int)Projectile.ai[0];
        private bool HasLock => Projectile.ai[1] != 0f || Projectile.ai[2] != 0f;
        private int Elapsed => (int)Projectile.localAI[1] - Projectile.timeLeft;
        private bool InStrike => Elapsed >= TrackFrames + LockFrames && Elapsed < TrackFrames + LockFrames + StrikeFrames;
        private bool Locked => Elapsed >= TrackFrames;

        /// <summary>爪臂伸展度 0~1</summary>
        private float Reach {
            get {
                int t = Elapsed - TrackFrames - LockFrames;
                if (t <= 0) {
                    return 0f;
                }
                if (t >= ReachFrames) {
                    return 1f;
                }
                float x = t / (float)ReachFrames;
                return 1f - (1f - x) * (1f - x);
            }
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 480;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.localAI[1] = TotalFrames;
                //迟入端：首帧已带锁定坐标 = 权威端早过锁定帧，本地相位快进到锁定起点（不重放追踪）
                Projectile.timeLeft = HasLock ? LockFrames + StrikeFrames + FadeFrames : TotalFrames;
            }

            int elapsed = Elapsed;

            //锁定前锚校验（index+type）：南瓜王没了则爪击取消（反制有效）
            if (elapsed < TrackFrames + LockFrames) {
                NPC anchor = AnchorIndex.TryGetNPC(out NPC a) ? a : null;
                if (!anchor.Alives() || anchor.type != NPCID.Pumpking) {
                    Projectile.Kill();
                    return;
                }
                if (HasLock) {
                    //权威端已写入锁定坐标（或迟入端快进）：坐标冻结
                    Projectile.Center = new Vector2(Projectile.ai[1], Projectile.ai[2]);
                }
                else if (elapsed < TrackFrames) {
                    //追踪期：直读目标坐标（各端从同步数据确定性推得）
                    int target = anchor.target;
                    if (target >= 0 && target < Main.maxPlayers) {
                        Player player = Main.player[target];
                        if (player.Alives()) {
                            Projectile.Center = player.Center;
                        }
                    }
                }
                //锁定帧：权威端把冻结坐标写回 ai 作各端纠偏
                if (elapsed == TrackFrames && !VaultUtils.isClient && !HasLock) {
                    Projectile.ai[1] = Projectile.Center.X;
                    Projectile.ai[2] = Projectile.Center.Y;
                    Projectile.netUpdate = true;
                }
            }

            //判定窗=爪现可见窗
            Projectile.hostile = InStrike;

            if (elapsed == TrackFrames && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.55f, Pitch = -0.25f }, Projectile.Center);
            }
            if (elapsed == TrackFrames + LockFrames && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.9f, Pitch = -0.45f }, Projectile.Center);
                //爆发帧粉尘 ≤6 粒（性能红线）：四臂各一 + 中心两粒
                for (int arm = 0; arm < 4; arm++) {
                    Vector2 dir = (arm * MathHelper.PiOver2).ToRotationVector2();
                    Dust slash = Dust.NewDustPerfect(Projectile.Center + dir * 40f, DustID.Torch,
                        dir * Main.rand.NextFloat(4f, 7f), 90, default, Main.rand.NextFloat(1.2f, 1.7f));
                    slash.noGravity = true;
                }
                for (int i = 0; i < 2; i++) {
                    Dust core = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                        Main.rand.NextVector2Circular(2f, 2f), 90, default, 1.4f);
                    core.noGravity = true;
                }
            }

            if (!Main.dedServ) {
                float glow = InStrike ? 0.6f : Locked ? 0.35f : 0.2f;
                Lighting.AddLight(Projectile.Center, 1f * glow, 0.55f * glow, 0.2f * glow);
            }
        }

        /// <summary>四臂×三段取样：判定几何与绘制共用 ArmLength/ArmHalfWidth（对角象限恒安全）</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Projectile.hostile) {
                return false;
            }
            float reach = ArmLength * Reach;
            if (reach < 10f) {
                return false;
            }
            //中心格
            if (Utils.CenteredRectangle(Projectile.Center, new Vector2(ArmHalfWidth * 2f)).Intersects(targetHitbox)) {
                return true;
            }
            for (int arm = 0; arm < 4; arm++) {
                Vector2 dir = (arm * MathHelper.PiOver2).ToRotationVector2();
                for (int i = 0; i < 3; i++) {
                    Vector2 point = Projectile.Center + dir * reach * (0.2f + 0.33f * i);
                    Rectangle sample = Utils.CenteredRectangle(point, new Vector2(ArmHalfWidth * 2f));
                    if (sample.Intersects(targetHitbox)) {
                        return true;
                    }
                }
            }
            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.OnFire, 150);
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float fadeIn = MathHelper.Clamp(elapsed / 8f, 0f, 1f);
            float strength;
            if (elapsed >= TrackFrames + LockFrames + StrikeFrames) {
                strength = MathHelper.Clamp(1f - (elapsed - TrackFrames - LockFrames - StrikeFrames) / (float)FadeFrames, 0f, 1f) * 0.3f;
            }
            else if (InStrike) {
                strength = 1f;
            }
            else {
                strength = fadeIn * (Locked ? 0.9f : 0.5f);
            }
            if (strength <= 0.01f) {
                return false;
            }

            Texture2D lane = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 laneOrigin = new Vector2(0f, lane.Height / 2f);
            float laneScaleX = ArmLength / lane.Width;
            float pulse = 0.65f + 0.35f * MathF.Sin(Main.GlobalTimeWrappedHourly * 11f + Projectile.identity);
            //锁定期白热脉冲宣告承诺
            float lockFlash = Locked && !InStrike
                ? 0.75f + 0.25f * MathF.Sin((elapsed - TrackFrames) / (float)LockFrames * MathHelper.Pi * 5f) : 1f;

            //四条臂道
            for (int arm = 0; arm < 4; arm++) {
                float armAngle = arm * MathHelper.PiOver2;
                Color laneColor = CrossWarn * (0.42f * strength * pulse * lockFlash);
                Main.EntitySpriteDraw(lane, drawPos, null, laneColor, armAngle, laneOrigin,
                    new Vector2(laneScaleX, ArmHalfWidth * 2f / lane.Height), SpriteEffects.None, 0);
                if (Locked && !InStrike) {
                    Main.EntitySpriteDraw(lane, drawPos, null, CrossHot * (0.5f * strength * lockFlash), armAngle,
                        laneOrigin, new Vector2(laneScaleX, 10f / lane.Height), SpriteEffects.None, 0);
                }
            }

            //中心印记
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Main.EntitySpriteDraw(glow, drawPos, null, CrossWarn * (0.7f * strength * pulse), 0f,
                glow.Size() / 2f, 0.45f + 0.15f * pulse, SpriteEffects.None, 0);

            //爪现：原版南瓜王镰刃臂贴图沿四臂扫出（实体层）
            if (InStrike || elapsed >= TrackFrames + LockFrames) {
                float reach = ArmLength * Reach;
                float strikeAlpha = InStrike ? 1f : strength;
                Main.instance.LoadNPC(NPCID.PumpkingBlade);
                Texture2D claw = TextureAssets.Npc[NPCID.PumpkingBlade].Value;
                int frameCount = Math.Max(1, Main.npcFrameCount[NPCID.PumpkingBlade]);
                Rectangle clawFrame = claw.Frame(1, frameCount, 0, 0);
                Vector2 clawOrigin = clawFrame.Size() / 2f;
                for (int arm = 0; arm < 4; arm++) {
                    float armAngle = arm * MathHelper.PiOver2;
                    Vector2 dir = armAngle.ToRotationVector2();
                    Vector2 clawPos = drawPos + dir * reach * 0.62f;
                    Color clawColor = Color.Lerp(lightColor, Color.White, 0.45f) * (0.95f * strikeAlpha);
                    Main.EntitySpriteDraw(claw, clawPos, clawFrame, clawColor, armAngle + MathHelper.PiOver2,
                        clawOrigin, 0.9f, SpriteEffects.None, 0);
                    //臂道炽闪
                    Main.EntitySpriteDraw(lane, drawPos, null, CrossHot * (0.6f * strikeAlpha), armAngle, laneOrigin,
                        new Vector2(reach / lane.Width, ArmHalfWidth * 1.6f / lane.Height), SpriteEffects.None, 0);
                }
            }
            return false;
        }
    }
}
