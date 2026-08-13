using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 献祭锁阵：投技全程的世界空间演出载体（收拢环/束缚阵/导流丝/幻龙掠影/远古光汇聚），
    /// 各端按本地状态机计时同走时间轴，旁观者可见完整动作；
    /// ai[0]=本体whoAmI；无伤害纯演出
    /// </summary>
    internal class CultistSacrificeSeal : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //献祭红（锁阵/束缚）与远古蓝金（幻龙/终结）双色语言
        private static readonly Color SealRed = new(255, 70, 60);
        private static readonly Color SealGold = new(255, 170, 90);
        private static readonly Color AncientBlue = new(110, 180, 255);
        private static readonly Color AncientGold = new(255, 230, 150);

        private const int FadeTime = 24;

        //状态退出后的淡出计时（各端本地）
        private float fadeTimer;
        //上帧观察到的判定结果，用于扑空碎裂一次性演出
        private int lastResult;
        //幻龙拍点/终结的一次性音画闩
        private bool beat1Fired, beat2Fired, finaleFired, snapFired;

        private NPC Boss {
            get {
                int idx = (int)Projectile.ai[0];
                if (idx < 0 || idx >= Main.maxNPCs) {
                    return null;
                }
                NPC boss = Main.npc[idx];
                return boss.active && boss.type == NPCID.CultistBoss ? boss : null;
            }
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 60;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 900;
            Projectile.netImportant = true;
        }

        /// <summary>取本体覆写与投技状态本地计时，任一无效返回 false</summary>
        private bool TryGetGrab(out CultistBossAI bossOverride, out int t, out int result) {
            bossOverride = null;
            t = 0;
            result = 0;
            NPC boss = Boss;
            if (boss == null || !boss.TryGetOverride(out bossOverride) || bossOverride?.Context == null) {
                return false;
            }
            if (bossOverride.Machine?.CurrentState is not CultistSacrificeGrabState grabState) {
                return false;
            }
            t = grabState.Timer;
            result = bossOverride.Context.GrabResult;
            return true;
        }

        public override void AI() {
            if (Boss == null) {
                Projectile.Kill();
                return;
            }

            bool active = TryGetGrab(out CultistBossAI bossOverride, out int t, out int result);
            if (!active) {
                //状态已离开：淡出收场
                fadeTimer++;
                if (fadeTimer >= FadeTime) {
                    Projectile.Kill();
                }
                return;
            }

            CultistStateContext ctx = bossOverride.Context;
            //阵心跟随吊升曲线（各端同算，无需同步）
            Projectile.Center = CultistSacrificeGrabState.SealCenter(ctx, t);
            Projectile.velocity = Vector2.Zero;

            Lighting.AddLight(Projectile.Center, SealRed.ToVector3() * 0.9f);

            //扑空碎裂：结果跳变到 2 的一次性演出
            if (result == 2 && lastResult != 2) {
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Shatter with { Volume = 1f, Pitch = 0.25f }, Projectile.Center);
                    for (int i = 0; i < 20; i++) {
                        PRTLoader.NewParticle<PRT_CultistShard>(
                            Projectile.Center + Main.rand.NextVector2Circular(CultistSacrificeGrabState.SealRadius, CultistSacrificeGrabState.SealRadius * 0.5f),
                            Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 10f),
                            SealRed, Main.rand.NextFloat(0.7f, 1.4f))?.Configure(Main.rand.Next(22, 38));
                    }
                }
            }
            lastResult = result;
            //扑空残阵衰减（表现量，AI 内推进避免绘制帧率耦合）
            if (result == 2) {
                whiffAge = Math.Min(whiffAge + 0.045f, 1f);
            }

            if (VaultUtils.isServer) {
                return;
            }

            //——以下全为客户端音画——
            if (result == 0 && t < CultistSacrificeGrabState.SealCloseEnd) {
                //收拢期：符文向锁阵中心汇聚（密度随读秒攀升，72%后静默）
                float p = t / (float)CultistSacrificeGrabState.SealCloseEnd;
                if (p < 0.72f) {
                    CultistRenderHelper.ConvergeRunes(Projectile.Center, CultistSacrificeGrabState.CloseRadius(t) + 120f,
                        ctx.Element, 0.5f + p);
                    if (Main.rand.NextBool(3)) {
                        PRTLoader.NewParticle<PRT_CultistRune>(
                            Projectile.Center + Main.rand.NextVector2CircularEdge(CultistSacrificeGrabState.CloseRadius(t), CultistSacrificeGrabState.CloseRadius(t)),
                            Vector2.Zero, SealRed, Main.rand.NextFloat(0.8f, 1.3f))?.Configure(Projectile.Center, 0.12f, 26);
                    }
                }
                return;
            }

            if (result != 1) {
                return;
            }

            //锁身瞬间：束缚阵成型爆点
            if (!snapFired && t >= CultistSacrificeGrabState.SealCloseEnd + 1) {
                snapFired = true;
                PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero, SealGold, 0.1f)?.Configure(0.1f, 1.3f, 16);
                for (int i = 0; i < 12; i++) {
                    float angle = MathHelper.TwoPi * i / 12f;
                    PRTLoader.NewParticle<PRT_CultistRune>(Projectile.Center + angle.ToRotationVector2() * 30f,
                        angle.ToRotationVector2() * 5f, SealRed, 1.2f)
                        ?.Configure(Projectile.Center + angle.ToRotationVector2() * CultistSacrificeGrabState.SealRadius * 0.6f, 0.14f, 20);
                }
            }

            //幻龙拍点：前摇警告与掠过爆发
            HandleBeatAudio(t, CultistSacrificeGrabState.Beat1Hit, ref beat1Fired, -1);
            HandleBeatAudio(t, CultistSacrificeGrabState.Beat2Hit, ref beat2Fired, 1);
            //持续被吸食的低鸣（锁身期间隔奏）
            if (t > CultistSacrificeGrabState.SnapEnd && t < CultistSacrificeGrabState.FinaleChargeStart && t % 26 == 0) {
                CultistRenderHelper.ChantVoice(Projectile.Center, 0.55f, -0.35f);
            }

            //终结蓄力：汇聚符文+吟唱升调，末 8 帧静默收缩（尖啸前吸气）
            if (t >= CultistSacrificeGrabState.FinaleChargeStart && t < CultistSacrificeGrabState.FinaleHit) {
                float charge = (t - CultistSacrificeGrabState.FinaleChargeStart)
                    / (float)(CultistSacrificeGrabState.FinaleHit - CultistSacrificeGrabState.FinaleChargeStart);
                if (charge < 0.72f) {
                    CultistRenderHelper.ConvergeRunes(Projectile.Center, 560f, ctx.Element, 0.8f + charge);
                    if (t % 12 == 0) {
                        CultistRenderHelper.ChantVoice(Projectile.Center, 0.9f, MathHelper.Lerp(-0.1f, 0.5f, charge));
                    }
                }
            }

            //终结引爆：远古光爆芒（屏幕级闪震由状态推送）
            if (!finaleFired && t >= CultistSacrificeGrabState.FinaleHit) {
                finaleFired = true;
                CultistRenderHelper.ElementImpact(Projectile.Center, ctx.Element, 2.4f);
                PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero, AncientGold, 0.14f)?.Configure(0.14f, 1.8f, 22);
                PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero, AncientBlue, 0.08f)?.Configure(0.08f, 1.2f, 18);
                for (int i = 0; i < 26; i++) {
                    Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(4f, 14f);
                    PRTLoader.NewParticle<PRT_CultistShard>(Projectile.Center, vel,
                        Main.rand.NextBool() ? AncientGold : SealRed,
                        Main.rand.NextFloat(0.8f, 1.6f))?.Configure(Main.rand.Next(26, 44));
                }
            }
        }

        /// <summary>拍点听觉：前摇 26 帧龙吟警告，命中帧咆哮+烬带</summary>
        private void HandleBeatAudio(int t, int hitTick, ref bool fired, int side) {
            if (t == hitTick - 26) {
                SoundEngine.PlaySound(SoundID.Zombie102 with { Volume = 0.9f, Pitch = -0.15f }, Projectile.Center);
            }
            if (!fired && t >= hitTick) {
                fired = true;
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.95f, Pitch = 0.35f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.8f, Pitch = -0.2f }, Projectile.Center);
                Vector2 dir = SweepDir(side);
                for (int i = 0; i < 16; i++) {
                    Vector2 pos = Projectile.Center + dir * Main.rand.NextFloat(-120f, 120f)
                        + dir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-26f, 26f);
                    PRTLoader.NewParticle<PRT_CultistEmber>(pos, dir * Main.rand.NextFloat(6f, 15f),
                        AncientBlue, Main.rand.NextFloat(0.8f, 1.4f))?.Configure(Main.rand.Next(14, 26));
                }
            }
        }

        /// <summary>掠影方向：拍1左上→右下，拍2右上→左下（确定性）</summary>
        private static Vector2 SweepDir(int side) {
            float angle = side < 0 ? 0.62f : MathHelper.Pi - 0.62f;
            return angle.ToRotationVector2();
        }

        public override bool PreDraw(ref Color lightColor) {
            NPC boss = Boss;
            if (boss == null) {
                return false;
            }
            float fade = 1f - MathHelper.Clamp(fadeTimer / FadeTime, 0f, 1f);
            if (fade <= 0.01f) {
                return false;
            }
            if (!TryGetGrab(out CultistBossAI bossOverride, out int t, out int result)) {
                //状态已退：画淡出残阵
                CultistRenderHelper.DrawSigil(Main.spriteBatch, Projectile.Center, CultistSacrificeGrabState.SealRadius,
                    CultistElement.Fire, fade, Main.GlobalTimeWrappedHourly * 2f, 0f, 1f - fade, fade * 0.6f);
                return false;
            }
            CultistStateContext ctx = bossOverride.Context;

            if (result == 0 && t < CultistSacrificeGrabState.SealCloseEnd + 6) {
                DrawTelegraph(ctx, t);
            }
            else if (result == 1) {
                DrawBound(ctx, t);
            }
            else if (result == 2) {
                //扑空：残阵快速碎散
                float crumble = 1f - whiffAge;
                CultistRenderHelper.DrawSigil(Main.spriteBatch, Projectile.Center, CultistSacrificeGrabState.SealRadius,
                    ctx.Element, 1f, Main.GlobalTimeWrappedHourly * 3f, 0.3f, whiffAge, crumble * 0.8f);
            }
            return false;
        }

        //扑空残阵衰减进度 0→1（AI 推进）
        private float whiffAge;

        /// <summary>收拢期：外环收拢+判定边界常显+读秒刻度</summary>
        private void DrawTelegraph(CultistStateContext ctx, int t) {
            SpriteBatch sb = Main.spriteBatch;
            float p = MathHelper.Clamp(t / (float)CultistSacrificeGrabState.SealCloseEnd, 0f, 1f);
            Vector2 center = Projectile.Center - Main.screenPosition;

            CultistRenderHelper.BeginAdditive(sb);
            Texture2D ring = CWRAsset.DiffusionCircle.Value;
            float ringHalf = ring.Width * 0.5f;

            //判定边界：从第一帧就常显的暗红内环（危险区与判定精确对齐）
            float pulse = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 9f);
            sb.Draw(ring, center, null, SealRed * (0.28f + 0.14f * pulse + 0.35f * p), -Main.GlobalTimeWrappedHourly * 1.5f,
                ring.Size() / 2f, CultistSacrificeGrabState.SealRadius / ringHalf, SpriteEffects.None, 0f);

            //收拢外环：亮金读秒环，越收越亮
            float closeR = CultistSacrificeGrabState.CloseRadius(t);
            sb.Draw(ring, center, null, SealGold * (0.35f + 0.5f * p), Main.GlobalTimeWrappedHourly * 3f,
                ring.Size() / 2f, closeR / ringHalf, SpriteEffects.None, 0f);
            sb.Draw(ring, center, null, SealRed * (0.25f + 0.3f * p), Main.GlobalTimeWrappedHourly * 3f,
                ring.Size() / 2f, closeR * 1.06f / ringHalf, SpriteEffects.None, 0f);

            //收拢环缘的旋转符标（8枚小光点沿环缘）
            Texture2D glow = CWRAsset.SoftGlow.Value;
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f + t * 0.05f;
                Vector2 pos = center + angle.ToRotationVector2() * closeR;
                sb.Draw(glow, pos, null, SealGold * (0.5f + 0.4f * p), 0f, glow.Size() / 2f, 0.22f, SpriteEffects.None, 0f);
            }
            CultistRenderHelper.EndAdditive(sb);

            //地面法阵随读秒成型
            CultistRenderHelper.DrawSigil(sb, Projectile.Center, CultistSacrificeGrabState.SealRadius,
                ctx.Element, p, t * 0.06f, p > 0.9f ? (p - 0.9f) * 10f : 0f, 0f, 0.55f + 0.45f * p);
        }

        /// <summary>锁身期：束缚阵+导流丝+幻龙掠影+终结汇聚</summary>
        private void DrawBound(CultistStateContext ctx, int t) {
            SpriteBatch sb = Main.spriteBatch;

            //束缚基阵：紧缩到锁身尺度，献祭进行时持续燃烧
            float bindFlash = t < CultistSacrificeGrabState.SnapEnd ? 1f - (t - CultistSacrificeGrabState.SealCloseEnd) / 12f : 0f;
            CultistRenderHelper.DrawSigil(sb, Projectile.Center, 96f, ctx.Element,
                1f, t * 0.045f, MathHelper.Clamp(bindFlash, 0f, 1f), 0f, 0.9f);

            CultistRenderHelper.BeginAdditive(sb);

            //受害者束缚环：双环反旋（锁扣感）
            Vector2 center = Projectile.Center - Main.screenPosition;
            Texture2D ring = CWRAsset.DiffusionCircle.Value;
            float ringHalf = ring.Width * 0.5f;
            sb.Draw(ring, center, null, SealRed * 0.55f, t * 0.08f, ring.Size() / 2f, 64f / ringHalf, SpriteEffects.None, 0f);
            sb.Draw(ring, center, null, SealGold * 0.4f, -t * 0.06f, ring.Size() / 2f, 84f / ringHalf, SpriteEffects.None, 0f);

            //导流丝：受害者→环上每一席（能量倒流，吸珠流向教徒）
            int slotCount = CultistSacrificeGrabState.RingSlotCount(ctx);
            for (int slot = 0; slot < slotCount; slot++) {
                Vector2 anchor = CultistSacrificeGrabState.RingSlotPos(ctx, t, slot, slotCount);
                DrawTether(sb, Projectile.Center, anchor, t, slot);
            }

            //幻龙掠影两拍
            DrawDragonSweep(sb, t, CultistSacrificeGrabState.Beat1Hit, -1);
            DrawDragonSweep(sb, t, CultistSacrificeGrabState.Beat2Hit, 1);

            //终结：远古光矛汇聚+核心球
            if (t >= CultistSacrificeGrabState.FinaleChargeStart && t <= CultistSacrificeGrabState.FinaleHit + 10) {
                DrawFinale(sb, t);
            }

            CultistRenderHelper.EndAdditive(sb);

            //核心充能球用现成 shader 球（加色批外调用，内部自管批次）
            if (t >= CultistSacrificeGrabState.FinaleChargeStart && t < CultistSacrificeGrabState.FinaleHit) {
                float charge = (t - CultistSacrificeGrabState.FinaleChargeStart)
                    / (float)(CultistSacrificeGrabState.FinaleHit - CultistSacrificeGrabState.FinaleChargeStart);
                //爆前 8 帧收缩 40%：越小越响
                int toHit = CultistSacrificeGrabState.FinaleHit - t;
                float collapse = toHit <= 8 ? MathHelper.Lerp(1f, 0.4f, (8 - toHit) / 8f) : 1f;
                float radius = MathHelper.Lerp(10f, 130f, charge * charge * charge) * collapse;
                CultistRenderHelper.DrawOrb(sb, Projectile.Center, radius, ctx.Element, 0.4f + 0.6f * charge,
                    charge > 0.9f ? (charge - 0.9f) * 10f : 0f, Projectile.identity * 0.37f);
            }
        }

        /// <summary>单条导流丝：宽暗衬+窄亮芯+吸珠流向环上教徒</summary>
        private void DrawTether(SpriteBatch sb, Vector2 from, Vector2 to, int t, int seed) {
            Texture2D line = CWRAsset.LightShot.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 span = to - from;
            float len = span.Length();
            if (len < 24f) {
                return;
            }
            float rot = span.ToRotation();
            Vector2 start = from - Main.screenPosition;
            Vector2 origin = new(0f, line.Height / 2f);
            float pulseT = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 5f + seed * 1.7f);
            sb.Draw(line, start, null, SealRed * 0.16f, rot, origin, new Vector2(len / line.Width, 0.3f), SpriteEffects.None, 0f);
            sb.Draw(line, start, null, SealGold * (0.2f + 0.12f * pulseT), rot, origin, new Vector2(len / line.Width, 0.11f), SpriteEffects.None, 0f);
            //吸珠：从受害者滑向教徒（能量被夺的方向叙事）
            for (int i = 0; i < 2; i++) {
                float ft = (Main.GlobalTimeWrappedHourly * 0.7f + i * 0.5f + seed * 0.23f) % 1f;
                Vector2 beadPos = start + rot.ToRotationVector2() * (len * ft);
                float beadFade = (float)Math.Sin(ft * MathHelper.Pi);
                sb.Draw(glow, beadPos, null, SealGold * (0.8f * beadFade), 0f, glow.Size() / 2f, 0.14f + 0.06f * beadFade, SpriteEffects.None, 0f);
            }
        }

        /// <summary>幻龙掠影：前摇警戒线→原版幻影龙鬼影贯穿阵心（调用方须处于加色批）</summary>
        private void DrawDragonSweep(SpriteBatch sb, int t, int hitTick, int side) {
            int rel = t - hitTick;
            Vector2 dir = SweepDir(side);
            Vector2 center = Projectile.Center;

            //前摇 26 帧：贯穿阵心的警戒线，亮度爬升
            if (rel >= -26 && rel < 0) {
                float warm = (rel + 26) / 26f;
                Texture2D line = CWRAsset.LightShot.Value;
                Vector2 a = center - dir * 900f - Main.screenPosition;
                float rot = dir.ToRotation();
                sb.Draw(line, a, null, AncientBlue * (0.12f + 0.3f * warm * warm), rot,
                    new Vector2(0f, line.Height / 2f), new Vector2(1800f / line.Width, 0.08f + 0.06f * warm), SpriteEffects.None, 0f);
            }

            //掠过窗口：鬼影龙头+体节沿线冲过（-12..+16，从阵外 600px 掠入）
            if (rel >= -12 && rel <= 16) {
                Main.instance.LoadNPC(NPCID.CultistDragonHead);
                Main.instance.LoadNPC(NPCID.CultistDragonBody1);
                Texture2D headTex = TextureAssets.Npc[NPCID.CultistDragonHead].Value;
                Texture2D bodyTex = TextureAssets.Npc[NPCID.CultistDragonBody1].Value;
                int headFrames = Math.Max(Main.npcFrameCount[NPCID.CultistDragonHead], 1);
                int bodyFrames = Math.Max(Main.npcFrameCount[NPCID.CultistDragonBody1], 1);
                Rectangle headSrc = new(0, 0, headTex.Width, headTex.Height / headFrames);
                Rectangle bodySrc = new(0, 0, bodyTex.Width, bodyTex.Height / bodyFrames);

                //头部位置：命中帧恰好过阵心
                Vector2 headPos = center + dir * (rel * 52f);
                //原版蠕虫贴图头朝下，行进旋转 = 方向-PiOver2 再补 Pi
                float rot = dir.ToRotation() + MathHelper.PiOver2;
                float ghost = 0.75f * (1f - Math.Abs(rel) / 18f);
                sb.Draw(headTex, headPos - Main.screenPosition, headSrc, AncientBlue * ghost, rot,
                    headSrc.Size() / 2f, 1.15f, SpriteEffects.None, 0f);
                sb.Draw(headTex, headPos - Main.screenPosition, headSrc, AncientGold * (ghost * 0.5f), rot,
                    headSrc.Size() / 2f, 1.3f, SpriteEffects.None, 0f);
                for (int i = 1; i <= 5; i++) {
                    Vector2 segPos = headPos - dir * (i * 44f);
                    float segGhost = ghost * (1f - i * 0.15f);
                    if (segGhost <= 0.02f) {
                        continue;
                    }
                    sb.Draw(bodyTex, segPos - Main.screenPosition, bodySrc, AncientBlue * segGhost, rot,
                        bodySrc.Size() / 2f, 1.1f, SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>终结汇聚：12 道远古光矛从环外指向阵心，随充能亮起推进（调用方须处于加色批）</summary>
        private void DrawFinale(SpriteBatch sb, int t) {
            float charge = MathHelper.Clamp((t - CultistSacrificeGrabState.FinaleChargeStart)
                / (float)(CultistSacrificeGrabState.FinaleHit - CultistSacrificeGrabState.FinaleChargeStart), 0f, 1f);
            bool exploded = t >= CultistSacrificeGrabState.FinaleHit;
            Texture2D star = CWRAsset.StarGlow01.Value;
            Vector2 center = Projectile.Center - Main.screenPosition;

            int lanceCount = 12;
            for (int i = 0; i < lanceCount; i++) {
                float angle = MathHelper.TwoPi * i / lanceCount + t * 0.006f;
                //矛尖随充能从远处推近阵心
                float tipDist = MathHelper.Lerp(520f, 150f, charge * charge);
                float lanceLen = MathHelper.Lerp(90f, 240f, charge);
                if (exploded) {
                    //爆后：光矛贯入阵心闪灭
                    float outFade = 1f - MathHelper.Clamp((t - CultistSacrificeGrabState.FinaleHit) / 10f, 0f, 1f);
                    tipDist = 20f;
                    lanceLen = 300f * outFade;
                    if (lanceLen < 8f) {
                        continue;
                    }
                }
                Vector2 tip = center + angle.ToRotationVector2() * tipDist;
                float rot = angle + MathHelper.Pi;   //指向阵心
                float bright = exploded ? 0.9f : 0.25f + 0.65f * charge;
                sb.Draw(star, tip, null, AncientBlue * (bright * 0.8f), rot,
                    new Vector2(0f, star.Height / 2f), new Vector2(lanceLen / star.Width * 1.6f, 0.3f), SpriteEffects.None, 0f);
                sb.Draw(star, tip, null, AncientGold * bright, rot,
                    new Vector2(0f, star.Height / 2f), new Vector2(lanceLen / star.Width * 1.2f, 0.14f), SpriteEffects.None, 0f);
            }
        }
    }
}
