using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniAnnihilates;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions
{
    /// <summary>
    /// 铭刻副斩与触发演出的统一生成器。副斩全走 <see cref="CrimsonRendCleave"/>，
    /// 伤害基数压低且不产生任何气力/架势回调；粒子按事件生成不按目标翻倍
    /// </summary>
    internal static class OniMeiStrikes
    {
        //====副斩倍率(相对本拍武器伤害)====
        /// <summary>狮势合颚:单刃倍率(两刃合计约 0.40)</summary>
        private const float LionJawDamageMul = 0.20f;
        /// <summary>咎影延迟斩</summary>
        private const float GuiltEchoDamageMul = 0.35f;
        /// <summary>龙火回环斩</summary>
        private const float KurikaraLoopDamageMul = 0.40f;
        /// <summary>咎影残像滞拍(帧),读得出位置再咬合</summary>
        private const int GuiltEchoDelayFrames = 6;

        //====介质色(旧金/龙火/漆铁,主体仍是系列绯红)====
        private static readonly Color GoldSpark = new(232, 186, 110);
        private static readonly Color DragonFire = new(235, 150, 80);
        private static readonly Color LacquerDark = new(30, 14, 16);
        private static readonly Color PaperSteel = new(255, 243, 226);

        //==================== 副斩 ====================

        /// <summary>狮子之子第五拍:合颚双刃波(上下收窄咬合)+中心暗墨压力波;owner 端</summary>
        public static void FireLionJaw(Player player, Vector2 origin, float aim, int beatDamage,
            float knockback, float sizeMul) {
            int damage = Math.Max(1, (int)(beatDamage * LionJawDamageMul));
            Vector2 aimDir = aim.ToRotationVector2();
            Vector2 center = origin + aimDir * 190f * sizeMul;
            //合颚:比普通 X 更窄的上下夹角,读作"狮口咬合"
            CrimsonRendCleave.FireCross(player, center, aim, 0.42f, damage, knockback * 0.5f
                , sizeMul * 0.92f, player.GetSource_ItemUse(player.HeldItem), CleaveStyle.LionJaw);

            if (Main.dedServ) {
                return;
            }
            //中心无伤害暗墨压力波:一圈向外的墨烟,吼开的那口气
            for (int i = 0; i < 8; i++) {
                Vector2 dir = (MathHelper.TwoPi * i / 8f).ToRotationVector2();
                PRTLoader.NewParticle<PRT_CrimsonSmoke>(center + dir * 20f, dir * Main.rand.NextFloat(2.2f, 4f)
                    , Color.White, Main.rand.NextFloat(0.07f, 0.11f) * sizeMul)
                    ?.Configure(Main.rand.Next(14, 22), new Color(120, 30, 34), new Color(24, 12, 18));
            }
            CrimsonImpactFX.PushImpact(center, 0.12f);
        }

        /// <summary>友切:疾走取消连段的原地延迟斩影(滞拍后咬合);owner 端</summary>
        public static void FireGuiltEcho(Player player, Vector2 center, float aim, int beatDamage,
            float knockback, float sizeMul) {
            int damage = Math.Max(1, (int)(beatDamage * GuiltEchoDamageMul));
            CrimsonRendCleave.Fire(player, center, aim, damage, knockback * 0.4f, sizeMul * 0.9f
                , flip: Main.rand.NextBool() ? 1 : -1, player.GetSource_ItemUse(player.HeldItem)
                , CleaveStyle.GuiltEcho, GuiltEchoDelayFrames);
        }

        /// <summary>倶利伽罗:处决点燃后第五拍的龙火回环斩;owner 端</summary>
        public static void FireKurikaraLoop(Player player, Vector2 origin, float aim, int beatDamage,
            float knockback, float sizeMul) {
            int damage = Math.Max(1, (int)(beatDamage * KurikaraLoopDamageMul));
            Vector2 aimDir = aim.ToRotationVector2();
            //回环:斜跨主斩弧的一道缠刃,与第五拍巨弧交叠成环
            CrimsonRendCleave.Fire(player, origin + aimDir * 150f * sizeMul, aim + 1.15f, damage
                , knockback * 0.4f, sizeMul * 0.95f, flip: -1
                , player.GetSource_ItemUse(player.HeldItem), CleaveStyle.KurikaraLoop);

            if (Main.dedServ) {
                return;
            }
            //收束:前四拍余火拢进刀身的一口吸
            for (int i = 0; i < 6; i++) {
                Vector2 pos = player.Center + Main.rand.NextVector2Circular(70f, 70f);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, (player.Center + aimDir * 90f - pos) * 0.10f
                    , DragonFire, Main.rand.NextFloat(0.25f, 0.45f) * sizeMul)
                    ?.Configure(Main.rand.Next(10, 16), affectedByGravity: false);
            }
        }

        //==================== 逐拍/状态演出 ====================

        /// <summary>狮势蓄势:成功续拍时刀光背缘的暗金共振线(全客户端,量随链数)</summary>
        public static void SpawnLionBuildup(Vector2 center, float aim, float sizeMul, int chain) {
            if (Main.dedServ) {
                return;
            }
            Vector2 aimDir = aim.ToRotationVector2();
            Vector2 back = center + aimDir * 120f * sizeMul;
            int count = Math.Min(1 + chain, 5);
            for (int i = 0; i < count; i++) {
                Vector2 pos = back + aimDir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-60f, 60f) * sizeMul;
                Vector2 vel = aimDir.RotatedBy(Main.rand.NextFloat(-0.3f, 0.3f)) * Main.rand.NextFloat(1.5f, 3.5f);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, vel, GoldSpark
                    , Main.rand.NextFloat(0.22f, 0.38f) * sizeMul)
                    ?.Configure(Main.rand.Next(12, 18), affectedByGravity: false);
            }
        }

        /// <summary>狮势被打断:失谐金属粉散落(全客户端)</summary>
        public static void SpawnLionScatter(Vector2 center, float sizeMul) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_CrimsonSpark>(center + Main.rand.NextVector2Circular(40f, 40f)
                    , Main.rand.NextVector2Circular(2f, 1f) + Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.5f)
                    , GoldSpark * 0.8f, Main.rand.NextFloat(0.18f, 0.3f) * sizeMul)
                    ?.Configure(Main.rand.Next(10, 16), affectedByGravity: true);
            }
        }

        /// <summary>倶利伽罗点燃:雕纹依笔序升温 + 一缕细火沿刃根爬行(owner 客户端)</summary>
        public static void SpawnKurikaraIgnite(Player player) {
            SoundEngine.PlaySound(SoundID.Item34 with { Pitch = 0.10f, Volume = 0.5f }, player.Center);
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_OniMeiGlyph>(player.Center - Vector2.UnitY * 46f, Vector2.Zero
                , Color.White, 1f)
                ?.Configure(nameof(MeiKurikara), 46, 44f, OnikiriUITheme.GoldInlay
                    , maxReveal: 1f, followPlayer: player.whoAmI, followOffset: -Vector2.UnitY * 46f);
            for (int i = 0; i < 10; i++) {
                float t = i / 10f;
                Vector2 pos = player.Center + new Vector2(MathF.Sin(t * MathHelper.TwoPi) * 26f, -t * 60f);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, -Vector2.UnitY * Main.rand.NextFloat(0.8f, 1.8f)
                    , DragonFire, Main.rand.NextFloat(0.2f, 0.4f))
                    ?.Configure(Main.rand.Next(14, 24), affectedByGravity: false);
            }
            PRTLoader.NewParticle<PRT_CrimsonSmoke>(player.Center - Vector2.UnitY * 30f
                , -Vector2.UnitY * 0.7f, Color.White, 0.07f)
                ?.Configure(20, new Color(150, 44, 22), new Color(22, 11, 10));
        }

        /// <summary>龙火窗口内连段拍:刀侧火鞘火星+黑烟(owner 客户端,量少)</summary>
        public static void SpawnDragonfireBeatFlame(Player player, float aim, float sizeMul) {
            if (Main.dedServ) {
                return;
            }
            Vector2 aimDir = aim.ToRotationVector2();
            for (int i = 0; i < 3; i++) {
                Vector2 pos = player.Center + aimDir * Main.rand.NextFloat(40f, 110f) * sizeMul;
                PRTLoader.NewParticle<PRT_CrimsonSpark>(pos
                    , aimDir * Main.rand.NextFloat(1f, 3f) - Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.2f)
                    , i == 0 ? GoldSpark : DragonFire, Main.rand.NextFloat(0.2f, 0.36f) * sizeMul)
                    ?.Configure(Main.rand.Next(10, 18), affectedByGravity: false);
            }
            if (Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_CrimsonSmoke>(player.Center + aimDir * 70f * sizeMul
                    , -Vector2.UnitY * 0.6f, Color.White, 0.055f * sizeMul)
                    ?.Configure(Main.rand.Next(14, 20), new Color(130, 38, 20), new Color(20, 10, 9));
            }
        }

        /// <summary>不动护发动:漆铁墨面内凹碎裂,「不动」字形显出一角再断成重片(owner 客户端)</summary>
        public static void SpawnFudoGuard(Player player) {
            SoundEngine.PlaySound(SoundID.Tink with { Pitch = -0.42f, Volume = 0.62f }, player.Center);
            SoundEngine.PlaySound(SoundID.Item37 with { Pitch = 0.05f, Volume = 0.32f }, player.Center);
            if (Main.dedServ) {
                return;
            }
            Vector2 anchor = player.Center - Vector2.UnitY * 6f - Vector2.UnitX * player.direction * 14f;
            //字形只显出一角(纸白/旧金两帧裂纹),不贴完整符号
            PRTLoader.NewParticle<PRT_OniMeiGlyph>(anchor, Vector2.Zero, Color.White, 1f)
                ?.Configure(nameof(MeiFudo), 30, 40f, OnikiriUITheme.GoldDeep
                    , maxReveal: 0.45f, followPlayer: player.whoAmI
                    , followOffset: anchor - player.Center);
            PRTLoader.NewParticle<PRT_CrimsonHitFlash>(anchor, Vector2.Zero, PaperSteel, 0.75f);
            //黑漆碎片有重量地下坠 + 少量钢屑
            for (int i = 0; i < 7; i++) {
                PRTLoader.NewParticle<PRT_OniInkDrop>(anchor + Main.rand.NextVector2Circular(16f, 20f)
                    , Main.rand.NextVector2Circular(2.4f, 1.2f) - Vector2.UnitY * Main.rand.NextFloat(0.5f, 2f)
                    , LacquerDark, Main.rand.NextFloat(0.2f, 0.38f))
                    ?.Configure(Main.rand.Next(16, 26));
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_CrimsonSteelSpark>(anchor
                    , Main.rand.NextVector2Circular(4f, 2.5f) - Vector2.UnitY * Main.rand.NextFloat(1f, 3f)
                    , GoldSpark, Main.rand.NextFloat(0.24f, 0.4f))
                    ?.Configure(Main.rand.Next(14, 22));
            }
        }

        //==================== 髭切断首 ====================

        /// <summary>断线:入线目标轮廓上的极细错位断口(仅命中者,不扫全屏)</summary>
        public static void SpawnSeverLine(NPC target, float cutAngle) {
            if (Main.dedServ || target == null) {
                return;
            }
            Vector2 dir = cutAngle.ToRotationVector2();
            float half = MathF.Max(target.width, target.height) * 0.42f;
            //两粒高速拉伸的纸白钢屑对开,读作一根发丝断线
            PRTLoader.NewParticle<PRT_CrimsonSpark>(target.Center + dir.RotatedBy(MathHelper.PiOver2) * 2f
                , dir * 16f, PaperSteel, 0.34f)?.Configure(9, affectedByGravity: false);
            PRTLoader.NewParticle<PRT_CrimsonSpark>(target.Center - dir.RotatedBy(MathHelper.PiOver2) * 2f
                , -dir * 16f, PaperSteel, 0.34f)?.Configure(9, affectedByGravity: false);
            PRTLoader.NewParticle<PRT_CrimsonSpark>(target.Center + dir * half, dir * 4f
                , PaperSteel * 0.8f, 0.22f)?.Configure(12, affectedByGravity: false);
        }

        /// <summary>断首了结:一粒纸白钢屑沿刀路倒飞回鞘(架势返还的具象)</summary>
        public static void SpawnExecuteRefundFleck(Player player, Vector2 from) {
            if (Main.dedServ || player == null) {
                return;
            }
            Vector2 vel = (player.Center - from) / 14f;
            PRTLoader.NewParticle<PRT_CrimsonSpark>(from, vel, PaperSteel, 0.42f)
                ?.Configure(15, affectedByGravity: false);
            PRTLoader.NewParticle<PRT_CrimsonSpark>(from, vel * 0.85f, GoldSpark * 0.9f, 0.26f)
                ?.Configure(15, affectedByGravity: false);
        }

        //==================== 血樋回流 ====================

        /// <summary>
        /// 回流:真正触发额外回气的首次命中才喷发;
        /// 血肉=重力血滴+贴地血渍+一缕回身湿痕,金属=湿墨碎屑+钢火花(沿用材质分流)
        /// </summary>
        public static void SpawnBloodBackflow(Player player, NPC target) {
            if (Main.dedServ || target == null || player == null) {
                return;
            }
            bool steel = CWRLoad.NPCValue.ISTheofSteel(target);
            Vector2 toPlayer = (player.Center - target.Center).SafeNormalize(Vector2.UnitX);
            if (steel) {
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_OniInkDrop>(target.Center + Main.rand.NextVector2Circular(10f, 10f)
                        , toPlayer.RotatedByRandom(0.6) * Main.rand.NextFloat(2f, 4.5f)
                        , new Color(88, 22, 26), Main.rand.NextFloat(0.18f, 0.3f))
                        ?.Configure(Main.rand.Next(14, 22));
                }
                PRTLoader.NewParticle<PRT_CrimsonSteelSpark>(target.Center, toPlayer * 3f
                    , new Color(255, 170, 120), 0.3f)?.Configure(14);
                return;
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(target.Center + Main.rand.NextVector2Circular(10f, 12f)
                    , toPlayer.RotatedByRandom(0.8) * Main.rand.NextFloat(2f, 5f)
                        - Vector2.UnitY * Main.rand.NextFloat(0.5f, 2f)
                    , new Color(158, 22, 28), Main.rand.NextFloat(0.5f, 0.8f))
                    ?.Configure(Main.rand.Next(20, 32));
            }
            //一缕沿刃回身的湿痕
            PRTLoader.NewParticle<PRT_HeartcarverDroplet>(target.Center, toPlayer * 6.5f
                , new Color(190, 30, 34), 0.7f)?.Configure(14, gravityPerFrame: 0.06f);
            PRTLoader.NewParticle<PRT_CrimsonBloodStain>(target.Center
                , toPlayer.RotatedByRandom(0.5) * 3f + Vector2.UnitY * 1.5f
                , new Color(150, 20, 26), Main.rand.NextFloat(0.4f, 0.6f))
                ?.Configure(Main.rand.Next(18, 26));
        }
    }

    /// <summary>
    /// 铭文字形世界闪现:短命刻痕(凿现→冷却淡出),不动护碎裂/龙火点燃共用;
    /// 字形数据直读 <see cref="OniMeiGlyph"/>,不复制笔画坐标
    /// </summary>
    internal class PRT_OniMeiGlyph : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override bool CanPool => true;

        private string glyphKey;
        private Color accent;
        private float maxReveal;
        private float glyphSize;
        private int followPlayer;
        private Vector2 followOffset;

        public PRT_OniMeiGlyph Configure(string key, int lifetime, float size, Color accentColor,
            float maxReveal = 1f, int followPlayer = -1, Vector2 followOffset = default) {
            glyphKey = key;
            Lifetime = lifetime;
            glyphSize = size;
            accent = accentColor;
            this.maxReveal = MathHelper.Clamp(maxReveal, 0.1f, 1f);
            this.followPlayer = followPlayer;
            this.followOffset = followOffset;
            return this;
        }

        public override void Reset() {
            base.Reset();
            glyphKey = null;
            maxReveal = 1f;
            followPlayer = -1;
            followOffset = default;
            glyphSize = 40f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
        }

        public override void AI() {
            if (followPlayer < 0 || followPlayer >= Main.maxPlayers) {
                return;
            }
            Player player = Main.player[followPlayer];
            if (player.active && !player.dead) {
                Position = player.Center + followOffset;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (string.IsNullOrEmpty(glyphKey)) {
                return false;
            }
            float t = LifetimeCompletion;
            //前 35% 依笔序凿现,末 40% 冷却淡出
            float reveal = maxReveal * MathHelper.Clamp(t / 0.35f, 0f, 1f);
            float alpha = 1f - MathHelper.Clamp((t - 0.6f) / 0.4f, 0f, 1f);
            OniMeiGlyphStyle style = new() {
                Alpha = alpha * 0.9f,
                ChiselReveal = reveal,
                Accent = accent,
                Lit = 0.55f * (1f - t),
                Time = Main.GlobalTimeWrappedHourly,
            };
            OniMeiGlyph.Draw(spriteBatch, glyphKey, Position - Main.screenPosition, glyphSize, in style);
            return false;
        }
    }
}
