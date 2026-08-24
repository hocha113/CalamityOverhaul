using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.KikasaTalismanGlyph;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>
    /// 渍「浸渍」（礼物序 06）：敌浸墨洼层层积渍（叠层走 <see cref="KikasaTalismanStackNPC"/>，
    /// 上限 8，接触扫描 10 帧一轮、每轮 +1），每层受墨系伤害 +2.5%；
    /// 代价墨洼本体伤害 -25%。<br/>
    /// 会话仓语义：不使用（叠层为每 NPC 状态，随 StackNPC 广播各端一致）
    /// </summary>
    internal sealed class FuZi : KikasaTalismanDefinition
    {
        /// <summary>渍层上限</summary>
        private const int StackCap = 8;

        /// <summary>每层墨系易伤</summary>
        private const float VulnPerStack = 0.025f;

        /// <summary>离洼后渍层滞留帧（每次浸洼刷新）</summary>
        private const int StackLingerFrames = 150;

        /// <summary>墨洼本体伤害代价</summary>
        private const float PuddleCostMul = 0.75f;

        public override int SortOrder => 106;

        /// <summary>暗沼绿：泡到发乌的墨渍</summary>
        public override Color InkAccent => new(96, 132, 92);

        public override void ModifyProfile(ref KikasaTalismanProfile profile) {
            //洼不急着咬人：本体 DoT 让位给渍层易伤
            profile.PuddleDamageMul *= PuddleCostMul;
        }

        //渍：雨盖下一只碗，一物斜沉半入碗中，碗沿旁一点朱渍
        internal override KikasaGlyphStroke[] BuildGlyph() => [
            Canopy(0.12f),
            Arc(0.12f, 0.00f, 0.16f, 0.42f, 0.30f, 2.84f, 12),
            L(0.10f, -0.12f, -0.20f, 0.14f, 0.34f),
            Dot(0.11f, 0.42f, -0.06f),
        ];

        //====行为====

        internal override void OnPuddleContact(in KikasaTalismanRainContext ctx,
            Projectile puddle, NPC npc) {
            //接触扫描本身就是所有者端 10 帧一轮的节流，每轮 +1 渍并刷新滞留计时；
            //写入即经 StackNPC 紧凑包广播，旁观端的洇染表现同拍跟上
            KikasaTalismanStackNPC.AddStacks(npc, this, 1, StackCap, StackLingerFrames);
        }

        internal override void ModifyRainHitNPC(in KikasaTalismanRainContext ctx, Projectile source,
            KikasaRainSourceKind kind, NPC npc, ref NPC.HitModifiers modifiers) {
            //渍层易伤吃全部墨系来源（滴/瀑/泉/洼），含洼自身的 DoT
            int stacks = KikasaTalismanStackNPC.GetStacks(npc, this);
            if (stacks > 0) {
                modifiers.FinalDamage *= 1f + VulnPerStack * stacks;
            }
        }

        internal override void DrawNPCStack(SpriteBatch spriteBatch, NPC npc,
            int stacks, int timerFrames, Vector2 screenPos, Color drawColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null || stacks <= 0) {
                return;
            }
            //渍层将尽时整体收淡，避免硬消失
            float fade = MathHelper.Clamp(timerFrames / 40f, 0f, 1f) * npc.Opacity;
            if (fade <= 0.02f) {
                return;
            }
            //环境光只作亮度参考，墨渍本身是暗色不吃满光照
            float lit = MathHelper.Clamp(drawColor.R / 255f + 0.35f, 0.35f, 1f);
            float frac = stacks / (float)StackCap;
            float soakH = npc.height * (0.18f + 0.55f * frac);
            float soakW = npc.width * 1.06f;
            Vector2 feet = npc.Bottom - screenPos + new Vector2(0f, 2f);
            //origin 取贴图下沿中点，墨自脚向上长
            Vector2 bottomOrigin = new(tex.Width * 0.5f, tex.Height);
            float time = Main.GlobalTimeWrappedHourly;
            float seed = npc.whoAmI * 0.73f;

            //暗缘垫底 + 沼绿墨体：自脚向上的洇染渐变
            Color deep = new Color(22, 30, 20) * (0.55f * fade * lit);
            Color body = Color.Lerp(new Color(34, 46, 30), InkAccent, 0.40f) * (0.42f * fade * lit);
            spriteBatch.Draw(tex, feet, null, deep, 0f, bottomOrigin,
                new Vector2(soakW * 1.12f / tex.Width, soakH * 1.1f / tex.Height), SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, feet, null, body, 0f, bottomOrigin,
                new Vector2(soakW / tex.Width, soakH / tex.Height), SpriteEffects.None, 0f);

            //洇染前沿：两团错相慢摆的浸线，读作墨还在往上爬
            for (int i = 0; i < 2; i++) {
                float sway = MathF.Sin(time * 1.5f + seed + i * 2.4f) * soakW * 0.14f;
                Vector2 edge = feet + new Vector2(sway, -soakH * (0.92f + 0.06f * i));
                spriteBatch.Draw(tex, edge, null, body * 0.8f, 0f, tex.Size() * 0.5f,
                    new Vector2(soakW * (0.36f - 0.1f * i) / tex.Width, 7f / tex.Height),
                    SpriteEffects.None, 0f);
            }

            //满层滴墨：两滴沿身缘往下淌，确定性动画不掷随机
            if (stacks >= StackCap) {
                for (int i = 0; i < 2; i++) {
                    float t = (time * (0.55f + 0.14f * i) + seed * 1.7f + i * 0.5f) % 1f;
                    float dx = (i == 0 ? -0.3f : 0.34f) * soakW;
                    Vector2 drip = feet + new Vector2(dx, t * 16f);
                    spriteBatch.Draw(tex, drip, null, body * (0.9f * (1f - t)), 0f,
                        tex.Size() * 0.5f, new Vector2(3.2f / tex.Width, (6f + t * 5f) / tex.Height),
                        SpriteEffects.None, 0f);
                }
            }
        }
    }

    /// <summary>渍符纸：礼物符不配合成配方，随礼物戏发放（礼物序 06）</summary>
    internal sealed class KikasaTalismanZi : KikasaTalismanItem
    {
        public override string TalismanKey => nameof(FuZi);

        //zh 正典文案写进代码默认值，双语 hjson 已整并（zh-Hans 为正典）
        public override LocalizedText DisplayName
            => this.GetLocalization(nameof(DisplayName), () => "唤雨符·渍");

        public override LocalizedText Tooltip
            => this.GetLocalization(nameof(Tooltip), () => "敌浸墨洼层层积渍，每层受墨伤加深；墨洼本体较轻");

        public override void SetDefaults() {
            //先于基类注册真实文案，基类的占位默认因键已存在不再生效
            this.GetLocalization("Origin",
                () => "洗不掉的东西不止一样。符纸在洼里泡了一夜，捞起来时字迹沉进了纸背");
            this.GetLocalization("Power",
                () => "「浸渍」浸在墨洼里的敌人层层积渍（至多八层），每层受墨系伤害 +2.5%。墨入骨，伤入里");
            this.GetLocalization("Burden",
                () => "墨洼本体伤害 -25%。洼不急着咬人，它等墨先渗进去");
            base.SetDefaults();
            //获取期二档
            Item.rare = Terraria.ID.ItemRarityID.LightRed;
        }
    }
}
