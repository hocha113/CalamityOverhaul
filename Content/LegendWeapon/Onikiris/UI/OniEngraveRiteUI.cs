using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.Onikiris.UI
{
    /// <summary>
    /// 铭刻仪式弹窗：新鬼入簿的一次性演出。<br/>
    /// 编舞（全部为 <see cref="timer"/> 的纯函数，点击可跳拍）：<br/>
    /// 青烟鬼影显形 → 白热刀痕划过 → 烟被收拢凝成一行湿墨名讳 → 朱印旋转砸下 + 屏震 → 静场落瓣
    /// </summary>
    internal sealed class OniEngraveRiteUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "Legend.OnikiriText";
        public static OniEngraveRiteUI Instance => UIHandleLoader.GetUIHandleOfType<OniEngraveRiteUI>();

        public static LocalizedText RiteTitle { get; private set; }
        public static LocalizedText RiteHint { get; private set; }

        public override void SetStaticDefaults() {
            RiteTitle = this.GetLocalization(nameof(RiteTitle), () => "铭 刻");
            RiteHint = this.GetLocalization(nameof(RiteHint), () => "落笔 · 归卷");
        }

        public override bool CloseOnEscape => true;
        //仪式是最上层的一次性演出,压过点鬼簿(2)与 HUD(1)
        public override float RenderPriority => 3f;
        public override SoundStyle? OpenSound => SoundID.Item29 with { Pitch = -0.85f, Volume = 0.35f };
        public override SoundStyle? CloseSound => SoundID.Item35 with { Pitch = 0.2f, Volume = 0.3f };

        //====编舞时间轴(帧),Condense 之后的节点依名讳长度计算====
        private const float TSlash = 66f;
        private const float TBreak = 74f;
        private const float TCondense = 90f;
        private const float CharInterval = 6f;
        private const float StampDelay = 16f;
        private const float StampDrop = 13f;

        private OniGhostEntry entry;
        private float timer;
        private bool motesSpawned;
        private bool slashPlayed;
        private bool stampPlayed;
        private int lastTypeChars = -1;
        private float inkAge = 60f;
        private readonly OniUIParticlePool particles = new(160);
        private int petalTimer;

        /// <summary>播放一场铭刻仪式（客户端演出，无数据写入）。无名之鬼没有可凝的字,不受理</summary>
        public static void Play(OniGhostEntry ghost) {
            OniEngraveRiteUI inst = Instance;
            if (inst == null || ghost == null || !ghost.HasName || Main.dedServ) {
                return;
            }
            inst.entry = ghost;
            inst.ResetPlayback();
            inst.Open();
        }

        private void ResetPlayback() {
            timer = 0f;
            motesSpawned = false;
            slashPlayed = false;
            stampPlayed = false;
            lastTypeChars = -1;
            inkAge = 60f;
            petalTimer = 0;
            particles.Clear();
        }

        //====布局(全部 UI 空间)====
        private static Vector2 Center => new(OnikiriUITheme.UIScreenW * 0.5f, OnikiriUITheme.UIScreenH * 0.5f - 16f);
        private Vector2 SilhouetteCenter => Center + new Vector2(-112f, 6f);
        private bool NameIsVertical => OniBrush.ContainsCJK(entry?.Name?.Invoke() ?? string.Empty);

        private string NameText => entry?.Name?.Invoke() ?? string.Empty;
        private string PowerText => entry?.Power?.Invoke() ?? string.Empty;

        //====时间轴派生量(纯函数)====
        private float NameDoneAt => TCondense + NameText.Length * CharInterval + 8f;
        private float StampHitAt => NameDoneAt + StampDelay + StampDrop;
        private float StillAt => StampHitAt + 16f;

        private int TypeChars => timer < TCondense ? 0 : Math.Min(NameText.Length, (int)((timer - TCondense) / CharInterval) + 1);
        private float SlashSweep => MathHelper.Clamp((timer - TSlash) / 9f, 0f, 1f);
        private float SilhouetteBreak => MathHelper.Clamp((timer - TBreak) / 15f, 0f, 1f);
        private float StampProgress => MathHelper.Clamp((timer - NameDoneAt - StampDelay) / StampDrop, 0f, 1f);
        private float StampFlash => timer < StampHitAt ? 0f : Math.Max(0f, 1f - (timer - StampHitAt) / 18f);

        public override void Update() {
            float a = OpenProgress;
            if (a < 0.01f) {
                return;
            }

            if (IsOpen) {
                player.mouseInterface = true;
                UIInputGuard.SuppressWeaponSwitch();
            }

            timer += 1f;
            particles.Update();
            inkAge = Math.Min(inkAge + 1f, 60f);

            int chars = TypeChars;
            if (chars != lastTypeChars) {
                lastTypeChars = chars;
                inkAge = 0f;
            }

            //刀痕落下的一拍:音效+屏震+烟碎
            if (!slashPlayed && timer >= TSlash) {
                slashPlayed = true;
                SoundEngine.PlaySound(CWRSound.SwiftSlice with { Volume = 0.6f, Pitch = -0.05f, MaxInstances = 1 });
                player.CWR().GetScreenShake(4f);
            }

            //烟凝成字:碎裂时刻一次性放出墨粒,目标为各字符落位
            if (!motesSpawned && timer >= TBreak) {
                motesSpawned = true;
                SpawnCondenseMotes();
            }

            //朱印砸章
            if (!stampPlayed && timer >= StampHitAt) {
                stampPlayed = true;
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.8f, Pitch = -0.6f, MaxInstances = 1 });
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.4f, Pitch = -0.25f, MaxInstances = 1 });
                player.CWR().GetScreenShake(6f);
                //章底灰飞
                Vector2 sealPos = SealCenter();
                for (int i = 0; i < 10; i++) {
                    particles.SpawnAsh(sealPos + Main.rand.NextVector2Circular(9f, 5f));
                }
            }

            //静场落瓣(只在两翼)
            if (timer >= StillAt) {
                petalTimer++;
                if (petalTimer >= 46) {
                    petalTimer = 0;
                    bool left = Main.rand.NextBool();
                    float x = Center.X + (left ? -1f : 1f) * Main.rand.NextFloat(180f, 250f);
                    particles.SpawnPetal(new Vector2(x, Center.Y - 210f), left ? -1f : 1f);
                }
            }

            //点击:静场前跳拍,静场后收卷
            if (IsOpen && a > 0.6f && keyLeftPressState == KeyPressState.Pressed) {
                if (timer < StillAt) {
                    SkipToStill();
                }
                else {
                    Close();
                }
            }
        }

        /// <summary>跳拍:时间轴直接推进到静场,补发未发生的一次性事件(不再重复演出音效)</summary>
        private void SkipToStill() {
            if (!motesSpawned) {
                motesSpawned = true;
            }
            slashPlayed = true;
            stampPlayed = true;
            timer = StillAt;
            lastTypeChars = NameText.Length;
            inkAge = 60f;
            SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.2f, Volume = 0.4f });
        }

        /// <summary>烟碎为墨:自鬼影范围放出墨粒,弧线收束到每个字符的落位</summary>
        private void SpawnCondenseMotes() {
            string name = NameText;
            if (name.Length == 0) {
                return;
            }
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 silhouette = SilhouetteCenter;
            int motesPerChar = Math.Max(3, 26 / name.Length);
            for (int c = 0; c < name.Length; c++) {
                Vector2 charPos = CharAnchor(font, c);
                for (int j = 0; j < motesPerChar; j++) {
                    Vector2 from = silhouette + Main.rand.NextVector2Circular(66f, 74f);
                    Color col = Color.Lerp(OnikiriUITheme.Ink, OnikiriUITheme.Deep, Main.rand.NextFloat(0.6f));
                    //按字符次序排延迟,墨是一笔一笔归位的
                    particles.SpawnInkMote(from, charPos + Main.rand.NextVector2Circular(4f, 4f), col, c * CharInterval + j * 1.3f);
                }
            }
        }

        /// <summary>第 index 个字符的屏幕落位(竖排沿列向下,拉丁横排沿行向右)</summary>
        private Vector2 CharAnchor(DynamicSpriteFont font, int index) {
            string name = NameText;
            const float NameScale = 1.28f;
            if (NameIsVertical) {
                float charH = font.MeasureString("字").Y * NameScale + 5f;
                Vector2 top = Center + new Vector2(96f, -MathF.Min(name.Length, 6f) * charH * 0.5f);
                return top + new Vector2(0f, index * charH + charH * 0.5f);
            }
            Vector2 size = font.MeasureString(name) * NameScale;
            Vector2 left = Center + new Vector2(40f - size.X * 0.5f, -66f);
            float w = font.MeasureString(name[..index]).X * NameScale;
            return left + new Vector2(w + font.MeasureString(name[index].ToString()).X * NameScale * 0.5f, size.Y * 0.5f);
        }

        private Vector2 SealCenter() {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string name = NameText;
            const float NameScale = 1.28f;
            if (NameIsVertical) {
                float charH = font.MeasureString("字").Y * NameScale + 5f;
                Vector2 top = Center + new Vector2(96f, -MathF.Min(name.Length, 6f) * charH * 0.5f);
                return top + new Vector2(0f, name.Length * charH + 26f);
            }
            Vector2 size = font.MeasureString(name) * NameScale;
            return Center + new Vector2(40f + size.X * 0.5f + 30f, -66f + size.Y * 0.5f);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            float a = OpenProgress;
            if (a < 0.01f || entry == null) {
                return;
            }

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 center = Center;

            //====压暗背景:全屏墨罩 + 构图后方一团深红背光====
            Rectangle full = new(0, 0, (int)OnikiriUITheme.UIScreenW + 2, (int)OnikiriUITheme.UIScreenH + 2);
            spriteBatch.Draw(pixel, full, src, Color.Black * (a * 0.74f));
            OniBrush.DrawBacklight(spriteBatch, center, 320f, OnikiriUITheme.Deep, a * 0.55f);

            //====小题「铭 刻」+ 两笔角签====
            float titleA = MathHelper.Clamp((timer - 8f) / 22f, 0f, 1f) * a;
            if (titleA > 0.01f) {
                string title = RiteTitle.Value;
                Vector2 tSize = font.MeasureString(title) * 0.92f;
                Vector2 tPos = center + new Vector2(-tSize.X * 0.5f, -206f);
                Utils.DrawBorderString(spriteBatch, title, tPos, OnikiriUITheme.HotWhite * titleA, 0.92f);
                OniBrush.DrawTaperedSlash(spriteBatch, tPos + new Vector2(-26f, tSize.Y * 0.55f), tPos + new Vector2(-6f, tSize.Y * 0.5f), 1.6f, 0.5f, titleA * 0.8f);
                OniBrush.DrawTaperedSlash(spriteBatch, tPos + new Vector2(tSize.X + 6f, tSize.Y * 0.5f), tPos + new Vector2(tSize.X + 26f, tSize.Y * 0.45f), 1.6f, 0.5f, titleA * 0.8f);
            }

            //====鬼影(青烟剪影 + 鬼火之眼)====
            DrawSilhouette(spriteBatch, a);

            //====白热刀痕:扫过鬼影,收势后余温残留====
            float sweep = SlashSweep;
            if (sweep > 0.02f) {
                float afterglow = timer <= TSlash + 9f ? 1f : Math.Max(0.25f, 1f - (timer - TSlash - 9f) / 40f);
                Vector2 s = SilhouetteCenter + new Vector2(-128f, 92f);
                Vector2 e = SilhouetteCenter + new Vector2(120f, -104f);
                OniBrush.DrawTaperedSlash(spriteBatch, s, e, 7f, 9f, a * afterglow, sweep);
            }

            //====墨粒/香灰/落瓣====
            particles.Draw(spriteBatch, a);

            //====名讳(湿墨打字机)====
            DrawName(spriteBatch, font, a);

            //====赋力小字:名讳写完后浮现====
            float powerA = MathHelper.Clamp((timer - NameDoneAt) / 24f, 0f, 1f) * a;
            if (powerA > 0.01f && PowerText.Length > 0) {
                string power = PowerText;
                Vector2 pSize = font.MeasureString(power) * 0.78f;
                Vector2 pPos = center + new Vector2(-pSize.X * 0.5f, 158f);
                Utils.DrawBorderString(spriteBatch, power, pPos, OnikiriUITheme.TextDim * powerA, 0.78f);
                OniBrush.DrawTaperedSlash(spriteBatch, pPos + new Vector2(-4f, -8f), pPos + new Vector2(pSize.X + 4f, -9f), 1.4f, 1.2f, powerA * 0.5f);
            }

            //====朱印砸章 + 冲击环====
            DrawStamp(spriteBatch, a);

            //====静场提示====
            float hintA = MathHelper.Clamp((timer - StillAt - 12f) / 30f, 0f, 1f) * a;
            if (hintA > 0.01f) {
                string hint = RiteHint.Value;
                float pulse = OnikiriUITheme.Breath(GlobalTimer, 1.3f, 2.2f);
                Vector2 hSize = font.MeasureString(hint) * 0.72f;
                Utils.DrawBorderString(spriteBatch, hint,
                    new Vector2(center.X - hSize.X * 0.5f, OnikiriUITheme.UIScreenH - 92f),
                    OnikiriUITheme.TextDim * (hintA * (0.55f + pulse * 0.35f)), 0.72f);
            }
        }

        /// <summary>鬼影:三层烟团缓旋扭动,碎裂时向外散逸;头部两点鬼火之眼</summary>
        private void DrawSilhouette(SpriteBatch sb, float a) {
            float grow = MathHelper.Clamp(timer / 42f, 0f, 1f);
            grow = grow * (2f - grow);
            float break_ = SilhouetteBreak;
            float alpha = a * grow * (1f - break_);
            if (alpha <= 0.01f) {
                return;
            }

            Texture2D smoke = OnikiriAssets.SmokeSheet01.Value;
            int frameSize = smoke.Width / 2;
            Vector2 origin = new(frameSize * 0.5f);
            Vector2 basePos = SilhouetteCenter;

            for (int i = 0; i < 3; i++) {
                int frame = (int)(GlobalTimer * 5f + i * 1.7f) % 4;
                Rectangle srcRect = new(frame % 2 * frameSize, frame / 2 * frameSize, frameSize, frameSize);
                float phase = i * 2.1f;
                //扭动:各层异相位横摆;碎裂:向外抛散
                Vector2 offset = new((float)Math.Sin(GlobalTimer * (0.9f + i * 0.28f) + phase) * (7f + i * 4f),
                    -20f + i * 22f + (float)Math.Cos(GlobalTimer * 0.7f + phase) * 4f);
                offset *= 1f + break_ * 2.2f;
                float scale = (0.34f + i * 0.075f) * (1f + break_ * 0.5f) * (0.8f + grow * 0.2f);
                float rot = (float)Math.Sin(GlobalTimer * 0.4f + phase) * 0.22f + break_ * (i - 1) * 0.5f;

                //墨黑本体,底层压深
                Color body = Color.Lerp(OnikiriUITheme.Ink, OnikiriUITheme.Dark, i * 0.3f) * (alpha * (0.88f - i * 0.14f));
                sb.Draw(smoke, basePos + offset, srcRect, body, rot, origin, scale, SpriteEffects.None, 0f);
            }
            //一缕青纱压在轮廓上,标记"这是鬼不是烟"
            {
                int frame = (int)(GlobalTimer * 4f + 2f) % 4;
                Rectangle srcRect = new(frame % 2 * frameSize, frame / 2 * frameSize, frameSize, frameSize);
                Vector2 offset = new((float)Math.Sin(GlobalTimer * 1.1f) * 6f, -34f);
                sb.Draw(smoke, basePos + offset * (1f + break_ * 2f), srcRect,
                    OnikiriUITheme.GhostDim * (alpha * 0.22f), 0.1f, origin, 0.30f, SpriteEffects.None, 0f);
            }

            //鬼火之眼:显形后段睁开,刀痕落下即熄
            float eyeA = MathHelper.Clamp((timer - 38f) / 14f, 0f, 1f) * (1f - MathHelper.Clamp((timer - TSlash) / 5f, 0f, 1f));
            if (eyeA > 0.01f && entry.HasEyes) {
                Texture2D pixel = VaultAsset.placeholder2.Value;
                Rectangle src = new(0, 0, 1, 1);
                float flick = 0.75f + 0.25f * (float)Math.Sin(GlobalTimer * 7.3f);
                Vector2 sway = new((float)Math.Sin(GlobalTimer * 0.9f) * 7f, (float)Math.Cos(GlobalTimer * 0.7f) * 4f);
                Vector2 eyeL = basePos + sway + new Vector2(-9f, -46f);
                Vector2 eyeR = basePos + sway + new Vector2(9f, -44f);
                foreach (Vector2 eye in new[] { eyeL, eyeR }) {
                    sb.Draw(pixel, eye, src, OnikiriUITheme.GhostDim * (a * eyeA * 0.5f * flick), 0f, new Vector2(0.5f), new Vector2(5.6f, 4.4f), SpriteEffects.None, 0f);
                    sb.Draw(pixel, eye, src, OnikiriUITheme.GhostFire * (a * eyeA * 0.95f * flick), 0f, new Vector2(0.5f), new Vector2(2.6f, 2.0f), SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>名讳打字机:CJK 竖排/拉丁横排,最新字符叠湿墨绯罩</summary>
        private void DrawName(SpriteBatch sb, DynamicSpriteFont font, float a) {
            string name = NameText;
            int chars = TypeChars;
            if (chars <= 0 || name.Length == 0) {
                return;
            }
            const float NameScale = 1.28f;
            float ink = 1f - MathHelper.Clamp(inkAge / 16f, 0f, 1f);

            for (int i = 0; i < chars && i < name.Length; i++) {
                Vector2 cPos = CharAnchor(font, i);
                string s = name[i].ToString();
                Vector2 size = font.MeasureString(s) * NameScale;
                Vector2 drawPos = cPos - size * 0.5f;
                Utils.DrawBorderString(sb, s, drawPos, OnikiriUITheme.Paper * a, NameScale);
                //湿墨:最新一字覆一层随时间褪去的绯红
                if (i == chars - 1 && ink > 0.02f) {
                    Utils.DrawBorderString(sb, s, drawPos, OnikiriUITheme.Bright * (a * 0.8f * ink), NameScale);
                }
            }
        }

        /// <summary>朱印:自高处带旋压落,命中一帧起冲击环</summary>
        private void DrawStamp(SpriteBatch sb, float a) {
            float p = StampProgress;
            if (p <= 0.001f) {
                return;
            }
            float ease = p * p * (3f - 2f * p);
            Vector2 sealPos = SealCenter();
            float scale = MathHelper.Lerp(2.0f, 1f, ease);
            float rot = MathHelper.Lerp(0.34f, 0.05f, ease);
            float integrity = entry.State == OniGhostState.Restless ? entry.Mastery + 0.25f : 1f;
            OniBrush.DrawSealGlyph(sb, sealPos, 15f * scale, a * (0.35f + ease * 0.65f), rot, MathHelper.Clamp(integrity, 0f, 1f));

            float flash = StampFlash;
            if (flash > 0.01f) {
                Texture2D ring = OnikiriAssets.Ring01.Value;
                //冲击环:直径自 30px 扩至 100px,随扩散淡出
                float diameter = 30f + (1f - flash) * 70f;
                sb.Draw(ring, sealPos, null, OnikiriUITheme.Seal * (a * flash * 0.8f), 0f,
                    ring.Size() * 0.5f, diameter / ring.Width, SpriteEffects.None, 0f);
            }
        }
    }
}
