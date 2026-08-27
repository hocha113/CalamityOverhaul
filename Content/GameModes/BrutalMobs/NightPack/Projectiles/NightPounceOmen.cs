using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.NightPack.Projectiles
{
    /// <summary>
    /// 僵尸扑抓预兆：ai[0]=僵尸索引 ai[1]=落点X ai[2]=落点Y。
    /// 预告期显示落点标记与逐点亮起的跳弧，落点自生成帧锁死不再移动（预告即承诺）。
    /// 突进期本体保留为缓速判定窗载体（受害端 <see cref="NightPackNPC.OnHitPlayer"/> 扫描本实体），
    /// 永不造成伤害
    /// </summary>
    internal class NightPounceOmen : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>预告帧数（≥30 帧契约）</summary>
        internal const int TelegraphFrames = 34;
        /// <summary>突进判定窗帧数，覆盖各档位滞空与落地收势（最长为档位 1：滞空 36 + 收势 14）</summary>
        internal const int StrikeFrames = 50;
        internal const int TotalFrames = TelegraphFrames + StrikeFrames;

        /// <summary>落点标记宽度。画得比僵尸判定更宽，把原版 AI 空中残余转向（约 ±40px）也包进警示范围</summary>
        private const float MarkerWidth = 132f;
        private const float MarkerHeight = 44f;
        /// <summary>跳弧顶点抬升量，仅视觉</summary>
        private const float ArcApexLift = 92f;

        private static readonly Color RimDark = new Color(30, 42, 16);
        private static readonly Color CoreGreen = new Color(150, 205, 60, 0);

        private int AnchorIndex => (int)Projectile.ai[0];
        private Vector2 LockPoint => new Vector2(Projectile.ai[1], Projectile.ai[2]);
        private int Elapsed => TotalFrames - Projectile.timeLeft;
        internal bool InStrike => Elapsed >= TelegraphFrames;

        /// <summary>受害端判定：该僵尸当前是否处于扑抓突进窗（缓速只在此窗内挂）</summary>
        internal static bool IsStrikeWindowFor(int npcIndex) {
            int type = ModContent.ProjectileType<NightPounceOmen>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == type && (int)proj.ai[0] == npcIndex
                    && proj.ModProjectile is NightPounceOmen omen && omen.InStrike) {
                    return true;
                }
            }
            return false;
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 480;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
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
                Projectile.Center = LockPoint;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.ZombieMoan with { Volume = 0.72f, Pitch = 0.4f }, Projectile.Center);
                }
            }

            NPC anchor = AnchorIndex.TryGetNPC(out NPC a) ? a : null;

            //预告期僵尸死亡：攻击不会发生，预兆直接消散
            if (!InStrike && !anchor.Alives()) {
                Projectile.Kill();
                return;
            }

            //起跳帧：落点爆一圈警示尘 + 挥空声
            if (Elapsed == TelegraphFrames && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.6f, Pitch = -0.35f }, Projectile.Center);
                for (int i = 0; i < 5; i++) {
                    Dust dust = Dust.NewDustPerfect(LockPoint + Main.rand.NextVector2Circular(MarkerWidth * 0.4f, 8f),
                        DustID.GreenTorch, new Vector2(0f, -Main.rand.NextFloat(1f, 2.6f)), 120, default, Main.rand.NextFloat(0.8f, 1.3f));
                    dust.noGravity = true;
                }
            }

            //预告期落点渗出低频警示尘
            if (!InStrike && !VaultUtils.isServer && Main.rand.NextBool(3)) {
                float progress = Elapsed / (float)TelegraphFrames;
                Dust seep = Dust.NewDustPerfect(LockPoint + new Vector2(Main.rand.NextFloat(-0.5f, 0.5f) * MarkerWidth, 4f),
                    DustID.GreenTorch, new Vector2(0f, -0.4f - progress), 150, default, 0.7f + progress * 0.4f);
                seep.noGravity = true;
            }

            Lighting.AddLight(LockPoint, 0.12f, 0.2f, 0.05f);
        }

        public override bool PreDraw(ref Color lightColor) {
            float pulse = 0.6f + 0.4f * MathF.Sin(Main.GlobalTimeWrappedHourly * 10f + Projectile.identity * 0.9f);
            float fadeIn = MathHelper.Clamp(Elapsed / 8f, 0f, 1f);
            //突进期标记降为余痕，可见窗与判定窗同一实体
            float strength = InStrike
                ? MathHelper.Clamp(1f - (Elapsed - TelegraphFrames) / 16f, 0f, 1f) * 0.25f
                : fadeIn * (0.5f + 0.5f * pulse);
            //暮雾联动（只读 Woodsong 信号）：浓雾夜预告反而更醒目，萤火替猎物照出落点
            float fog = Ambience.Woodsong.WoodsongAmbience.FogStrength;
            if (!InStrike && fog > 0.15f) {
                strength = Math.Min(1f, strength * (1f + fog * 0.45f));
            }
            if (strength <= 0.01f) {
                return false;
            }

            Texture2D rim = CWRAsset.Extra_98.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 markerPos = LockPoint - Main.screenPosition;

            //暗色实底外圈（真 alpha 才能压亮背景）+ 加色芯
            Vector2 rimScale = new Vector2(MarkerWidth / rim.Width, MarkerHeight / rim.Height) * 1.15f;
            Main.EntitySpriteDraw(rim, markerPos, null, RimDark * (0.8f * strength), 0f,
                rim.Size() / 2f, rimScale, SpriteEffects.None, 0);
            Vector2 coreScale = new Vector2(MarkerWidth / glow.Width, MarkerHeight / glow.Height);
            Main.EntitySpriteDraw(glow, markerPos, null, CoreGreen * strength, 0f,
                glow.Size() / 2f, coreScale, SpriteEffects.None, 0);

            //雾夜萤火环：绕落点缓旋的冷绿光点（纯表现，各端本地按自身雾浓度绘制）
            if (!InStrike && fog > 0.15f) {
                Color firefly = new Color(186, 240, 120, 0);
                for (int i = 0; i < 5; i++) {
                    float a = Main.GlobalTimeWrappedHourly * 1.6f + i * MathHelper.TwoPi / 5f + Projectile.identity * 0.7f;
                    Vector2 p = markerPos + new Vector2(MathF.Cos(a) * MarkerWidth * 0.52f,
                        MathF.Sin(a * 1.4f) * 12f - 8f);
                    float twinkle = 0.55f + 0.45f * MathF.Sin(a * 3.1f);
                    Main.EntitySpriteDraw(glow, p, null, firefly * (fog * 0.7f * twinkle * strength), 0f,
                        glow.Size() / 2f, 0.05f, SpriteEffects.None, 0);
                }
            }

            //预告期跳弧：从僵尸到落点的点列随预告进度逐个亮起
            if (!InStrike && AnchorIndex.TryGetNPC(out NPC anchor) && anchor.Alives()) {
                float march = Elapsed / (float)TelegraphFrames * 1.15f;
                Vector2 start = anchor.Center - Main.screenPosition;
                Vector2 end = LockPoint - Main.screenPosition;
                for (int i = 1; i <= 7; i++) {
                    float t = i / 8f;
                    if (t > march) {
                        break;
                    }
                    Vector2 dot = Vector2.Lerp(start, end, t) - Vector2.UnitY * (ArcApexLift * 4f * t * (1f - t));
                    Main.EntitySpriteDraw(glow, dot, null, CoreGreen * (strength * 0.55f), 0f,
                        glow.Size() / 2f, 0.14f, SpriteEffects.None, 0);
                }
            }
            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => behindNPCsAndTiles.Add(index);
    }
}
