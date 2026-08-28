using CalamityOverhaul.Content.LegendWeapon.KikasaLegend;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Scenarios.Kiame.Overlay;
using CalamityOverhaul.Content.Scenarios.Shenyo;
using InnoVault.Cinematics;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiame.Gate
{
    /// <summary>
    /// 鬼域门伞：获得鬼伞之后，主世界随黎明游走的那把入口伞。<br/>
    /// 与故事伞同一副骨架，但这一把是恐怖变体：显形不稳的鬼影重影、
    /// 压暗压冷的着色、涨大的黑水洼、更密的黑水粒子、从不真正阖上的眼——
    /// 剧情里那把是等你来拿的礼，这把是从雨里长出来的门。<br/>
    /// 门禁相反：故事伞获伞即隐，门伞获伞才见。
    /// 右键起整套入雨转场（涨水浮镜→窥影→翻转，与剧情线同一支演出），
    /// 结算白闪处落进 <see cref="KiameWorld"/>；锚点维护与黎明换位在 <see cref="KiameGateSpawn"/>
    /// </summary>
    internal class KiameGateUmbrella : OniRainWorldUmbrella
    {
        //湿墨色板，与鬼雨体系一致（基类同名常量为 private，此处自持一份）
        private static readonly Color PoolBlack = new(16, 21, 24);
        private static readonly Color MistDamp = new(58, 66, 70);
        private static readonly Color PaleTint = new(190, 205, 208);
        //门伞本体压向的冷沉色
        private static readonly Color BodyCold = new(30, 40, 44);

        private int horrorTimer;

        /// <summary>本地玩家是否已真正获得鬼伞（门伞可见性与交互的共用门）</summary>
        internal static bool LocalPlayerHasKikasa() {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                return false;
            }
            return ShenyoStorySync.KikasaGranted
                || player.HasItem(ModContent.ItemType<KikasaItem>());
        }

        internal override bool VisibleToLocalPlayer() => LocalPlayerHasKikasa();

        //躁动沿用基类：入雨转场活跃时伞与水洼随演出一起躁起来

        //══════ 恐怖变体面 ══════

        /// <summary>洼子常态就比故事伞大一圈</summary>
        protected override float PuddleBaseSwell => 1.3f;

        /// <summary>这只眼从不真正阖上</summary>
        protected override float EyeOpenFloor => 0.3f;

        /// <summary>显形不稳：伞的轮廓永远对不上焦</summary>
        protected override float GhostSmear => 1f;

        /// <summary>着色压暗压冷：这把伞吸光</summary>
        protected override Color TintBody(Color body)
            => Color.Lerp(body, BodyCold, 0.45f);

        //交互资格沿用基类（雨外、无演出进行中）；去向同一句话：撑伞入雨
        protected override string HintText => OniRainWorldSystem.InteractHint.Value;

        /// <summary>整套入雨转场（涨水浮镜→窥影→翻转），结算白闪处切进子世界；
        /// 运镜失败不致命，演出照走</summary>
        protected override void OnInteract(Player player) {
            OniRainWorldTransition.Begin(player, Position, OniRainCommitTarget.KiameSubworld);
            CutsceneDirector.Play<OniRainWorldCutscene>(player);
        }

        public override void AI() {
            base.AI();
            if (!Main.dedServ && VisibleToLocalPlayer()) {
                UpdateHorrorAmbience();
            }
        }

        /// <summary>
        /// 恐怖变体的加密氛围（叠在基类常规滴水之上）：
        /// 洼里黑水滴上浮更密、伞身周围渗出暗雾、偶发一粒被猛拽回伞喉——
        /// 洼里的水一直在爬回伞里，这把伞在进食
        /// </summary>
        private void UpdateHorrorAmbience() {
            horrorTimer++;
            float agitation = AgitationLevel;

            //加密上浮黑水滴：基类 15f 一粒，这里再叠一路 5f 一粒
            int riseInterval = Math.Max(3, 5 - (int)(agitation * 3f));
            if (horrorTimer % riseInterval == 0) {
                float x = Main.rand.NextFloat(-0.9f, 0.9f) * PuddleHalfWidth * PuddleSwell;
                PRTLoader.NewParticle<PRT_OniPuddleRise>(
                    PuddleCenter + new Vector2(x, -1f),
                    new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.7f)),
                    Color.Lerp(PoolBlack, MistDamp, Main.rand.NextFloat(0.5f))
                        * Main.rand.NextFloat(0.7f, 1f),
                    Main.rand.NextFloat(0.6f, 1.05f))
                    ?.Configure(Main.rand.Next(50, 96));
            }

            //伞身渗出的暗雾：贴着伞盖缓缓洇开
            if (Main.rand.NextBool(9)) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    CanopyAnchor + new Vector2(Main.rand.NextFloat(-34f, 34f), Main.rand.NextFloat(-10f, 16f)),
                    new Vector2(Main.rand.NextFloat(-0.25f, 0.25f), -Main.rand.NextFloat(0.03f, 0.12f)),
                    MistDamp * Main.rand.NextFloat(0.55f, 0.85f),
                    Main.rand.NextFloat(0.6f, 1.05f))
                    ?.Configure(Main.rand.Next(80, 130));
            }

            //被拽回伞喉的水珠：比故事伞频得多，洼在被喝
            if (Main.rand.NextBool(4)) {
                float x = Main.rand.NextFloat(-0.7f, 0.7f) * PuddleHalfWidth * PuddleSwell;
                PRTLoader.NewParticle<PRT_GhostRainYank>(
                    PuddleCenter + new Vector2(x, -1f),
                    new Vector2(0f, -Main.rand.NextFloat(1.2f, 2.2f)),
                    PaleTint * Main.rand.NextFloat(0.3f, 0.5f),
                    Main.rand.NextFloat(0.4f, 0.68f))
                    ?.Configure(CanopyThroat, Main.rand.Next(22, 36));
            }
        }
    }
}
