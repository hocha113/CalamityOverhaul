using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.DungeonDeep.Projectiles
{
    /// <summary>
    /// Diabolist 地狱火柱：目标脚下的地面预告 ≥34 帧（暗座+升腾余烬）→ 喷发。
    /// ai[0]=来源打包（槽位+1|类型&lt;&lt;8） ai[1]=柱半宽 ai[2]=柱高（红袍宽矮/白袍窄高的型差随生成包同步）。
    /// 落点在生成帧锁死（预告即承诺）；伤害窗=喷发可见期，且判定高度随火焰视觉同帧升起。
    /// 预告期施法者死亡/槽位复用/吟唱被打断则取消喷发（火柱被 NPC 侧或来源校验撤回）
    /// </summary>
    internal class DdFirePillarProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>地面预告帧数（契约 ≥34，档位不缩短）</summary>
        internal const int WarnFrames = 36;
        /// <summary>喷发帧数（伤害窗=此窗）</summary>
        internal const int EruptFrames = 44;
        private const int FadeFrames = 12;
        /// <summary>喷发升柱帧：火焰在此帧数内窜到全高，判定高度同步爬升</summary>
        private const int RiseFrames = 8;

        private int SourcePacked => (int)Projectile.ai[0];
        private int AnchorIndex => (SourcePacked & 255) - 1;
        private float HalfWidth => Projectile.ai[1];
        private float PillarHeight => Projectile.ai[2];
        private int TotalLife => WarnFrames + EruptFrames + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        private bool Cancelled {
            get => Projectile.localAI[1] == 1f;
            set => Projectile.localAI[1] = value ? 1f : 0f;
        }

        private bool Erupting => !Cancelled && Elapsed >= WarnFrames && Elapsed < WarnFrames + EruptFrames;

        /// <summary>当前可见火柱高度（喷发升柱期逐帧爬升；判定与绘制共用=伤害窗即可见窗）</summary>
        private float VisibleHeight {
            get {
                if (!Erupting) {
                    return 0f;
                }
                float t = MathHelper.Clamp((Elapsed - WarnFrames) / (float)RiseFrames, 0f, 1f);
                return PillarHeight * t;
            }
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 700;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = false;//喷发帧才置真（伤害窗=可见窗，双保险见 CanDamage）
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = WarnFrames + EruptFrames + FadeFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>伤害窗=喷发可见期</summary>
        public override bool? CanDamage() => Erupting ? null : false;

        /// <summary>柱形判定：底座向上 VisibleHeight 高、双向 HalfWidth 宽的矩形</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Erupting) {
                return false;
            }
            float height = VisibleHeight;
            Rectangle column = new((int)(Projectile.Center.X - HalfWidth), (int)(Projectile.Center.Y - height),
                (int)(HalfWidth * 2f), (int)height);
            return column.Intersects(targetHitbox);
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.45f, Pitch = -0.5f, MaxInstances = 4 }, Projectile.Center);
                }
            }
            int elapsed = Elapsed;

            //来源校验：预告期施法者倒下则火不喷（击杀施法者=有效反制）；喷发后火已出膛不再收回
            if (!Cancelled && elapsed < WarnFrames) {
                if (AnchorIndex < 0 || AnchorIndex >= Main.maxNPCs || !Main.npc[AnchorIndex].active
                    || Main.npc[AnchorIndex].type != SourcePacked >> 8) {
                    Cancelled = true;
                    //取消即快进到淡出（各端由同步的 npc.active 确定性得到同一结论）
                    Projectile.timeLeft = Math.Min(Projectile.timeLeft, FadeFrames);
                }
            }

            Projectile.hostile = Erupting;

            if (Main.dedServ) {
                return;
            }
            if (!Cancelled && elapsed < WarnFrames) {
                //预告期：升腾余烬（≤3 粒/帧）
                if (Main.rand.NextBool(2)) {
                    Dust ember = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-HalfWidth, HalfWidth), -Main.rand.NextFloat(6f)),
                        DustID.Torch, new Vector2(0f, -Main.rand.NextFloat(1.5f, 3.2f)), 120, default, 1.1f);
                    ember.noGravity = true;
                }
                float progress = elapsed / (float)WarnFrames;
                Lighting.AddLight(Projectile.Center, 0.5f * progress, 0.22f * progress, 0.06f * progress);
            }
            else if (Erupting) {
                if (elapsed == WarnFrames) {
                    SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.8f, Pitch = -0.15f, MaxInstances = 4 }, Projectile.Center);
                }
                //喷发期：火舌密集上窜（≤6 粒/帧）
                for (int i = 0; i < 3; i++) {
                    if (!Main.rand.NextBool(2)) {
                        continue;
                    }
                    Dust flame = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-HalfWidth, HalfWidth), 0f),
                        DustID.Torch, new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(4f, 9f)),
                        90, default, Main.rand.NextFloat(1.4f, 2.1f));
                    flame.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center - Vector2.UnitY * VisibleHeight * 0.5f, 0.9f, 0.42f, 0.12f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            Texture2D core = CWRAsset.Extra_98.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 basePos = Projectile.Center - Main.screenPosition;
            float pulse = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 14f + Projectile.identity);

            if (elapsed < WarnFrames) {
                float progress = MathHelper.Clamp(elapsed / (float)WarnFrames, 0f, 1f);
                float fade = Cancelled ? 0.3f * (1f - progress) : MathHelper.Clamp(elapsed / 8f, 0f, 1f);
                if (fade <= 0.02f) {
                    return false;
                }
                //暗座（真透暗底=有遮挡像素）+ 渐亮核 + 升腾预热光（高度指示柱形范围）
                Main.EntitySpriteDraw(core, basePos, null, new Color(70, 24, 14, 220) * (0.8f * fade), 0f,
                    core.Size() / 2f, new Vector2(HalfWidth * 2.4f / core.Width, 0.14f), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, basePos, null,
                    new Color(255, 130, 50, 0) * ((0.3f + 0.5f * progress) * fade * pulse), 0f,
                    glow.Size() / 2f, new Vector2(HalfWidth * 2f / glow.Width, 0.16f), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, basePos - Vector2.UnitY * (PillarHeight * 0.25f * progress), null,
                    new Color(255, 110, 40, 0) * (0.30f * progress * fade * pulse), 0f, glow.Size() / 2f,
                    new Vector2(HalfWidth * 1.2f / glow.Width, PillarHeight * 0.5f * progress / glow.Height),
                    SpriteEffects.None, 0);
                return false;
            }

            if (Erupting) {
                float height = VisibleHeight;
                float endT = MathHelper.Clamp((WarnFrames + EruptFrames - elapsed) / 10f, 0f, 1f);
                Vector2 bottomOrigin = new(core.Width / 2f, core.Height);
                //暗外壳（真透贴图全 alpha ×1.18 宽）→ 亮焰 → 白热芯（A=0），伤害判定高度=此可见高度
                Main.EntitySpriteDraw(core, basePos, null, new Color(70, 22, 12, 240) * endT, 0f, bottomOrigin,
                    new Vector2(HalfWidth * 2f * 1.18f / core.Width, height / core.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(core, basePos, null, (new Color(255, 150, 60, 0)) * (0.85f * endT * pulse), 0f,
                    bottomOrigin, new Vector2(HalfWidth * 1.6f / core.Width, height * 0.96f / core.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(core, basePos, null, (new Color(255, 235, 190, 0)) * (0.7f * endT), 0f,
                    bottomOrigin, new Vector2(HalfWidth * 0.8f / core.Width, height * 0.9f / core.Height), SpriteEffects.None, 0);
                //底部余光
                Main.EntitySpriteDraw(glow, basePos, null, new Color(255, 140, 60, 0) * (0.5f * endT * pulse), 0f,
                    glow.Size() / 2f, new Vector2(HalfWidth * 3f / glow.Width, 0.2f), SpriteEffects.None, 0);
                return false;
            }

            //收尾淡出（无判定）
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / (float)FadeFrames, 0f, 1f);
            Main.EntitySpriteDraw(glow, basePos, null, new Color(255, 120, 50, 0) * (0.3f * fadeOut), 0f,
                glow.Size() / 2f, new Vector2(HalfWidth * 2f / glow.Width, 0.14f), SpriteEffects.None, 0);
            return false;
        }
    }
}
