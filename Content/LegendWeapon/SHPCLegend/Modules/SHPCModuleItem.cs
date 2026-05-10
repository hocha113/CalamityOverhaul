using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules
{
    /// <summary>
    /// SHPC 改件物品基类，提供槽位类别声明、战利品生成声明与对 <see cref="ShootContext"/> 的修改入口
    /// 子类只需覆写 <see cref="SlotCategory"/> 与 <see cref="Apply"/>
    /// </summary>
    internal abstract class SHPCModuleItem : ModItem
    {
        public override string Texture => CWRConstant.Item_Tools + "Mewtwo";

        /// <summary>
        /// 该改件能装入的槽位类别
        /// </summary>
        public abstract SHPCSlotCategory SlotCategory { get; }

        /// <summary>
        /// 是否自动加入实验室安全箱的改件随机池；特殊或隐藏改件可覆写为 false。
        /// </summary>
        public virtual bool CanGenerateInLabChest => true;

        /// <summary>
        /// 改件作用：修改传入的 <see cref="ShootContext"/>，对浮点倍率字段使用加算叠加（增量 += delta）
        /// </summary>
        public abstract void Apply(ref ShootContext ctx);
        #region 弹幕生命周期钩子

        /// <summary>
        /// 每帧追踪光束 AI 结束时调用，可附加持续视觉效果或状态修改
        /// extraUpdates=2，每游戏刻被调用 3 次
        /// </summary>
        public virtual void OnBeamAI(CyberTraceBeamProj beam) { }

        /// <summary>
        /// 追踪光束命中NPC后调用，仅在非服务端执行
        /// 产生派生弹幕时需配合 beam.IsDerived 与 Projectile.owner == Main.myPlayer 判断
        /// </summary>
        public virtual void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) { }

        /// <summary>
        /// 追踪光束消亡时调用，仅在非服务端执行
        /// </summary>
        public virtual void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) { }

        /// <summary>
        /// 蓄力球每帧蓄力 AI 结束时调用
        /// </summary>
        public virtual void OnOrbCharging(CyberChargeOrbProj orb, Player owner) { }

        /// <summary>
        /// 蓄力球发射瞬间调用（状态切换为飞行前）
        /// </summary>
        public virtual void OnOrbLaunched(CyberChargeOrbProj orb) { }

        /// <summary>
        /// 蓄力球引爆时调用，仅在弹幕拥有者客户端执行
        /// </summary>
        public virtual void OnOrbDetonation(CyberChargeOrbProj orb) { }

        /// <summary>
        /// 蓄力球消亡时调用，服务端与客户端均触发
        /// 纯视觉操作需自行判断 Main.netMode != NetmodeID.Server
        /// </summary>
        public virtual void OnOrbKill(CyberChargeOrbProj orb, int timeLeft) { }

        /// <summary>
        /// 能量球飞行阶段每帧 AI 结束时调用，可在飞行轨迹上做持续行为
        /// 仅弹幕拥有者侧触发，产生弹幕需自行判断 Projectile.owner == Main.myPlayer
        /// </summary>
        public virtual void OnOrbFlyingAI(CyberChargeOrbProj orb) { }

        //═════════════ 激光生命周期钩子 ═════════════

        /// <summary>
        /// 激光每帧 AI 结束时调用，可在光束上叠加持续效果
        /// 持有时循环触发，松键即停
        /// </summary>
        public virtual void OnLaserAI(CyberPrismLaserProj laser) { }

        /// <summary>
        /// 激光命中 NPC 后调用，仅在非服务端执行
        /// 每次命中间隔由 localNPCHitCooldown 控制
        /// </summary>
        public virtual void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) { }

        /// <summary>
        /// 激光熄灭时调用（玩家松键或切换武器），服务端与客户端均触发
        /// 纯视觉操作需自行判断 Main.netMode != NetmodeID.Server
        /// </summary>
        public virtual void OnLaserKill(CyberPrismLaserProj laser) { }

        /// <summary>
        /// 每游戏帧玩家更新时调用，由 SHPCPlayer.PostUpdate 分发
        /// 用于需要逐帧衰减或持续效果的改件逻辑
        /// </summary>
        public virtual void OnPlayerUpdate(Player player) { }

        #endregion
        /// <summary>
        /// 改件属性差值文字列表，自动对 <see cref="Apply"/> 前后的 <see cref="ShootContext"/> 做 diff
        /// 数值来自代码，本地化只提供字段名模板（含 {0} 占位符）
        /// 子类无需覆写此方法
        /// </summary>
        public virtual IEnumerable<string> GetStatLines() {
            ShootContext ctx = ShootContext.Default;
            Apply(ref ctx);
            return BuildStatLines(ctx);
        }

        internal static IEnumerable<string> BuildStatLines(ShootContext ctx) {
            if (ctx.LaserMode)
                yield return Language.GetTextValue("Mods.CalamityOverhaul.Legend.SHPCModuleStat.LaserMode");
            if (ctx.MergeBeams)
                yield return Language.GetTextValue("Mods.CalamityOverhaul.Legend.SHPCModuleStat.MergeBeams");
            foreach (string s in FloatStat("AttackSpeed", ctx.AttackSpeedMul)) yield return s;
            foreach (string s in FloatStat("Damage", ctx.DamageMul)) yield return s;
            foreach (string s in FloatStat("Spread", ctx.SpreadMul)) yield return s;
            foreach (string s in FloatStat("BeamSpeed", ctx.BeamSpeedMul)) yield return s;
            foreach (string s in FloatStat("Homing", ctx.HomingMul)) yield return s;
            foreach (string s in FloatStat("MergedDamage", ctx.MergedDamageBonus)) yield return s;
            foreach (string s in FloatStat("ManaCost", ctx.ManaCostMul)) yield return s;
            foreach (string s in FloatStat("ChargeTime", ctx.ChargeTimeMul)) yield return s;
            foreach (string s in FloatStat("OrbSpeed", ctx.OrbSpeedMul)) yield return s;
            foreach (string s in FloatStat("BeamLife", ctx.BeamLifeMul)) yield return s;
            foreach (string s in FloatStat("ExplosionRadius", ctx.OrbExplosionRadiusMul)) yield return s;
            if (ctx.BeamCountAdd != 0) yield return IntStat("BeamCount", ctx.BeamCountAdd);
            if (ctx.CritAdd != 0) yield return IntStat("Crit", ctx.CritAdd);
            if (ctx.BeamExtraPierce != 0) yield return IntStat("Pierce", ctx.BeamExtraPierce);
            if (ctx.BeamChainCount != 0) yield return IntStat("Chain", ctx.BeamChainCount);
            if (ctx.BeamSplitOnDeath != 0) yield return IntStat("Split", ctx.BeamSplitOnDeath);
            if (ctx.OrbDetonationMinions != 0) yield return IntStat("Minions", ctx.OrbDetonationMinions);
            if (ctx.BeamExplodeOnHit)
                yield return Language.GetTextValue("Mods.CalamityOverhaul.Legend.SHPCModuleStat.BeamExplodeOnHit");
            if (ctx.OrbDrainAura)
                yield return Language.GetTextValue("Mods.CalamityOverhaul.Legend.SHPCModuleStat.OrbDrainAura");
            if (ctx.OrbExplosionPropels)
                yield return Language.GetTextValue("Mods.CalamityOverhaul.Legend.SHPCModuleStat.OrbExplosionPropels");
            if (ctx.LaserScorchOnHit)
                yield return Language.GetTextValue("Mods.CalamityOverhaul.Legend.SHPCModuleStat.LaserScorchOnHit");
            if (ctx.LaserPulseInterval > 0)
                yield return Language.GetTextValue("Mods.CalamityOverhaul.Legend.SHPCModuleStat.LaserPulse");
            if (ctx.OrbFlyingAttract)
                yield return Language.GetTextValue("Mods.CalamityOverhaul.Legend.SHPCModuleStat.OrbFlyingAttract");
        }

        private static IEnumerable<string> FloatStat(string key, float mulValue) {
            float delta = mulValue - 1f;
            if (MathF.Abs(delta) < 0.001f) yield break;
            int pct = (int)MathF.Round(delta * 100f);
            //正数补 + 号，负数 pct 自带 - 号，sign 对负数为空串
            string sign = pct > 0 ? "+" : "";
            yield return Language.GetTextValue($"Mods.CalamityOverhaul.Legend.SHPCModuleStat.{key}", $"{sign}{pct}");
        }

        private static string IntStat(string key, int value) {
            string sign = value > 0 ? "+" : "";
            return Language.GetTextValue($"Mods.CalamityOverhaul.Legend.SHPCModuleStat.{key}", $"{sign}{value}");
        }

        private static Color SlotCategoryColor(SHPCSlotCategory cat) => cat switch {
            SHPCSlotCategory.Barrel => new Color(255, 160, 60),
            SHPCSlotCategory.Optic => new Color(0, 200, 255),
            SHPCSlotCategory.Power => new Color(255, 220, 0),
            SHPCSlotCategory.Stock => new Color(80, 220, 120),
            SHPCSlotCategory.Grip => new Color(200, 100, 255),
            SHPCSlotCategory.Frame => new Color(255, 140, 200),
            _ => Color.White,
        };

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
            foreach (string line in GetStatLines()) {
                if (string.IsNullOrEmpty(line)) continue;
                bool isNeg = line.StartsWith("-");
                tooltips.Add(new TooltipLine(Mod, $"SHPCStat{idx++}", line) {
                    OverrideColor = isNeg ? new Color(255, 120, 110) : new Color(120, 255, 170)
                });
            }
        }

        /// <summary>
        /// 赛博朋克滤镜识别色，用于<see cref="SHPCModuleRender"/>对图标做双调色与边缘霓虹描边
        /// 缺省为青色，子类按风味自由覆写以做区分
        /// </summary>
        public virtual Color TintColor => new(0, 220, 255);

        /// <summary>
        /// 滤镜强度，缺省1.0，可在子类中调低以保留更多原贴图特征
        /// </summary>
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
