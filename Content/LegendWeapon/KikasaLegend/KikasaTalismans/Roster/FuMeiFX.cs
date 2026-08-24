using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>霉的演出集中处：NPC 身上的墨霉绒斑与死亡孢子云，全部端本地纯表现</summary>
    internal static class FuMeiFX
    {
        //霉斑色板：暗霉底近黑、霉体沉绿、孢子点灰黄绿
        private static readonly Color MoldDark = new(30, 34, 16);
        private static readonly Color MoldBody = new(88, 102, 44);

        /// <summary>
        /// 墨霉绒斑：层数决定斑数，斑位由确定性散列钉在身上（各端一致、不逐帧跳），
        /// 每斑慢呼吸微胀。暗斑走真透明 Extra_98（霉必须能压暗宿主），
        /// 孢子点 A=0 加色微亮
        /// </summary>
        internal static void DrawMoldSpots(SpriteBatch spriteBatch, NPC npc,
            int stacks, Vector2 screenPos, Color drawColor, Color accent) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null || stacks <= 0) {
                return;
            }
            Vector2 origin = tex.Size() * 0.5f;
            //受光下限：黑暗里霉也隐约可辨，别彻底消失
            float light = MathF.Max((drawColor.R + drawColor.G + drawColor.B) / 765f, 0.35f);
            int count = Math.Min(stacks, 9);
            int seed = npc.whoAmI * 131 + npc.type;

            for (int i = 0; i < count; i++) {
                //斑位钉在命中盒内圈，散列只吃静态种子，不随帧漂
                float u = KikasaInk.Hash(seed, i * 2 + 1);
                float v = KikasaInk.Hash(seed, i * 2 + 2);
                Vector2 pos = npc.Hitbox.TopLeft() - screenPos + new Vector2(
                    npc.width * (0.16f + 0.68f * u), npc.height * (0.14f + 0.7f * v));

                //慢呼吸：逐斑错相位，读作活的绒霉
                float breath = 0.9f + 0.14f * MathF.Sin(
                    Main.GlobalTimeWrappedHourly * 2.2f + i * 2.1f + seed * 0.13f);
                float size = (7f + 4.5f * KikasaInk.Hash(seed, i + 40)) * breath * npc.scale;
                float rot = KikasaInk.Hash(seed, i + 80) * MathHelper.TwoPi;
                Vector2 scale = new Vector2(size * 1.5f / tex.Width, size * 1.2f / tex.Height);

                //暗霉底衬 → 沉绿霉体 → 孢子点微亮
                spriteBatch.Draw(tex, pos, null, MoldDark * (0.7f * light), rot, origin,
                    scale * new Vector2(1.35f, 1.25f), SpriteEffects.None, 0f);
                spriteBatch.Draw(tex, pos, null, MoldBody * (0.85f * light), rot, origin,
                    scale, SpriteEffects.None, 0f);
                spriteBatch.Draw(tex, pos, null, (accent with { A = 0 }) * (0.3f * light * breath),
                    rot, origin, scale * 0.45f, SpriteEffects.None, 0f);
            }
        }

        /// <summary>
        /// 死亡孢子云：黄绿雾团缓涨缓散+孢子珠四散飘落+一声潮湿的草叶噗。
        /// 由 <see cref="FuMeiSporeNPC"/> 在死亡帧各端本地调用（感染逻辑另在服务端走）
        /// </summary>
        internal static void SporeBurst(NPC npc, int stacks, Color accent) {
            if (Main.dedServ) {
                return;
            }
            float potency = MathHelper.Clamp(stacks / (float)FuMei.MoldCap, 0.3f, 1f);

            //孢子雾团：霉绿染色，比墨雾更轻更飘
            int mistCount = 3 + (int)(3 * potency);
            for (int i = 0; i < mistCount; i++) {
                PRTLoader.NewParticle<PRT_KikasaInkMist>(
                    npc.Center + Main.rand.NextVector2Circular(npc.width * 0.4f, npc.height * 0.4f),
                    Main.rand.NextVector2Circular(1.6f, 1.2f) - Vector2.UnitY * 0.4f,
                    Color.Lerp(MoldBody, accent, Main.rand.NextFloat(0.3f, 0.7f)),
                    Main.rand.NextFloat(1.0f, 1.5f) * potency + 0.4f)
                    ?.Configure(Main.rand.Next(34, 52));
            }
            //孢子珠：轻小低重力，飘起再缓落
            int beadCount = 5 + (int)(5 * potency);
            for (int i = 0; i < beadCount; i++) {
                PRTLoader.NewParticle<PRT_KikasaInkBead>(
                    npc.Center + Main.rand.NextVector2Circular(10f, 8f),
                    Main.rand.NextVector2Circular(2.4f, 1.6f) - Vector2.UnitY * Main.rand.NextFloat(0.8f, 2.2f),
                    Color.Lerp(accent, MoldBody, Main.rand.NextFloat(0.5f)),
                    Main.rand.NextFloat(0.2f, 0.34f))
                    ?.Configure(Main.rand.Next(26, 40), 0.1f, 0.97f);
            }
            //潮湿草叶声垫底，水花收尾
            KikasaInk.Play(SoundID.Grass, npc.Center, 0.7f, -0.35f, 3);
            KikasaInk.Play(KikasaInk.InkSplash, npc.Center, 0.4f, -0.5f, 3);
        }
    }

    /// <summary>
    /// 霉的死亡观测钩：叠层数据随广播各端就位，死亡帧（HitEffect 且 life≤0）
    /// 各端本地喷孢子云——感染与小伤的权威逻辑在
    /// <see cref="KikasaTalismanDefinition.OnStackNPCKill"/>（服务端/单机），这里只管画
    /// </summary>
    internal sealed class FuMeiSporeNPC : GlobalNPC
    {
        public override void HitEffect(NPC npc, NPC.HitInfo hit) {
            if (Main.dedServ || npc.life > 0) {
                return;
            }
            if (!KikasaTalismanRegistry.TryGet(nameof(FuMei), out KikasaTalismanDefinition definition)) {
                return;
            }
            int stacks = KikasaTalismanStackNPC.GetStacks(npc, definition);
            if (stacks > 0) {
                FuMeiFX.SporeBurst(npc, stacks, definition.InkAccent);
            }
        }
    }
}
