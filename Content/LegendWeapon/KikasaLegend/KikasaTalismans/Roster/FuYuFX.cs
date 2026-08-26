using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>
    /// 雩的演出集中处：开坛伞影升空、雩坛符环大阵、编钟列拍、窗终三泉与雩泉礼花。
    /// 全部端本地纯表现；三泉生成是唯一的战斗侧出口，仅 owner 调用
    /// </summary>
    internal static class FuYuFX
    {
        //祭器色板：丹漆沉朱与礼金
        private static readonly Color RitualDeep = new(150, 52, 30);
        private static readonly Color RitualGold = new(244, 196, 110);

        /// <summary>找归属玩家的悬伞，未撑伞返回 null</summary>
        private static Projectile FindUmbrella(Player owner) {
            int type = ModContent.ProjectileType<KikasaRainUmbrella>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == type && proj.owner == owner.whoAmI) {
                    return proj;
                }
            }
            return null;
        }

        /// <summary>场上是否还有该玩家的虹墨标滴（旁观端窗近似判据）</summary>
        internal static bool AnyTaggedDrop(Player owner, KikasaTalismanDefinition definition) {
            int dropType = ModContent.ProjectileType<KikasaInkDrop>();
            int tag = KikasaTalismanHooks.TagIdFor(definition);
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == dropType && proj.owner == owner.whoAmI
                    && KikasaTalismanHooks.ReadTagId(proj.ai[2]) == tag) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>自探针点向下逐格找实心地表</summary>
        private static bool TryFindGroundBelow(Vector2 probe, float maxDown, out Vector2 surface) {
            int x = (int)(probe.X / 16f);
            int startY = (int)(probe.Y / 16f);
            int endY = (int)((probe.Y + maxDown) / 16f);
            for (int y = startY; y <= endY; y++) {
                Tile t = Framing.GetTileSafely(x, y);
                if (t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]) {
                    surface = new Vector2(probe.X, y * 16f);
                    return true;
                }
            }
            surface = default;
            return false;
        }

        /// <summary>
        /// 开坛：伞影脱手升空自旋（表现层，不动真伞）+脚下铺开雩坛符环大阵+
        /// 一记深钟起典+朱金珠环。各端在各自的窗边沿起演
        /// </summary>
        internal static void GrandRainOpening(Player owner, Color accent) {
            Projectile umbrella = FindUmbrella(owner);
            Vector2 anchor = umbrella?.Center ?? owner.Top - new Vector2(0f, 52f);

            //升空伞影两道：一慢一快错帧上抽，越升越转
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_FuYuUmbrellaAscend>(anchor,
                    new Vector2(0f, -(2.0f + 1.3f * i)),
                    Color.Lerp(accent, RitualGold, 0.35f + 0.3f * i), 0.9f - 0.18f * i)
                    ?.Configure(76 - i * 10, 0.16f + 0.08f * i);
            }
            //开坛脉冲环+朱金珠环甩
            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(anchor, Vector2.Zero,
                accent * 0.6f, 0.14f)?.Configure(0.14f, 1.05f, 16);
            for (int i = 0; i < 12; i++) {
                Vector2 dir = (MathHelper.TwoPi * i / 12f).ToRotationVector2();
                PRTLoader.NewParticle<PRT_KikasaInkBead>(anchor + dir * 12f,
                    dir * Main.rand.NextFloat(2.6f, 4.6f) - Vector2.UnitY * 1.6f,
                    Color.Lerp(accent, RitualGold, Main.rand.NextFloat(0.6f)),
                    Main.rand.NextFloat(0.24f, 0.4f))?.Configure(Main.rand.Next(16, 26));
            }
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(anchor + Main.rand.NextVector2Circular(30f, 20f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.4f),
                    Color.Lerp(RitualGold, Color.White, 0.3f), Main.rand.NextFloat(0.26f, 0.42f))
                    ?.Configure(accent * 0.6f, Main.rand.Next(14, 22), 0.1f, 0.8f);
            }

            //雩坛大阵铺在脚下地表，随窗同寿；悬空开坛则免铺（无地可坛）
            if (TryFindGroundBelow(owner.Bottom, 320f, out Vector2 altarPos)) {
                PRTLoader.NewParticle<PRT_FuYuAltar>(altarPos - new Vector2(0f, 6f), Vector2.Zero,
                    accent, 1f)?.Configure(FuYu.WindowFrames);
            }

            //起典三声：深钟定场、弦音随起、伞骨闷扫垫底
            KikasaInk.Play(SoundID.Item35, anchor, 0.95f, -0.5f, 2);
            KikasaInk.Play(SoundID.Item26, anchor, 0.6f, 0.1f, 2);
            KikasaInk.Play(KikasaInk.UmbrellaWhoosh, anchor, 0.7f, -0.3f, 2);
        }

        /// <summary>编钟列拍：一记比一记高，读作典礼推进（step 从 1 起）</summary>
        internal static void RitualChime(Vector2 pos, int step) {
            KikasaInk.Play(SoundID.Item35, pos, 0.7f, -0.45f + 0.22f * step, 2);
        }

        /// <summary>窗内雨幕加密的装饰细雨与坛边金浮尘，逐帧低频抽签</summary>
        internal static void WindowAmbient(Player owner, Color accent) {
            if (Main.rand.NextBool(3)) {
                //细雨丝：机制上的雨拍减半之外，再给一层视觉密度
                PRTLoader.NewParticle<PRT_Line>(
                    owner.Center + new Vector2(Main.rand.NextFloat(-240f, 240f),
                        -200f + Main.rand.NextFloat(-40f, 40f)),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(8f, 12f)),
                    Color.Lerp(new Color(120, 90, 70), accent, Main.rand.NextFloat(0.3f)) * 0.38f,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(false, Main.rand.Next(12, 17));
            }
            if (Main.rand.NextBool(10)) {
                PRTLoader.NewParticle<PRT_Sparkle>(
                    owner.Top + Main.rand.NextVector2Circular(60f, 30f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.8f),
                    Color.Lerp(RitualGold, Color.White, 0.25f), Main.rand.NextFloat(0.18f, 0.3f))
                    ?.Configure(accent * 0.5f, Main.rand.Next(12, 18), 0.08f, 0.6f);
            }
        }

        /// <summary>祭毕终鼓：沉钟收典+弦音回落+重水花（三泉的水声由泉体自奏）</summary>
        internal static void GrandRainClosing(Vector2 pos) {
            KikasaInk.Play(SoundID.Item35, pos, 0.9f, -0.7f, 2);
            KikasaInk.Play(SoundID.Item26, pos, 0.5f, -0.2f, 2);
            KikasaInk.Play(KikasaInk.InkSplash, pos, 0.8f, -0.4f, 3);
        }

        /// <summary>
        /// 窗终三墨泉齐发（仅 owner 调用，弹幕自然同步）：伞位为锚、无伞落在玩家脚下，
        /// 沿地表三点错拍唤泉；伤害对齐墨瀑终幕口径（0.9x 基伤×沛/雩乘区×伞下鬼乘区），
        /// 带雩标供喷发礼花，柱高 1.15x 随 ai 同步判定绘制同源
        /// </summary>
        internal static void FireFinaleGeysers(Player owner, KikasaTalismanDefinition definition) {
            if (owner?.active != true) {
                return;
            }
            Projectile umbrella = FindUmbrella(owner);
            Vector2 anchor = umbrella?.Center ?? owner.Top;
            int damage;
            float knockback;
            if (umbrella != null) {
                damage = umbrella.damage;
                knockback = umbrella.knockBack * 1.6f;
            }
            else {
                Item held = owner.HeldItem;
                damage = held == null ? 0 : owner.GetWeaponDamage(held);
                knockback = (held?.knockBack ?? 4f) * 1.6f;
            }
            if (damage <= 0) {
                return;
            }
            KikasaTalismanProfile profile = KikasaTalismanCombat.Resolve(owner);
            float mul = 0.9f * profile.GeyserDamageMul
                * KikasaOverride.GetSlotDamageMul(KikasaOverride.GetSlotCount(owner));
            float ai1 = KikasaTalismanHooks.PackTag(KikasaTalismanHooks.TagIdFor(definition), 2);
            //柱高 1.15x，量化 x1000 随生成包各端一致
            const float ai2 = 1150f;

            int fired = 0;
            for (int i = 0; i < 3; i++) {
                float off = (i - 1) * 96f;
                if (!TryFindGroundBelow(new Vector2(anchor.X + off, anchor.Y), 480f, out Vector2 basePos)) {
                    continue;
                }
                Projectile.NewProjectile(owner.GetSource_Misc("CWR_FuYuFinale"), basePos, Vector2.Zero,
                    ModContent.ProjectileType<KikasaInkGeyser>(), (int)(damage * mul), knockback,
                    owner.whoAmI, fired * 5f, ai1, ai2);
                fired++;
            }
        }

        /// <summary>雩泉喷发礼花：朱金脉冲环+上掷金珠+错音编钟，各端喷发拍本地</summary>
        internal static void GeyserEruptFlourish(Projectile geyser, Color accent) {
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(geyser.Center, Vector2.Zero,
                accent * 0.55f, 0.12f)?.Configure(0.12f, 0.8f, 13);
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_KikasaInkBead>(
                    geyser.Center + new Vector2(Main.rand.NextFloat(-18f, 18f), -4f),
                    new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), -Main.rand.NextFloat(3f, 7f)),
                    Color.Lerp(accent, RitualGold, Main.rand.NextFloat(0.7f)),
                    Main.rand.NextFloat(0.24f, 0.38f))?.Configure(Main.rand.Next(16, 24));
            }
            //编钟错音：逐泉音高微差，三泉齐发读作一组钟
            KikasaInk.Play(SoundID.Item35, geyser.Center, 0.55f,
                -0.1f + geyser.identity % 5 * 0.12f, 3);
        }
    }

    /// <summary>
    /// 雩·升空伞影：借鬼伞物品贴图的加色虚影脱手升空，越升越快、自旋渐急，
    /// 伪偏航（cos 压缩+过零翻面）读作绕柄旋转。纯表现层，真伞投射物不动
    /// </summary>
    internal class PRT_FuYuUmbrellaAscend : BasePRT
    {
        //本体直接采鬼伞物品贴图，此纹理仅供加载器占位
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 12;

        private Color initialColor;
        private float spinPhase;
        private float spinSpeed;

        public PRT_FuYuUmbrellaAscend Configure(int lifetime, float spinStart) {
            Lifetime = lifetime;
            initialColor = Color;
            spinSpeed = spinStart;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            spinPhase = 0f;
            spinSpeed = 0f;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            //升空：越走越快；自旋：越转越急——大雩的伞是被典礼请上去的
            Velocity *= 1.045f;
            spinSpeed = MathF.Min(spinSpeed + 0.014f, 0.6f);
            spinPhase += spinSpeed;
            float t = LifetimeCompletion;
            Opacity = MathF.Min(t * 6f, 1f) * (1f - MathF.Pow(t, 2.2f)) * 0.6f;
            Color = initialColor * Opacity;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            int itemType = ModContent.ItemType<KikasaItem>();
            Main.instance.LoadItem(itemType);
            Texture2D tex = TextureAssets.Item[itemType]?.Value;
            if (tex == null) {
                return false;
            }
            Rectangle frame = Main.itemAnimations[itemType]?.GetFrame(tex) ?? tex.Frame();
            //伪偏航：cos 过零翻面+横向压缩，不对称剪影的翻面读作绕柄自旋
            float yaw = MathF.Cos(spinPhase);
            SpriteEffects flip = yaw < 0f ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Vector2 scale = new Vector2(0.7f + 0.3f * MathF.Abs(yaw), 1f) * Scale;
            float lean = MathF.Sin(spinPhase * 0.5f) * 0.08f;
            spriteBatch.Draw(tex, Position - Main.screenPosition, frame, Color, lean,
                frame.Size() * 0.5f, scale, flip, 0f);
            return false;
        }
    }

    /// <summary>
    /// 雩·雩坛符环大阵：贴地双环逐笔扫开（透视压扁），环上五枚小雩符错拍逐笔显形，
    /// 坛心一面大雩符悬浮徐展（<see cref="KikasaTalismanGlyph.DrawInk"/> 的 reveal 逐笔展开），
    /// 全阵缓旋、朱金呼吸；窗尾缓灭。端本地表现，一窗一面
    /// </summary>
    internal class PRT_FuYuAltar : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 4;

        /// <summary>贴地透视：环的纵向压缩比</summary>
        private const float Squish = 0.42f;

        private const float OuterR = 122f;
        private const float InnerR = 84f;

        //坛墨：丹漆沉黑
        private static readonly Color AltarInk = new(48, 20, 14);

        private Color accent;

        public PRT_FuYuAltar Configure(int lifetime) {
            Lifetime = lifetime;
            accent = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            accent = default;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Velocity = Vector2.Zero;
        }

        public override void AI() {
            float alpha = Envelope();
            Lighting.AddLight(Position, 0.5f * alpha, 0.28f * alpha, 0.12f * alpha);
        }

        /// <summary>入场快起+窗尾缓灭的总包络</summary>
        private float Envelope() {
            float fadeIn = MathHelper.Clamp(Time / 20f, 0f, 1f);
            float fadeOut = MathHelper.Clamp((Lifetime - Time) / 70f, 0f, 1f);
            return fadeIn * fadeOut;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            float alpha = Envelope();
            if (alpha <= 0.02f) {
                return false;
            }
            Vector2 center = Position - Main.screenPosition;
            float time = Main.GlobalTimeWrappedHourly;
            //全阵缓旋+墨迹呼吸
            float rot = Time * 0.0035f;
            float breath = 0.9f + 0.1f * MathF.Sin(time * 1.8f);

            //坛心暖光垫底：光丘不夺墨线
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                spriteBatch.Draw(glow, center, null, (accent with { A = 0 }) * (0.3f * alpha * breath),
                    0f, glow.Size() * 0.5f, new Vector2(3.4f, 1.2f), SpriteEffects.None, 0f);
            }

            //双环逐笔扫开：外环先行，内环滞后一拍
            float outerSweep = MathHelper.Clamp(Time / 55f, 0f, 1f);
            float innerSweep = MathHelper.Clamp((Time - 14f) / 55f, 0f, 1f);
            DrawRing(spriteBatch, center, OuterR, rot, outerSweep, alpha, 4.6f);
            DrawRing(spriteBatch, center, InnerR, -rot * 1.4f, innerSweep, alpha, 3.4f);

            //外环四方朱位：环扫过其角位即点亮，随阵缓旋
            for (int i = 0; i < 4; i++) {
                if (i * 0.25f > outerSweep) {
                    continue;
                }
                float a = rot + MathHelper.PiOver2 * i;
                Vector2 pos = center + new Vector2(MathF.Cos(a) * OuterR,
                    MathF.Sin(a) * OuterR * Squish);
                float pulse = 0.8f + 0.2f * MathF.Sin(time * 3f + i * 1.7f);
                spriteBatch.Draw(VaultAsset.placeholder2.Value, pos, new Rectangle(0, 0, 1, 1),
                    accent * (alpha * 0.85f * pulse), MathHelper.PiOver4, new Vector2(0.5f),
                    new Vector2(4.4f * pulse), SpriteEffects.None, 0f);
            }

            //环上五枚小雩符：错拍逐笔显形，随内外环之间的中带反向缓行
            for (int i = 0; i < 5; i++) {
                float reveal = MathHelper.Clamp((Time - 46f - i * 13f) / 24f, 0f, 1f);
                if (reveal <= 0f) {
                    continue;
                }
                float a = MathHelper.TwoPi * i / 5f - rot * 0.6f;
                Vector2 pos = center + new Vector2(MathF.Cos(a) * (InnerR + 19f),
                    MathF.Sin(a) * (InnerR + 19f) * Squish);
                KikasaTalismanGlyph.DrawInk(spriteBatch, nameof(FuYu), pos, 34f,
                    alpha * 0.9f, AltarInk, accent, time, 0f, reveal);
            }

            //坛心大雩符：悬于坛上徐徐写就，微微浮息
            float bob = MathF.Sin(time * 2.6f) * 3f;
            float centerReveal = MathHelper.Clamp(Time / 75f, 0f, 1f);
            KikasaTalismanGlyph.DrawInk(spriteBatch, nameof(FuYu),
                center - new Vector2(0f, 44f + bob), 112f, alpha, AltarInk, accent,
                time, 0f, centerReveal);
            return false;
        }

        /// <summary>一圈贴地墨环：分段折线扫出，深墨承底、朱金细芯走内侧</summary>
        private void DrawRing(SpriteBatch sb, Vector2 center, float radius,
            float rotOffset, float sweep, float alpha, float thick) {
            if (sweep <= 0.01f) {
                return;
            }
            const int SegCount = 44;
            int visible = (int)MathF.Ceiling(sweep * SegCount);
            Vector2 prev = RingPoint(center, radius, rotOffset);
            for (int i = 1; i <= visible; i++) {
                float a = MathHelper.TwoPi * MathF.Min(i / (float)SegCount, sweep) + rotOffset;
                Vector2 cur = RingPoint(center, radius, a);
                DrawSeg(sb, prev, cur, AltarInk * (alpha * 0.9f), thick);
                DrawSeg(sb, prev, cur, accent * (alpha * 0.55f), thick * 0.36f);
                prev = cur;
            }
            //扫掠笔锋：环的书写前沿一点亮
            if (sweep < 1f) {
                sb.Draw(VaultAsset.placeholder2.Value, prev, new Rectangle(0, 0, 1, 1),
                    Color.Lerp(accent, Color.White, 0.4f) * (alpha * 0.9f), MathHelper.PiOver4,
                    new Vector2(0.5f), new Vector2(3.6f), SpriteEffects.None, 0f);
            }
        }

        private static Vector2 RingPoint(Vector2 center, float radius, float angle)
            => center + new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius * Squish);

        private static void DrawSeg(SpriteBatch sb, Vector2 a, Vector2 b, Color color, float thick) {
            Vector2 edge = b - a;
            float len = edge.Length();
            if (len < 0.5f) {
                return;
            }
            sb.Draw(VaultAsset.placeholder2.Value, a, new Rectangle(0, 0, 1, 1), color,
                edge.ToRotation(), new Vector2(0f, 0.5f), new Vector2(len + 0.7f, thick),
                SpriteEffects.None, 0f);
        }
    }
}
