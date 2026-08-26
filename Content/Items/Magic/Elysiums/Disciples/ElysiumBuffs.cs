using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums.Disciples
{
    /// <summary>门徒系buff的程序化图标共用底座：暗底板 + 身份色边框 + 圣徽线稿</summary>
    internal static class DiscipleBuffIcon
    {
        public static bool Draw(SpriteBatch sb, ref BuffDrawParams drawParams, int seat, float pulseSpeed = 2.2f) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null) {
                return true;
            }
            DiscipleDef def = DiscipleCatalog.Get(seat);
            float alpha = drawParams.DrawColor.A / 255f;
            Point pos = drawParams.Position.ToPoint();
            const int Size = 32;

            //暗金底板 + 身份色边框
            sb.Draw(px, new Rectangle(pos.X, pos.Y, Size, Size), new Color(22, 18, 12) * alpha);
            Color border = def.BodyColor * (0.85f * alpha);
            sb.Draw(px, new Rectangle(pos.X, pos.Y, Size, 1), border);
            sb.Draw(px, new Rectangle(pos.X, pos.Y + Size - 1, Size, 1), border);
            sb.Draw(px, new Rectangle(pos.X, pos.Y, 1, Size), border);
            sb.Draw(px, new Rectangle(pos.X + Size - 1, pos.Y, 1, Size), border);

            //圣徽线稿：呼吸明灭
            SvgPath path = SvgPathPen.Path(def.EmblemPath);
            if (path != null) {
                float breath = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * pulseSpeed);
                Vector2 center = new(pos.X + Size / 2f, pos.Y + Size / 2f);
                SvgPathPen.Stroke(sb, path, center, 11f, 0f,
                    def.AccentColor with { A = 0 } * (breath * alpha), 1.2f, breath * alpha,
                    core: Color.White with { A = 0 } * (0.5f * breath * alpha));
            }
            return false;
        }
    }

    /// <summary>约翰·启示印(敌怪)：受到的一切伤害提高</summary>
    internal class RevelationMarkDebuff : ModBuff
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;
        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }
        public override bool PreDraw(SpriteBatch sb, int buffIndex, ref BuffDrawParams drawParams)
            => DiscipleBuffIcon.Draw(sb, ref drawParams, 3);
    }

    /// <summary>巴多罗买·真言揭示(敌怪)：护甲被剥离并显形</summary>
    internal class TruthRevealDebuff : ModBuff
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;
        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }
        public override bool PreDraw(SpriteBatch sb, int buffIndex, ref BuffDrawParams drawParams)
            => DiscipleBuffIcon.Draw(sb, ref drawParams, 5);
    }

    /// <summary>马太·财富祝福(敌怪)：死亡时迸出奉献金雨</summary>
    internal class WealthBlessingDebuff : ModBuff
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;
        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }
        public override bool PreDraw(SpriteBatch sb, int buffIndex, ref BuffDrawParams drawParams)
            => DiscipleBuffIcon.Draw(sb, ref drawParams, 7);
    }

    /// <summary>多马·验证之目(玩家)：攻击必然暴击</summary>
    internal class VerificationBuff : ModBuff
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;
        public override void SetStaticDefaults() {
            Main.debuff[Type] = false;
            Main.buffNoSave[Type] = true;
        }
        public override bool PreDraw(SpriteBatch sb, int buffIndex, ref BuffDrawParams drawParams)
            => DiscipleBuffIcon.Draw(sb, ref drawParams, 6, 3.4f);
    }

    /// <summary>瘟疫骑士·瘟疫印(敌怪)：圣瘟持续侵蚀生命</summary>
    internal class PlagueMarkDebuff : ModBuff
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;
        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }
        public override bool PreDraw(SpriteBatch sb, int buffIndex, ref BuffDrawParams drawParams) {
            //瘟疫无席位，借骑士色画病绿底板
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null) {
                return true;
            }
            float alpha = drawParams.DrawColor.A / 255f;
            Point pos = drawParams.Position.ToPoint();
            const int Size = 32;
            sb.Draw(px, new Rectangle(pos.X, pos.Y, Size, Size), new Color(18, 26, 12) * alpha);
            Color border = new Color(122, 168, 92) * (0.9f * alpha);
            sb.Draw(px, new Rectangle(pos.X, pos.Y, Size, 1), border);
            sb.Draw(px, new Rectangle(pos.X, pos.Y + Size - 1, Size, 1), border);
            sb.Draw(px, new Rectangle(pos.X, pos.Y, 1, Size), border);
            sb.Draw(px, new Rectangle(pos.X + Size - 1, pos.Y, 1, Size), border);
            //病瘟滴痕：三道下淌的绿线
            float drip = Main.GlobalTimeWrappedHourly * 14f;
            for (int i = 0; i < 3; i++) {
                float x = pos.X + 7 + i * 9;
                float len = 8f + 5f * MathF.Sin(drip * 0.4f + i * 2.1f);
                sb.Draw(px, new Rectangle((int)x, pos.Y + 6, 2, (int)len), new Color(190, 235, 130) * (0.8f * alpha));
            }
            return false;
        }
    }

    /// <summary>奋锐党西门·狂热(玩家)：攻速与移速昂扬</summary>
    internal class ZealotFervorBuff : ModBuff
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;
        public override void SetStaticDefaults() {
            Main.debuff[Type] = false;
            Main.buffNoSave[Type] = true;
        }
        public override void Update(Player player, ref int buffIndex) {
            player.GetAttackSpeed(DamageClass.Generic) += 0.1f;
            player.moveSpeed += 0.12f;
        }
        public override bool PreDraw(SpriteBatch sb, int buffIndex, ref BuffDrawParams drawParams)
            => DiscipleBuffIcon.Draw(sb, ref drawParams, 10, 4.5f);
    }
}
