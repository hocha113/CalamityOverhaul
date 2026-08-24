using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.KikasaTalismanGlyph;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>
    /// 雹「冰雹」（礼物序 10）：占有"齐掷拍"通道——栏位不足 S7 也强制每第 4 拍齐掷；
    /// 齐掷拍全滴化巨雹（体积 x1.6、伤 x2.2、打雹标、不留洼），命中破甲（灾厄破甲 debuff）
    /// +强击退+小震屏。代价普通拍滴伤 x0.92。<br/>
    /// 会话仓语义：不使用（齐掷判定是节拍纯函数，巨雹状态走滴标签）
    /// </summary>
    internal sealed class FuBao : KikasaTalismanDefinition
    {
        /// <summary>巨雹体积/伤害倍率</summary>
        private const float HailScaleMul = 1.6f;
        private const float HailDamageMul = 2.2f;

        /// <summary>普通拍代价</summary>
        private const float NormalVolleyMul = 0.92f;

        /// <summary>破甲持续帧（灾厄 ArmorCrunch，-15 防）</summary>
        private const int ArmorCrunchFrames = 180;

        public override int SortOrder => 110;

        /// <summary>冰蓝白：冻住一声响的雹子</summary>
        public override Color InkAccent => new(176, 216, 240);

        //雹：雨盖下带棱方雹，内一道裂纹，旁一点朱点
        internal override KikasaGlyphStroke[] BuildGlyph() => [
            Canopy(0.12f),
            L(0.11f, 0.00f, -0.14f, 0.30f, 0.17f, 0.00f, 0.48f, -0.30f, 0.17f, 0.00f, -0.14f),
            L(0.06f, -0.07f, 0.03f, 0.06f, 0.16f, -0.03f, 0.31f),
            Dot(0.10f, 0.46f, -0.10f),
        ];

        //====行为====

        internal override void ModifyVolleyRhythm(in KikasaTalismanRainContext ctx,
            Projectile umbrella, ref KikasaVolleyRhythm rhythm) {
            //占有齐掷拍通道：栏位不足 S7 时也强制每第 4 拍齐掷。
            //纯函数整拍操作：与基准解同一套拍序公式，S≥7 时对既有齐掷拍幂等
            if (!rhythm.GhostVolley
                && (int)(umbrella.ai[2] / Math.Max(rhythm.Period, 1)) % 4 == 3) {
                rhythm.GhostVolley = true;
            }
        }

        internal override void OnVolley(in KikasaTalismanRainContext ctx,
            Projectile umbrella, int volleyIndex, bool ghostVolley) {
            //齐掷重音：伞面凝霜一沉，各端同拍
            if (ghostVolley) {
                FuBaoFX.VolleyAccent(umbrella, InkAccent);
            }
        }

        internal override void ModifyDropSpawn(in KikasaTalismanRainContext ctx,
            ref KikasaDropSpawnContext drop) {
            if (drop.GhostVolley) {
                //齐掷拍全滴化巨雹：重、痛、砸实了不留洼
                drop.Scale *= HailScaleMul;
                drop.DamageMul *= HailDamageMul;
                drop.Puddle = false;
                if (drop.TagId == 0) {
                    drop.TagId = KikasaTalismanHooks.TagIdFor(this);
                }
            }
            else if (!drop.FromPourScatter) {
                //代价只落在普通拍的伞缘滴上，墨瀑散射滴不在拍序里
                drop.DamageMul *= NormalVolleyMul;
            }
        }

        internal override void ModifyDropCurve(in KikasaTalismanRainContext ctx,
            Projectile drop, ref KikasaDropCurve curve) {
            if (KikasaTalismanHooks.ReadTagId(drop.ai[2]) != KikasaTalismanHooks.TagIdFor(this)) {
                return;
            }
            //巨雹坠得更沉：确定性叠乘，各端同参
            curve.PlungeGravity *= 1.25f;
            curve.PlungeMaxSpeed *= 1.15f;
        }

        internal override void ModifyDropDraw(in KikasaTalismanRainContext ctx,
            Projectile drop, ref KikasaDropDrawParams draw) {
            //巨雹换冰蓝白：冷冰材质允许近白亮芯
            draw.Body = new Color(198, 226, 244);
            draw.Deep = new Color(112, 152, 192);
            draw.Core = new Color(238, 248, 255);
        }

        internal override void ModifyRainHitNPC(in KikasaTalismanRainContext ctx, Projectile source,
            KikasaRainSourceKind kind, NPC npc, ref NPC.HitModifiers modifiers) {
            //雹标滴的强击退（滴标签在 ai[2]）
            if (kind == KikasaRainSourceKind.Drop
                && KikasaTalismanHooks.ReadTagId(source.ai[2]) == KikasaTalismanHooks.TagIdFor(this)) {
                modifiers.Knockback *= 2.2f;
            }
        }

        internal override void OnRainHitNPC(in KikasaTalismanRainContext ctx, Projectile source,
            KikasaRainSourceKind kind, NPC npc, in NPC.HitInfo hit, int damageDone) {
            if (kind != KikasaRainSourceKind.Drop
                || KikasaTalismanHooks.ReadTagId(source.ai[2]) != KikasaTalismanHooks.TagIdFor(this)) {
                return;
            }
            //破甲走灾厄 ArmorCrunch（仓库先例：StarshipPlanet 经 CWRID 反射取 id），
            //AddBuff 在命中端调用骑原版 NPC buff 同步；缺灾厄时 id=0，破甲缺位（报告已记）
            if (CWRID.Buff_ArmorCrunch > 0) {
                npc.AddBuff(CWRID.Buff_ArmorCrunch, ArmorCrunchFrames);
            }
            //小震屏：命中钩只在所有者端跑，所有者本机按距离自决；
            //旁观端的碎裂表现走 OnDropKill（各端派发）
            if (Vector2.Distance(Main.LocalPlayer.Center, npc.Center) < 900f) {
                Main.LocalPlayer.CWR()?.GetScreenShake(2f);
            }
        }

        internal override void OnDropKill(in KikasaTalismanRainContext ctx,
            Projectile drop, bool onTile) {
            //雹标滴的碎裂：冰屑爆+重音+近距小震，非服务器各端派发
            if (KikasaTalismanHooks.ReadTagId(drop.ai[2]) != KikasaTalismanHooks.TagIdFor(this)) {
                return;
            }
            FuBaoFX.HailShatter(drop, InkAccent);
        }

        internal override void UpdateWhileHeld(in KikasaTalismanRainContext ctx) {
            if (Main.dedServ) {
                return;
            }
            //雹标坠滴的旋坠表现：绕体冰棱闪+抖落霜屑，纯表现各端本地
            int dropType = ModContent.ProjectileType<KikasaInkDrop>();
            int myTag = KikasaTalismanHooks.TagIdFor(this);
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || proj.type != dropType || proj.owner != ctx.Owner.whoAmI
                    || KikasaTalismanHooks.ReadTagId(proj.ai[2]) != myTag) {
                    continue;
                }
                FuBaoFX.HailSpin(proj, InkAccent);
            }
        }
    }

    /// <summary>雹符纸：礼物符不配合成配方，随礼物戏发放（礼物序 10）</summary>
    internal sealed class KikasaTalismanBao : KikasaTalismanItem
    {
        public override string TalismanKey => nameof(FuBao);

        public override LocalizedText DisplayName
            => this.GetLocalization(nameof(DisplayName), () => "冰雹符");

        public override LocalizedText Tooltip
            => this.GetLocalization(nameof(Tooltip), () => "每第四拍全滴化作巨雹：破甲重击不留洼；平时滴伤略轻");

        public override void SetDefaults() {
            this.GetLocalization("Origin",
                () => "冰雹是雨里的横脾气，砸瓦穿盆没人拦得住。符师没想驯它，只与它讲定：每四拍，让它砸一拍");
            this.GetLocalization("Power",
                () => "「冰雹」每第四拍必为齐掷，全体墨滴化作巨雹：体积 x1.6、伤害 x2.2，命中破甲并强力击退；巨雹不留墨洼");
            this.GetLocalization("Burden",
                () => "普通拍滴伤 -8%");
            base.SetDefaults();
            Item.rare = Terraria.ID.ItemRarityID.LightRed;
        }
    }
}
