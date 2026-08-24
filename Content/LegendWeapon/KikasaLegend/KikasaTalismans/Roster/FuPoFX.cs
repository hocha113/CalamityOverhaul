using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>霸的演出集中处：月瀑起势银鸣、银粒逆升、月痕爆闪，全部端本地纯表现</summary>
    internal static class FuPoFX
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

        /// <summary>月瀑起势：碗口银鸣一记+一圈银辉脉冲，各端随同步标签同拍</summary>
        internal static void MoonPourCue(Projectile pour, Color accent) {
            if (Main.dedServ) {
                return;
            }
            KikasaInk.Play(SoundID.Item29, pour.Center, 0.55f, -0.5f, 2);
            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(pour.Center, Vector2.Zero,
                Color.Lerp(accent, Color.White, 0.3f) * 0.6f, 0.1f)?.Configure(0.1f, 0.9f, 14);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_KikasaInkBead>(
                    pour.Center + Main.rand.NextVector2Circular(12f, 6f),
                    -Vector2.UnitY * Main.rand.NextFloat(1.5f, 3f),
                    Color.Lerp(accent, Color.White, 0.4f),
                    Main.rand.NextFloat(0.24f, 0.36f))?.Configure(Main.rand.Next(16, 26));
            }
        }

        /// <summary>
        /// 月瀑银辉银粒逆升：沿瀑体自下而上飘银（用瀑暴露的落线终点锚定），
        /// 瀑缘偶发一线银光。逐帧调用自带节流，纯表现
        /// </summary>
        internal static void MoonPourMotes(Projectile pour, KikasaInkPour inkPour, Color accent) {
            //排空尾段银辉随瀑一起收
            if (pour.timeLeft <= KikasaInkPour.CollapseFrames || !Main.rand.NextBool(2)) {
                return;
            }
            Vector2 dir = pour.ai[0].ToRotationVector2();
            Vector2 end = inkPour.FallEndPoint;
            float len = MathF.Min(Vector2.Distance(pour.Center, end), 2400f);
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);

            //银粒逆升：出生在瀑身随机深度，逆着瀑向缓缓上飘
            Vector2 pos = pour.Center + dir * Main.rand.NextFloat(0.15f, 0.95f) * len
                + perp * Main.rand.NextFloat(-30f, 30f);
            PRTLoader.NewParticle<PRT_Light>(pos, -dir * Main.rand.NextFloat(0.8f, 2.2f),
                Color.Lerp(accent, Color.White, 0.5f) * 0.7f,
                Main.rand.NextFloat(0.14f, 0.24f))?.Configure(Main.rand.Next(24, 40), 0.7f);
            //瀑缘银光线：偶发一道贴缘的细银线顺流掠下
            if (Main.rand.NextBool(4)) {
                float side = Main.rand.NextBool() ? 1f : -1f;
                PRTLoader.NewParticle<PRT_Line>(
                    pour.Center + dir * Main.rand.NextFloat(0.1f, 0.7f) * len + perp * side * 34f,
                    dir * Main.rand.NextFloat(7f, 11f),
                    Color.Lerp(accent, Color.White, 0.55f) * 0.5f,
                    Main.rand.NextFloat(0.35f, 0.55f))?.Configure(false, 9);
            }
        }

        /// <summary>月痕爆演出：银白弯月闪+上扬银屑（爆伤弹幕首帧在各端自播，旁观可见）</summary>
        internal static void MoonburstFlash(Vector2 pos, float radius, Color accent) {
            if (Main.dedServ) {
                return;
            }
            KikasaInk.Play(SoundID.Item29, pos, 0.38f, 0.2f, 4);
            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(pos, Vector2.Zero,
                Color.Lerp(accent, Color.White, 0.5f) * 0.6f, 0.07f)
                ?.Configure(0.07f, radius / 105f, 11);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Line>(pos + Main.rand.NextVector2Circular(8f, 8f),
                    -Vector2.UnitY * Main.rand.NextFloat(2f, 4.5f)
                        + new Vector2(Main.rand.NextFloat(-1f, 1f), 0f),
                    Color.Lerp(accent, Color.White, 0.6f) * 0.75f,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(12, 18));
            }
        }
    }

    /// <summary>
    /// 霸·伞顶墨月：夜间常驻表现件。月相随所有者盈量自新月渐盈至满月
    /// （旁观端盈量恒 0，见常暗新月），满月银辉呼吸；入昼或伞收即散。
    /// 月体=软辉盘，月相=真透明暗盘错位遮掩（暗层不走加色，遮得住）
    /// </summary>
    internal class PRT_FuPoMoon : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override bool CanPool => true;

        private int ownerWho;
        private Color accent;
        private string sessionKey;
        private float fadeOut;
        private float phaseSmooth;
        private float fullPulse;

        public PRT_FuPoMoon Configure(int owner, Color accentColor, string key) {
            ownerWho = owner;
            accent = accentColor;
            sessionKey = key;
            Lifetime = -1;
            return this;
        }

        public override void Reset() {
            base.Reset();
            ownerWho = 0;
            accent = default;
            sessionKey = null;
            fadeOut = 0f;
            phaseSmooth = 0f;
            fullPulse = 0f;
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
            Projectile umbrella = owner?.active == true ? FuPoFX.FindUmbrella(owner) : null;
            KikasaTalismanSessionState state = State;
            if (umbrella == null || state == null || Main.dayTime) {
                //入昼/收伞：月隐，不再写信标
                fadeOut += 0.08f;
                if (fadeOut >= 1f) {
                    Kill();
                }
                Opacity = 1f - fadeOut;
                return;
            }
            state.TimerB = (int)Main.GameUpdateCount;
            fadeOut = MathF.Max(fadeOut - 0.15f, 0f);
            Opacity = 1f - fadeOut;

            Vector2 target = umbrella.Center - Vector2.UnitY * 58f
                + new Vector2(0f, MathF.Sin(Main.GlobalTimeWrappedHourly * 1.3f) * 3f);
            Position = Position == default ? target : Vector2.Lerp(Position, target, 0.3f);

            phaseSmooth = MathHelper.Lerp(phaseSmooth,
                MathHelper.Clamp(state.MeterA, 0f, 1f), 0.08f);
            fullPulse = phaseSmooth >= 0.98f
                ? 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 4f) : 0f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D shade = CWRAsset.Extra_98?.Value;
            if (glow == null || shade == null) {
                return false;
            }
            Vector2 pos = Position - Main.screenPosition;
            float r = 13f;
            float bright = (0.45f + 0.45f * phaseSmooth + 0.25f * fullPulse) * Opacity;

            //月晕：随月相渐盛的外辉
            spriteBatch.Draw(glow, pos, null, (accent with { A = 0 }) * (0.30f * bright), 0f,
                glow.Size() * 0.5f, r * 4.6f / glow.Width, SpriteEffects.None, 0f);
            //月盘：紧致软辉双层读作一轮银月
            spriteBatch.Draw(glow, pos, null, (accent with { A = 0 }) * (0.9f * bright), 0f,
                glow.Size() * 0.5f, r * 2.15f / glow.Width, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, pos, null,
                (Color.Lerp(accent, Color.White, 0.5f) with { A = 0 }) * (0.8f * bright), 0f,
                glow.Size() * 0.5f, r * 1.5f / glow.Width, SpriteEffects.None, 0f);
            //月相暗盘：真透明贴图压暗遮掩，随盈量向缺口滑出——新月到满月
            float slide = MathHelper.Lerp(r * 0.42f, r * 2.6f, phaseSmooth);
            spriteBatch.Draw(shade, pos + new Vector2(slide * 0.8f, -slide * 0.35f), null,
                KikasaInk.InkBody * (0.85f * Opacity * (1f - phaseSmooth * 0.35f)), 0f,
                shade.Size() * 0.5f, r * 3.4f / shade.Width, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 霸·月痕爆：不可见的一瞬 AoE 判定（ai[0]=判定半径 px），
    /// 首帧在各端自播银白月闪，伤害随生成包自含
    /// </summary>
    internal class FuPoMoonburstProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>判定半径（px），生成包 ai[0]</summary>
        private ref float Radius => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 3;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                float radius = MathHelper.Clamp(Radius <= 0f ? 62f : Radius, 30f, 130f);
                Projectile.Resize((int)(radius * 2f), (int)(radius * 2f));
                FuPoFX.MoonburstFlash(Projectile.Center, radius, new Color(224, 220, 202));
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
