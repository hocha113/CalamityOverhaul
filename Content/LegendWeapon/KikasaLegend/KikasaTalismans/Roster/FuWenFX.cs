using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>雯的演出集中处：符星轨道几何（出箭点与绘制共用一处）、墨箭与符文尾迹</summary>
    internal static class FuWenFX
    {
        /// <summary>找归属玩家当前的悬伞</summary>
        internal static Projectile FindUmbrella(Player owner) {
            if (owner?.active != true) {
                return null;
            }
            int type = ModContent.ProjectileType<KikasaRainUmbrella>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.owner == owner.whoAmI && proj.type == type) {
                    return proj;
                }
            }
            return null;
        }

        /// <summary>
        /// 符星轨道位：绕伞缘的扁椭圆进动。出箭点与符星绘制共用本式，
        /// 时间基各端一致（全局时驱动），端间近似同位
        /// </summary>
        internal static Vector2 StarPos(Projectile umbrella, int index) {
            float ang = Main.GlobalTimeWrappedHourly * 1.4f + MathHelper.TwoPi * index / 3f;
            return umbrella.Center + new Vector2(MathF.Cos(ang) * 47f, MathF.Sin(ang) * 15f + 4f);
        }
    }

    /// <summary>
    /// 雯·三枚云篆符星：常驻表现件，绕伞缘进动，Glyph 笔画实描本符字形；
    /// 亮度随充能（旁观端充能恒 0，星常暗但在场），满充闪明，
    /// 掷出后轮值星熄灭再凝。逐帧写会话活性信标（TimerB）供失联补生
    /// </summary>
    internal class PRT_FuWenStars : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override bool CanPool => true;

        private int ownerWho;
        private Color accent;
        private string sessionKey;
        private float fadeOut;
        private int lastLaunchSeq;
        private float reformT;
        private Projectile umbrella;

        public PRT_FuWenStars Configure(int owner, Color accentColor, string key) {
            ownerWho = owner;
            accent = accentColor;
            sessionKey = key;
            Lifetime = -1;
            reformT = 1f;
            return this;
        }

        public override void Reset() {
            base.Reset();
            ownerWho = 0;
            accent = default;
            sessionKey = null;
            fadeOut = 0f;
            lastLaunchSeq = 0;
            reformT = 1f;
            umbrella = null;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            ShouldKillWhenOffScreen = false;
        }

        private KikasaTalismanSessionState State {
            get {
                if (sessionKey == null || ownerWho < 0 || ownerWho >= Main.maxPlayers) {
                    return null;
                }
                Player owner = Main.player[ownerWho];
                return owner?.active == true
                    && owner.TryGetModPlayer(out KikasaTalismanPlayer session)
                    ? session.GetTalismanState(sessionKey) : null;
            }
        }

        public override void AI() {
            Player owner = ownerWho >= 0 && ownerWho < Main.maxPlayers ? Main.player[ownerWho] : null;
            umbrella = owner?.active == true ? FuWenFX.FindUmbrella(owner) : null;
            KikasaTalismanSessionState state = State;
            if (umbrella == null || state == null) {
                fadeOut += 0.12f;
                if (fadeOut >= 1f) {
                    Kill();
                }
                Opacity = 1f - fadeOut;
                return;
            }
            state.TimerB = (int)Main.GameUpdateCount;
            fadeOut = MathF.Max(fadeOut - 0.2f, 0f);
            Opacity = 1f - fadeOut;
            Position = umbrella.Center;

            //掷星侦测：序号变了就让轮值星走一遍"熄灭→再凝"
            if (state.CounterA != lastLaunchSeq) {
                lastLaunchSeq = state.CounterA;
                reformT = 0f;
                KikasaInk.Play(KikasaInk.InkFlick, Position, 0.5f, 0.4f, 3);
            }
            reformT = MathF.Min(reformT + 1f / 36f, 1f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (umbrella == null) {
                return false;
            }
            KikasaTalismanSessionState state = State;
            float charge = MathHelper.Clamp(state?.MeterA ?? 0f, 0f, 1f);
            int launchedIdx = (((state?.CounterA ?? 0) - 1) % 3 + 3) % 3;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            float time = Main.GlobalTimeWrappedHourly;

            for (int i = 0; i < 3; i++) {
                Vector2 pos = FuWenFX.StarPos(umbrella, i);
                //刚掷出的轮值星按再凝进度回魂，其余星走充能亮度
                float presence = i == launchedIdx ? reformT : 1f;
                float bright = (0.35f + 0.65f * charge) * presence * Opacity;
                if (bright <= 0.03f) {
                    continue;
                }
                if (glow != null) {
                    spriteBatch.Draw(glow, pos - Main.screenPosition, null,
                        (accent with { A = 0 }) * (0.5f * bright * (0.8f + 0.2f * MathF.Sin(time * 3f + i * 2.1f))),
                        0f, glow.Size() * 0.5f, (14f + 8f * charge) / glow.Width,
                        SpriteEffects.None, 0f);
                }
                //云篆符星本体：本符字形的微缩湿墨，随轨道缓转
                KikasaTalismanGlyph.DrawInk(spriteBatch, nameof(FuWen), pos - Main.screenPosition,
                    15f + 3f * charge, bright, KikasaInk.InkBody, accent, time,
                    MathF.Sin(time * 1.1f + i * 2.4f) * 0.3f);
            }
            return false;
        }
    }

    /// <summary>
    /// 雯·追踪墨箭：符星满充自掷的一笔飞墨（ai[0]=目标 whoAmI、ai[1]=星序种子）。
    /// 曲率限幅追踪保弧线，尾迹=速度线+散落的云篆残文
    /// </summary>
    internal class FuWenArrowProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float TargetAi => ref Projectile.ai[0];
        private ref float SeedAi => ref Projectile.ai[1];

        private float life;

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            life++;
            //出手 8 帧后才吃地形：先离开伞缘的檐下空间
            Projectile.tileCollide = life > 8f;

            //首帧出手闪：各端本地（弹幕已同步，旁观可见）
            if (life == 1f && !Main.dedServ) {
                PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center, Vector2.Zero,
                    Color.Lerp(new Color(172, 204, 150), Color.White, 0.4f), 0.4f)
                    ?.Configure(new Color(172, 204, 150) * 0.7f, 14, 0.1f, 1f);
            }

            //加速+曲率限幅追踪：只转方向不改速率，恒为弧线
            float speed = MathF.Min(Projectile.velocity.Length() + 0.4f, 21f);
            NPC target = ResolveTarget();
            if (target != null) {
                Vector2 want = target.Center + target.velocity * 5f - Projectile.Center;
                float dAng = MathHelper.WrapAngle(
                    want.ToRotation() - Projectile.velocity.ToRotation());
                Projectile.velocity = Projectile.velocity.RotatedBy(
                    MathHelper.Clamp(dAng, -0.09f, 0.09f));
            }
            Projectile.velocity = Projectile.velocity.SafeNormalize(-Vector2.UnitY) * speed;
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (Main.dedServ) {
                return;
            }
            //符文尾迹：速度线逐帧、云篆残文每 6 帧一枚
            PRTLoader.NewParticle<PRT_Line>(
                Projectile.Center - Projectile.velocity * 0.5f,
                Projectile.velocity * 0.15f,
                (new Color(172, 204, 150)) * 0.55f,
                Main.rand.NextFloat(0.3f, 0.45f))?.Configure(false, 8);
            if ((int)life % 6 == 0) {
                PRTLoader.NewParticle<PRT_FuWenRuneMote>(
                    Projectile.Center - Projectile.velocity * 0.8f,
                    -Projectile.velocity * 0.04f + Main.rand.NextVector2Circular(0.4f, 0.4f),
                    new Color(172, 204, 150), 1f)
                    ?.Configure(Main.rand.Next(22, 32), SeedAi + life);
            }
        }

        private NPC ResolveTarget() {
            int who = (int)TargetAi;
            if (who < 0 || who >= Main.maxNPCs) {
                return null;
            }
            NPC npc = Main.npc[who];
            return npc?.active == true && npc.CanBeChasedBy(Projectile) ? npc : null;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //谢幕：墨点炸开+两枚残文散落
            KikasaInk.Play(KikasaInk.InkSplash, Projectile.Center, 0.4f, 0.1f, 4);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_KikasaInkBead>(
                    Projectile.Center + Main.rand.NextVector2Circular(4f, 4f),
                    Main.rand.NextVector2Circular(2.5f, 2f) - Vector2.UnitY,
                    Main.rand.NextBool() ? KikasaInk.InkDeep : new Color(172, 204, 150),
                    Main.rand.NextFloat(0.16f, 0.26f))?.Configure(Main.rand.Next(14, 22));
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_FuWenRuneMote>(
                    Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    Main.rand.NextVector2Circular(1f, 0.6f) - Vector2.UnitY * 0.4f,
                    new Color(172, 204, 150), 1f)
                    ?.Configure(Main.rand.Next(18, 26), Projectile.identity + i * 3.7f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (tex == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            Color accent = new(172, 204, 150);
            float fade = MathHelper.Clamp(life / 4f, 0f, 1f);
            //墨箭体：暗墨细梭沿速度拉伸，芯线金青，箭头一粒亮芒
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.05f, 0.3f, 1.2f);
            Vector2 scale = new Vector2(0.30f * (1f + stretch * 1.6f), 0.10f);
            Main.EntitySpriteDraw(tex, pos, null, KikasaInk.InkDeep * (0.9f * fade),
                Projectile.rotation, origin, scale * 1.25f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, KikasaInk.InkBody * fade,
                Projectile.rotation, origin, scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, (accent with { A = 0 }) * (0.6f * fade),
                Projectile.rotation, origin, scale * new Vector2(0.7f, 0.4f), SpriteEffects.None, 0);
            if (glow != null) {
                Vector2 head = pos + Projectile.velocity.SafeNormalize(Vector2.Zero) * 10f * stretch;
                Main.EntitySpriteDraw(glow, head, null, (accent with { A = 0 }) * (0.7f * fade),
                    0f, glow.Size() * 0.5f, 10f / glow.Width, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    /// <summary>雯·云篆残文：尾迹上散落的微缩虚影符文，缓旋渐淡</summary>
    internal class PRT_FuWenRuneMote : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override bool CanPool => true;

        private float seed;

        public PRT_FuWenRuneMote Configure(int lifetime, float runeSeed) {
            Lifetime = lifetime;
            seed = runeSeed;
            return this;
        }

        public override void Reset() {
            base.Reset();
            seed = 0f;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AlphaBlend;

        public override void AI() {
            Velocity *= 0.95f;
            Rotation += 0.02f * (seed % 2f < 1f ? 1f : -1f);
            Opacity = 1f - LifetimeCompletion;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            //残文：本符字形的隔段虚描，读作一枚正在散掉的小篆
            KikasaTalismanGlyph.DrawGhost(spriteBatch, nameof(FuWen),
                Position - Main.screenPosition, 12f * (1f - LifetimeCompletion * 0.35f),
                Opacity * 0.8f, Color, Rotation);
            return false;
        }
    }
}
