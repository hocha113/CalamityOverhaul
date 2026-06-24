using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.TimeFreezes;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules
{
    /// <summary>SHPC 改件基类：槽位、Apply 改 ShootContext、生命周期钩子</summary>
    internal abstract class SHPCModuleItem : ModItem
    {
        public override string Texture => SlotCategory switch {
            SHPCSlotCategory.Barrel => "CalamityOverhaul/Content/LegendWeapon/SHPCLegend/Modules/Barrel",
            SHPCSlotCategory.Optic => "CalamityOverhaul/Content/LegendWeapon/SHPCLegend/Modules/Optic",
            SHPCSlotCategory.Power => "CalamityOverhaul/Content/LegendWeapon/SHPCLegend/Modules/Power",
            SHPCSlotCategory.Stock => "CalamityOverhaul/Content/LegendWeapon/SHPCLegend/Modules/Stock",
            SHPCSlotCategory.Grip => "CalamityOverhaul/Content/LegendWeapon/SHPCLegend/Modules/Grip",
            SHPCSlotCategory.Frame => "CalamityOverhaul/Content/LegendWeapon/SHPCLegend/Modules/Frame",
            _ => CWRConstant.Item_Tools + "Mewtwo",
        };

        /// <summary>槽位类别</summary>
        public abstract SHPCSlotCategory SlotCategory { get; }

        /// <summary>是否加入实验室安全箱随机池，默认 true</summary>
        public virtual bool CanGenerateInLabChest => true;

        /// <summary>改件 Apply，浮点倍率加算叠加</summary>
        public abstract void Apply(ref ShootContext ctx);
        #region 弹幕生命周期钩子

        /// <summary>光束 AI 结束，extraUpdates=2 每刻 3 次</summary>
        public virtual void OnBeamAI(CyberTraceBeamProj beam) { }

        /// <summary>光束命中 NPC，非服务端；派生需 IsDerived+myPlayer</summary>
        public virtual void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) { }

        /// <summary>光束消亡，非服务端</summary>
        public virtual void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) { }

        /// <summary>蓄力球蓄力 AI 结束</summary>
        public virtual void OnOrbCharging(CyberChargeOrbProj orb, Player owner) { }

        /// <summary>蓄力球发射瞬间</summary>
        public virtual void OnOrbLaunched(CyberChargeOrbProj orb) { }

        /// <summary>蓄力球引爆，拥有者客户端</summary>
        public virtual void OnOrbDetonation(CyberChargeOrbProj orb) { }

        /// <summary>蓄力球消亡；视觉需判 netMode</summary>
        public virtual void OnOrbKill(CyberChargeOrbProj orb, int timeLeft) { }

        /// <summary>球飞行 AI 结束，拥有者侧</summary>
        public virtual void OnOrbFlyingAI(CyberChargeOrbProj orb) { }

        //═════════════ 激光生命周期钩子 ═════════════

        /// <summary>激光 AI 结束，持键循环</summary>
        public virtual void OnLaserAI(CyberPrismLaserProj laser) { }

        /// <summary>激光命中 NPC，非服务端</summary>
        public virtual void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) { }

        /// <summary>激光熄灭；视觉需判 netMode</summary>
        public virtual void OnLaserKill(CyberPrismLaserProj laser) { }

        /// <summary>玩家 PostUpdate 分发，逐帧衰减/持续效果</summary>
        public virtual void OnPlayerUpdate(Player player) { }

        /// <summary>按 <see cref="TimeGear"/> 推进整帧倒计时</summary>
        protected static void TickDown(ref int frames, ref float carry, float scale = -1f)
            => TimeGear.ConsumeFrames(ref frames, ref carry, scale);

        /// <summary>按 <see cref="TimeGear"/> 返回本帧应推进的整帧数（正计时）</summary>
        protected static int TickUp(ref float carry, float scale = -1f)
            => TimeGear.PullFrameAdvance(ref carry, scale);

        #endregion
        /// <summary>属性 diff 文案，子类勿覆写</summary>
        public virtual IEnumerable<(string text, bool isNeg)> GetStatLines() {
            ShootContext ctx = ShootContext.Default;
            Apply(ref ctx);
            return BuildStatLines(ctx);
        }

        internal static IEnumerable<(string text, bool isNeg)> BuildStatLines(ShootContext ctx) {
            if (ctx.LaserMode)
                yield return (Language.GetTextValue("Mods.CalamityOverhaul.Legend.SHPCModuleStat.LaserMode"), false);
            if (ctx.MergeBeams)
                yield return (Language.GetTextValue("Mods.CalamityOverhaul.Legend.SHPCModuleStat.MergeBeams"), false);
            foreach (var t in FloatStat("AttackSpeed", ctx.AttackSpeedMul)) yield return t;
            foreach (var t in FloatStat("Damage", ctx.DamageMul)) yield return t;
            foreach (var t in FloatStat("Spread", ctx.SpreadMul, inverse: true)) yield return t;
            foreach (var t in FloatStat("BeamSpeed", ctx.BeamSpeedMul)) yield return t;
            foreach (var t in FloatStat("Homing", ctx.HomingMul)) yield return t;
            foreach (var t in FloatStat("MergedDamage", ctx.MergedDamageBonus)) yield return t;
            foreach (var t in FloatStat("ManaCost", ctx.ManaCostMul, inverse: true)) yield return t;
            foreach (var t in FloatStat("ChargeTime", ctx.ChargeTimeMul, inverse: true)) yield return t;
            foreach (var t in FloatStat("OrbSpeed", ctx.OrbSpeedMul)) yield return t;
            foreach (var t in FloatStat("BeamLife", ctx.BeamLifeMul)) yield return t;
            foreach (var t in FloatStat("ExplosionRadius", ctx.OrbExplosionRadiusMul)) yield return t;
            if (ctx.BeamCountAdd != 0) yield return IntStat("BeamCount", ctx.BeamCountAdd);
            if (ctx.CritAdd != 0) yield return IntStat("Crit", ctx.CritAdd);
            if (ctx.BeamExtraPierce != 0) yield return IntStat("Pierce", ctx.BeamExtraPierce);
            if (ctx.BeamChainCount != 0) yield return IntStat("Chain", ctx.BeamChainCount);
            if (ctx.BeamSplitOnDeath != 0) yield return IntStat("Split", ctx.BeamSplitOnDeath);
            if (ctx.OrbDetonationMinions != 0) yield return IntStat("Minions", ctx.OrbDetonationMinions);
            if (ctx.BeamExplodeOnHit)
                yield return (Language.GetTextValue("Mods.CalamityOverhaul.Legend.SHPCModuleStat.BeamExplodeOnHit"), false);
            if (ctx.OrbDrainAura)
                yield return (Language.GetTextValue("Mods.CalamityOverhaul.Legend.SHPCModuleStat.OrbDrainAura"), false);
            if (ctx.OrbExplosionPropels)
                yield return (Language.GetTextValue("Mods.CalamityOverhaul.Legend.SHPCModuleStat.OrbExplosionPropels"), false);
            if (ctx.LaserScorchOnHit)
                yield return (Language.GetTextValue("Mods.CalamityOverhaul.Legend.SHPCModuleStat.LaserScorchOnHit"), false);
            if (ctx.LaserPulseInterval > 0)
                yield return (Language.GetTextValue("Mods.CalamityOverhaul.Legend.SHPCModuleStat.LaserPulse"), false);
            if (ctx.OrbFlyingAttract)
                yield return (Language.GetTextValue("Mods.CalamityOverhaul.Legend.SHPCModuleStat.OrbFlyingAttract"), false);
        }

        //inverse=true 表示该字段越低越好（如法力消耗、蓄力时间），减少为正面，增加为负面
        private static IEnumerable<(string, bool isNeg)> FloatStat(string key, float mulValue, bool inverse = false) {
            float delta = mulValue - 1f;
            if (MathF.Abs(delta) < 0.001f) yield break;
            int pct = (int)MathF.Round(delta * 100f);
            string sign = pct > 0 ? "+" : "";
            string text = Language.GetTextValue($"Mods.CalamityOverhaul.Legend.SHPCModuleStat.{key}", $"{sign}{pct}");
            yield return (text, inverse ? delta > 0 : delta < 0);
        }

        private static (string, bool isNeg) IntStat(string key, int value) {
            string sign = value > 0 ? "+" : "";
            return (Language.GetTextValue($"Mods.CalamityOverhaul.Legend.SHPCModuleStat.{key}", $"{sign}{value}"), value < 0);
        }

        /// <summary>槽位 UI 色，与 MoldProcessing 共用</summary>
        public static Color SlotCategoryColor(SHPCSlotCategory cat) => cat switch {
            SHPCSlotCategory.Barrel => new Color(255, 160, 60),
            SHPCSlotCategory.Optic => new Color(0, 200, 255),
            SHPCSlotCategory.Power => new Color(255, 220, 0),
            SHPCSlotCategory.Stock => new Color(80, 220, 120),
            SHPCSlotCategory.Grip => new Color(200, 100, 255),
            SHPCSlotCategory.Frame => new Color(255, 140, 200),
            _ => Color.White,
        };

        public override bool OnPickup(Player player) {
            //首次拾取自动登记进模具图鉴，所有 90+ 子类自动受益
            if (player != null && player.whoAmI == Main.myPlayer) {
                SHPCPlayer.Get(player)?.RegisterDiscovered(Item.type);
            }
            return base.OnPickup(player);
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            string slotName = Language.GetTextValue("Mods.CalamityOverhaul.Legend.SHPCSlotName." + SlotCategory.ToString());
            string slotTag = Language.GetTextValue("Mods.CalamityOverhaul.Legend.SHPCSlotTag", slotName);
            int index = tooltips.FindIndex(line => line.Name == "ItemName");
            if (index != -1) {
                tooltips.Insert(index + 1, new TooltipLine(Mod, "SHPCSlotTag", slotTag) {
                    OverrideColor = SlotCategoryColor(SlotCategory)
                });
            }
            int idx = 0;
            foreach (var (line, isNeg) in GetStatLines()) {
                if (string.IsNullOrEmpty(line)) continue;
                tooltips.Add(new TooltipLine(Mod, $"SHPCStat{idx++}", line) {
                    OverrideColor = isNeg ? new Color(255, 120, 110) : new Color(120, 255, 170)
                });
            }
        }

        /// <summary>改件 TintColor，ModuleRender 双调+霓虹边</summary>
        public virtual Color TintColor => new(0, 220, 255);

        /// <summary>滤镜强度，默认 1</summary>
        public virtual float TintIntensity => 1f;

        public override void SetDefaults() {
            Item.maxStack = 1;
            Item.width = 32;
            Item.height = 32;
            Item.rare = Terraria.ID.ItemRarityID.Yellow;
            Item.value = Item.sellPrice(0, 2, 0, 0);
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position
            , Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            //背包绘制走UI变换矩阵，按识别色做双调色重映射
            Texture2D tex = TextureAssets.Item[Item.type]?.Value;
            if (tex == null) {
                return true;
            }
            Vector2 texSize = new(tex.Width, tex.Height);
            if (!SHPCModuleRender.Begin(spriteBatch, TintColor, texSize, Main.UIScaleMatrix, TintIntensity)) {
                return true;
            }
            spriteBatch.Draw(tex, position, frame, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);
            SHPCModuleRender.End(spriteBatch);
            return false;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor
            , ref float rotation, ref float scale, int whoAmI) {
            //世界掉落物使用游戏视角矩阵保持滤镜随屏幕缩放
            Texture2D tex = TextureAssets.Item[Item.type]?.Value;
            if (tex == null) {
                return true;
            }
            Rectangle frame = Main.itemAnimations[Item.type] != null
                ? Main.itemAnimations[Item.type].GetFrame(tex)
                : tex.Bounds;
            Vector2 texSize = new(tex.Width, tex.Height);
            Vector2 drawPos = Item.Center - Main.screenPosition;
            Vector2 origin = new(frame.Width * 0.5f, frame.Height * 0.5f);
            Matrix transform = Main.GameViewMatrix.TransformationMatrix;
            if (!SHPCModuleRender.Begin(spriteBatch, TintColor, texSize, transform, TintIntensity)) {
                return true;
            }
            spriteBatch.Draw(tex, drawPos, frame, lightColor, rotation, origin, scale, SpriteEffects.None, 0f);
            SHPCModuleRender.End(spriteBatch);
            return false;
        }
    }
}
