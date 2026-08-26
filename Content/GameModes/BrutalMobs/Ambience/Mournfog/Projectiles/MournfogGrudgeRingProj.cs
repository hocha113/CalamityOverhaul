using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Mournfog.Projectiles
{
    /// <summary>
    /// 怨聚鬼火环。ai[0]=目标玩家 ai[1]=档位(1~3)。
    /// 空间围拢型敌意（与地牢"被凝视"计量型划清）：十二只怨火在锚点四周成环显形，
    /// 聚形预告 80 帧（转红 + 低语渐清，公平契约 ≥45）→ 缓慢收环（可见环=判定环，
    /// 环带扫过身体才咬人）→ 合拢穿身施加短暂寒颤 + 微量伤害 → 鬼火散灭。
    /// 目标移出环径六成六、离开墓地、死亡或 Boss 入场即消散（无伤退场）。
    /// 时间轴由 ai 值在各端确定性展开（镜像 WastesIceSlickZone）；命中在受害者本机结算
    /// </summary>
    internal class MournfogGrudgeRingProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        [VaultLoaden(CWRConstant.Other)]
        private static Asset<Texture2D> SoulFire = null;

        /// <summary>聚形预告帧数（公平契约 ≥45，各档位一律不缩短）</summary>
        private const int TelegraphFrames = 80;
        /// <summary>收环速度（px/帧），档位只调合拢速度</summary>
        private static readonly float[] CloseSpeedByTier = [1.05f, 1.3f, 1.55f];
        /// <summary>起始环半径</summary>
        private const float StartRadius = 430f;
        /// <summary>环带判定半宽（不宽于鬼火可见体）</summary>
        private const float BandHalfWidth = 22f;
        /// <summary>合拢后散灭帧数</summary>
        private const int FadeFrames = 36;
        /// <summary>无伤消散帧数（逃逸/失效）</summary>
        private const int DissolveFrames = 26;
        /// <summary>穿身寒颤时长（短暂）</summary>
        private const int ChillFrames = 130;
        /// <summary>环径小于此值后不再判定逃逸（收势已成）</summary>
        private const float EscapeMinRadius = 70f;
        /// <summary>逃逸阈值 = 环径 × 此系数（下限 150px）</summary>
        private const float EscapeFrac = 0.66f;
        /// <summary>环上鬼火只数</summary>
        private const int WispCount = 12;

        private int Tier => Math.Clamp((int)Projectile.ai[1], 1, 3);
        private float CloseSpeed => CloseSpeedByTier[Tier - 1];
        private int CloseFrames => (int)MathF.Ceiling(StartRadius / CloseSpeed);
        private int CloseEnd => TelegraphFrames + CloseFrames;
        private int TotalLife => CloseEnd + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        /// <summary>消散计数：0=未消散，>0 起为无伤退场（各端各自判定，受害者端读数最准）</summary>
        private ref float Dissolving => ref Projectile.localAI[0];
        private ref float Inited => ref Projectile.localAI[1];

        private Player TargetPlayer {
            get {
                int idx = (int)Projectile.ai[0];
                return idx >= 0 && idx < Main.maxPlayers ? Main.player[idx] : null;
            }
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 520;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = false;//收环期才置真
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>当前环半径：由时间轴确定性展开，绘制与判定读同一几何</summary>
        private float RadiusAt(int elapsed) {
            if (elapsed <= TelegraphFrames) {
                return StartRadius;
            }
            return MathF.Max(StartRadius - CloseSpeed * (elapsed - TelegraphFrames), 0f);
        }

        /// <summary>转红度：聚形期 0→1，此后恒 1</summary>
        private float RedShiftAt(int elapsed) {
            float x = MathHelper.Clamp(elapsed / (float)TelegraphFrames, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        public override void AI() {
            if (Inited == 0f) {
                Inited = 1f;
                //存续期由 ai[1] 决定，两端以同一 ai 值各自展开时间轴
                Projectile.timeLeft = TotalLife;
            }
            int elapsed = Elapsed;
            float radius = RadiusAt(elapsed);
            Player target = TargetPlayer;

            //消散判定（合拢完成前有效）：目标失效 / 离开墓地 / 移动足够距离 / Boss 入场。
            //受害者端用本机精确位置判定自身逃逸，公平性以受害者视角为准
            if (Dissolving == 0f && elapsed < CloseEnd) {
                bool invalid = target == null || !target.active || target.dead
                    || !target.ZoneGraveyard || CWRWorld.HasBoss;
                bool escaped = !invalid && radius > EscapeMinRadius
                    && target.Center.Distance(Projectile.Center) > MathF.Max(radius * EscapeFrac, 150f);
                if (invalid || escaped) {
                    Dissolving = 1f;
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.LiquidsWaterLava
                            with { Volume = 0.28f, Pitch = -0.1f, MaxInstances = 3 }, Projectile.Center);
                    }
                }
            }

            if (Dissolving > 0f) {
                Projectile.hostile = false;
                Dissolving++;
                //余灰（≤1 粒/2 帧）
                if (!Main.dedServ && Dissolving < DissolveFrames && Main.rand.NextBool(2)) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    Dust dust = Dust.NewDustPerfect(Projectile.Center + ang.ToRotationVector2() * radius,
                        DustID.Shadowflame, new Vector2(0f, -Main.rand.NextFloat(0.4f, 1f)), 160, default, 0.8f);
                    dust.noGravity = true;
                }
                if (Dissolving > DissolveFrames + 2 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Projectile.Kill();
                }
                return;
            }

            //判定窗=收环窗：环带扫过才咬人，无需临时清伤害（伤害字段恒满，见联机契约）
            Projectile.hostile = elapsed >= TelegraphFrames && elapsed < CloseEnd && radius > 4f;

            if (!Main.dedServ) {
                RunBeats(elapsed, radius);
            }

            //环上取 4 点照明（省预算）
            float red = RedShiftAt(elapsed);
            Vector3 light = Vector3.Lerp(new Vector3(0.06f, 0.14f, 0.08f),
                new Vector3(0.2f, 0.05f, 0.04f), red);
            for (int i = 0; i < 4; i++) {
                float ang = MathHelper.PiOver2 * i + elapsed * 0.004f;
                Lighting.AddLight(Projectile.Center + ang.ToRotationVector2() * radius, light);
            }
        }

        /// <summary>声音与粉尘节拍（确定性帧点，各端本机播，位置衰减免费给旁观者）</summary>
        private void RunBeats(int elapsed, float radius) {
            //低语渐清三拍 + 收环启动拍（视觉+听觉双通道预告）
            if (elapsed == 8) {
                SoundEngine.PlaySound(SoundID.NPCHit36
                    with { Volume = 0.26f, Pitch = -0.35f, MaxInstances = 3 }, Projectile.Center);
            }
            else if (elapsed == 36) {
                SoundEngine.PlaySound(SoundID.NPCHit36
                    with { Volume = 0.36f, Pitch = -0.12f, MaxInstances = 3 }, Projectile.Center);
            }
            else if (elapsed == 64) {
                SoundEngine.PlaySound(SoundID.NPCDeath39
                    with { Volume = 0.5f, Pitch = -0.3f, MaxInstances = 3 }, Projectile.Center);
            }
            else if (elapsed == TelegraphFrames) {
                SoundEngine.PlaySound(SoundID.NPCDeath39
                    with { Volume = 0.55f, Pitch = -0.05f, MaxInstances = 3 }, Projectile.Center);
            }

            if (elapsed > TelegraphFrames && elapsed < CloseEnd) {
                //收环期低语越来越近、越来越清
                float progress = (elapsed - TelegraphFrames) / (float)CloseFrames;
                if ((elapsed - TelegraphFrames) % 40 == 0) {
                    SoundEngine.PlaySound(SoundID.NPCHit36
                        with { Volume = 0.22f + 0.16f * progress, Pitch = -0.3f + 0.45f * progress, MaxInstances = 3 },
                        Projectile.Center);
                }
            }

            if (elapsed == CloseEnd) {
                //合拢帧：散灭嘶声 + 怨火爆散
                SoundEngine.PlaySound(SoundID.LiquidsWaterLava
                    with { Volume = 0.45f, Pitch = -0.2f, MaxInstances = 3 }, Projectile.Center);
                for (int i = 0; i < 14; i++) {
                    float ang = MathHelper.TwoPi * i / 14f;
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame,
                        ang.ToRotationVector2() * Main.rand.NextFloat(1.5f, 4f), 130, default,
                        Main.rand.NextFloat(0.9f, 1.4f));
                    dust.noGravity = true;
                }
            }

            //环缘怨尘（≤1 粒/2 帧，落在判定半径上，强化环=判定的读法）
            if (elapsed < CloseEnd && radius > 4f && Main.rand.NextBool(2)) {
                float red = RedShiftAt(elapsed);
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Dust dust = Dust.NewDustPerfect(Projectile.Center + ang.ToRotationVector2() * radius,
                    Main.rand.NextFloat() < red ? DustID.RedTorch : DustID.CursedTorch,
                    new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.8f)), 150, default, 0.85f);
                dust.noGravity = true;
            }
        }

        /// <summary>环带判定：矩形到环心的最近/最远距离与 [半径±半宽] 区间求交</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Projectile.hostile) {
                return false;
            }
            float radius = RadiusAt(Elapsed);
            Vector2 center = Projectile.Center;
            float nearX = MathHelper.Clamp(center.X, targetHitbox.Left, targetHitbox.Right);
            float nearY = MathHelper.Clamp(center.Y, targetHitbox.Top, targetHitbox.Bottom);
            float nearDist = center.Distance(new Vector2(nearX, nearY));
            float farX = Math.Abs(center.X - targetHitbox.Left) > Math.Abs(center.X - targetHitbox.Right)
                ? targetHitbox.Left : targetHitbox.Right;
            float farY = Math.Abs(center.Y - targetHitbox.Top) > Math.Abs(center.Y - targetHitbox.Bottom)
                ? targetHitbox.Top : targetHitbox.Bottom;
            float farDist = center.Distance(new Vector2(farX, farY));
            return nearDist <= radius + BandHalfWidth && farDist >= radius - BandHalfWidth;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            //命中钩子只在受害者本机跑：寒颤走原版 buff 同步
            target.AddBuff(BuffID.Chilled, ChillFrames);
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.DeerclopsIceAttack
                with { Volume = 0.5f, Pitch = -0.15f, MaxInstances = 3 }, target.Center);
            for (int i = 0; i < 10; i++) {
                Dust dust = Dust.NewDustPerfect(target.Center, DustID.Shadowflame,
                    Main.rand.NextVector2Circular(3f, 2.4f) - new Vector2(0f, 1f), 120, default,
                    Main.rand.NextFloat(1f, 1.5f));
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D flame = SoulFire?.Value;
            if (flame == null) {
                return false;
            }
            int elapsed = Elapsed;
            float radius = RadiusAt(elapsed);
            float red = RedShiftAt(elapsed);
            float dissolveFade = Dissolving > 0f
                ? MathHelper.Clamp(1f - Dissolving / DissolveFrames, 0f, 1f) : 1f;
            if (dissolveFade <= 0.01f) {
                return false;
            }

            //合拢后的散灭段：怨火向外上方飘散熄灭
            float scatterT = 0f;
            if (elapsed >= CloseEnd) {
                scatterT = MathHelper.Clamp((elapsed - CloseEnd) / (float)FadeFrames, 0f, 1f);
                radius = 4f + scatterT * 54f;
            }

            Texture2D glow = CWRAsset.SoftGlow.Value;
            float time = Main.GlobalTimeWrappedHourly;
            float spin = elapsed * 0.004f;
            bool closing = elapsed >= TelegraphFrames && elapsed < CloseEnd && Dissolving == 0f;

            for (int i = 0; i < WispCount; i++) {
                //聚形期逐只错拍显形
                float appear = MathHelper.Clamp((elapsed - i * 4) / 20f, 0f, 1f);
                if (appear <= 0f) {
                    continue;
                }
                float hash = (Projectile.identity * 7 + i * 13) % 17 / 17f * MathHelper.TwoPi;
                float ang = MathHelper.TwoPi * i / WispCount + spin + MathF.Sin(time * 1.7f + hash) * 0.05f;
                float rJit = MathF.Sin(time * 3.1f + hash * 2f) * 9f;
                Vector2 dir = ang.ToRotationVector2();
                Vector2 pos = Projectile.Center + dir * (radius + rJit);
                if (scatterT > 0f) {
                    pos.Y -= scatterT * 34f;
                }

                float flick = 0.82f + 0.18f * MathF.Sin(time * 7f + hash * 5f);
                float alpha = appear * dissolveFade * flick * (1f - scatterT * scatterT);
                if (alpha <= 0.02f) {
                    continue;
                }
                int frame = ((elapsed / 5) + i * 2) % 5;
                Rectangle frameRect = flame.GetRectangle(frame, 5);
                Vector2 orig = frameRect.Size() * 0.5f;
                float sway = MathF.Sin(time * 2.3f + hash * 3f) * 0.1f;
                Vector2 screenPos = pos - Main.screenPosition;

                //收环期的向心运动拖影（各向异性：怨火拖出体后的余焰）
                if (closing) {
                    Color smear = new Color(150, 40, 30) * (0.30f * alpha);
                    Main.EntitySpriteDraw(flame, screenPos + dir * (CloseSpeed * 5f), frameRect,
                        smear, sway, orig, 0.56f, SpriteEffects.None, 0);
                    Main.EntitySpriteDraw(flame, screenPos + dir * (CloseSpeed * 10f), frameRect,
                        smear * 0.45f, sway, orig, 0.5f, SpriteEffects.None, 0);
                }

                //垫晕（黑底图，A=0 只加光）
                Color halo = Color.Lerp(new Color(66, 150, 86, 0), new Color(200, 58, 38, 0), red)
                    * (0.34f * alpha);
                Main.EntitySpriteDraw(glow, screenPos, null, halo, 0f, glow.Size() * 0.5f,
                    0.55f, SpriteEffects.None, 0);

                //火体：绿相→怨烬压暗
                Color body = Color.Lerp(new Color(150, 255, 170), new Color(118, 44, 40), red)
                    * (0.88f * alpha);
                Main.EntitySpriteDraw(flame, screenPos, frameRect, body, sway, orig,
                    0.62f, SpriteEffects.None, 0);

                //怨红芯（A=0 加色，点亮白芯）
                if (red > 0.02f) {
                    Color core = new Color(255, 84, 52, 0) * (0.8f * alpha * red);
                    Main.EntitySpriteDraw(flame, screenPos, frameRect, core, sway, orig,
                        0.62f, SpriteEffects.None, 0);
                }
            }
            return false;
        }
    }
}
