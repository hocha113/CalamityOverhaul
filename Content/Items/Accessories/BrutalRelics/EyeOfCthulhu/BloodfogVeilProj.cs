using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.EyeOfCthulhu
{
    /// <summary>
    /// 血雾之瞳随身演出锚点：本身无伤害，承载跨端可见的血带/血盾/血茧/血流鞭。<br/>
    /// owner 端生成后经原生弹幕同步；时间轴按本端 AI 帧数推进，各端自演。<br/>
    /// ai[0]=模式：0 突进（蓄力起手即生成）/ 1 消隐点血爆+血流鞭 / 2 重凝落点血茧；ai[1]=总寿命(帧)；
    /// velocity=冲刺方向（ShouldUpdatePosition=false，只当同步载体）
    /// </summary>
    internal class BloodfogVeilProj : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal int Mode => (int)Projectile.ai[0];
        internal float TotalLife => Math.Max(Projectile.ai[1], 1f);
        /// <summary>时间轴帧号，首帧 AI 与玩家突进 elapsed=0 同帧</summary>
        private int Frame => (int)Projectile.localAI[1] - 1;
        private Vector2 DashDir => Projectile.velocity.SafeNormalize(Vector2.UnitX * Main.player[Projectile.owner].direction);

        #region 突进盾时间轴（帧，与 BloodfogIrisPlayer 的突进相位对齐）
        /// <summary>0..2 蓄力成形（弧顶先出）</summary>
        private const int FormFrames = BloodfogIrisPlayer.DashWindupFrames;
        /// <summary>起步帧：满形 + 过曝 + 血爆</summary>
        private const int LaunchFrame = BloodfogIrisPlayer.DashWindupFrames;
        /// <summary>急刹首帧：盾碎</summary>
        private const int BreakFrame = BloodfogIrisPlayer.DashWindupFrames + BloodfogIrisPlayer.DashTravelFrames;
        private const int BreakFrames = BloodfogIrisPlayer.DashBrakeFrames + 1;
        private const int ShieldEndFrame = BreakFrame + BreakFrames;
        private const float ShieldRadius = 30f;
        /// <summary>弧张角 ≈146°</summary>
        private const float ShieldSpan = 2.55f;
        private const float ShieldThick = 15f;
        #endregion

        #region 重凝时间轴（帧）
        /// <summary>血流鞭头到位帧数</summary>
        private const int StreamHeadFrames = 7;
        /// <summary>此后尾端开始龄蚀</summary>
        private const int StreamHoldFrame = 9;
        private const int StreamErodeFrames = 26;
        /// <summary>血茧起形帧（等血流到位）</summary>
        private const int CocoonFormFrame = 5;
        private const int CocoonFormFrames = 4;
        private const int CocoonBreakFrame = 13;
        private const int CocoonBreakFrames = 6;
        private const int CocoonEndFrame = CocoonBreakFrame + CocoonBreakFrames;
        private const float CocoonRadius = 24f;
        private const float CocoonThick = 9f;
        /// <summary>冲击环帧数</summary>
        private const int RingFrames = 8;
        #endregion

        /// <summary>起步过曝，逐帧 ×0.5</summary>
        private float flash;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 64;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.alpha = 255;
            //伏击窗口全程存在，晚入场玩家也要收到
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            int frame = (int)Projectile.localAI[1];
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = (int)TotalLife;
            }
            Projectile.localAI[1]++;
            flash *= 0.5f;

            if (Mode == 1) {
                UpdateBurst(frame);
                return;
            }

            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = owner.Center;

            //雾态视觉计时：各端(含服务端)每帧点亮，
            //服务端由 PostUpdateEquips 读它压仇恨，客户端驱动本体褪色
            owner.GetModPlayer<BloodfogIrisPlayer>().VeilVisualTimer = 2;

            if (Mode == 0) {
                UpdateDash(owner, frame);
            }
            else {
                UpdateRebirth(frame);
            }
            UpdateAmbient(owner, frame);
        }

        #region 逐模式演出拍
        private void UpdateDash(Player owner, int frame) {
            Vector2 dir = DashDir;
            if (frame == 0) {
                //蓄力吸气：血珠 4 帧内向前方盾位汇聚，起步帧刚好收完
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.5f, Pitch = -0.5f }, Projectile.Center);
                    BloodfogIrisFX.Converge(Projectile.Center + dir * 24f, 64f, 8, FormFrames + 1);
                }
            }
            else if (frame == LaunchFrame) {
                flash = 1f;
                BloodfogIrisFX.LaunchBurst(Projectile.Center, dir, 0.95f);
            }
            else if (frame > LaunchFrame && frame < BreakFrame) {
                if (frame % 2 == 0) {
                    BloodfogIrisFX.TipShed(Projectile.Center + dir * 10f, dir, ShieldRadius * (1f + BloodShieldMesh.TipFlare), ShieldSpan);
                }
            }
            else if (frame == BreakFrame) {
                BloodfogIrisFX.ShieldShatter(Projectile.Center + dir * 10f, dir, ShieldRadius, ShieldSpan);
            }
        }

        private void UpdateRebirth(int frame) {
            if (frame < CocoonFormFrame) {
                //血流未到，外圈血珠先汇，8 帧到位正落在血茧成形期
                BloodfogIrisFX.Converge(Projectile.Center, 110f, 2, 8);
            }
            else if (frame == CocoonFormFrame) {
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.7f, Pitch = -0.25f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Zombie2 with { Volume = 0.55f, Pitch = -0.6f }, Projectile.Center);
                }
            }
            else if (frame == CocoonBreakFrame) {
                BloodfogIrisFX.BloodBurst(Projectile.Center, 0.8f, playSound: false);
                EocMotion.Shake(Projectile.Center, 3.5f, 8);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.7f, Pitch = -0.1f }, Projectile.Center);
                }
            }
        }

        /// <summary>消隐点：躯体炸成血 + 表皮碎屑；血流鞭尾端蚀退时前沿掉血珠</summary>
        private void UpdateBurst(int frame) {
            if (frame == 0) {
                BloodfogIrisFX.BloodBurst(Projectile.Center, 1.5f);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie3 with { Volume = 0.75f, Pitch = -0.45f }, Projectile.Center);
                    for (int i = 0; i < 7; i++) {
                        Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 7f);
                        vel.Y -= 2.2f;
                        PRTLoader.NewParticle<PRT_EocSkinShred>(
                            Projectile.Center + Main.rand.NextVector2Circular(16f, 24f), vel,
                            Color.Lerp(EocMotion.Arterial, EocMotion.VenousDark, Main.rand.NextFloat()),
                            Main.rand.NextFloat(0.8f, 1.4f))?.Configure(Main.rand.Next(26, 44));
                    }
                }
                return;
            }
            if (VaultUtils.isServer || frame <= StreamHoldFrame || frame % 2 != 0) {
                return;
            }
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || !EocMotion.OnScreen(Projectile.Center, 520f)) {
                return;
            }
            float erode = MathHelper.Clamp((frame - StreamHoldFrame) / (float)StreamErodeFrames, 0f, 1f);
            if (erode >= 1f) {
                return;
            }
            Vector2 front = Vector2.Lerp(Projectile.Center, owner.Center, erode * 0.85f) + Main.rand.NextVector2Circular(10f, 10f);
            PRTLoader.NewParticle<PRT_HeartcarverDroplet>(front, new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(0.5f, 2f)),
                Color.Lerp(EocMotion.VenousDark, EocMotion.Arterial, Main.rand.NextFloat()), Main.rand.NextFloat(0.8f, 1.3f))?
                .Configure(Main.rand.Next(18, 30), 0.3f, 0.99f);
        }

        /// <summary>常驻：高速甩血、雾态滴血、心跳微光，纯客户端</summary>
        private void UpdateAmbient(Player owner, int frame) {
            if (VaultUtils.isServer || !EocMotion.OnScreen(Projectile.Center, 520f)) {
                return;
            }

            float speed = owner.velocity.Length();
            if (speed > 16f && Main.GameUpdateCount % 2 == 0) {
                Vector2 back = -owner.velocity.SafeNormalize(Vector2.Zero);
                Vector2 vel = back.RotatedBy(Main.rand.NextFloat(-0.6f, 0.6f)) * Main.rand.NextFloat(2f, 6f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(
                    owner.Center + Main.rand.NextVector2Circular(18f, 22f), vel,
                    Color.Lerp(EocMotion.Arterial, EocMotion.BrightBlood, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.8f, 1.4f))?.Configure(Main.rand.Next(16, 26), 0.3f, 0.98f);
            }

            int quietFrom = Mode == 0 ? ShieldEndFrame : CocoonEndFrame;
            if (frame >= quietFrom && frame % 7 == 0) {
                BloodfogIrisFX.Drip(owner);
            }

            float pulse = 0.55f + 0.25f * MathF.Sin((float)Main.timeForVisualEffects * 0.19f);
            float fade = MathHelper.Clamp(Projectile.timeLeft / 24f, 0f, 1f);
            Lighting.AddLight(Projectile.Center, EocMotion.MistWine.ToVector3() * pulse * 0.6f * fade);
        }
        #endregion

        #region 本体下层：血带 / 血流鞭 / 冲击环 / 瞳光
        public override bool PreDraw(ref Color lightColor) {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active) {
                return false;
            }
            if (Mode == 1) {
                DrawStream(owner);
                return false;
            }
            if (owner.dead) {
                return false;
            }
            if (owner.TryGetModPlayer(out BloodfogIrisPlayer mp)) {
                DrawPlayerTrail(owner, mp);
                DrawImpactRing();
                DrawPupilGlint(owner, mp);
            }
            return false;
        }

        /// <summary>盾碎 / 茧裂的冲击环，共享 ShockRing 血色板</summary>
        private void DrawImpactRing() {
            int start = Mode == 0 ? BreakFrame : CocoonBreakFrame;
            int k = Frame - start;
            if (k < 0 || k >= RingFrames) {
                return;
            }
            float p = (k + 0.5f) / RingFrames;
            float baseR = Mode == 0 ? 28f : 22f;
            float radius = baseR + VaultUtils.EaseOutCubic(p) * 56f;
            float alpha = (1f - p) * (1f - p) * 0.9f;
            Vector2 center = Mode == 0 ? Projectile.Center + DashDir * 10f : Projectile.Center;
            ShockRingDraw.Draw(Main.spriteBatch, center, radius, 5f,
                EocMotion.BrightBlood, EocMotion.Arterial, EocMotion.VenousDark, alpha,
                tearPx: 6f, squish: 1f, innerGlow: 0.15f, timeSeed: Projectile.whoAmI * 0.37f);
        }

        /// <summary>雾中瞳光：常亮微光(位置的公平线索) + 间歇一闪；突进冷却期微暗</summary>
        private void DrawPupilGlint(Player owner, BloodfogIrisPlayer mp) {
            float bloom = VaultUtils.EaseOutCubic(MathHelper.Clamp((Frame + 1) / 10f, 0f, 1f));
            float fade = MathHelper.Clamp(Projectile.timeLeft / 24f, 0f, 1f);
            float blink = MathF.Pow(MathF.Max(MathF.Sin(
                (float)Main.timeForVisualEffects * 0.11f + Projectile.whoAmI * 1.7f), 0f), 7f);
            float glow = (0.2f + blink * 0.8f) * bloom * fade;
            //可见冷却：突进转好前瞳光收敛(计时仅所有者端非零，远端不暗)
            if (mp.DashCooldown > 0) {
                glow *= 0.6f;
            }
            if (glow < 0.03f) {
                return;
            }

            Texture2D soft = CWRAsset.SoftGlow.Value;
            Texture2D flare = CWRAsset.StarFlare02.Value;
            Vector2 pos = owner.Center + new Vector2(owner.direction * 7f, -5f) - Main.screenPosition;
            //黑底贴图在预乘批里走 A=0 加色
            Color glintColor = EocMotion.IrisRed with { A = 0 };
            Main.spriteBatch.Draw(soft, pos, null, glintColor * (glow * 0.8f), 0f,
                soft.Size() / 2f, 0.4f * glow + 0.15f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(flare, pos, null, glintColor * (glow * 0.65f),
                Main.GlobalTimeWrappedHourly * 1.4f, flare.Size() / 2f, 0.11f * glow + 0.03f, SpriteEffects.None, 0f);
        }

        private static readonly List<Vector2> trailPos = new(64);
        private static readonly List<float> trailAge = new(64);

        /// <summary>突进血带三股：主带 + 两侧细股（不同淌速做视差，头端汇入本体）</summary>
        private static readonly BloodRibbonRenderer.StrandDef[] DashStrands = [
            new() { HalfWidth = 14f, PerpOffset = 0f, Seed = 0.13f, FlowMul = 1f, TearPx = 9f, OpacityMul = 1f, TailKeep = 1f },
            new() { HalfWidth = 7f, PerpOffset = 10f, Seed = 0.57f, FlowMul = 1.45f, TearPx = 7f, OpacityMul = 0.9f, TailKeep = 0.35f },
            new() { HalfWidth = 5f, PerpOffset = -11f, Seed = 0.91f, FlowMul = 0.8f, TearPx = 6f, OpacityMul = 0.85f, TailKeep = 0.35f },
        ];

        /// <summary>血流鞭三股：偏移为零，靠贝塞尔弯度各异编成绞股</summary>
        private static readonly BloodRibbonRenderer.StrandDef[] StreamStrands = [
            new() { HalfWidth = 9f, PerpOffset = 0f, Seed = 0.21f, FlowMul = 1.2f, TearPx = 7f, OpacityMul = 1f, TailKeep = 1f },
            new() { HalfWidth = 6f, PerpOffset = 0f, Seed = 0.66f, FlowMul = 1.6f, TearPx = 6f, OpacityMul = 0.9f, TailKeep = 1f },
            new() { HalfWidth = 5f, PerpOffset = 0f, Seed = 0.83f, FlowMul = 0.9f, TearPx = 6f, OpacityMul = 0.85f, TailKeep = 1f },
        ];
        private static readonly float[] StreamBends = [0.16f, -0.11f, 0.07f];
        private static readonly List<Vector2> streamPos = new(24);
        private static readonly List<float> streamAge = new(24);

        /// <summary>玩家血带：位移采样点 + 点龄，龄蚀成珠链由着色器完成；传送级断口只保留连着头的一段</summary>
        private static void DrawPlayerTrail(Player owner, BloodfogIrisPlayer mp) {
            float heat = mp.TrailHeat;
            if (heat <= 0.06f || mp.TrailPoints.Count < 2) {
                return;
            }

            trailPos.Clear();
            trailAge.Clear();
            long now = Main.GameUpdateCount;
            foreach (BloodfogIrisPlayer.TrailPoint p in mp.TrailPoints) {
                if (trailPos.Count > 0 && Vector2.DistanceSquared(p.Pos, trailPos[^1]) > 380f * 380f) {
                    trailPos.Clear();
                    trailAge.Clear();
                }
                trailPos.Add(p.Pos);
                trailAge.Add(1f - MathHelper.Clamp((p.DeathAt - now) / (float)BloodfogIrisPlayer.TrailPointLife, 0f, 1f));
            }
            if (Vector2.DistanceSquared(owner.Center, trailPos[^1]) > 380f * 380f) {
                return;
            }
            trailPos.Add(owner.Center);
            trailAge.Add(0f);
            if (trailPos.Count < 3) {
                return;
            }

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            if (!BloodRibbonRenderer.Begin(gd, out Effect fx, out BlendState pb, out RasterizerState pr, out DepthStencilState pd)) {
                DrawTrailFallback(heat);
                return;
            }
            foreach (BloodRibbonRenderer.StrandDef def in DashStrands) {
                BloodRibbonRenderer.DrawStrand(gd, fx, trailPos, trailAge, def, heat, 1f);
            }
            BloodRibbonRenderer.End(gd, pb, pr, pd);
        }

        /// <summary>缺 fxc 简笔：沿采样点画一串血珠（Extra_98 真 alpha），杜绝无形演出</summary>
        private static void DrawTrailFallback(float heat) {
            Texture2D bead = CWRAsset.Extra_98.Value;
            int n = trailPos.Count;
            for (int i = 0; i < n; i++) {
                float u = i / (float)(n - 1);
                float alive = 1f - trailAge[i];
                float scale = (0.25f + 0.35f * u) * heat * alive;
                if (scale < 0.04f) {
                    continue;
                }
                Color col = Color.Lerp(EocMotion.VenousDark, EocMotion.Arterial, u) * (0.85f * alive);
                Main.spriteBatch.Draw(bead, trailPos[i] - Main.screenPosition, null, col, u * 3f,
                    bead.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            }
        }

        /// <summary>血流鞭：消隐点 → 玩家现位，7 帧鞭到、9 帧后尾端龄蚀，三股不同弯度</summary>
        private void DrawStream(Player owner) {
            int f = Frame;
            if (f < 0) {
                return;
            }
            Vector2 from = Projectile.Center;
            Vector2 to = owner.Center;
            float len = Vector2.Distance(from, to);
            if (len < 24f) {
                return;
            }
            float headReveal = VaultUtils.EaseOutCubic(MathHelper.Clamp((f + 1) / (float)StreamHeadFrames, 0f, 1f));
            float erode = MathHelper.Clamp((f - StreamHoldFrame) / (float)StreamErodeFrames, 0f, 1f);
            if (erode >= 1f) {
                return;
            }
            Vector2 dir = (to - from) / len;
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            if (!BloodRibbonRenderer.Begin(gd, out Effect fx, out BlendState pb, out RasterizerState pr, out DepthStencilState pd)) {
                return;
            }
            const int Samples = 22;
            for (int s = 0; s < StreamStrands.Length; s++) {
                Vector2 ctrl = (from + to) * 0.5f + perp * (StreamBends[s] * len);
                streamPos.Clear();
                streamAge.Clear();
                for (int i = 0; i < Samples; i++) {
                    float t = i / (float)(Samples - 1);
                    Vector2 p = Vector2.Lerp(Vector2.Lerp(from, ctrl, t), Vector2.Lerp(ctrl, to, t), t);
                    streamPos.Add(p);
                    streamAge.Add(MathHelper.Clamp(erode * 1.5f - t * 0.5f, 0f, 1f));
                }
                BloodRibbonRenderer.DrawStrand(gd, fx, streamPos, streamAge, StreamStrands[s], 1f, 1f, headReveal);
            }
            BloodRibbonRenderer.End(gd, pb, pr, pd);
        }
        #endregion

        #region 本体上层：冲击血盾 / 血茧
        void IPrimitiveDrawable.DrawPrimitives() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead || Mode == 1) {
                return;
            }
            int f = Frame;
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            float seed = Projectile.whoAmI * 0.37f;

            if (Mode == 0) {
                if (f < 0 || f >= ShieldEndFrame) {
                    return;
                }
                float form = MathHelper.Clamp((f + 1) / (float)(FormFrames + 1), 0f, 1f);
                float brk = f >= BreakFrame
                    ? VaultUtils.EaseInQuad(MathHelper.Clamp((f - BreakFrame + 1) / (float)BreakFrames, 0f, 1f))
                    : 0f;
                if (!BloodShieldMesh.Begin(gd, out Effect fx, out BlendState pb, out RasterizerState pr, out DepthStencilState pd)) {
                    return;
                }
                //盾心随成形从体心向前推：血被挤到迎风面
                Vector2 center = owner.Center + DashDir * (4f + 6f * form);
                BloodShieldMesh.Draw(gd, fx, center, DashDir, ShieldRadius, ShieldSpan, ShieldThick, 1f,
                    form, brk, flash, 1f, seed, 1f);
                BloodShieldMesh.End(gd, pb, pr, pd);
                return;
            }

            if (f < CocoonFormFrame || f >= CocoonEndFrame) {
                return;
            }
            float cForm = MathHelper.Clamp((f - CocoonFormFrame + 1) / (float)CocoonFormFrames, 0f, 1f);
            float cBrk = f >= CocoonBreakFrame
                ? MathHelper.Clamp((f - CocoonBreakFrame + 1) / (float)CocoonBreakFrames, 0f, 1f)
                : 0f;
            if (!BloodShieldMesh.Begin(gd, out Effect cfx, out BlendState cpb, out RasterizerState cpr, out DepthStencilState cpd)) {
                return;
            }
            BloodShieldMesh.Draw(gd, cfx, owner.Center, Vector2.UnitX, CocoonRadius, MathHelper.TwoPi, CocoonThick, 0f,
                cForm, cBrk, 0f, 0.8f, seed, 0.95f);
            BloodShieldMesh.End(gd, cpb, cpr, cpd);
        }
        #endregion
    }

    /// <summary>
    /// 伏击印记：伏击命中点炸开的红色裂瞳，owner 命中钩子里生成、
    /// 经弹幕同步各端可见；纯演出无伤害
    /// </summary>
    internal class BloodfogAmbushMark : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int Life = 34;
        private float Progress => 1f - Projectile.timeLeft / (float)Life;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Life;
            Projectile.alpha = 255;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                PlayBurst();
            }
            Lighting.AddLight(Projectile.Center, EocMotion.IrisRed.ToVector3() * 0.8f * (1f - Progress));
        }

        /// <summary>命中拍：血爆 + 放射血滴 + 瞳色电花 + 湿裂响 + 血闪</summary>
        private void PlayBurst() {
            BloodfogIrisFX.BloodBurst(Projectile.Center, 1.2f, playSound: false);
            EocMotion.Shake(Projectile.Center, 5f, 10);
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.9f, Pitch = 0.22f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.75f, Pitch = -0.12f }, Projectile.Center);

            for (int i = 0; i < 10; i++) {
                float angle = MathHelper.TwoPi * i / 10f + Main.rand.NextFloat(0.3f);
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(5f, 11f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Projectile.Center, vel,
                    Color.Lerp(EocMotion.Arterial, EocMotion.BrightBlood, Main.rand.NextFloat()),
                    Main.rand.NextFloat(1f, 1.8f))?.Configure(Main.rand.Next(20, 34), 0.32f, 0.984f);
            }
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 8f);
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, vel,
                    EocMotion.IrisRed, Main.rand.NextFloat(0.8f, 1.3f))?.Configure(false, Main.rand.Next(10, 18));
            }
            if (EocMotion.OnScreen(Projectile.Center)) {
                BloodfogScreenFX.PushFlash(0.3f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float progress = Progress;
            SpriteBatch sb = Main.spriteBatch;
            Vector2 center = Projectile.Center - Main.screenPosition;
            Effect effect = EffectLoader.BRelicIrisMark?.Value;
            if (effect == null) {
                DrawFallbackSigil(sb, center, progress);
                return false;
            }

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            effect.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.03f);
            effect.Parameters["uProgress"]?.SetValue(progress);
            effect.Parameters["uIntensity"]?.SetValue(1f);
            //噪声显式绑 s1(shader 内 register(s1))
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            gd.Textures[1] = CWRAsset.PerlinNoise.Value;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            effect.CurrentTechnique.Passes[0].Apply();

            Texture2D pixel = VaultAsset.placeholder2.Value;
            float side = 300f * (0.85f + 0.35f * VaultUtils.EaseOutCubic(MathHelper.Clamp(progress * 2.4f, 0f, 1f)));
            Vector2 scale = new(side / pixel.Width, side / pixel.Height);
            sb.Draw(pixel, center, null, Color.White, 0f, pixel.Size() / 2f, scale, SpriteEffects.None, 0f);

            sb.End();
            gd.Textures[1] = null;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        /// <summary>缺 fxc 简笔：血环扩张 + 竖瞳暗芯 + 星芒，杜绝无形演出</summary>
        private void DrawFallbackSigil(SpriteBatch sb, Vector2 center, float progress) {
            float fade = 1f - VaultUtils.EaseInQuad(progress);
            //DiffusionCircle 真 alpha，可正常染色
            Texture2D disc = CWRAsset.DiffusionCircle.Value;
            float discScale = (60f + progress * 90f) * 2f / disc.Width;
            sb.Draw(disc, center, null, EocMotion.Arterial * (0.7f * fade), 0f,
                disc.Size() / 2f, discScale, SpriteEffects.None, 0f);
            //Extra_98 真 alpha，允许画暗竖瞳
            Texture2D dark = CWRAsset.Extra_98.Value;
            Vector2 slitScale = new(0.12f, 0.9f * (0.4f + progress * 0.6f));
            sb.Draw(dark, center, null, EocMotion.VenousDark * (0.85f * fade), 0f,
                dark.Size() / 2f, slitScale, SpriteEffects.None, 0f);
            //星芒走 A=0 加色
            Texture2D flare = CWRAsset.StarFlare02.Value;
            sb.Draw(flare, center, null, (EocMotion.IrisRed with { A = 0 }) * fade,
                progress * 1.6f, flare.Size() / 2f, 0.3f * (0.5f + progress), SpriteEffects.None, 0f);
        }
    }
}
