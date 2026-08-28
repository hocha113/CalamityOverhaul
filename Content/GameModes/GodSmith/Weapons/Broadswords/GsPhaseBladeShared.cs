using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 光剑族色板：刃缘/体色/过热/光弧四色 + 本色音高偏移（七色的次要变奏载体）
    /// </summary>
    internal readonly struct GsPhasebladePalette
    {
        /// <summary>刃缘亮色</summary>
        public readonly Color Edge;
        /// <summary>束刃体色</summary>
        public readonly Color Body;
        /// <summary>过热白炽色</summary>
        public readonly Color Hot;
        /// <summary>光弧体色</summary>
        public readonly Color Arc;
        /// <summary>本色音高偏移（每色的微小 rider）</summary>
        public readonly float Pitch;

        public GsPhasebladePalette(Color edge, Color body, Color hot, Color arc, float pitch) {
            Edge = edge;
            Body = body;
            Hot = hot;
            Arc = arc;
            Pitch = pitch;
        }

        public static readonly GsPhasebladePalette Blue = new(new(165, 220, 255), new(70, 140, 255), new(235, 250, 255), new(105, 175, 255), -0.04f);
        public static readonly GsPhasebladePalette Red = new(new(255, 160, 150), new(255, 75, 85), new(255, 235, 225), new(255, 115, 105), -0.08f);
        public static readonly GsPhasebladePalette Green = new(new(170, 255, 180), new(85, 235, 110), new(240, 255, 235), new(120, 245, 140), 0f);
        public static readonly GsPhasebladePalette Purple = new(new(215, 165, 255), new(170, 85, 255), new(250, 240, 255), new(190, 125, 255), -0.12f);
        public static readonly GsPhasebladePalette White = new(new(245, 248, 255), new(205, 216, 235), new(255, 255, 255), new(225, 232, 248), 0.10f);
        public static readonly GsPhasebladePalette Yellow = new(new(255, 240, 160), new(255, 218, 70), new(255, 255, 235), new(255, 228, 110), 0.06f);
        public static readonly GsPhasebladePalette Orange = new(new(255, 200, 140), new(255, 150, 55), new(255, 240, 220), new(255, 172, 92), 0.02f);
        /// <summary>兜底色板（方案查询失败时的中性白蓝）</summary>
        public static readonly GsPhasebladePalette Fallback = new(new(230, 240, 255), new(160, 190, 235), new(255, 255, 255), new(190, 212, 245), 0f);
    }

    /// <summary>
    /// 【光剑族方案核心】材质：等离子束刃（核心炽白线 + 色罩束鞘 + 端头收口光点）。
    /// 族签名「充能弧」：①能量刃命中积攒充能，刃身随充能延展变亮、嗡鸣升调
    /// ②满充能后下一次终结拍放电，甩出一道等离子光弧③七色共享机制，色板与音高是次要变奏。<br/>
    /// 联机纪律：方案单例跨玩家共享，充能只在 myPlayer 守门路径读写，
    /// 远端靠手持弹幕 ai[2] 看到同一场延展与放电
    /// </summary>
    internal abstract class GsPhasebladeSchemeCore : GsBroadswordScheme
    {
        /// <summary>族色板（七色子类指定，与手持侧同一静态实例）</summary>
        internal abstract GsPhasebladePalette Palette { get; }

        /// <summary>充能上限（Phasesaber 档更高）</summary>
        internal virtual int ChargeMax => 5;

        /// <summary>当前充能；跨玩家共享单例，只在 myPlayer 守门路径读写</summary>
        internal int Charge;

        protected override string GsDescFallback =>
            "Reforged: a plasma edge that charges as it cuts; every hit extends the blade, " +
            "and at full charge the next finishing slash hurls a plasma arc";

        //底伤不动：拍均 0.92x、三拍循环约 62 帧（原版 75 帧）提速约 21%，
        //满充光弧 0.5x 单目标约每两轮连段一发（5 命中充满），
        //悲观全命中约 121%、典型 105%~118%；Phaseblade 系前硬模式中游剑，
        //按公约注明放宽至 125% 上限内。充能延展刃长记为触及收益，不进伤害预算
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 【光剑（Phasesaber）档方案核心】精制等离子刃：充能上限更高、刃长延展更远；
    /// 终结拍必有小光刃延展斩，满充能时升级为过载光弧，命中再炸开等离子爆裂
    /// </summary>
    internal abstract class GsPhasesaberSchemeCore : GsPhasebladeSchemeCore
    {
        internal override int ChargeMax => 6;

        protected override string GsDescFallback =>
            "Reforged: a perfected plasma saber; hits overcharge the blade for greater reach, " +
            "every finishing slash casts a short energy arc, and a full charge unleashes " +
            "an overloaded arc that bursts on impact";

        //底伤不动：拍均 0.94x、三拍循环约 68 帧（原版 75 帧）小幅提速，
        //终结小光刃 0.25x 每轮、满充过载弧 0.5x + 爆裂 0.25x 约每三轮一发（6 命中充满），
        //悲观全命中约 119%、典型 108%~116%，在 120% 包络内
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 光剑族手持核心：三拍连段（顺斩/返斩/终结轮斩）。
    /// 能量刃视觉以自绘束刃层为主：原版贴图压暗垫底当发射器，
    /// 色鞘 + 炽白核心线 + 端头收口光点，挥动时刃身沿走向速度拉伸。
    /// 充能出手时由 owner 写入 ai[2] 随包过线，全端一致地延展刃长、抬升音调。
    /// ai[0]=拍号 ai[1]=交替符号 ai[2]=本次挥砍充能数
    /// </summary>
    internal abstract class GsPhasebladeHeldCore : GsBroadswordHeldBase
    {
        /// <summary>族色板（与方案侧同一静态实例）</summary>
        protected abstract GsPhasebladePalette Palette { get; }

        protected sealed override Color EdgeBright => Palette.Edge;
        protected sealed override Color BodyMain => Palette.Body;
        protected sealed override Color HotAccent => Palette.Hot;

        //==================== 档位参数（Phasesaber 档重写） ====================

        /// <summary>每层充能的刃长延展</summary>
        protected virtual float ReachPerCharge => 0.045f;
        /// <summary>光弧伤害系数（对当前拍伤害，终结拍 1.05x 后约合底伤 0.5x）</summary>
        protected virtual float ArcDamageFactor => 0.48f;
        /// <summary>放电甩出的光弧档位（Phasesaber 档 2：更大且命中爆裂）</summary>
        protected virtual float OverloadTier => 1f;
        /// <summary>束刃色鞘宽度</summary>
        protected virtual float SheathWidth => 26f;
        /// <summary>炽白核心线宽度</summary>
        protected virtual float CoreWidth => 7f;
        /// <summary>刃尖收口光点尺度</summary>
        protected virtual float TipScale => 0.14f;

        protected override float BaseReach => 108f;
        //等离子灼烧不见血
        protected override bool BleedOnFlesh => false;
        //能量刃常亮
        protected override bool GlowAlways => true;
        protected override Color GlowColor => OverloadSwing ? Palette.Hot : Palette.Body;

        /// <summary>充能上限缓存（出手时从方案侧取，单一事实源）</summary>
        protected int chargeMaxCache = 5;
        private bool arcFired;

        /// <summary>本次挥砍的充能数（ai[2] 随包过线，全端一致）</summary>
        protected int ChargeAtSwing => Math.Clamp((int)Projectile.ai[2], 0, chargeMaxCache);
        /// <summary>是否满充放电斩（终结拍 + 满充能）</summary>
        protected bool OverloadSwing => IsFinisher && ChargeAtSwing >= chargeMaxCache;

        /// <summary>方案实例（充能记账用；跨玩家共享，只在 myPlayer 路径写）</summary>
        protected GsPhasebladeSchemeCore Core =>
            GodSmithScheme.TryGetScheme(SwordItemID, out GodSmithScheme s) ? s as GsPhasebladeSchemeCore : null;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 顺斩：轻快平抹，能量刃没有钢铁的惯性
            0 => new GsBroadBeat {
                Raise = 5, Hold = 1, Slash = 4, Recover = 8,
                RaiseBack = 1.75f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.04f,
                DamageMult = 0.85f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0f,
            },
            //拍1 返斩：更短的回手
            1 => new GsBroadBeat {
                Raise = 4, Hold = 1, Slash = 4, Recover = 8,
                RaiseBack = 1.8f, Follow = 1.05f, ReachScale = 1f, LeanAmp = 0.042f,
                DamageMult = 0.85f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.08f,
            },
            //拍2 终结轮斩：小前压，满充能时在此放电
            _ => new GsBroadBeat {
                Raise = 6, Hold = 2, Slash = 4, Recover = 9,
                RaiseBack = 2.1f, Follow = 1.2f, ReachScale = 1.1f, LeanAmp = 0.07f,
                DamageMult = 1.05f, Hitstop = 2, LungeSpeed = 2.4f, SwingPitch = -0.12f,
            },
        };

        /// <summary>出手时 owner 把方案侧充能写进 ai[2]；生成包不含本值，补一发同步（远端只用于演出）</summary>
        protected override void OnStageInit() {
            chargeMaxCache = Core?.ChargeMax ?? 5;
            if (Owner.whoAmI == Main.myPlayer) {
                Projectile.ai[2] = Math.Clamp(Core?.Charge ?? 0, 0, chargeMaxCache);
                Projectile.netUpdate = true;
            }
        }

        /// <summary>充能延展刃长：全端从 ai[2] 推导，几何逐帧重算</summary>
        protected override void UpdateBladeTransform(int phase) {
            reachScale = Beat.ReachScale * (1f + ReachPerCharge * ChargeAtSwing);
            base.UpdateBladeTransform(phase);
        }

        /// <summary>能量嗡鸣：Item15 相位嗡鸣 + Item1 切风；充能抬升音调，色板再偏移</summary>
        protected override void PlaySwingSound() {
            float pitch = Beat.SwingPitch + Palette.Pitch + 0.05f * ChargeAtSwing;
            SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.72f, Pitch = pitch }, Owner.Center);
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.3f, Pitch = pitch + 0.12f }, Owner.Center);
            if (OverloadSwing) {
                //放电斩：低鸣加厚 + 一记电浆哨音
                SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.6f, Pitch = pitch - 0.5f }, Owner.Center);
                SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.45f, Pitch = 0.2f + Palette.Pitch }, Owner.Center);
            }
        }

        /// <summary>终结拍放电：满充能甩出等离子光弧并清空充能；未满充能走档位钩子</summary>
        protected override void OnSlashBegin() {
            if (!IsFinisher || arcFired) {
                return;
            }
            arcFired = true;
            if (!OverloadSwing) {
                OnFinisherWithoutCharge();
                return;
            }
            SetFlash(8);
            //清账只在 myPlayer；远端靠 ai[2] 看到同一场放电
            if (Owner.whoAmI == Main.myPlayer && Core != null) {
                Core.Charge = 0;
            }
            Vector2 dir = baseAngle.ToRotationVector2();
            int dmg = Math.Max(1, (int)(Projectile.damage * ArcDamageFactor));
            SpawnOwnedProj(ModContent.ProjectileType<GsPhasebladeArcProj>(),
                Hand + dir * (FullReach * 0.9f), dir * 12f, dmg, Projectile.knockBack * 0.5f,
                swingDir, OverloadTier, SwordItemID);
        }

        /// <summary>未满充能的终结拍追加（Phasesaber 档甩小光刃）</summary>
        protected virtual void OnFinisherWithoutCharge() { }

        /// <summary>命中记账：非放电斩每个目标 +1 充能；攒满一记升调提示音 + 刃闪</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Owner.whoAmI != Main.myPlayer || OverloadSwing) {
                return;
            }
            GsPhasebladeSchemeCore core = Core;
            if (core == null || core.Charge >= core.ChargeMax) {
                return;
            }
            core.Charge++;
            if (core.Charge >= core.ChargeMax) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.55f, Pitch = 0.25f + Palette.Pitch }, Owner.Center);
                SetFlash(6);
            }
        }

        /// <summary>等离子演出：束刃逸散光屑；放电斩蓄力时光屑向刃身汇聚</summary>
        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            if (fanFade > 0.3f && Main.rand.NextBool(4)) {
                Vector2 at = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.35f, 1f));
                PRTLoader.NewParticle<PRT_Light>(at,
                    -Vector2.UnitY * Main.rand.NextFloat(0.2f, 0.7f),
                    Palette.Body, Main.rand.NextFloat(0.05f, 0.09f))?.Configure(9, 0.6f);
            }
            if (OverloadSwing && phase <= PhaseHold) {
                Vector2 hand = Hand;
                Vector2 at = hand + Main.rand.NextVector2Unit() * Main.rand.NextFloat(36f, 64f);
                PRTLoader.NewParticle<PRT_Light>(at, (Vector2.Lerp(hand, mainTip, 0.6f) - at) * 0.16f,
                    Palette.Edge, Main.rand.NextFloat(0.06f, 0.1f))?.Configure(8, 0.6f);
            }
        }

        /// <summary>命中反馈：等离子灼蚀，色光噼啪 + 一记短促电嘶（放电斩加量）</summary>
        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.22f, Pitch = 0.5f + Palette.Pitch }, target.Center);
            int motes = OverloadSwing ? 5 : 2;
            for (int i = 0; i < motes; i++) {
                PRTLoader.NewParticle<PRT_Light>(
                    target.Center + Main.rand.NextVector2Circular(10f, 10f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.4f),
                    Main.rand.NextBool() ? Palette.Edge : Palette.Body,
                    Main.rand.NextFloat(0.07f, 0.12f))?.Configure(11, 0.65f);
            }
        }

        //==================== 能量刃绘制 ====================

        /// <summary>原版贴图压暗当发射器底衬，能量层才是刀身本体</summary>
        protected override Color BodyTint(Color lightColor) => Color.Lerp(lightColor, new Color(26, 28, 40), 0.55f);

        /// <summary>原版贴图只画基准刃长：充能延展全交给能量层，发射器不跟着拉伸</summary>
        protected override void DrawBladeSet(SpriteBatch sb, Color lightColor) {
            float real = mainReach;
            mainReach = real * MathF.Min(1f, Beat.ReachScale / MathF.Max(reachScale, 0.01f));
            base.DrawBladeSet(sb, lightColor);
            mainReach = real;
        }

        /// <summary>能量束刃：速度拉伸残层 + 三层束刃 + 端头收口光点（绘制禁 Main.rand，全走确定性播种）</summary>
        protected override void DrawExtra(SpriteBatch sb, Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (glow == null || star == null) {
                return;
            }
            Vector2 hand = Hand;
            Vector2 unit = mainAngle.ToRotationVector2();
            Vector2 from = hand + unit * (mainReach * 0.14f);
            Vector2 to = hand + unit * (mainReach * 1.02f);
            Vector2 mid = (from + to) * 0.5f - Main.screenPosition;
            float len = Vector2.Distance(from, to);
            //等离子嗡鸣：亮度低频微颤
            float hum = 0.94f + 0.06f * MathF.Sin(Main.GlobalTimeWrappedHourly * 21f + DrawRand01(3) * 6.28f);
            float charged = ChargeAtSwing / (float)chargeMaxCache;
            float alpha = fanFade * hum * (0.8f + 0.2f * charged);

            //挥动速度拉伸：斩切期束刃沿走向甩出两层残层
            float sweep = MathF.Abs(mainAngle - lastAngle);
            if (sweep > 0.02f) {
                for (int i = 1; i <= 2; i++) {
                    float backAng = mainAngle - swingDir * sweep * i * 0.55f;
                    Vector2 bMid = hand + backAng.ToRotationVector2() * (mainReach * 0.58f) - Main.screenPosition;
                    Color bc = Palette.Body * (alpha * (i == 1 ? 0.30f : 0.14f));
                    bc.A = 0;
                    sb.Draw(glow, bMid, null, bc, backAng, glow.Size() * 0.5f,
                        new Vector2(len / glow.Width, SheathWidth * 1.2f / glow.Height), SpriteEffects.None, 0f);
                }
            }

            DrawBeamLayers(sb, glow, mid, len, alpha);

            //端头收口：刃尖光点随充能增亮，根部小点压住起笔
            Vector2 tipAt = to - Main.screenPosition;
            Color tipC = Palette.Edge * (alpha * (0.55f + 0.35f * charged));
            tipC.A = 0;
            sb.Draw(star, tipAt, null, tipC, 0f, star.Size() * 0.5f, TipScale, SpriteEffects.None, 0f);
            Color tipCore = Palette.Hot * (alpha * 0.7f);
            tipCore.A = 0;
            sb.Draw(star, tipAt, null, tipCore, 0f, star.Size() * 0.5f, TipScale * 0.45f, SpriteEffects.None, 0f);
            Color rootC = Palette.Body * (alpha * 0.5f);
            rootC.A = 0;
            sb.Draw(glow, from - Main.screenPosition, null, rootC, 0f, glow.Size() * 0.5f, 0.16f, SpriteEffects.None, 0f);

            DrawEnergyAccents(sb, hand, unit, alpha, charged);
        }

        /// <summary>束刃三层：外鞘（色）/内鞘（缘）/炽白核心线</summary>
        protected virtual void DrawBeamLayers(SpriteBatch sb, Texture2D glow, Vector2 mid, float len, float alpha) {
            float lenScale = len / glow.Width;
            Color sheath = Palette.Body * (alpha * 0.55f);
            sheath.A = 0;
            sb.Draw(glow, mid, null, sheath, mainAngle, glow.Size() * 0.5f,
                new Vector2(lenScale, SheathWidth / glow.Height), SpriteEffects.None, 0f);
            Color inner = Palette.Edge * (alpha * 0.6f);
            inner.A = 0;
            sb.Draw(glow, mid, null, inner, mainAngle, glow.Size() * 0.5f,
                new Vector2(lenScale * 0.98f, SheathWidth * 0.55f / glow.Height), SpriteEffects.None, 0f);
            Color core = Palette.Hot * (alpha * 0.85f);
            core.A = 0;
            sb.Draw(glow, mid, null, core, mainAngle, glow.Size() * 0.5f,
                new Vector2(lenScale * 0.96f, CoreWidth / glow.Height), SpriteEffects.None, 0f);
        }

        /// <summary>追加光饰：满充时刃尖旋转光斑预告放电（Phasesaber 档另加护手光点）</summary>
        protected virtual void DrawEnergyAccents(SpriteBatch sb, Vector2 hand, Vector2 unit, float alpha, float charged) {
            if (ChargeAtSwing < chargeMaxCache) {
                return;
            }
            Texture2D flare = CWRAsset.StarFlare01?.Value;
            if (flare == null) {
                return;
            }
            Vector2 tipAt = hand + unit * (mainReach * 1.02f) - Main.screenPosition;
            float rot = Main.GlobalTimeWrappedHourly * 2.2f + DrawRand01(7) * 6.28f;
            Color c = Palette.Hot * (alpha * 0.5f);
            c.A = 0;
            sb.Draw(flare, tipAt, null, c, rot, flare.Size() * 0.5f, 0.2f, SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    /// 光剑（Phasesaber）档手持核心：束刃更厚更长、双层鞘 + 护手光点；
    /// 未满充能的终结拍甩小光刃延展斩，满充过载弧命中炸开等离子爆裂
    /// </summary>
    internal abstract class GsPhasesaberHeldCore : GsPhasebladeHeldCore
    {
        protected override float ReachPerCharge => 0.05f;
        protected override float OverloadTier => 2f;
        protected override float SheathWidth => 31f;
        protected override float CoreWidth => 8f;
        protected override float TipScale => 0.17f;
        protected override float BaseReach => 120f;
        protected override float CollisionWidth => 44f;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 顺斩：比 Phaseblade 多半分权威
            0 => new GsBroadBeat {
                Raise = 5, Hold = 1, Slash = 4, Recover = 9,
                RaiseBack = 1.8f, Follow = 1.05f, ReachScale = 1f, LeanAmp = 0.045f,
                DamageMult = 0.88f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.02f,
            },
            //拍1 返斩
            1 => new GsBroadBeat {
                Raise = 5, Hold = 1, Slash = 4, Recover = 8,
                RaiseBack = 1.85f, Follow = 1.1f, ReachScale = 1.02f, LeanAmp = 0.05f,
                DamageMult = 0.88f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.06f,
            },
            //拍2 终结重轮斩：更长的举、更深的前压，小光刃/过载弧都在此出手
            _ => new GsBroadBeat {
                Raise = 7, Hold = 2, Slash = 5, Recover = 11,
                RaiseBack = 2.2f, Follow = 1.25f, ReachScale = 1.12f, LeanAmp = 0.08f,
                DamageMult = 1.05f, Hitstop = 2, LungeSpeed = 3.0f, SwingPitch = -0.16f,
            },
        };

        /// <summary>档位签名：未满充能的终结拍也送出小光刃延展斩</summary>
        protected override void OnFinisherWithoutCharge() {
            Vector2 dir = baseAngle.ToRotationVector2();
            int dmg = Math.Max(1, (int)(Projectile.damage * 0.24f));
            SpawnOwnedProj(ModContent.ProjectileType<GsPhasebladeArcProj>(),
                Hand + dir * (FullReach * 0.85f), dir * 9f, dmg, Projectile.knockBack * 0.35f,
                swingDir, 0f, SwordItemID);
        }

        /// <summary>双层鞘：基础三层外再罩一层弧色外晕，束刃更厚</summary>
        protected override void DrawBeamLayers(SpriteBatch sb, Texture2D glow, Vector2 mid, float len, float alpha) {
            Color outer = Palette.Arc * (alpha * 0.3f);
            outer.A = 0;
            sb.Draw(glow, mid, null, outer, mainAngle, glow.Size() * 0.5f,
                new Vector2(len / glow.Width * 1.02f, SheathWidth * 1.6f / glow.Height), SpriteEffects.None, 0f);
            base.DrawBeamLayers(sb, glow, mid, len, alpha);
        }

        /// <summary>护手光点：剑格处一粒定位星光，精制工艺的记号</summary>
        protected override void DrawEnergyAccents(SpriteBatch sb, Vector2 hand, Vector2 unit, float alpha, float charged) {
            base.DrawEnergyAccents(sb, hand, unit, alpha, charged);
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (star == null) {
                return;
            }
            Vector2 at = hand + unit * (mainReach * 0.14f) - Main.screenPosition;
            Color c = Palette.Edge * (alpha * 0.55f);
            c.A = 0;
            sb.Draw(star, at, null, c, MathHelper.PiOver4, star.Size() * 0.5f, 0.1f, SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    /// 等离子光弧：光剑族终结放电。出生 2 帧撑满带 10% 过冲，减速滑行、渐薄渐透；
    /// 色体 + 亮缘 + 炽白核心线，月牙双角收口光点。
    /// ai[0]=挥动符号 ai[1]=档位（0 小光刃 / 1 满充弧 / 2 过载弧）ai[2]=本体物品 ID（查方案取色板）。
    /// 过载弧首个命中炸开等离子爆裂
    /// </summary>
    internal class GsPhasebladeArcProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private float SwingSign => Projectile.ai[0] >= 0f ? 1f : -1f;
        private int Tier => Math.Clamp((int)Projectile.ai[1], 0, 2);
        private ref float Life => ref Projectile.localAI[0];
        private ref float BurstSpent => ref Projectile.localAI[1];

        /// <summary>按本体物品 ID 查方案取色板，查不到用兜底白</summary>
        internal static GsPhasebladePalette PaletteFor(int itemID)
            => GodSmithScheme.TryGetScheme(itemID, out GodSmithScheme s) && s is GsPhasebladeSchemeCore core
                ? core.Palette : GsPhasebladePalette.Fallback;

        private GsPhasebladePalette Pal => PaletteFor((int)Projectile.ai[2]);

        private float SizeMul => Tier switch { 0 => 0.72f, 2 => 1.3f, _ => 1f };

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 48;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 24;
            Projectile.timeLeft = 26;
        }

        public override void AI() {
            if (Life == 0f) {
                //档位就位（ai[1] 随生成包过线，各端一致）：小光刃短寿少穿，过载弧多穿
                if (Tier == 0) {
                    Projectile.timeLeft = 18;
                    Projectile.penetrate = 2;
                }
                else if (Tier == 2) {
                    Projectile.penetrate = 4;
                }
            }
            Life++;
            //减速滑行：12 → 约 4，光弧是刃外延伸不是远程光束
            Projectile.velocity *= 0.95f;
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, Pal.Body.ToVector3() * (0.35f * SizeMul));

            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                //航迹：等离子光屑从弧身脱离上浮
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + Main.rand.NextVector2Circular(14f, 14f) * SizeMul,
                    -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.8f),
                    Pal.Body, Main.rand.NextFloat(0.05f, 0.08f))?.Configure(9, 0.6f);
            }
        }

        public override bool? CanDamage() => Life >= 1f ? null : false;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //过载弧：首个命中炸开等离子爆裂（owner 端生成，随包过线）
            if (Tier == 2 && BurstSpent == 0f && Projectile.owner == Main.myPlayer) {
                BurstSpent = 1f;
                int dmg = Math.Max(1, (int)(Projectile.damage * 0.5f));
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsPhasesaberBurstProj>(), dmg, Projectile.knockBack * 0.6f,
                    Projectile.owner, Projectile.ai[2]);
            }
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f),
                    Main.rand.NextBool() ? Pal.Edge : Pal.Hot,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(10, 16));
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //消散：几粒光屑缓缓上浮
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.9f),
                    Pal.Edge, Main.rand.NextFloat(0.05f, 0.09f))?.Configure(11, 0.6f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D smear = CWRAsset.SemiCircularSmear?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (smear == null || glow == null) {
                return false;
            }
            GsPhasebladePalette pal = Pal;
            Vector2 center = Projectile.Center - Main.screenPosition;
            Vector2 ahead = Projectile.velocity.SafeNormalize(Vector2.Zero);
            float rot = Projectile.rotation + SwingSign * 0.3f;
            //出生暴烈：2 帧撑满带 10% 过冲再回坐；消亡温和渐隐
            float grow = Life <= 2f ? 1.10f * (Life / 2f)
                : MathHelper.Lerp(1.10f, 1f, MathHelper.Clamp((Life - 2f) / 4f, 0f, 1f));
            float fade = MathHelper.Clamp(Projectile.timeLeft / 9f, 0f, 1f);
            //滑行渐薄：越飞越锋利
            float thin = MathHelper.Lerp(1f, 0.5f, MathHelper.Clamp(Life / 26f, 0f, 1f));
            float sizeMul = SizeMul * grow;

            //旧位置残弧
            for (int i = 1; i <= 3; i++) {
                Vector2 back = center - Projectile.velocity * (i * 1.5f);
                Color trail = pal.Arc * (0.13f * (1f - i / 4f) * fade);
                trail.A = 0;
                Main.EntitySpriteDraw(smear, back, null, trail, rot,
                    smear.Size() * 0.5f, new Vector2(0.33f, 0.14f * thin) * sizeMul, SpriteEffects.None, 0);
            }

            //弧身：色体 + 亮缘 + 炽白核心线
            Color body = pal.Arc * (0.5f * fade);
            body.A = 0;
            Main.EntitySpriteDraw(smear, center, null, body, rot,
                smear.Size() * 0.5f, new Vector2(0.4f, 0.17f * thin) * sizeMul, SpriteEffects.None, 0);
            Color edge = pal.Edge * (0.65f * fade);
            edge.A = 0;
            Main.EntitySpriteDraw(smear, center + ahead * 4f, null, edge, rot,
                smear.Size() * 0.5f, new Vector2(0.37f, 0.09f * thin) * sizeMul, SpriteEffects.None, 0);
            Color coreLine = pal.Hot * (0.8f * fade);
            coreLine.A = 0;
            Main.EntitySpriteDraw(smear, center + ahead * 6f, null, coreLine, rot,
                smear.Size() * 0.5f, new Vector2(0.34f, 0.045f * thin) * sizeMul, SpriteEffects.None, 0);

            //月牙双角收口光点
            Vector2 side = (Projectile.rotation + MathHelper.PiOver2).ToRotationVector2() * SwingSign;
            for (int i = -1; i <= 1; i += 2) {
                Color horn = pal.Edge * (0.35f * fade);
                horn.A = 0;
                Main.EntitySpriteDraw(glow, center + side * (i * 18f * sizeMul) - ahead * 3f,
                    null, horn, 0f, glow.Size() * 0.5f, 0.2f * sizeMul, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    /// <summary>
    /// 等离子爆裂：过载光弧命中炸开的小范围二段。6 帧过冲撑到满径后回坐，
    /// 伤害只在扩张期结算一次；ai[0]=本体物品 ID（取色板）。绘制确定性播种，禁 Main.rand
    /// </summary>
    internal class GsPhasesaberBurstProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int TotalLife = 18;
        private const float MaxRadius = 82f;
        private ref float Life => ref Projectile.localAI[0];
        private float Life01 => MathHelper.Clamp(Life / TotalLife, 0f, 1f);
        private GsPhasebladePalette Pal => GsPhasebladeArcProj.PaletteFor((int)Projectile.ai[0]);

        /// <summary>当前扩张半径：6 帧过冲 8% 再回坐</summary>
        private float Radius {
            get {
                float p = MathHelper.Clamp(Life / 6f, 0f, 1f);
                float burst = p < 0.7f ? 1.08f * (p / 0.7f) : MathHelper.Lerp(1.08f, 1f, (p - 0.7f) / 0.3f);
                return MaxRadius * burst;
            }
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = TotalLife;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Life++;
            if (Life == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.5f, Pitch = -0.25f + Pal.Pitch }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.4f, Pitch = -0.4f }, Projectile.Center);
                //爆心电浆迸溅
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 7f),
                        Main.rand.NextBool(3) ? Pal.Hot : Pal.Edge,
                        Main.rand.NextFloat(0.3f, 0.55f))?.Configure(true, Main.rand.Next(12, 20));
                }
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_Light>(
                        Projectile.Center + Main.rand.NextVector2Circular(16f, 16f),
                        -Vector2.UnitY * Main.rand.NextFloat(0.6f, 1.8f),
                        Pal.Body, Main.rand.NextFloat(0.07f, 0.13f))?.Configure(12, 0.7f);
                }
            }
            Lighting.AddLight(Projectile.Center, Pal.Body.ToVector3() * (0.7f * (1f - Life01)));
        }

        //伤害只在扩张期结算（一目标一次）
        public override bool? CanDamage() => Life <= 7f ? null : false;

        /// <summary>圆形判定：目标碰到当前扩张半径即命中</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Distance(Projectile.Center) <= Radius;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = Math.Sign(target.Center.X - Projectile.Center.X);//击退向外

        /// <summary>绘制路径确定性伪随机</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (glow == null || star == null) {
                return false;
            }
            GsPhasebladePalette pal = Pal;
            Vector2 center = Projectile.Center - Main.screenPosition;
            float fade = 1f - Life01;
            float radius = Radius;

            //爆心星芒：首帧最亮随后蚀散
            Color flash = pal.Hot * (0.7f * fade * fade);
            flash.A = 0;
            Main.EntitySpriteDraw(star, center, null, flash, SegRand(9) * 6.28f,
                star.Size() * 0.5f, 0.3f, SpriteEffects.None, 0);

            //扩张光环：一圈光珠沿当前半径排布，相位确定性错开
            const int beads = 10;
            for (int i = 0; i < beads; i++) {
                float ang = MathHelper.TwoPi * i / beads + SegRand(i) * 0.4f;
                Vector2 at = center + ang.ToRotationVector2() * radius;
                float pulse = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + SegRand(i + 30) * 6.28f);
                Color bead = pal.Body * (0.5f * fade * pulse);
                bead.A = 0;
                Main.EntitySpriteDraw(glow, at, null, bead, 0f, glow.Size() * 0.5f,
                    0.24f + 0.08f * SegRand(i + 60), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
