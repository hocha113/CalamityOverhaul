using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains
{
    /// <summary>
    /// 墨环：墨印（<see cref="KikasaInkTag"/>）的贴身演出层，接替原版集火白圈的视觉语言。
    /// 盖印瞬间笔圈从外圈收紧贴身、沿环一圈墨珠迸开；持续期环随呼吸微胀缩、
    /// 血芯点沿环游走（暗处保底可读）、环底限频淌墨；
    /// 按 buffTime 余量在到期前干涸淡出，墨雨续印会重新洇湿。
    /// 纯可视层，结算在 <see cref="KikasaServants.KikasaServantBalanceGlobal"/>；
    /// 真透明贴图在 NPC 批内直画，不切批不套 shader，
    /// 比鬼火灼身（<see cref="KikasaWisps.KikasaWispBurnNPC"/>）轻一档
    /// </summary>
    internal sealed class KikasaInkTagNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>笔圈贴图（真透明，生成脚本 make_kikasa_inkring_tex.py）</summary>
        [VaultLoaden(CWRConstant.Masking + "KikasaInkRing")]
        private static Asset<Texture2D> ringAsset = null;

        /// <summary>贴图内环的基准半径（px），与生成脚本 R_BASE 同参，缩放换算用</summary>
        private const float TexRingRadius = 38f;

        /// <summary>收紧拍帧数：环从 1.6 倍半径收贴身</summary>
        private const int StampFrames = 9;

        /// <summary>到期干涸窗口（帧）：buffTime 余量进窗后透明度衰减到零</summary>
        private const float DryOutFrames = 40f;

        /// <summary>在场包络 0~1：盖印淡入、失印淡出</summary>
        private float fade;

        /// <summary>收紧拍进度 0~1，1=已贴身</summary>
        private float stampT = 1f;

        /// <summary>干涸乘子 0~1：带印时逐帧自 buffTime 解出，失印后沿用末值收尾</summary>
        private float dry = 1f;

        private bool hadTag;

        /// <summary>环的椭圆半径：贴合命中箱留一圈笔幅余量，巨体钳顶</summary>
        private static Vector2 RingRadii(NPC npc) => new(
            Math.Clamp(npc.width * 0.72f + 14f, 26f, 150f),
            Math.Clamp(npc.height * 0.72f + 14f, 26f, 150f));

        public override void PostAI(NPC npc) {
            int idx = npc.FindBuffIndex(ModContent.BuffType<KikasaInkTag>());
            bool tagged = idx >= 0;
            if (tagged) {
                dry = Math.Clamp(npc.buffTime[idx] / DryOutFrames, 0f, 1f);
                if (!hadTag) {
                    //盖印拍：上升沿各端本地检测（buff 走原版同步，旁观端同样触发）
                    stampT = 0f;
                    StampBurst(npc);
                }
            }
            hadTag = tagged;
            stampT = MathF.Min(stampT + 1f / StampFrames, 1f);
            fade = tagged ? MathF.Min(fade + 0.14f, 1f) : MathF.Max(fade - 0.1f, 0f);

            //环底淌墨：凝在笔圈下缘的墨往下坠（PRT 服务器端自空转）
            if (tagged && fade > 0.5f && Main.rand.NextBool(26)) {
                Vector2 radii = RingRadii(npc);
                float ang = MathHelper.PiOver2 + Main.rand.NextFloat(-0.6f, 0.6f);
                Vector2 pos = npc.Center
                    + new Vector2(MathF.Cos(ang) * radii.X, MathF.Sin(ang) * radii.Y);
                PRTLoader.NewParticle<PRT_KikasaInkDrip>(pos, Vector2.Zero,
                    KikasaInk.InkBody, Main.rand.NextFloat(0.4f, 0.6f) * dry)
                    ?.Configure(Main.rand.Next(20, 32));
            }
        }

        /// <summary>盖印拍：沿环一圈墨珠向外迸开，圈定的读法</summary>
        private static void StampBurst(NPC npc) {
            if (Main.dedServ) {
                return;
            }
            Vector2 radii = RingRadii(npc);
            float baseAng = Main.rand.NextFloat(MathHelper.TwoPi);
            for (int i = 0; i < 10; i++) {
                float ang = baseAng + MathHelper.TwoPi * i / 10f;
                Vector2 dir = ang.ToRotationVector2();
                Vector2 pos = npc.Center + new Vector2(dir.X * radii.X, dir.Y * radii.Y);
                PRTLoader.NewParticle<PRT_KikasaInkBead>(pos,
                    dir * Main.rand.NextFloat(1.4f, 2.8f),
                    Main.rand.NextBool(3) ? KikasaInk.InkDeep : KikasaInk.InkBody,
                    Main.rand.NextFloat(0.26f, 0.4f))?.Configure(Main.rand.Next(12, 20));
            }
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D ring = ringAsset?.Value;
            if (ring == null || fade <= 0.02f || npc.IsABestiaryIconDummy) {
                return;
            }
            //不乘世界光：印记是标识层，黑暗里也要认得出（原版集火白圈同款不吃光）
            float alpha = fade * MathHelper.Lerp(0.2f, 1f, dry);
            if (alpha <= 0.02f) {
                return;
            }
            float seed = npc.whoAmI * 0.618f % 1f;
            float time = Main.GlobalTimeWrappedHourly;

            //收紧拍：1.6 倍半径 EaseOut 收贴身，途中略透明
            float ease = 1f - (1f - stampT) * (1f - stampT);
            alpha *= MathHelper.Lerp(0.45f, 1f, ease);

            //呼吸与轻摆：逐 NPC 撒相位群体错拍；摆幅压小，非均匀缩放下椭圆不被拧歪
            float breath = 1f + 0.035f * MathF.Sin(time * 2.6f + seed * 17f);
            float sway = 0.05f * MathF.Sin(time * 1.4f + seed * 23f);
            Vector2 radii = RingRadii(npc) * (MathHelper.Lerp(1.6f, 1f, ease) * breath);

            Vector2 pos = npc.Center - screenPos;
            //翻面凑四款笔迹朝向，同屏多印不撞款
            SpriteEffects flip = (SpriteEffects)(npc.whoAmI & 3);
            spriteBatch.Draw(ring, pos, null, Color.White * alpha, sway,
                ring.Size() * 0.5f, radii / TexRingRadius, flip, 0f);

            //血芯游点：沿椭圆参数角缓行（A=0 加色，AlphaBlend 批内即加色）
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            float orbit = time * 1.7f * (seed > 0.5f ? 1f : -1f) + seed * MathHelper.TwoPi;
            Vector2 dotPos = pos
                + new Vector2(MathF.Cos(orbit) * radii.X, MathF.Sin(orbit) * radii.Y);
            float pulse = 0.8f + 0.2f * MathF.Sin(time * 5.3f + seed * 31f);
            Color core = KikasaInk.BloodCore with { A = 0 };
            spriteBatch.Draw(glow, dotPos, null, core * (0.55f * alpha * pulse), 0f,
                glow.Size() * 0.5f, 15f * pulse / glow.Width, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, dotPos, null, (Color.White with { A = 0 }) * (0.2f * alpha * pulse), 0f,
                glow.Size() * 0.5f, 6f / glow.Width, SpriteEffects.None, 0f);
        }
    }
}
