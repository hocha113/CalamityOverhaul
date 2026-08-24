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
    /// <summary>霅的演出集中处：重霅触发爆点、重霅滴落点鼓纹，全部端本地纯表现</summary>
    internal static class FuZhaFX
    {
        /// <summary>找归属玩家当前的悬伞（节拍环/出手点锚定用）</summary>
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

        /// <summary>重霅触发拍：雨鼓齐鸣+金环炸开（所有者端本地；旁观端靠重霅滴的鼓纹补拍）</summary>
        internal static void HeavyBeatBurst(Projectile umbrella, Color accent) {
            if (Main.dedServ) {
                return;
            }
            KikasaInk.Play(SoundID.DrumTomLow, umbrella.Center, 0.85f, -0.05f, 2);
            KikasaInk.Play(SoundID.DrumTomMid, umbrella.Center, 0.6f, 0f, 2);
            KikasaInk.Play(SoundID.DrumTamaSnare, umbrella.Center, 0.5f, 0.1f, 2);
            KikasaInk.Play(KikasaInk.InkSpray, umbrella.Center, 0.4f, -0.2f, 2);

            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(umbrella.Center, Vector2.Zero,
                accent * 0.6f, 0.12f)?.Configure(0.12f, 1f, 14);
            //金色鼓槌辐线：自伞顶炸开一圈
            for (int i = 0; i < 8; i++) {
                Vector2 dir = (MathHelper.TwoPi * i / 8f + 0.2f).ToRotationVector2();
                PRTLoader.NewParticle<PRT_Line>(umbrella.Center + dir * 8f,
                    dir * Main.rand.NextFloat(4f, 7f),
                    Color.Lerp(accent, Color.White, 0.35f) * 0.8f,
                    Main.rand.NextFloat(0.4f, 0.6f))?.Configure(false, 12);
            }
        }

        /// <summary>重霅滴谢幕：鼓面波纹（双环）+雨鼓点+溅金珠，各客户端本地</summary>
        internal static void DrumRipple(Projectile drop, Color accent) {
            if (Main.dedServ) {
                return;
            }
            KikasaInk.Play(drop.whoAmI % 2 == 0 ? SoundID.DrumTomMid : SoundID.DrumTomHigh,
                drop.Center, 0.5f, Main.rand.NextFloat(-0.1f, 0.1f), 4);

            //鼓面波纹：一急一缓两圈
            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(drop.Center, Vector2.Zero,
                accent * 0.55f, 0.08f)?.Configure(0.08f, 0.55f, 10);
            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(drop.Center, Vector2.Zero,
                accent * 0.35f, 0.06f)?.Configure(0.06f, 0.8f, 16);

            //鼓皮溅珠：向上弹的金点
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_KikasaInkBead>(
                    drop.Center + Main.rand.NextVector2Circular(6f, 3f),
                    new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(2f, 4f)),
                    Color.Lerp(accent, Color.White, 0.3f),
                    Main.rand.NextFloat(0.18f, 0.28f))?.Configure(Main.rand.Next(14, 22));
            }
        }
    }

    /// <summary>
    /// 霅·七点节拍环：悬在伞顶的常驻表现件。逐帧跟伞、写会话活性信标
    /// （TimerB，供 UpdateWhileHeld 失联补生），读连击拍数点亮环点；
    /// 重霅拍整环炸亮。伞收即散，无伞自灭。连击账在旁观端恒为 0，
    /// 旁观环靠"场上有无重霅标滴"近似补出爆发拍
    /// </summary>
    internal class PRT_FuZhaBeatRing : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override bool CanPool => true;

        private int ownerWho;
        private Color accent;
        private int tagId;
        private string sessionKey;
        private float fadeOut;
        private int lastCombo;
        private float comboPop;
        private float blaze;
        private int scanCadence;
        private bool tagSeen;

        public PRT_FuZhaBeatRing Configure(int owner, Color accentColor, int tag, string key) {
            ownerWho = owner;
            accent = accentColor;
            tagId = tag;
            sessionKey = key;
            Lifetime = -1;
            return this;
        }

        public override void Reset() {
            base.Reset();
            ownerWho = 0;
            accent = default;
            tagId = 0;
            sessionKey = null;
            fadeOut = 0f;
            lastCombo = 0;
            comboPop = 0f;
            blaze = 0f;
            scanCadence = 0;
            tagSeen = false;
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
            Projectile umbrella = owner?.active == true ? FuZhaFX.FindUmbrella(owner) : null;
            KikasaTalismanSessionState state = State;
            if (umbrella == null || state == null) {
                //伞已收/人已走：渐隐谢幕，不再写信标（让位下一次补生）
                fadeOut += 0.12f;
                if (fadeOut >= 1f) {
                    Kill();
                }
                Opacity = 1f - fadeOut;
                return;
            }
            //活性信标：环活着，UpdateWhileHeld 就不补生
            state.TimerB = (int)Main.GameUpdateCount;
            fadeOut = MathF.Max(fadeOut - 0.2f, 0f);
            Opacity = 1f - fadeOut;

            Vector2 target = umbrella.Center - Vector2.UnitY * 46f;
            Position = Position == default ? target : Vector2.Lerp(Position, target, 0.35f);

            //连击变动的落点弹跳
            int combo = Math.Max(state.CounterA, 0);
            if (combo != lastCombo) {
                lastCombo = combo;
                comboPop = 1f;
            }
            comboPop *= 0.86f;

            //重霅炸亮：所有者端读 MeterA，旁观端每 5 帧扫一次重霅标滴近似
            if (++scanCadence >= 5) {
                scanCadence = 0;
                tagSeen = false;
                int dropType = ModContent.ProjectileType<KikasaInkDrop>();
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile proj = Main.projectile[i];
                    if (proj.active && proj.type == dropType && proj.owner == ownerWho
                        && KikasaTalismanHooks.ReadTagId(proj.ai[2]) == tagId) {
                        tagSeen = true;
                        break;
                    }
                }
            }
            float blazeTarget = state.MeterA > 0.5f || tagSeen ? 1f : 0f;
            blaze = MathHelper.Lerp(blaze, blazeTarget, 0.25f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            KikasaTalismanSessionState state = State;
            int lit = Math.Max(state?.CounterA ?? 0, 0);
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            float radius = 16f * (1f + comboPop * 0.1f + blaze * 0.2f);

            //重霅拍的整环光晕
            if (glow != null && blaze > 0.05f) {
                spriteBatch.Draw(glow, Position - Main.screenPosition, null,
                    (Color.Lerp(accent, Color.White, 0.3f) with { A = 0 }) * (0.4f * blaze * Opacity),
                    0f, glow.Size() * 0.5f, radius * 3.4f / glow.Width, SpriteEffects.None, 0f);
            }

            for (int i = 0; i < FuZha.ComboBeats; i++) {
                //顶点起顺时针排七点
                float ang = -MathHelper.PiOver2 + MathHelper.TwoPi * i / FuZha.ComboBeats;
                Vector2 pos = Position + ang.ToRotationVector2() * radius - Main.screenPosition;
                bool isLit = i < lit || blaze > 0.5f;
                if (isLit) {
                    float pop = i == lit - 1 ? 1f + comboPop * 0.5f : 1f;
                    if (glow != null) {
                        spriteBatch.Draw(glow, pos, null,
                            (accent with { A = 0 }) * (0.7f * Opacity), 0f, glow.Size() * 0.5f,
                            9f * pop / glow.Width, SpriteEffects.None, 0f);
                    }
                    spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1),
                        Color.Lerp(accent, Color.White, 0.5f + blaze * 0.4f) * Opacity,
                        MathHelper.PiOver4, new Vector2(0.5f), new Vector2(2.6f * pop),
                        SpriteEffects.None, 0f);
                }
                else {
                    //未点亮：一粒沉暗墨点
                    spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1),
                        KikasaInk.InkDeep * (0.55f * Opacity), MathHelper.PiOver4,
                        new Vector2(0.5f), new Vector2(2f), SpriteEffects.None, 0f);
                }
            }
            return false;
        }
    }
}
