using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.UI;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel.CybCourses
{
    /// <summary>
    /// 全息训练标靶：超梦空间里由 SHPC 投影出来的骇入练习体。
    /// 材质=被扫描线维持的全息构造体（与天空/入场揭示同一套语言）：
    /// 线骨架剪影 + 胸口标靶环 + 上下往复的扫描线 + 偶发故障切片，
    /// 受损越重故障越频繁。站桩无 AI，锁位/保活由 HackTimeTutorialLead 负责
    /// </summary>
    internal class CybTrainingDummy : ModNPC
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //绘制几何（相对悬浮中心）
        private const float HeadR = 8f;
        private const float ChestRingR = 15f;
        private const float ChestRingR2 = 8f;
        private const float HoverRingR = 15f;
        private const float HoverGap = 6f;

        /// <summary>本地表现计时，不入同步</summary>
        private ref float AmbientClock => ref NPC.localAI[0];

        private float Seed => NPC.whoAmI * 0.7391f;

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.NPCBestiaryDrawModifiers hide = new() { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, hide);
            NPCID.Sets.MustAlwaysDraw[Type] = true;
        }

        public override void SetDefaults() {
            NPC.width = 36;
            NPC.height = 78;
            NPC.damage = 0;
            NPC.defense = 8;
            NPC.lifeMax = 2600;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.noGravity = true;
            NPC.noTileCollide = false;
            NPC.value = 0;
            NPC.npcSlots = 0f;
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath14 with { Volume = 0.45f, Pitch = 0.35f };
        }

        public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;

        public override bool CheckActive() => false;

        public override void AI() {
            NPC.velocity = Vector2.Zero;
            AmbientClock++;

            //构造体自发冷光
            Lighting.AddLight(NPC.Center, 0.09f, 0.26f, 0.30f);

            //数据尘：投影体缘不断有尘粒析出上浮
            if (!Main.dedServ && (int)AmbientClock % 24 == 0) {
                PRTLoader.NewParticle<PRT_CyberMote>(
                    NPC.Center + new Vector2(Main.rand.NextFloat(-14f, 14f), Main.rand.NextFloat(-30f, 30f)),
                    new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.7f)),
                    SHPCTheme.Cyan * 0.8f, Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(Main.rand.Next(40, 70));
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ) {
                return;
            }
            int count = NPC.life <= 0 ? 14 : 3;
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_CyberMote>(
                    NPC.Center + Main.rand.NextVector2Circular(16f, 34f),
                    Main.rand.NextVector2Circular(1.4f, 1.4f) - new Vector2(0f, 0.8f),
                    SHPCTheme.CyanHi * 0.9f, Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(Main.rand.Next(30, 55));
            }
        }

        //==================== 表现参数 ====================

        /// <summary>悬浮呼吸（纯绘制，判定框不动）</summary>
        private float BobOffset()
            => MathF.Sin(Main.GlobalTimeWrappedHourly * 1.4f + Seed) * 2.6f;

        /// <summary>故障窗口 0..1：低血量时更频繁更猛</summary>
        private float GlitchLevel() {
            float hurt = 1f - MathHelper.Clamp((float)NPC.life / Math.Max(NPC.lifeMax, 1), 0f, 1f);
            //周期抽签：每 ~3.2s 掷一次，命中则开 8 帧窗口
            float cycle = 192f;
            float n = MathF.Floor((AmbientClock + Seed * 60f) / cycle);
            float roll = MathF.Abs(MathF.Sin(n * 12.9898f + Seed * 78.233f));
            float gate = roll < 0.34f + hurt * 0.45f ? 1f : 0f;
            float inWin = (AmbientClock + Seed * 60f) % cycle < 8f ? 1f : 0f;
            return gate * inWin * (0.6f + hurt * 0.8f);
        }

        //==================== 绘制：底光 → 线骨架 → 标靶环 → 扫描线/故障 ====================

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (px == null || glow == null) {
                return false;
            }

            float glitch = GlitchLevel();
            float jitterX = glitch > 0f ? MathF.Sin(AmbientClock * 2.7f + Seed) * 2.2f * glitch : 0f;
            Vector2 center = NPC.Center + new Vector2(jitterX, BobOffset());
            Vector2 c = center - screenPos;

            float breath = 0.82f + 0.18f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.1f + Seed);
            Color line = SHPCTheme.Cyan * (0.85f * breath);
            Color lineHi = SHPCTheme.CyanHi * (0.95f * breath);

            //全息线骨架属加色材质，切 Additive 批
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Vector2 gOrigin = glow.Size() * 0.5f;

            //体积底光（下层，占比克制）
            spriteBatch.Draw(glow, c, null, SHPCTheme.Cyan * 0.16f, 0f, gOrigin,
                new Vector2(30f * 2f / glow.Width, 52f * 2f / glow.Height), SpriteEffects.None, 0f);

            float top = c.Y - NPC.height * 0.5f;
            float bottom = c.Y + NPC.height * 0.5f;

            //头环
            Vector2 headC = new(c.X, top + HeadR + 2f);
            SHPCRenderer.DrawArcStroke(spriteBatch, px, headC, HeadR, 0f, MathHelper.TwoPi, 1.4f, line);

            //肩线
            float shoulderY = headC.Y + HeadR + 7f;
            spriteBatch.Draw(px, new Rectangle((int)(c.X - 17f), (int)shoulderY, 34, 2), line);

            //脊线：肩到盆线
            float pelvisY = bottom - HoverGap - 12f;
            spriteBatch.Draw(px, new Rectangle((int)(c.X - 1f), (int)shoulderY, 2, (int)(pelvisY - shoulderY)), line * 0.9f);

            //盆线
            spriteBatch.Draw(px, new Rectangle((int)(c.X - 9f), (int)pelvisY, 18, 2), line);

            //胸口标靶环：外环慢转开口 + 内环 + 琥珀芯（全构造体唯一暖点）
            Vector2 chestC = new(c.X, shoulderY + 20f);
            float spin = Main.GlobalTimeWrappedHourly * 0.9f + Seed;
            SHPCRenderer.DrawArcStroke(spriteBatch, px, chestC, ChestRingR, spin, spin + MathHelper.TwoPi * 0.78f, 1.6f, line);
            SHPCRenderer.DrawArcStroke(spriteBatch, px, chestC, ChestRingR2, -spin * 0.7f, -spin * 0.7f + MathHelper.TwoPi * 0.85f, 1.2f, lineHi * 0.8f);
            spriteBatch.Draw(glow, chestC, null, SHPCTheme.Accent * (0.55f * breath), 0f, gOrigin,
                new Vector2(7f * 2f / glow.Width), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, new Rectangle((int)(chestC.X - 1f), (int)(chestC.Y - 1f), 2, 2), Color.White * (0.7f * breath));

            //悬浮环：脚下开口双弧对转 + 触地光斑
            Vector2 hoverC = new(c.X, bottom - 2f);
            float hspin = Main.GlobalTimeWrappedHourly * 1.6f;
            SHPCRenderer.DrawArcStroke(spriteBatch, px, hoverC, HoverRingR, hspin, hspin + MathHelper.Pi * 0.7f, 1.3f, line * 0.85f);
            SHPCRenderer.DrawArcStroke(spriteBatch, px, hoverC, HoverRingR, hspin + MathHelper.Pi, hspin + MathHelper.Pi * 1.7f, 1.3f, line * 0.85f);
            spriteBatch.Draw(glow, hoverC + new Vector2(0f, 6f), null, SHPCTheme.Cyan * 0.22f, 0f, gOrigin,
                new Vector2(24f * 2f / glow.Width, 7f / glow.Height), SpriteEffects.None, 0f);

            //扫描线：沿身高往复，维持投影的那道"当前行"
            float scanPhase = (Main.GlobalTimeWrappedHourly * 0.45f + Seed * 0.31f) % 1f;
            float scanY = MathHelper.Lerp(top, bottom - HoverGap, scanPhase);
            spriteBatch.Draw(px, new Rectangle((int)(c.X - 20f), (int)scanY, 40, 1), lineHi * 0.5f);
            spriteBatch.Draw(px, new Rectangle((int)(c.X - 20f), (int)scanY + 1, 40, 1), line * 0.18f);

            //故障切片：窗口内三条横带错位（受损越重越猛）
            if (glitch > 0.01f) {
                for (int i = 0; i < 3; i++) {
                    float sy = MathHelper.Lerp(top + 6f, bottom - 14f,
                        MathF.Abs(MathF.Sin(Seed * 3.1f + i * 1.7f + MathF.Floor(AmbientClock / 192f))));
                    float off = MathF.Sin(AmbientClock * 3.1f + i * 2.4f) * 4f * glitch;
                    spriteBatch.Draw(px, new Rectangle((int)(c.X - 18f + off), (int)sy, 36, 3), lineHi * (0.30f * glitch));
                }
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}
