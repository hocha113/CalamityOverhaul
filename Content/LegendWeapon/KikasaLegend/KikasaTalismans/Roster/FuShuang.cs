using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using System;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.KikasaTalismanGlyph;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>
    /// 霜「霜降」（礼物序 13，肉山后）：占"洼面材质"通道——墨洼凝为霜镜：
    /// 判定关断无 DoT（命中修正兜底清零），敌踏镜挂霜印减速 40%
    /// （霜印走 NPC 叠层宿主同步，减速由叠层逐帧回调做位移回滚，各端同规则一致），
    /// 墨滴击中镜面碎镜爆伤（按剩余镜寿折算，owner 端）+ 立冰锥表现；
    /// 代价 PuddleNoRefresh：洼不可合并续命，每滴各起各的镜。<br/>
    /// 会话仓语义：无（霜镜是无状态材质通道，碎镜走事件驱动）
    /// </summary>
    internal sealed class FuShuang : KikasaTalismanDefinition
    {
        /// <summary>踏镜减速比例（位移回滚系数）</summary>
        private const float MirrorSlow = 0.40f;

        /// <summary>霜印保鲜帧数：接触扫描约 10 帧一轮，16 帧覆盖间隙，离镜即散</summary>
        private const int SlowMarkFrames = 16;

        public override int SortOrder => 113;

        /// <summary>霜银：结晶镜面上那层冷白</summary>
        public override Color InkAccent => new(206, 218, 234);

        public override void ModifyProfile(ref KikasaTalismanProfile profile) {
            //霜占洼面材质通道，单挂必须自洽：大滴落地必凝霜洼，不须湖倾档
            profile.PuddleUnlock = true;
            //霜镜各自为镜：禁墨洼合并续命
            profile.PuddleNoRefresh = true;
        }

        //霜：雨盖下一面平镜，镜上立三竖锥，镜下垂一道裂纹，朱点缀镜角
        internal override KikasaGlyphStroke[] BuildGlyph() => [
            Canopy(0.12f),
            L(0.10f, -0.52f, 0.30f, 0.52f, 0.30f),
            L(0.08f, -0.50f, 0.28f, -0.40f, 0.02f, -0.30f, 0.28f, -0.20f, 0.10f, -0.10f, 0.28f),
            L(0.06f, 0.10f, 0.32f, 0.22f, 0.46f, 0.16f, 0.60f),
            Dot(0.10f, 0.44f, 0.14f),
        ];

        //====行为====

        internal override void OnDropKill(in KikasaTalismanRainContext ctx,
            Projectile drop, bool onTile) {
            //滴落碎镜：死点就近找自家霜镜（洼），命中即碎。
            //本挂钩非服务器各端派发：表现各端本地，爆伤与消镜只在所有者端做
            Projectile mirror = FindMirrorAt(ctx.Owner, drop.Center, out float lifeFrac, out float widthPx);
            if (mirror == null) {
                return;
            }
            FuShuangFX.MirrorShatter(mirror.Center, widthPx, lifeFrac, InkAccent);
            if (!ctx.IsOwnerClient) {
                return;
            }
            //碎镜爆伤按剩余镜寿折算：新镜爆得狠，残镜只剩余威
            int damage = (int)(drop.damage * (0.5f + 1.5f * lifeFrac));
            if (damage > 0) {
                Projectile.NewProjectile(drop.GetSource_FromThis(), mirror.Center, Vector2.Zero,
                    ModContent.ProjectileType<FuShuangShatterProj>(), damage,
                    drop.knockBack * 1.2f, ctx.Owner.whoAmI,
                    MathHelper.Clamp(widthPx * 0.55f + 30f, 60f, 130f));
            }
            mirror.Kill();
        }

        internal override void ModifyRainHitNPC(in KikasaTalismanRainContext ctx, Projectile source,
            KikasaRainSourceKind kind, NPC npc, ref NPC.HitModifiers modifiers) {
            //霜镜无 DoT：判定关断之外的兜底，洼源伤害一律清零
            if (kind == KikasaRainSourceKind.Puddle) {
                modifiers.FinalDamage *= 0f;
            }
        }

        internal override void OnPuddleUpdate(in KikasaTalismanRainContext ctx, Projectile puddle) {
            //占洼面材质通道：判定关断旋钮逐帧写（每帧派发前已复位，卸符自动回落）
            if (puddle.ModProjectile is KikasaInkPuddle mirror) {
                mirror.TalismanDamageOff = true;
            }
            FuShuangFX.MirrorSurfaceGlint(puddle, InkAccent);
        }

        internal override void OnPuddleContact(in KikasaTalismanRainContext ctx,
            Projectile puddle, NPC npc) {
            //踏镜挂霜印（仅所有者端写入，叠层宿主广播到各端）；
            //减速本体在 ModifyStackLifeRegen 里逐帧回滚位移
            KikasaTalismanStackNPC.SetStacks(npc, this, 1, SlowMarkFrames);
            FuShuangFX.FrostStep(npc, InkAccent);
        }

        internal override void ModifyPuddleDraw(in KikasaTalismanRainContext ctx,
            Projectile puddle, ref KikasaPuddleDrawParams draw) {
            //结晶白纹：整套换霜银调，墨洼读作一面薄镜
            draw.Deep = new Color(96, 116, 142);
            draw.Body = new Color(174, 192, 212);
            draw.Core = new Color(236, 244, 252);
            draw.Sheen = new Color(244, 250, 255);
        }

        internal override void ModifyStackLifeRegen(NPC npc, int stacks, ref int damage) {
            //借叠层逐帧回调做位移回滚：各端同规则运行、服务器为权威，
            //联机同样成立；只回滚位移不写速度，不与 AI 加速度打架
            if (stacks > 0) {
                npc.position -= npc.velocity * MirrorSlow;
            }
        }

        internal override void DrawNPCStack(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch,
            NPC npc, int stacks, int timerFrames, Vector2 screenPos, Color drawColor) {
            FuShuangFX.DrawFrostRime(spriteBatch, npc, timerFrames, screenPos, InkAccent);
        }

        /// <summary>死点就近找自家霜镜：横向按镜宽、纵向给一薄带；顺带解出剩余镜寿与镜宽</summary>
        private static Projectile FindMirrorAt(Player owner, Vector2 pos,
            out float lifeFrac, out float widthPx) {
            lifeFrac = 0f;
            widthPx = 0f;
            if (owner == null) {
                return null;
            }
            int puddleType = ModContent.ProjectileType<KikasaInkPuddle>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || proj.owner != owner.whoAmI || proj.type != puddleType) {
                    continue;
                }
                float w = KikasaInkPuddle.WidthPx * (proj.ai[0] > 0.01f ? proj.ai[0] : 1f);
                if (MathF.Abs(pos.X - proj.Center.X) > w * 0.55f + 10f
                    || pos.Y < proj.Center.Y - 30f || pos.Y > proj.Center.Y + 22f) {
                    continue;
                }
                //剩余镜寿：出生寿命同源取洼身折算（与首帧钳制同式）
                float lifeMul = proj.ai[1] > 0.01f ? proj.ai[1] : 1f;
                lifeFrac = MathHelper.Clamp(
                    proj.timeLeft / MathF.Max(KikasaInkPuddle.SpawnLifeFrames(lifeMul), 1f), 0f, 1f);
                widthPx = w;
                return proj;
            }
            return null;
        }
    }

    /// <summary>霜符纸：礼物符不配合成配方，随礼物戏发放</summary>
    internal sealed class KikasaTalismanShuang : KikasaTalismanItem
    {
        public override string TalismanKey => nameof(FuShuang);

        public override LocalizedText DisplayName
            => this.GetLocalization(nameof(DisplayName), () => "霜镜符");

        public override LocalizedText Tooltip
            => this.GetLocalization(nameof(Tooltip), () => "墨洼凝成霜镜：踏镜减速、滴击碎镜爆伤；洼不可续命");

        public override void SetDefaults() {
            this.GetLocalization("Origin",
                () => "霜降之后，洼水一夜结成薄冰，看着像面镜子，踩上去才知道厉害。符师收下的，正是这层\"看着没事\"");
            this.GetLocalization("Power",
                () => "「霜镜」大滴落地自凝霜洼（无需蓄到第四档『湖倾』），墨洼凝为霜镜：不再灼敌，踏镜者减速 40%；墨滴击中镜面立刻碎镜，按剩余镜寿折算爆伤并立起冰锥");
            this.GetLocalization("Burden",
                () => "霜镜各自成镜，墨洼不可合并续命");
            base.SetDefaults();
            Item.rare = ItemRarityID.Lime;
            Item.value = Item.sellPrice(gold: 1);
        }
    }
}
