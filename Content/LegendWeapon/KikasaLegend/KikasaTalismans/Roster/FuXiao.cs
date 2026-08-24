using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.KikasaTalismanGlyph;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>
    /// 霄「九霄」（礼物序 18）：悬伞升上霄位（高度 x2），墨滴打霄标改高空直坠——
    /// 顶点抬高、坠落加速、终速 +40%、伤害 +25%，落点先浮出一圈雨影预告；
    /// 代价是雨拍间隔 +10%。<br/>
    /// 会话仓：本符不占任何会话字段（雨影与速度线全部无状态逐帧推演）
    /// </summary>
    internal sealed class FuXiao : KikasaTalismanDefinition
    {
        /// <summary>霄标滴伤害倍率</summary>
        private const float PlungeDamageMul = 1.25f;

        /// <summary>坠落终速倍率（+40%）</summary>
        private const float PlungeSpeedMul = 1.4f;

        public override int SortOrder => 118;

        /// <summary>高空靛：霄位稀薄天光的冷靛蓝</summary>
        public override Color InkAccent => new(112, 126, 216);

        public override void ModifyProfile(ref KikasaTalismanProfile profile) {
            //霄位：悬点高度翻倍，雨从更高处来
            profile.HoverHeightMul *= 2f;
            //代价：路远一息，雨拍 +10%
            profile.RainTempoMul *= 1.10f;
        }

        //霄：雨盖上一点星，盖下一缕薄云横过、一竖贯天直落
        internal override KikasaGlyphStroke[] BuildGlyph() => [
            Canopy(0.12f),
            L(0.08f, -0.26f, -0.10f, 0.26f, -0.16f),
            L(0.13f, 0.00f, -0.32f, 0.00f, 0.72f),
            Dot(0.10f, 0.36f, -0.86f),
        ];

        //====行为====

        internal override void ModifyDropSpawn(in KikasaTalismanRainContext ctx,
            ref KikasaDropSpawnContext drop) {
            //打霄标（先到先得）：只有拿到标的滴走高空直坠通道，弹道/伤害/演出一体
            if (drop.TagId != 0) {
                return;
            }
            drop.TagId = KikasaTalismanHooks.TagIdFor(this);
            drop.DamageMul *= PlungeDamageMul;
        }

        internal override void ModifyDropCurve(in KikasaTalismanRainContext ctx,
            Projectile drop, ref KikasaDropCurve curve) {
            if (KikasaTalismanHooks.ReadTagId(drop.ai[2]) != KikasaTalismanHooks.TagIdFor(this)) {
                return;
            }
            //九霄直坠：顶点抬进霄位（天花板钳制仍然生效），坠得更狠更快。
            //叠乘既有值、无随机，各端首帧同参确定性
            curve.ApexAboveTarget *= 2.4f;
            curve.PlungeGravity *= 1.5f;
            curve.PlungeMaxSpeed *= PlungeSpeedMul;
        }

        internal override void ModifyDropDraw(in KikasaTalismanRainContext ctx,
            Projectile drop, ref KikasaDropDrawParams draw) {
            //霄标滴染高空靛：冷靛暗体+靛白芯（高空冷雨允许白芯）
            draw.Body = new Color(26, 30, 56);
            draw.Deep = new Color(64, 74, 140);
            draw.Core = new Color(206, 214, 255);
        }

        internal override void UpdateWhileHeld(in KikasaTalismanRainContext ctx) {
            if (Main.dedServ) {
                return;
            }
            //霄标滴的两件套：急坠段拖长速度线、落点预浮雨影圈。
            //纯表现各端本地跑；标签随生成包同步，旁观端同样看得到
            int dropType = ModContent.ProjectileType<KikasaInkDrop>();
            int myTag = KikasaTalismanHooks.TagIdFor(this);
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || proj.type != dropType || proj.owner != ctx.Owner.whoAmI
                    || KikasaTalismanHooks.ReadTagId(proj.ai[2]) != myTag) {
                    continue;
                }
                FuXiaoFX.DropSpeedLine(proj, InkAccent);
                FuXiaoFX.TickRainShadow(proj, InkAccent);
            }
        }
    }

    /// <summary>霄符纸：礼物符不配合成配方，随礼物戏发放（获取期四）</summary>
    internal sealed class KikasaTalismanXiao : KikasaTalismanItem
    {
        public override string TalismanKey => nameof(FuXiao);

        //zh 正典文案写进代码默认值，双语 hjson 已整并（zh-Hans 为正典）
        public override LocalizedText DisplayName
            => this.GetLocalization(nameof(DisplayName), () => "唤雨符·霄");

        public override LocalizedText Tooltip
            => this.GetLocalization(nameof(Tooltip), () => "悬伞升上霄位，墨滴自高空直坠更急更痛；雨拍稍缓");

        public override void SetDefaults() {
            //先于基类注册真实文案，基类的占位默认因键已存在不再生效
            this.GetLocalization("Origin",
                () => "登高放伞的人没有下山。有人说他把伞挂在了云上，从此那片天的雨都落得又直又急");
            this.GetLocalization("Power",
                () => "「九霄」悬伞高度翻倍，墨滴尽改高空直坠：终速 +40%、伤害 +25%，落点先浮出一圈雨影");
            this.GetLocalization("Burden",
                () => "雨拍间隔 +10%。雨从霄位下来，路远，总要多等一息");
            base.SetDefaults();
            Item.rare = ItemRarityID.Red;
        }
    }
}
