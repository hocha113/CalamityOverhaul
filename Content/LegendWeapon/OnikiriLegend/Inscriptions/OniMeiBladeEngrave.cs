using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions
{
    /// <summary>
    /// 在世刀身的铭刻读数。三槽 Key 随物品/动作快照同步,各端一致;
    /// 活仪表(槽内充盈、雕纹点亮、裂痕)只有本地玩家维护——
    /// <see cref="OnikiriPlayer.PostUpdate"/> 对远端玩家直接返回,
    /// 故远端刀只画静态材质身份,不画读数
    /// </summary>
    internal struct OniMeiEngraveState
    {
        public string HiKey;
        public string HorimonoKey;
        public string NakagoKey;
        /// <summary>雕位是否金象嵌阶</summary>
        public bool HorimonoGold;

        /// <summary>樋内材质充盈 0~1(血位/烬量/息量/珠量/瓣量)</summary>
        public float HiFill;
        /// <summary>樋内一次性冲击 0~1(命中涌血、剪落抖瓣),自行衰减</summary>
        public float HiPulse;
        /// <summary>樋内循环相位 0~1(气流跑动、烬点爬行、潮相)</summary>
        public float HiPhase;
        /// <summary>樋位条件是否成立(闲息接上、正在合潮窗)</summary>
        public bool HiArmed;
        /// <summary>雕纹点亮 0~1(条件就绪)</summary>
        public float HoriLit;
        /// <summary>刀身咎裂 0~1(友切咎层的常态读法)</summary>
        public float BladeCrack;
        /// <summary>狮势链 0~1(狮子之子逐拍蓄势,满即第五拍合颚)</summary>
        public float LionChain;

        public readonly bool AnyEngraved
            => !string.IsNullOrEmpty(HiKey) || !string.IsNullOrEmpty(HorimonoKey)
            || !string.IsNullOrEmpty(NakagoKey);
    }

    /// <summary>
    /// 在世刀身铭刻层:三槽本就是刀的物理部位(茎=刀名、樋=血槽、雕位=彫物),
    /// 故不再往身边贴一次性符号,而是把铭刻画在实际挥动的那把刀上。<br/>
    /// 剖面走 <see cref="OniBladeProfile"/> 的真实剪影,字形走
    /// <see cref="OniMeiGlyph"/> 的笔画库,两者与改铭台共用同一份数据
    /// </summary>
    internal static class OniMeiBladeEngrave
    {
        //====贴图轴上的铭位(u:0=锋尖 1=柄尾)====
        /// <summary>樋槽起点(近锋),不顶到尖</summary>
        private const float HiStartU = 0.17f;
        /// <summary>樋槽讫点(近镡)</summary>
        private const float HiEndU = 0.82f;
        private const int HiSegments = 14;
        /// <summary>雕位:刀根彫物</summary>
        private const float HoriU = 0.70f;
        /// <summary>
        /// 雕纹字径(贴图 px),取改铭台同一标定 <see cref="OnikiriUITheme.MeiBladeMarkPx"/>。<br/>
        /// 更大就会挂出刃宽读成贴纸——彫物是刻进刀里的,不是盖在刀上的
        /// </summary>
        private const float HorimonoMarkPx = 13f;
        /// <summary>雕纹相对剪影厚度的硬上限,保证任何缩放下都不越过刃缘</summary>
        private const float HorimonoMaxOfThickness = 0.78f;

        /// <summary>樋槽自中线向栋侧偏(真实血槽贴近镐地而非正中)</summary>
        private const float HiTowardBack = 0.30f;
        /// <summary>槽宽相对剪影厚度</summary>
        private const float HiWidthOfThickness = 0.17f;
        /// <summary>低于此世界槽宽不值得画(远景/小刀)</summary>
        private const float MinGrooveWidth = 0.9f;

        //====介质色(与系列绯红/纸白/旧金同源)====
        private static readonly Color GrooveShell = new(20, 8, 12);
        private static readonly Color GrooveLip = new(236, 226, 212);
        private static readonly Color BloodBody = new(122, 14, 22);
        private static readonly Color BloodFront = new(196, 32, 36);
        private static readonly Color AirStreak = new(228, 236, 240);
        private static readonly Color CharBed = new(26, 14, 12);
        private static readonly Color EmberHot = new(238, 138, 62);
        private static readonly Color BreathFilm = new(214, 214, 206);
        private static readonly Color InkBead = new(14, 6, 10);
        private static readonly Color TideBody = new(62, 30, 44);
        private static readonly Color TideCrest = new(214, 178, 190);
        private static readonly Color PetalMark = new(212, 122, 140);
        private static readonly Color DullSteel = new(104, 96, 92);
        private static readonly Color CrackInk = new(10, 3, 6);

        /// <summary>
        /// 解析这把刀上的三槽:招式弹幕优先读已同步的动作快照,
        /// 否则读持握刀的铭数据(<see cref="CWRItem.NetSend"/> 同步 LegendData)
        /// </summary>
        public static OniMeiEngraveState Resolve(Projectile host, Player owner) {
            OniMeiEngraveState state = default;
            OniMeiActionContext context = OniMeiActionContext.Get(host);
            if (context?.HasSnapshot == true) {
                state.NakagoKey = context.NakagoKey;
                state.HiKey = context.HiKey;
                state.HorimonoKey = context.HorimonoKey;
            }
            else {
                OniMeiStore store = OnikiriData.TryGet(owner?.GetItem())?.Mei;
                state.NakagoKey = store?.Get(OniMeiSlotKind.Nakago);
                state.HiKey = store?.Get(OniMeiSlotKind.Hi);
                state.HorimonoKey = store?.Get(OniMeiSlotKind.Horimono);
            }
            if (!string.IsNullOrEmpty(state.HorimonoKey)
                && OniMeiRegistry.TryGet(state.HorimonoKey, out OniMeiDefinition horimono)) {
                state.HorimonoGold = horimono.IsGoldTier;
            }
            //活读数只有本地玩家有(OnikiriPlayer.PostUpdate 对远端直接返回)
            if (owner != null && owner.whoAmI == Main.myPlayer
                && owner.TryGetModPlayer(out OnikiriPlayer onikiri)) {
                onikiri.FillEngraveGauges(ref state);
            }
            else {
                FillRemoteFallback(ref state);
            }
            return state;
        }

        /// <summary>
        /// 远端刀没有活读数。留空对多数樋是诚实的(空槽=没打中过),
        /// 但对"常态就在动"的介质会读成卡住的贴图,故用共享时钟补相位:<br/>
        /// 风樋的气流与潮樋的潮汐都不依赖玩家动作,补出来不算撒谎
        /// </summary>
        private static void FillRemoteFallback(ref OniMeiEngraveState state) {
            switch (state.HiKey) {
                case nameof(MeiKazehi):
                    state.HiPhase = Main.GlobalTimeWrappedHourly * 0.75f % 1f;
                    //顺风常态就有气流,给一档静息强度而不是空槽
                    state.HiFill = 0.35f;
                    break;
                case nameof(MeiShiohi):
                    //潮汐是固定周期,用各端一致的帧计数推,远端也看得见涨落
                    int period = OniMeiCombat.TidePeriodTicks;
                    if (period > 0) {
                        state.HiPhase = Main.GameUpdateCount % (ulong)period / (float)period;
                    }
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 刀体主绘之后叠这一层(残影不画)。调用方须处于刀体所在的 SpriteBatch 批内;
        /// alpha 已含刀体的深度权重与速度淡出
        /// </summary>
        public static void Draw(SpriteBatch sb, in OniBladeProfile.BladeXform xform,
            in OniMeiEngraveState state, float alpha) {
            if (Main.dedServ || alpha <= 0.02f || !xform.Valid || !state.AnyEngraved) {
                return;
            }
            float time = Main.GlobalTimeWrappedHourly;
            DrawNakagoTraits(sb, in xform, in state, alpha);
            DrawHi(sb, in xform, in state, alpha, time);
            DrawHorimono(sb, in xform, in state, alpha, time);
        }

        //==================== 茎铭的常态刀相 ====================

        /// <summary>
        /// 把只写在面板上的负担还给刀身:铁截的 0.90 面板伤是"钝",
        /// 友切的 1.06 承伤是"咎"。两者都常驻可见,不必等触发
        /// </summary>
        private static void DrawNakagoTraits(SpriteBatch sb, in OniBladeProfile.BladeXform xform,
            in OniMeiEngraveState state, float alpha) {
            if (state.NakagoKey == nameof(MeiTessetsu)) {
                DrawDullEdge(sb, in xform, alpha);
            }
            if (state.NakagoKey == nameof(MeiShishinoko) && state.LionChain > 0.01f) {
                DrawLionCharge(sb, in xform, state.LionChain, alpha);
            }
            if (state.BladeCrack > 0.01f) {
                DrawGuiltCracks(sb, in xform, state.BladeCrack, alpha);
            }
        }

        /// <summary>
        /// 狮子之子「狮势」:每续一拍,金象嵌自镡向锋多爬一程,前锋一点更亮。
        /// 攒到满即第五拍合颚——蓄了几拍看刃就知道,不必去数粒子
        /// </summary>
        private static void DrawLionCharge(SpriteBatch sb, in OniBladeProfile.BladeXform xform,
            float chain, float alpha) {
            const float RootU = 0.86f;
            const float TipU = 0.14f;
            const int Steps = 12;
            float thick = xform.MapLength(2.6f);
            if (thick < 0.5f) {
                return;
            }
            float filled = MathHelper.Clamp(chain, 0f, 1f);
            int lit = Math.Max(1, (int)MathF.Ceiling(Steps * filled));
            Vector2 previous = xform.Map(OniBladeProfile.EdgePx(RootU, -1.2f)) - Main.screenPosition;
            Vector2 head = previous;
            for (int i = 1; i <= lit; i++) {
                float span = MathHelper.Clamp(i / (float)Steps / MathF.Max(filled, 0.01f), 0f, 1f);
                float u = MathHelper.Lerp(RootU, TipU, MathHelper.Lerp(0f, filled, span));
                Vector2 current = xform.Map(OniBladeProfile.EdgePx(u, -1.2f)) - Main.screenPosition;
                //越靠前锋越亮:金在往刀尖聚
                float weight = 0.45f + 0.55f * (i / (float)lit);
                Seg(sb, previous, current, OnikiriUITheme.GoldDeep * (alpha * 0.55f * weight), thick);
                Seg(sb, previous, current, OnikiriUITheme.GoldInlay * (alpha * 0.60f * weight), thick * 0.45f);
                previous = current;
                head = current;
            }
            //前锋一粒:这一拍攒到哪儿
            Blob(sb, head, OnikiriUITheme.GoldInlay * (alpha * 0.75f), thick * 0.85f);
            if (filled >= 0.99f) {
                //满链:整条金线过热,下一拍就是合颚
                Blob(sb, head, OnikiriUITheme.HotWhite * (alpha * 0.5f), thick * 0.55f);
            }
        }

        /// <summary>
        /// 铁截「钝刃」:刃缘上压一条哑光灰,截金的代价是刀不再快
        /// </summary>
        private static void DrawDullEdge(SpriteBatch sb, in OniBladeProfile.BladeXform xform, float alpha) {
            const int Steps = 10;
            float thick = xform.MapLength(2.2f);
            if (thick < 0.5f) {
                return;
            }
            Vector2 previous = xform.Map(OniBladeProfile.EdgePx(0.12f, -0.8f)) - Main.screenPosition;
            for (int i = 1; i <= Steps; i++) {
                float u = MathHelper.Lerp(0.12f, 0.88f, i / (float)Steps);
                Vector2 current = xform.Map(OniBladeProfile.EdgePx(u, -0.8f)) - Main.screenPosition;
                //近锋处最钝,越靠镡越淡:钝口是磨出来的
                float weight = 1f - i / (float)Steps * 0.55f;
                Seg(sb, previous, current, DullSteel * (alpha * 0.42f * weight), thick);
                previous = current;
            }
        }

        /// <summary>
        /// 友切「咎」:每积一层,刀身中段多一道细发裂。
        /// 疾走越来越贵这件事于是写在刀上,而不只写在气力条上。<br/>
        /// 落在中段而非茎侧:柄那一段被手挡着,且裂在柄上不成立
        /// </summary>
        private static void DrawGuiltCracks(SpriteBatch sb, in OniBladeProfile.BladeXform xform,
            float crack, float alpha) {
            int count = (int)MathF.Ceiling(MathHelper.Clamp(crack, 0f, 1f) * 3f);
            float thick = xform.MapLength(1.4f);
            if (thick < 0.4f) {
                return;
            }
            for (int i = 0; i < count; i++) {
                float hash = OniBrush.Hash01(i * 89 + 23);
                float u = 0.46f + i * 0.09f;
                Vector2 root = xform.Map(OniBladeProfile.SpinePx(u)) - Main.screenPosition;
                Vector2 toEdge = xform.Map(OniBladeProfile.EdgePx(u)) - Main.screenPosition - root;
                if (toEdge.LengthSquared() < 0.5f) {
                    continue;
                }
                //裂自茎向刃走一小段再折,折线才读作裂而非划痕
                Vector2 mid = root + toEdge * (0.35f + hash * 0.2f);
                Vector2 tip = mid + toEdge.RotatedBy((hash - 0.5f) * 1.3f) * (0.25f + hash * 0.2f);
                Seg(sb, root, mid, CrackInk * (alpha * 0.8f), thick);
                Seg(sb, mid, tip, CrackInk * (alpha * 0.6f), thick * 0.7f);
            }
        }

        //==================== 樋(血槽) ====================

        /// <summary>樋槽轴上 u 处的贴图 px 落点:自剪影中线向栋侧让一点</summary>
        private static Vector2 GroovePx(float u)
            => Vector2.Lerp(OniBladeProfile.SpinePx(u), OniBladeProfile.BackPx(u), HiTowardBack);

        private static void DrawHi(SpriteBatch sb, in OniBladeProfile.BladeXform xform,
            in OniMeiEngraveState state, float alpha, float time) {
            if (string.IsNullOrEmpty(state.HiKey)) {
                return;
            }
            float width = xform.MapLength(OniBladeProfile.Thickness(0.5f) * HiWidthOfThickness);
            if (width < MinGrooveWidth) {
                return;
            }

            //槽腔:一条沿真实剪影走的暗线,给填充留底
            Span<Vector2> path = stackalloc Vector2[HiSegments + 1];
            for (int i = 0; i <= HiSegments; i++) {
                float u = MathHelper.Lerp(HiStartU, HiEndU, i / (float)HiSegments);
                path[i] = xform.Map(GroovePx(u)) - Main.screenPosition;
            }
            //受光唇:光固定自左上来,凿口下缘接住光——这条才是"凹进去"的主读物
            Vector2 lip = new Vector2(0.55f, 0.8f) * MathF.Max(1f, width * 0.5f);
            for (int i = 0; i < HiSegments; i++) {
                float t = (i + 0.5f) / HiSegments;
                float taper = GrooveTaper(t);
                Seg(sb, path[i], path[i + 1], GrooveShell * (alpha * 0.85f), width * taper);
                Seg(sb, path[i] + lip, path[i + 1] + lip,
                    GrooveLip * (alpha * 0.22f * taper), width * 0.4f * taper);
            }

            switch (state.HiKey) {
                case nameof(MeiChihi):
                    DrawBloodGroove(sb, path, in state, alpha, width);
                    break;
                case nameof(MeiKazehi):
                    DrawWindGroove(sb, path, in state, alpha, width);
                    break;
                case nameof(MeiKogehi):
                    DrawScorchGroove(sb, path, in state, alpha, width);
                    break;
                case nameof(MeiKanhi):
                    DrawQuietGroove(sb, path, in state, alpha, width, time);
                    break;
                case nameof(MeiTodohi):
                    DrawStickyGroove(sb, path, in state, alpha, width);
                    break;
                case nameof(MeiShiohi):
                    DrawTideGroove(sb, path, in state, alpha, width);
                    break;
                case nameof(MeiShiorihi):
                    DrawPetalGroove(sb, path, in state, alpha, width);
                    break;
                default:
                    break;
            }
        }

        /// <summary>沿槽 t∈[0,1] 取点(t 以段为单位插值)</summary>
        private static Vector2 Along(ReadOnlySpan<Vector2> path, float t) {
            float f = MathHelper.Clamp(t, 0f, 1f) * HiSegments;
            int i = Math.Min((int)f, HiSegments - 1);
            return Vector2.Lerp(path[i], path[i + 1], f - i);
        }

        /// <summary>槽体两端收窄,不做齐头齐尾的贴纸条</summary>
        private static float GrooveTaper(float t)
            => MathHelper.Clamp(MathF.Sin(t * MathHelper.Pi) * 1.35f, 0.25f, 1f);

        /// <summary>
        /// 血樋「回流」:血自锋侧涌起沿槽往柄走,湿front 在前、干涸段在后;
        /// 命中一记把血位顶满,随后慢慢排空
        /// </summary>
        private static void DrawBloodGroove(SpriteBatch sb, ReadOnlySpan<Vector2> path,
            in OniMeiEngraveState state, float alpha, float width) {
            float fill = MathHelper.Clamp(state.HiFill + state.HiPulse * 0.35f, 0f, 1f);
            if (fill <= 0.01f) {
                return;
            }
            float wet = fill * HiSegments;
            int full = (int)wet;
            for (int i = 0; i < HiSegments; i++) {
                if (i > full) {
                    break;
                }
                float t = (i + 0.5f) / HiSegments;
                float taper = GrooveTaper(t);
                Vector2 b = path[i + 1];
                //末段按血位截断,血面停在槽中而非整条齐平
                if (i == full) {
                    b = Vector2.Lerp(path[i], b, MathHelper.Clamp(wet - full, 0.08f, 1f));
                }
                //越靠柄越暗越稠(先涌到的血已经在氧化)
                float age = 1f - t;
                Color body = Color.Lerp(BloodBody, new Color(72, 8, 14), age * 0.6f);
                Seg(sb, path[i], b, body * (alpha * 0.9f), width * 0.78f * taper);
            }
            //湿面:血位前沿一小段更亮更薄,读作还在流
            int frontIndex = Math.Min(full, HiSegments - 1);
            Vector2 fa = path[frontIndex];
            Vector2 fb = Vector2.Lerp(fa, path[frontIndex + 1],
                MathHelper.Clamp(wet - frontIndex, 0.1f, 1f));
            float pulse = 0.55f + state.HiPulse * 0.45f;
            Seg(sb, fa, fb, BloodFront * (alpha * pulse), width * 0.5f);
        }

        /// <summary>
        /// 风樋「顺风」:槽里跑的是空气不是液体——三段错相的高速细亮丝掠过槽腔,
        /// 不挂壁、不留痕,疾走时跑得更急更亮
        /// </summary>
        private static void DrawWindGroove(SpriteBatch sb, ReadOnlySpan<Vector2> path,
            in OniMeiEngraveState state, float alpha, float width) {
            float gust = 0.45f + state.HiFill * 0.55f;
            for (int k = 0; k < 3; k++) {
                float head = state.HiPhase + k / 3f;
                head -= MathF.Floor(head);
                //气丝本身有长度,越急拖得越长
                float len = 0.13f + state.HiFill * 0.12f;
                float tail = head - len;
                if (tail < 0f) {
                    tail = 0f;
                }
                if (head - tail < 0.02f) {
                    continue;
                }
                Vector2 a = Along(path, tail);
                Vector2 b = Along(path, head);
                //头亮尾淡:两段叠出速度而非一根均匀亮条
                Seg(sb, a, b, AirStreak * (alpha * 0.16f * gust), width * 0.42f);
                Seg(sb, Vector2.Lerp(a, b, 0.55f), b, AirStreak * (alpha * 0.34f * gust), width * 0.24f);
            }
        }

        /// <summary>
        /// 焦樋「焦痕」:炭床垫底,几粒余烬贴着槽往柄爬,各自明灭;
        /// 烬后拖出更深的炭色,读作烧过一遍
        /// </summary>
        private static void DrawScorchGroove(SpriteBatch sb, ReadOnlySpan<Vector2> path,
            in OniMeiEngraveState state, float alpha, float width) {
            float heat = MathHelper.Clamp(state.HiFill, 0f, 1f);
            for (int i = 0; i < HiSegments; i++) {
                float t = (i + 0.5f) / HiSegments;
                Seg(sb, path[i], path[i + 1], CharBed * (alpha * 0.7f * GrooveTaper(t)), width * 0.7f);
            }
            if (heat <= 0.02f) {
                return;
            }
            const int Embers = 4;
            for (int k = 0; k < Embers; k++) {
                float at = state.HiPhase + k / (float)Embers;
                at -= MathF.Floor(at);
                //各烬独立明灭:同一颗在爬完前会暗下去再亮起
                float flicker = 0.45f + 0.55f * MathF.Abs(MathF.Sin((at + k * 0.37f) * 11f));
                float life = MathF.Sin(at * MathHelper.Pi);
                float glow = heat * flicker * life;
                if (glow <= 0.05f) {
                    continue;
                }
                Vector2 head = Along(path, at);
                Vector2 back = Along(path, MathF.Max(0f, at - 0.05f));
                Seg(sb, back, head, EmberHot * (alpha * 0.45f * glow), width * 0.42f);
                Seg(sb, head, Vector2.Lerp(head, back, 0.3f),
                    Color.Lerp(EmberHot, OnikiriUITheme.BurnHot, 0.5f) * (alpha * 0.7f * glow), width * 0.24f);
            }
        }

        /// <summary>
        /// 闲樋「闲息」:接上脱战窗时槽面浮一层极淡白息并缓慢起伏,唇线转锐;
        /// 交战中槽面压暗——玩家第一次能看见那 120 帧窗口的开合
        /// </summary>
        private static void DrawQuietGroove(SpriteBatch sb, ReadOnlySpan<Vector2> path,
            in OniMeiEngraveState state, float alpha, float width, float time) {
            float calm = MathHelper.Clamp(state.HiFill, 0f, 1f);
            if (calm <= 0.02f) {
                //未入闲息:槽里积着一层浊,把"没在回气"说清楚
                for (int i = 0; i < HiSegments; i++) {
                    float t = (i + 0.5f) / HiSegments;
                    Seg(sb, path[i], path[i + 1], new Color(38, 26, 28) * (alpha * 0.55f * GrooveTaper(t)),
                        width * 0.62f);
                }
                return;
            }
            //一口息自柄向锋走完再退回,起伏极慢
            float breath = 0.5f + 0.5f * MathF.Sin(time * 0.9f);
            for (int i = 0; i < HiSegments; i++) {
                float t = (i + 0.5f) / HiSegments;
                //息头所在处最厚,两端稀
                float band = MathF.Exp(-MathF.Abs(t - (1f - breath)) * 5.5f);
                float a = alpha * calm * (0.10f + 0.22f * band);
                Seg(sb, path[i], path[i + 1], BreathFilm * a, width * (0.45f + 0.3f * band));
            }
        }

        /// <summary>
        /// 滞樋「滞缚」:墨不流只挂——沿槽结着大小不一的墨珠,珠体带一点湿高光;
        /// 每轮只有一颗慢慢往柄滑,滑到底重挂
        /// </summary>
        private static void DrawStickyGroove(SpriteBatch sb, ReadOnlySpan<Vector2> path,
            in OniMeiEngraveState state, float alpha, float width) {
            float amount = MathHelper.Clamp(state.HiFill + state.HiPulse * 0.4f, 0f, 1f);
            if (amount <= 0.02f) {
                return;
            }
            const int Beads = 5;
            for (int k = 0; k < Beads; k++) {
                float hash = OniBrush.Hash01(k * 37 + 5);
                float at = 0.14f + k * (0.72f / (Beads - 1)) + (hash - 0.5f) * 0.05f;
                //本轮轮到的那颗在往柄滑
                bool sliding = (int)(state.HiPhase * Beads) == k;
                if (sliding) {
                    at += (state.HiPhase * Beads % 1f) * 0.10f;
                }
                Vector2 pos = Along(path, MathHelper.Clamp(at, 0f, 1f));
                float r = width * (0.42f + hash * 0.34f) * amount;
                if (r < 0.5f) {
                    continue;
                }
                //珠体两层:暗墨壳 + 偏上的一点湿高光,不做发光球
                Blob(sb, pos, InkBead * (alpha * 0.92f), r);
                Blob(sb, pos - new Vector2(0.35f, 0.5f) * r, GrooveLip * (alpha * 0.22f), r * 0.34f);
            }
        }

        /// <summary>
        /// 潮樋「潮拍」:节拍器搬到刀上——槽内水位随 48 帧周期自柄涨到锋再退,
        /// 水面是一条明确的横线;合潮窗整槽提亮一线,不必再盯 HUD 游标
        /// </summary>
        private static void DrawTideGroove(SpriteBatch sb, ReadOnlySpan<Vector2> path,
            in OniMeiEngraveState state, float alpha, float width) {
            //相位 0.5 为满潮(与 OniMeiCombat.IsTideOnBeat 的窗心同源)
            float level = 0.5f - 0.5f * MathF.Cos(state.HiPhase * MathHelper.TwoPi);
            float onBeat = state.HiArmed ? 1f : 0f;
            //水自柄侧(t=1)往锋侧涨,故淹没段是 [1-level, 1]
            float surface = 1f - level;
            for (int i = HiSegments - 1; i >= 0; i--) {
                float t = (i + 0.5f) / HiSegments;
                if (t < surface) {
                    break;
                }
                float depth = MathHelper.Clamp((t - surface) / MathF.Max(level, 0.05f), 0f, 1f);
                Color body = Color.Lerp(TideBody, new Color(38, 18, 30), depth * 0.7f);
                Seg(sb, path[i], path[i + 1], body * (alpha * 0.85f), width * 0.74f * GrooveTaper(t));
            }
            if (level > 0.03f && level < 0.98f) {
                //水面横线:潮位读数的主体
                Vector2 a = Along(path, MathF.Max(0f, surface - 0.035f));
                Vector2 b = Along(path, MathF.Min(1f, surface + 0.035f));
                Seg(sb, a, b, TideCrest * (alpha * (0.5f + onBeat * 0.5f)), width * (0.45f + onBeat * 0.3f));
            }
            if (onBeat > 0f) {
                //合潮:整条槽提一线,拍点看得见也就打得准
                for (int i = 0; i < HiSegments; i++) {
                    float t = (i + 0.5f) / HiSegments;
                    Seg(sb, path[i], path[i + 1], TideCrest * (alpha * 0.30f * GrooveTaper(t)), width * 0.3f);
                }
            }
        }

        /// <summary>
        /// 谢樋「剪落」:槽里压着几片斜落的瓣痕,越积越满;
        /// 剪落触发时整排一亮,最末一片被抖出槽外
        /// </summary>
        private static void DrawPetalGroove(SpriteBatch sb, ReadOnlySpan<Vector2> path,
            in OniMeiEngraveState state, float alpha, float width) {
            float amount = MathHelper.Clamp(state.HiFill + state.HiPulse * 0.5f, 0f, 1f);
            if (amount <= 0.02f) {
                return;
            }
            const int Petals = 5;
            for (int k = 0; k < Petals; k++) {
                float hash = OniBrush.Hash01(k * 53 + 17);
                float at = 0.12f + k * (0.76f / (Petals - 1));
                Vector2 mid = Along(path, at);
                Vector2 dir = Along(path, MathF.Min(1f, at + 0.05f)) - mid;
                if (dir.LengthSquared() < 0.01f) {
                    continue;
                }
                //瓣痕斜压在槽里,不与槽同向,才不读作虚线
                Vector2 lean = dir.SafeNormalize(Vector2.UnitX)
                    .RotatedBy(MathHelper.Lerp(0.6f, 1.1f, hash)) * width * (0.7f + hash * 0.5f);
                float shed = state.HiPulse * (k == Petals - 1 ? 1f : 0.25f);
                Vector2 offset = lean * shed * 0.8f;
                Seg(sb, mid - lean * 0.5f + offset, mid + lean * 0.5f + offset,
                    PetalMark * (alpha * amount * (0.42f + state.HiPulse * 0.45f)), width * 0.34f);
            }
        }

        //==================== 雕位(彫物) ====================

        private static void DrawHorimono(SpriteBatch sb, in OniBladeProfile.BladeXform xform,
            in OniMeiEngraveState state, float alpha, float time) {
            if (string.IsNullOrEmpty(state.HorimonoKey)) {
                return;
            }
            float size = xform.MapLength(MathF.Min(HorimonoMarkPx,
                OniBladeProfile.Thickness(HoriU) * HorimonoMaxOfThickness));
            if (size < 5f) {
                return;
            }
            Vector2 pos = xform.Map(OniBladeProfile.SpinePx(HoriU)) - Main.screenPosition;
            OniMeiGlyphStyle style = new() {
                Alpha = alpha * 0.92f,
                Rotation = GlyphRotation(in xform),
                ChiselReveal = -1f,
                Accent = state.HorimonoGold ? OnikiriUITheme.GoldInlay : OnikiriUITheme.Bright,
                Inlay = state.HorimonoGold ? 1f : 0f,
                Lit = MathHelper.Clamp(state.HoriLit, 0f, 1f),
                Time = time,
            };
            OniMeiGlyph.Draw(sb, state.HorimonoKey, pos, size, in style);
        }

        //====茎铭不在世界里画====
        //茎是插在柄里的部分,刀装好了就看不见茎铭——这正是改铭台要把刀拆开展示它的理由。
        //在世画它既不成立(位置落在柄上、还撞玩家的手),也只是"加了个考据细节"的姿态。
        //茎铭在世的职责改为常态刀相,见 DrawNakagoTraits(铁截钝刃/友切咎裂/狮势金线)

        /// <summary>字沿刀立正:字形的"下"指向柄尾</summary>
        private static float GlyphRotation(in OniBladeProfile.BladeXform xform)
            => xform.MapDir(OniBladeProfile.SpritePommel - OniBladeProfile.SpriteTip).ToRotation()
            - MathHelper.PiOver2;

        //==================== 段绘 ====================

        private static Texture2D Pixel => VaultAsset.placeholder2.Value;
        private static readonly Rectangle PixelSrc = new(0, 0, 1, 1);

        private static void Seg(SpriteBatch sb, Vector2 a, Vector2 b, Color color, float thick) {
            Vector2 edge = b - a;
            float len = edge.Length();
            if (len < 0.4f || thick < 0.35f) {
                return;
            }
            sb.Draw(Pixel, a, PixelSrc, color, edge.ToRotation(),
                new Vector2(0f, 0.5f), new Vector2(len, thick), SpriteEffects.None, 0f);
        }

        /// <summary>墨珠一类的小实体:45° 方块作菱形珠体,不用径向渐变球充数</summary>
        private static void Blob(SpriteBatch sb, Vector2 center, Color color, float radius) {
            if (radius < 0.35f) {
                return;
            }
            sb.Draw(Pixel, center, PixelSrc, color, MathHelper.PiOver4,
                new Vector2(0.5f), new Vector2(radius * 1.55f), SpriteEffects.None, 0f);
        }
    }
}
