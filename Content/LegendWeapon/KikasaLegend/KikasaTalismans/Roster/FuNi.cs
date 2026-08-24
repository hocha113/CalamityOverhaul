using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.KikasaTalismanGlyph;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>
    /// 霓「霓裳」（礼物序 15，肉山后）：墨滴按序轮染三色（标签载荷存色序
    /// 0赤/1青/2紫），命中按色触发：赤滴小爆（0.4x AoE 弹幕，owner 端）、
    /// 青滴挂缓速印（15%，60 帧）、紫滴挂易伤印（墨系受伤 +5%，180 帧）。
    /// 两种印共用本符的 NPC 叠层条目（Count 位掩码：bit0=易伤、bit1=缓速，
    /// 计时取"后到效果的时长"，轮染连击下互相续期），缓速由叠层逐帧回调
    /// 做位移回滚、各端一致。代价 DropDamageMul 0.95。<br/>
    /// 会话仓语义：CounterA=染色轮转序号（仅所有者端 ModifyDropSpawn 自增）
    /// </summary>
    internal sealed class FuNi : KikasaTalismanDefinition
    {
        /// <summary>叠层位：紫·易伤</summary>
        internal const int BitVuln = 1;

        /// <summary>叠层位：青·缓速</summary>
        internal const int BitSlow = 2;

        /// <summary>紫滴易伤幅度</summary>
        private const float VulnMul = 1.05f;

        /// <summary>青滴缓速比例（位移回滚系数）</summary>
        private const float SlowAmount = 0.15f;

        /// <summary>赤滴小爆伤害倍率</summary>
        private const float BloomDamageMul = 0.4f;

        public override int SortOrder => 115;

        /// <summary>虹紫：副虹最深处那一瓣紫</summary>
        public override Color InkAccent => new(186, 126, 216);

        public override void ModifyProfile(ref KikasaTalismanProfile profile) {
            //分了色的墨浅了些
            profile.DropDamageMul *= 0.95f;
        }

        //霓：雨盖下反向双层弧（副虹色序倒悬），两点弧脚坠地
        internal override KikasaGlyphStroke[] BuildGlyph() => [
            Canopy(0.12f),
            Arc(0.11f, 0.00f, -0.02f, 0.52f, 0.60f, 2.54f, 12),
            Arc(0.08f, 0.00f, -0.06f, 0.34f, 0.72f, 2.42f, 10),
            Dot(0.10f, -0.50f, 0.44f),
            Dot(0.09f, 0.52f, 0.40f),
        ];

        //====行为====

        internal override void ModifyDropSpawn(in KikasaTalismanRainContext ctx,
            ref KikasaDropSpawnContext drop) {
            //三色轮染：伞掷与瀑散一视同仁；标签先到先得，被别符占标的滴不染色也不进序
            if (drop.TagId != 0) {
                return;
            }
            KikasaTalismanSessionState state = ctx.StateFor(this);
            int tagId = KikasaTalismanHooks.TagIdFor(this);
            if (state == null || tagId == 0) {
                return;
            }
            drop.TagId = tagId;
            drop.TagPayload = state.CounterA % 3;
            state.CounterA++;
        }

        internal override void ModifyDropDraw(in KikasaTalismanRainContext ctx,
            Projectile drop, ref KikasaDropDrawParams draw) {
            //滴身按色序染色，色相向下一色缓慢流转（纯绘制，端本地确定性）
            FuNiFX.PaintDrop(drop, ref draw);
        }

        internal override void OnDropKill(in KikasaTalismanRainContext ctx,
            Projectile drop, bool onTile) {
            //染色滴谢幕的对应色花火：标签与载荷随生成包同步，各客户端本地开花
            if (KikasaTalismanHooks.ReadTagId(drop.ai[2]) != KikasaTalismanHooks.TagIdFor(this)) {
                return;
            }
            FuNiFX.ColorBurst(drop.Center, KikasaTalismanHooks.ReadTagPayload(drop.ai[2]));
        }

        internal override void ModifyRainHitNPC(in KikasaTalismanRainContext ctx, Projectile source,
            KikasaRainSourceKind kind, NPC npc, ref NPC.HitModifiers modifiers) {
            //紫印易伤：吃全部墨系四源（仅所有者端结算，印记 owner 端可见即可）
            if ((KikasaTalismanStackNPC.GetStacks(npc, this) & BitVuln) != 0) {
                modifiers.FinalDamage *= VulnMul;
            }
        }

        internal override void OnRainHitNPC(in KikasaTalismanRainContext ctx, Projectile source,
            KikasaRainSourceKind kind, NPC npc, in NPC.HitInfo hit, int damageDone) {
            //只认自己的染色滴，按色序分支（仅所有者端）
            if (kind != KikasaRainSourceKind.Drop
                || KikasaTalismanHooks.ReadTagId(source.ai[2]) != KikasaTalismanHooks.TagIdFor(this)) {
                return;
            }
            int payload = KikasaTalismanHooks.ReadTagPayload(source.ai[2]);
            switch (payload) {
                case 0: {
                    //赤：小爆，伤害与半径随生成包自含
                    int damage = (int)(source.damage * BloomDamageMul);
                    if (damage > 0) {
                        Projectile.NewProjectile(source.GetSource_FromThis(), npc.Center,
                            Vector2.Zero, ModContent.ProjectileType<FuNiBloomProj>(),
                            damage, source.knockBack * 0.5f, ctx.Owner.whoAmI, 66f);
                    }
                    break;
                }
                case 1: {
                    //青：缓速印 60 帧；若紫印在身则随其取长计时，轮染连击下不互相截断
                    int bits = KikasaTalismanStackNPC.GetStacks(npc, this);
                    KikasaTalismanStackNPC.SetStacks(npc, this, bits | BitSlow,
                        (bits & BitVuln) != 0 ? 180 : 60);
                    break;
                }
                default: {
                    //紫：易伤印 180 帧
                    int bits = KikasaTalismanStackNPC.GetStacks(npc, this);
                    KikasaTalismanStackNPC.SetStacks(npc, this, bits | BitVuln, 180);
                    break;
                }
            }
        }

        internal override void ModifyStackLifeRegen(NPC npc, int stacks, ref int damage) {
            //青印缓速：借叠层逐帧回调做位移回滚，各端同规则运行、服务器为权威
            if ((stacks & BitSlow) != 0) {
                npc.position -= npc.velocity * SlowAmount;
            }
        }

        internal override void DrawNPCStack(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch,
            NPC npc, int stacks, int timerFrames, Vector2 screenPos, Color drawColor) {
            FuNiFX.DrawColorMarks(spriteBatch, npc, stacks, timerFrames, screenPos);
        }
    }

    /// <summary>霓符纸：礼物符不配合成配方，随礼物戏发放</summary>
    internal sealed class KikasaTalismanNi : KikasaTalismanItem
    {
        public override string TalismanKey => nameof(FuNi);

        public override LocalizedText DisplayName
            => this.GetLocalization(nameof(DisplayName), () => "唤雨符·霓");

        public override LocalizedText Tooltip
            => this.GetLocalization(nameof(Tooltip), () => "墨滴轮染三色：赤爆、青缓、紫易伤；滴伤微减");

        public override void SetDefaults() {
            this.GetLocalization("Origin",
                () => "副虹曰霓，色序与虹相反。写符的人蘸了雨里最深的三色，一瓣一瓣写完了它");
            this.GetLocalization("Power",
                () => "「三染」墨滴按序轮染三色：赤滴命中小爆（40% 溅伤），青滴缓敌 15%，紫滴令敌受墨系伤 +5%");
            this.GetLocalization("Burden",
                () => "分了色的墨浅了些，墨滴伤害 -5%。霓终究不如虹烈");
            base.SetDefaults();
            Item.rare = ItemRarityID.Yellow;
            Item.value = Item.sellPrice(gold: 1, silver: 50);
        }
    }
}
