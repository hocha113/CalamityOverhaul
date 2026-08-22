using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniAnnihilates;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

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
        private const float LionJawDamageMul = 0.22f;
        /// <summary>咎影延迟斩</summary>
        private const float GuiltEchoDamageMul = 0.35f;
        /// <summary>龙火回环斩</summary>
        private const float KurikaraLoopDamageMul = 0.50f;
        /// <summary>咎影残像滞拍(帧),读得出位置再咬合</summary>
        private const int GuiltEchoDelayFrames = 6;

        //====介质色(旧金/龙火/漆铁,主体仍是系列绯红)====
        private static readonly Color GoldSpark = new(232, 186, 110);
        private static readonly Color DragonFire = new(235, 150, 80);
        private static readonly Color LacquerDark = new(30, 14, 16);
        private static readonly Color PaperSteel = new(255, 243, 226);
        private static readonly Color AirPale = new(226, 236, 240);
        private static readonly Color BreathPale = new(152, 152, 144);
        private static readonly Color TideCrest = new(214, 178, 190);

        /// <summary>成群了结时的断首刀响间隔(帧,纯客户端节流)</summary>
        private const int SeverSoundGapTicks = 10;
        private static ulong nextSeverSoundTick;

        private static int ResolveBaseWeaponDamage(IEntitySource source, int fallback) {
            if (source is EntitySource_Parent { Entity: Projectile parent }) {
                OniMeiActionContext context = OniMeiActionContext.Get(parent);
                if (context?.HasSnapshot == true) {
                    return context.BaseWeaponDamage;
                }
            }
            return Math.Max(1, fallback);
        }

        //==================== 副斩 ====================

        /// <summary>狮子之子第五拍:合颚双刃波(上下收窄咬合)+中心暗墨压力波;owner 端</summary>
        public static void FireLionJaw(Player player, Vector2 origin, float aim, int beatDamage,
            float knockback, float sizeMul, IEntitySource source = null) {
            int damage = Math.Max(1, (int)(ResolveBaseWeaponDamage(source, beatDamage) * LionJawDamageMul));
            Vector2 aimDir = aim.ToRotationVector2();
            Vector2 center = origin + aimDir * 190f * sizeMul;
            //合颚:比普通 X 更窄的上下夹角,读作"狮口咬合"
            CrimsonRendCleave.FireCross(player, center, aim, 0.42f, damage, knockback * 0.5f
                , sizeMul * 0.92f, source ?? player.GetSource_ItemUse(player.HeldItem), CleaveStyle.LionJaw);

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
        }

        /// <summary>友切:疾走取消连段的原地延迟斩影(滞拍后咬合);owner 端</summary>
        public static void FireGuiltEcho(Player player, Vector2 center, float aim, int beatDamage,
            float knockback, float sizeMul, IEntitySource source = null) {
            int damage = Math.Max(1, (int)(ResolveBaseWeaponDamage(source, beatDamage) * GuiltEchoDamageMul));
            CrimsonRendCleave.Fire(player, center, aim, damage, knockback * 0.4f, sizeMul * 0.9f
                , flip: Main.rand.NextBool() ? 1 : -1, source ?? player.GetSource_ItemUse(player.HeldItem)
                , CleaveStyle.GuiltEcho, GuiltEchoDelayFrames);
        }

        /// <summary>倶利伽罗:处决点燃后第五拍的龙火回环斩;owner 端</summary>
        public static void FireKurikaraLoop(Player player, Vector2 origin, float aim, int beatDamage,
            float knockback, float sizeMul, IEntitySource source = null) {
            int damage = Math.Max(1, (int)(ResolveBaseWeaponDamage(source, beatDamage) * KurikaraLoopDamageMul));
            Vector2 aimDir = aim.ToRotationVector2();
            //回环:斜跨主斩弧的一道缠刃,与第五拍巨弧交叠成环
            CrimsonRendCleave.Fire(player, origin + aimDir * 150f * sizeMul, aim + 1.15f, damage
                , knockback * 0.4f, sizeMul * 0.95f, flip: -1
                , source ?? player.GetSource_ItemUse(player.HeldItem), CleaveStyle.KurikaraLoop);

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

        /// <summary>谢樋剪落:了结点溅一小段邻域剪刃;花瓣仅 PRT;不得再触发剪落</summary>
        public static void FirePetalPrune(Player player, NPC target, Vector2 origin, float aim,
            int weaponDamage, float knockback, IEntitySource source = null) {
            int damage = Math.Max(1,
                (int)(ResolveBaseWeaponDamage(source, weaponDamage) * OniMeiCombat.PetalPruneDamageMul));
            CrimsonRendCleave.Fire(player, origin, aim, damage, knockback * 0.25f, scale: 0.72f
                , flip: Main.rand.NextBool() ? 1 : -1, source ?? player.GetSource_ItemUse(player.HeldItem)
                , CleaveStyle.PetalPrune, trackedRoot: target?.whoAmI ?? -1);

            if (Main.dedServ) {
                return;
            }
            Vector2 aimDir = aim.ToRotationVector2();
            for (int i = 0; i < 8; i++) {
                Vector2 vel = aimDir.RotatedByRandom(0.9f) * Main.rand.NextFloat(2.5f, 7f)
                    - Vector2.UnitY * Main.rand.NextFloat(0.2f, 1.4f);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(origin + Main.rand.NextVector2Circular(18f, 18f)
                    , vel, new Color(255, 150, 170), Main.rand.NextFloat(0.22f, 0.4f))
                    ?.Configure(Main.rand.Next(14, 24), affectedByGravity: true);
            }
        }

        /// <summary>
        /// 蜘蛛切墨丝:三锚闭网。整张网走一枚 <see cref="OniMeiInkThread"/>,
        /// 松弛垂坠一拍后向重心收紧,扫过者即被割;owner 端
        /// </summary>
        public static void FireSilkSnare(Player player, System.Collections.Generic.List<Vector2> points,
            int weaponDamage, float knockback, IEntitySource source = null) {
            if (player == null || points == null || points.Count < 3) {
                return;
            }
            int damage = Math.Max(1,
                (int)(ResolveBaseWeaponDamage(source, weaponDamage) * OniMeiCombat.SilkSnareDamageMul));
            OniMeiInkThread.Fire(player, points, OniMeiThreadStyle.Snare, damage, knockback * 0.35f,
                source ?? player.GetSource_ItemUse(player.HeldItem));

            if (Main.dedServ) {
                return;
            }
            player.CWR()?.GetScreenShake(1.8f);
            //闭网告知:三锚同时一亮,读作"网合上了"而不是"又一个特效"
            foreach (Vector2 point in points) {
                PRTLoader.NewParticle<PRT_CrimsonHitFlash>(point, Vector2.Zero, PaperSteel, 0.42f);
            }
        }

        /// <summary>墨丝钉锚:湿墨钉进目标,第几枚就多几粒回丝(数得出还差几枚)</summary>
        public static void SpawnSilkAnchor(Vector2 at, int index) {
            SoundEngine.PlaySound(SoundID.Item17 with {
                Pitch = 0.10f + index * 0.22f,
                Volume = 0.30f,
            }, at);
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_CrimsonHitFlash>(at, Vector2.Zero, PaperSteel, 0.26f);
            for (int i = 0; i < 3 + index * 2; i++) {
                float ang = MathHelper.TwoPi * i / (3f + index * 2f) + index * 0.4f;
                PRTLoader.NewParticle<PRT_OniInkDrop>(at + ang.ToRotationVector2() * 5f
                    , ang.ToRotationVector2() * Main.rand.NextFloat(1.2f, 2.8f)
                    , new Color(58, 16, 22), Main.rand.NextFloat(0.14f, 0.24f))
                    ?.Configure(Main.rand.Next(16, 26));
            }
        }

        /// <summary>
        /// 雷切落雷落空(头顶有遮挡):刃上憋住的那点电噼一声散在刀身,
        /// 让"这一刀没落雷"是看得见的结果而不是无事发生
        /// </summary>
        public static void SpawnThunderChoke(Player player) {
            if (player == null) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item93 with { Pitch = -0.35f, Volume = 0.30f }, player.Center);
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = Vector2.UnitX * player.direction;
            for (int i = 0; i < 6; i++) {
                //沿刃甩出的短促电屑,方向贴着刀而不是四散
                PRTLoader.NewParticle<PRT_CrimsonSteelSpark>(
                    player.Center + dir * Main.rand.NextFloat(10f, 44f)
                        - Vector2.UnitY * Main.rand.NextFloat(0f, 18f)
                    , dir.RotatedByRandom(0.5f) * Main.rand.NextFloat(1.5f, 4f)
                    , GoldSpark, Main.rand.NextFloat(0.16f, 0.28f))
                    ?.Configure(Main.rand.Next(8, 14));
            }
        }

        /// <summary>般若面变:鬼面咬合，目标处一记血黑窄斩,白牙只在前缘一线</summary>
        public static void FireHannyaBite(Player player, Vector2 at, float aim, int weaponDamage,
            float knockback, IEntitySource source = null) {
            if (player == null) {
                return;
            }
            int damage = Math.Max(1,
                (int)(ResolveBaseWeaponDamage(source, weaponDamage) * OniMeiCombat.HannyaBiteDamageMul));
            //咬合:上下夹角比普通 X 更窄,读作"一张嘴合上"
            CrimsonRendCleave.FireCross(player, at, aim, 0.34f, damage, knockback * 0.4f,
                0.86f, source ?? player.GetSource_ItemUse(player.HeldItem), CleaveStyle.HannyaBite);

            if (Main.dedServ) {
                return;
            }
            //面碎:血黑碎片沿咬合线对开,不做成红雾
            Vector2 dir = aim.ToRotationVector2();
            for (int i = 0; i < 7; i++) {
                float side = i % 2 == 0 ? 1f : -1f;
                PRTLoader.NewParticle<PRT_OniInkDrop>(at + dir * Main.rand.NextFloat(-14f, 14f)
                    , dir.RotatedBy(MathHelper.PiOver2) * side * Main.rand.NextFloat(2f, 5f)
                    , new Color(96, 10, 18), Main.rand.NextFloat(0.18f, 0.32f))
                    ?.Configure(Main.rand.Next(16, 26));
            }
            PRTLoader.NewParticle<PRT_CrimsonHitFlash>(at, Vector2.Zero, PaperSteel, 0.42f);
        }

        /// <summary>
        /// 般若翻面:女面↔鬼面的那一帧。翻成鬼时刀身转血黑并炸开一张面,
        /// 翻回去时只余一缕散墨，玩家从画面就知道自己进了/出了哪一档
        /// </summary>
        public static void SpawnHannyaShift(Player player, bool masked) {
            if (player == null) {
                return;
            }
            if (masked) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.65f, Volume = 0.42f }, player.Center);
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.55f, Volume = 0.38f }, player.Center);
            }
            else {
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.35f, Volume = 0.22f }, player.Center);
            }
            if (Main.dedServ) {
                return;
            }
            player.CWR()?.GetScreenShake(masked ? 3.2f : 1.0f);
            Vector2 anchor = player.Center - Vector2.UnitY * 10f;
            if (!masked) {
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_CrimsonSmoke>(anchor + Main.rand.NextVector2Circular(14f, 18f)
                        , -Vector2.UnitY * 0.5f, Color.White, 0.05f)
                        ?.Configure(Main.rand.Next(14, 22), new Color(70, 20, 26), new Color(18, 8, 12));
                }
                return;
            }
            //鬼面显形:字形一凿 + 血黑向外崩
            PRTLoader.NewParticle<PRT_OniMeiGlyph>(anchor, Vector2.Zero, Color.White, 1f)
                ?.Configure(nameof(MeiHannya), 34, 44f, new Color(196, 24, 30)
                    , maxReveal: 1f, followPlayer: player.whoAmI, followOffset: anchor - player.Center);
            for (int i = 0; i < 12; i++) {
                float ang = MathHelper.TwoPi * i / 12f;
                PRTLoader.NewParticle<PRT_OniInkDrop>(anchor + ang.ToRotationVector2() * 12f
                    , ang.ToRotationVector2() * Main.rand.NextFloat(2.5f, 5.5f)
                    , new Color(86, 8, 16), Main.rand.NextFloat(0.18f, 0.34f))
                    ?.Configure(Main.rand.Next(18, 28));
            }
        }

        /// <summary>虚吼空鸣:空场低压脉冲,半径内叠短「滞缚」(真实阻尼),无狮颚伤害</summary>
        public static void FireHollowRoarPulse(Player player) {
            if (player == null) {
                return;
            }
            Vector2 center = player.Center;
            float radiusSq = OniMeiCombat.HollowRoarRadius * OniMeiCombat.HollowRoarRadius;
            System.Collections.Generic.HashSet<int> affectedRoots = [];
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy() || npc.friendly) {
                    continue;
                }
                if (npc.DistanceSQ(center) > radiusSq) {
                    continue;
                }
                NPC root = OniMeiCombat.ResolveEffectRoot(npc);
                if (root != null && affectedRoots.Add(root.whoAmI)) {
                    root.AddBuff(ModContent.BuffType<OniBindDebuff>(), OniMeiCombat.HollowRoarSlowTicks);
                }
            }
            SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.55f, Volume = 0.22f }, center);
            if (Main.dedServ) {
                return;
            }
            //威压是一道波前,不是一圈等角喷雾:一层外扩的压缩环 + 半拍后的余波
            PRTLoader.NewParticle<PRT_OniHollowWave>(center, Vector2.Zero, Color.White, 1f)
                ?.Configure(OniMeiCombat.HollowRoarRadius, 26, 0f);
            PRTLoader.NewParticle<PRT_OniHollowWave>(center, Vector2.Zero, Color.White, 0.62f)
                ?.Configure(OniMeiCombat.HollowRoarRadius * 0.66f, 22, 1.9f);
            //被压出来的墨:贴地几滴,给这圈空气一个重量
            for (int i = 0; i < 4; i++) {
                Vector2 side = Vector2.UnitX * (Main.rand.NextBool() ? 1f : -1f);
                PRTLoader.NewParticle<PRT_OniInkDrop>(center + side * Main.rand.NextFloat(10f, 26f)
                    , side * Main.rand.NextFloat(1.2f, 2.6f) - Vector2.UnitY * Main.rand.NextFloat(0.3f, 1.1f)
                    , new Color(46, 14, 20), Main.rand.NextFloat(0.18f, 0.30f))
                    ?.Configure(Main.rand.Next(18, 28));
            }
        }

        /// <summary>
        /// 铁截「截金」命中反馈:刃咬进钢里再刮出去，沿刃向的刮擦火舌 + 卷起的旧金屑,
        /// 音是咬入不是弹开(owner 客户端)
        /// </summary>
        public static void SpawnIronSeverFX(NPC target, Vector2 scrapeDir) {
            if (target == null) {
                return;
            }
            //Tink 是"弹开"的音,与截断钢铁的语义相反,换成钝一档的刀咬钢。
            //只给一个音:截金按连段每拍首击结算,约 4 次/秒,再叠一层磨擦就成了噪音墙
            SoundEngine.PlaySound(CWRSound.KatanaHit with { Pitch = -0.35f, Volume = 0.48f }, target.Center);
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = scrapeDir.SafeNormalize(Vector2.UnitX);
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            Vector2 bite = target.Center - dir * 6f;
            //刮擦火舌:贴着刃走的一束,越靠刃口越快
            for (int i = 0; i < 6; i++) {
                float along = Main.rand.NextFloat(0.5f, 1f);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(bite + perp * Main.rand.NextFloat(-7f, 7f)
                    , dir * Main.rand.NextFloat(7f, 15f) * along + perp * Main.rand.NextFloat(-1.1f, 1.1f)
                    , GoldSpark, Main.rand.NextFloat(0.20f, 0.34f))
                    ?.Configure(Main.rand.Next(9, 15), affectedByGravity: false);
            }
            //卷屑:被刨下来的金属有重量,弹一下落地
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_CrimsonSteelSpark>(bite + perp * Main.rand.NextFloat(-5f, 5f)
                    , dir.RotatedByRandom(0.7f) * Main.rand.NextFloat(2.2f, 4.5f)
                        - Vector2.UnitY * Main.rand.NextFloat(1.2f, 3f)
                    , Color.Lerp(GoldSpark, PaperSteel, 0.35f), Main.rand.NextFloat(0.24f, 0.40f))
                    ?.Configure(Main.rand.Next(16, 26));
            }
            //咬口的一缕摩擦烟
            PRTLoader.NewParticle<PRT_CrimsonSmoke>(bite, -dir * 0.7f, Color.White, 0.055f)
                ?.Configure(Main.rand.Next(14, 20), new Color(120, 70, 34), new Color(22, 14, 10));
        }

        /// <summary>止足消费反馈:足元「止足」字形一闪 + 立定环碎成纸白屑(owner 客户端)</summary>
        public static void SpawnPlantedConsumeFX(Player player) {
            if (player == null) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.35f, Volume = 0.40f }, player.Center);
            if (Main.dedServ) {
                return;
            }
            Vector2 foot = player.Bottom - Vector2.UnitY * 4f;
            PRTLoader.NewParticle<PRT_OniMeiGlyph>(foot - Vector2.UnitY * 18f, Vector2.Zero, Color.White, 1f)
                ?.Configure(nameof(MeiAshidome), 26, 30f, OnikiriUITheme.Bright
                    , maxReveal: 1f, followPlayer: player.whoAmI
                    , followOffset: foot - Vector2.UnitY * 18f - player.Center);
            for (int i = 0; i < 8; i++) {
                float ang = MathHelper.TwoPi * i / 8f;
                PRTLoader.NewParticle<PRT_CrimsonSpark>(foot + ang.ToRotationVector2() * 12f
                    , ang.ToRotationVector2() * Main.rand.NextFloat(2f, 4f) - Vector2.UnitY * 0.5f
                    , PaperSteel, Main.rand.NextFloat(0.18f, 0.30f))
                    ?.Configure(12, affectedByGravity: false);
            }
        }

        /// <summary>默切消费反馈:一记消音重击，发丝白闪 + 坠墨,声音沉短(owner 客户端)</summary>
        public static void SpawnSilentConsumeFX(Player player) {
            if (player == null) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.90f, Volume = 0.45f }, player.Center);
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_CrimsonHitFlash>(player.Center, Vector2.Zero, PaperSteel, 0.55f);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_OniInkDrop>(player.Center + Main.rand.NextVector2Circular(14f, 18f)
                    , Main.rand.NextVector2Circular(1.6f, 1f) + Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.2f)
                    , LacquerDark, Main.rand.NextFloat(0.18f, 0.30f))
                    ?.Configure(Main.rand.Next(14, 22));
            }
        }

        /// <summary>
        /// 痺反命中反馈:来手被顶回去再发麻。火花沿"打过来的那只手被弹回"的方向成束,
        /// 各向同性的一圈读不出这是反击(owner 客户端)
        /// </summary>
        public static void SpawnNumbCounterFX(NPC source, Vector2 knockDir) {
            if (source == null) {
                return;
            }
            //先一记顶回去的闷响,再是麻
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.30f, Volume = 0.34f }, source.Center);
            SoundEngine.PlaySound(SoundID.Item37 with { Pitch = 0.42f, Volume = 0.34f }, source.Center);
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = knockDir.SafeNormalize(Vector2.UnitX);
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            Vector2 contact = source.Center - dir * (source.width * 0.28f);
            //反弹束:顺着来手被推开的方向甩出,速度带梯度
            for (int i = 0; i < 7; i++) {
                float lane = Main.rand.NextFloat(-1f, 1f);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(contact + perp * lane * source.width * 0.22f
                    , dir * Main.rand.NextFloat(4.5f, 9f) + perp * lane * Main.rand.NextFloat(0.6f, 1.8f)
                    , PaperSteel, Main.rand.NextFloat(0.18f, 0.32f))
                    ?.Configure(Main.rand.Next(9, 15), affectedByGravity: false);
            }
            //麻:成对的短颤纹留在手上,与反弹束分开读
            for (int i = 0; i < 3; i++) {
                Vector2 at = source.Center
                    + Main.rand.NextVector2Circular(source.width * 0.34f, source.height * 0.34f);
                Vector2 jitter = perp.RotatedByRandom(0.5f) * Main.rand.NextFloat(1.1f, 2.2f);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(at, jitter, PaperSteel * 0.85f
                    , Main.rand.NextFloat(0.12f, 0.20f))
                    ?.Configure(Main.rand.Next(6, 10), affectedByGravity: false);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(at, -jitter, PaperSteel * 0.85f
                    , Main.rand.NextFloat(0.12f, 0.20f))
                    ?.Configure(Main.rand.Next(6, 10), affectedByGravity: false);
            }
        }

        /// <summary>
        /// 息合吐息:第五拍爆发脆响同帧沿瞄准甩出一道行进弧形剑气(凸面朝前,穿透每目标一次);
        /// 不回调气/架势。origin 应为刃弧中段(由 Slash 爆发帧传入)
        /// </summary>
        public static void FireBreathWave(Player player, Vector2 origin, float aim, int beatDamage,
            float knockback, float sizeMul = 1f, float flip = 1f, IEntitySource source = null) {
            if (player == null) {
                return;
            }
            float arcSize = sizeMul * OniMeiCombat.BreathArcSizeMul;
            int damage = Math.Max(1,
                (int)(ResolveBaseWeaponDamage(source, beatDamage) * OniMeiCombat.BreathArcDamageMul));
            Vector2 aimDir = aim.ToRotationVector2();
            //刃上已卡点,只略前送一截读作甩离
            Vector2 muzzle = origin;
            OniMeiBreathArc.Fire(player, muzzle, aim, damage, knockback * 0.55f, arcSize, flip
                , source ?? player.GetSource_ItemUse(player.HeldItem));

            if (Main.dedServ) {
                return;
            }
            player.CWR()?.GetScreenShake(2.6f);
            Vector2 perp = aimDir.RotatedBy(MathHelper.PiOver2);
            //出手爆点:沿甩向拉长的纸白火花 + 墨烟尾,禁各向同性喷雾
            for (int i = 0; i < 10; i++) {
                float along = Main.rand.NextFloat(0.4f, 1.2f);
                Vector2 vel = aimDir * Main.rand.NextFloat(8f, 18f) * along
                    + perp * Main.rand.NextFloat(-2.2f, 2.2f);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(muzzle + perp * Main.rand.NextFloat(-18f, 18f) * arcSize
                    , vel, new Color(255, 236, 220), Main.rand.NextFloat(0.32f, 0.55f) * arcSize)
                    ?.Configure(Main.rand.Next(10, 16), affectedByGravity: false);
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_CrimsonSmoke>(muzzle - aimDir * (10f + i * 8f)
                    , -aimDir * Main.rand.NextFloat(0.6f, 1.6f) + perp * Main.rand.NextFloat(-0.8f, 0.8f)
                    , Color.White, (0.08f + i * 0.015f) * arcSize)
                    ?.Configure(18 + i * 3, new Color(110, 30, 34), new Color(24, 12, 16));
            }
        }

        //==================== 逐拍/状态演出 ====================

        /// <summary>
        /// 狮势蓄势:每续一拍,暗金共振自柄向锋窜一程,链越长窜得越远越亮、音越高。
        /// "攒了四拍"同时写在刃上(<see cref="OniMeiBladeEngrave"/> 的狮势金线),不靠数粒子(全客户端)
        /// </summary>
        public static void SpawnLionBuildup(Vector2 center, float aim, float sizeMul, int chain) {
            if (Main.dedServ) {
                return;
            }
            //只在末两拍出声:连段本身每拍已有 ping,逐拍再加一记会把连段变成噪音墙。
            //这两声的信息量也最大，"再一拍就合颚"
            float ramp = MathHelper.Clamp((chain - 1) / 4f, 0f, 1f);
            if (chain >= 4) {
                SoundEngine.PlaySound(
                    SoundID.Item37 with { Pitch = -0.10f + ramp * 0.75f, Volume = 0.14f + ramp * 0.12f },
                    center);
            }

            Vector2 aimDir = aim.ToRotationVector2();
            Vector2 perp = aimDir.RotatedBy(MathHelper.PiOver2);
            int count = 2 + chain;
            for (int i = 0; i < count; i++) {
                //共振顺刀身往锋跑,不再是背缘的一片随机散点
                float along = (i + Main.rand.NextFloat()) / count;
                Vector2 pos = center + aimDir * MathHelper.Lerp(34f, 148f, along) * sizeMul
                    + perp * Main.rand.NextFloat(-9f, 9f) * sizeMul;
                Vector2 vel = aimDir * MathHelper.Lerp(3.5f, 10f, ramp * along)
                    + perp * Main.rand.NextFloat(-0.7f, 0.7f);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, vel
                    , Color.Lerp(GoldSpark, OnikiriUITheme.HotWhite, ramp * 0.35f)
                    , Main.rand.NextFloat(0.18f, 0.30f) * sizeMul * (0.8f + ramp * 0.5f))
                    ?.Configure(Main.rand.Next(10, 16), affectedByGravity: false);
            }
        }

        /// <summary>狮势被打断:攒在刃上的金抖落下来,音随之泄气(全客户端)</summary>
        public static void SpawnLionScatter(Vector2 center, float sizeMul) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item37 with { Pitch = -0.75f, Volume = 0.24f }, center);
            //金是"掉"下来的:横向初速小、下坠明确,和蓄势的横窜正好相反
            for (int i = 0; i < 7; i++) {
                PRTLoader.NewParticle<PRT_CrimsonSteelSpark>(
                    center + Main.rand.NextVector2Circular(34f, 22f)
                    , new Vector2(Main.rand.NextFloat(-1.1f, 1.1f), Main.rand.NextFloat(0.6f, 1.8f))
                    , GoldSpark * 0.75f, Main.rand.NextFloat(0.16f, 0.28f) * sizeMul)
                    ?.Configure(Main.rand.Next(14, 22));
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

        //==================== 樋位状态演出 ====================

        /// <summary>
        /// 风樋「顺风」:疾走起步一记风哨,身前甩出一串领先半身的气线。
        /// 气线沿冲刺轴高速拉长,不留痕不挂壁，空气不是液体(owner 客户端)
        /// </summary>
        public static void SpawnWindGrooveDash(Player player, Vector2 aim) {
            if (player == null) {
                return;
            }
            Vector2 dir = aim.SafeNormalize(Vector2.UnitX * player.direction);
            SoundEngine.PlaySound(SoundID.DoubleJump with { Pitch = 0.75f, Volume = 0.30f }, player.Center);
            if (Main.dedServ) {
                return;
            }
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            for (int i = 0; i < 7; i++) {
                float ahead = 26f + i * 24f;
                Vector2 pos = player.Center + dir * ahead + perp * Main.rand.NextFloat(-14f, 14f);
                //速度越高 PRT_CrimsonSpark 拉得越长,一串短线读作被劈开的空气
                PRTLoader.NewParticle<PRT_CrimsonSpark>(pos
                    , dir * Main.rand.NextFloat(13f, 21f) + perp * Main.rand.NextFloat(-1.2f, 1.2f)
                    , AirPale * 0.85f, Main.rand.NextFloat(0.16f, 0.26f))
                    ?.Configure(Main.rand.Next(7, 12), affectedByGravity: false);
            }
        }

        /// <summary>
        /// 滞樋「滞缚」自黏:疾走起步时足下墨丝先绷紧再脱手,
        /// 那几帧再触发锁于是有了看得见的理由(owner 客户端)
        /// </summary>
        public static void SpawnStickyDashDrag(Player player, Vector2 aim) {
            if (player == null) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item37 with { Pitch = -0.72f, Volume = 0.30f }, player.Center);
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = aim.SafeNormalize(Vector2.UnitX * player.direction);
            Vector2 foot = player.Bottom - Vector2.UnitY * 3f;
            //墨丝朝冲刺反向被留下:人走了,丝还黏在原地才读作"扯断"
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_OniInkDrop>(
                    foot + Vector2.UnitX * Main.rand.NextFloat(-9f, 9f)
                    , -dir * Main.rand.NextFloat(3.2f, 7f)
                        + Vector2.UnitY * Main.rand.NextFloat(-1.6f, -0.4f)
                    , new Color(30, 12, 18), Main.rand.NextFloat(0.20f, 0.34f))
                    ?.Configure(Main.rand.Next(16, 26));
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_CrimsonSmoke>(foot + Vector2.UnitX * Main.rand.NextFloat(-8f, 8f)
                    , -dir * Main.rand.NextFloat(0.5f, 1.3f), Color.White, 0.05f)
                    ?.Configure(Main.rand.Next(14, 20), new Color(70, 22, 32), new Color(18, 9, 14));
            }
        }

        /// <summary>
        /// 闲樋「闲息」的进出:接上脱战窗时一口长息呼出,
        /// 被自己一刀打断时槽内回涌。那 120 帧窗口第一次有了外观(owner 客户端)
        /// </summary>
        public static void SpawnQuietBreathShift(Player player, bool entered) {
            if (player == null) {
                return;
            }
            if (entered) {
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -1f, Volume = 0.22f }, player.Center);
                if (Main.dedServ) {
                    return;
                }
                //长息:极慢、极淡、只往上,不做爆发
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_CrimsonSmoke>(
                        player.Center + Main.rand.NextVector2Circular(12f, 16f)
                        , -Vector2.UnitY * Main.rand.NextFloat(0.20f, 0.45f), Color.White
                        , Main.rand.NextFloat(0.045f, 0.075f))
                        ?.Configure(Main.rand.Next(26, 40), BreathPale, new Color(52, 52, 48));
                }
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_CrimsonSpark>(
                        player.Center + Main.rand.NextVector2Circular(14f, 18f)
                        , -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.1f)
                        , BreathPale * 0.8f, Main.rand.NextFloat(0.12f, 0.18f))
                        ?.Configure(Main.rand.Next(20, 30), affectedByGravity: false);
                }
                return;
            }
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.2f, Volume = 0.26f }, player.Center);
            if (Main.dedServ) {
                return;
            }
            //回涌:息断了,槽里重新见血
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_OniInkDrop>(
                    player.Center + Main.rand.NextVector2Circular(12f, 14f)
                    , Main.rand.NextVector2Circular(1.4f, 0.8f) + Vector2.UnitY * 0.6f
                    , new Color(120, 20, 26), Main.rand.NextFloat(0.18f, 0.28f))
                    ?.Configure(Main.rand.Next(14, 22));
            }
        }

        /// <summary>潮樋「潮拍」:踩中合潮的那一记,刃上抖出一圈薄水纹(owner 客户端)</summary>
        public static void SpawnTideBeatRipple(Player player) {
            if (Main.dedServ || player == null) {
                return;
            }
            Vector2 side = Vector2.UnitX * player.direction;
            for (int i = 0; i < 5; i++) {
                float t = i / 4f;
                //沿身侧铺开的浅弧,不是围着人转一圈
                Vector2 pos = player.Center + side * MathHelper.Lerp(8f, 34f, t)
                    - Vector2.UnitY * MathHelper.Lerp(-14f, 18f, t);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(pos
                    , side * Main.rand.NextFloat(1.4f, 3.2f) - Vector2.UnitY * Main.rand.NextFloat(0.2f, 0.9f)
                    , TideCrest * 0.9f, Main.rand.NextFloat(0.14f, 0.24f))
                    ?.Configure(Main.rand.Next(12, 18), affectedByGravity: false);
            }
        }

        /// <summary>
        /// 镇鸣「镇弹」:来弹撞在一面听不见的鼓上被压扁。
        /// 火花沿撞击面横向铺开而非各向同性外扩,回弹极浅,配金属闷响下沉(owner 客户端)
        /// </summary>
        public static void SpawnQuellStruck(Player player, Vector2 contact, Vector2 incomingDir) {
            if (player == null) {
                return;
            }
            //单音:中弹本身已有香草受伤音,这里只补一记"被镇住"的金属闷响
            SoundEngine.PlaySound(SoundID.Tink with { Pitch = -0.68f, Volume = 0.45f }, contact);
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = incomingDir.SafeNormalize(Vector2.UnitX * -player.direction);
            Vector2 face = dir.RotatedBy(MathHelper.PiOver2);
            //撞击面上的横向铺散:沿面走得远,朝来路只退一点
            for (int i = 0; i < 9; i++) {
                float spread = Main.rand.NextFloat(-1f, 1f);
                Vector2 vel = face * spread * Main.rand.NextFloat(4.5f, 9f)
                    - dir * Main.rand.NextFloat(0.3f, 1.4f);
                PRTLoader.NewParticle<PRT_CrimsonSteelSpark>(contact + face * spread * 6f, vel
                    , PaperSteel, Main.rand.NextFloat(0.18f, 0.32f))
                    ?.Configure(Main.rand.Next(10, 17));
            }
            //被吃掉的那部分动量化作贴面的两团闷烟。
            //此处**不放白闪**:中弹点紧贴角色,弹幕阶段每次挨打炸一下白等于遮住自己
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_CrimsonSmoke>(contact + face * Main.rand.NextFloat(-10f, 10f)
                    , -dir * Main.rand.NextFloat(0.4f, 1.1f), Color.White, 0.055f)
                    ?.Configure(Main.rand.Next(14, 22), new Color(96, 34, 34), new Color(20, 10, 14));
            }
        }

        //==================== 断首/取首 ====================

        /// <summary>
        /// 断口:入线目标轮廓上被切开的那一线,两片沿切面错开、缝里透光、两端侵蚀。<br/>
        /// 髭切=纸白锐断(白热芯一闪);旧首=旧钢钝断(无芯,断面更毛)。
        /// 断面两侧按材质喷溅:血肉出血,钢体出屑。仅命中者,不扫全屏
        /// </summary>
        public static void SpawnSeverLine(NPC target, float cutAngle, bool aged = false,
            bool killed = false) {
            if (Main.dedServ || target == null) {
                return;
            }
            //断口是结构件不是火花:连段最快每 5 帧一次命中,不设门闩会在残血 boss 上
            //堆成一片白糊,既毁掉"决定性一刀"的读法,也挡住二阶段的弹幕
            NPC gateRoot = OniMeiCombat.ResolveEffectRoot(target) ?? target;
            if (!gateRoot.GetGlobalNPC<OniSeverCutGate>().TryClaim(killed)) {
                return;
            }
            Vector2 dir = cutAngle.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            float half = MathF.Max(target.width, target.height) * 0.46f;
            bool steelBody = CWRLoad.NPCValue.ISTheofSteel(target);

            //断口本体:一条会张开的刻线,这才是"斩首"该有的主读物
            PRTLoader.NewParticle<PRT_OniSeverCut>(target.Center, Vector2.Zero, Color.White, 1f)
                ?.Configure(cutAngle, half, aged, killed, target.whoAmI);

            //断面喷溅:只沿切面法向走两侧,不做球形爆散
            int count = killed ? 7 : 4;
            for (int i = 0; i < count; i++) {
                float side = i % 2 == 0 ? 1f : -1f;
                float along = Main.rand.NextFloat(-0.55f, 0.55f);
                Vector2 at = target.Center + dir * along * half;
                Vector2 vel = perp * side * Main.rand.NextFloat(2.4f, 6.5f)
                    + dir * Main.rand.NextFloat(-1.2f, 1.2f);
                if (steelBody) {
                    PRTLoader.NewParticle<PRT_CrimsonSteelSpark>(at, vel
                        , aged ? new Color(214, 196, 170) : PaperSteel
                        , Main.rand.NextFloat(0.20f, 0.34f))
                        ?.Configure(Main.rand.Next(12, 20));
                    continue;
                }
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(at, vel - Vector2.UnitY * 0.6f
                    , new Color(158, 22, 28), Main.rand.NextFloat(0.42f, 0.72f))
                    ?.Configure(Main.rand.Next(18, 30));
            }
            if (!killed) {
                return;
            }
            //了结帧才出声,且成群了结时再节流一层:十只同帧死不该变成十记刀响
            if (Main.GameUpdateCount >= nextSeverSoundTick) {
                nextSeverSoundTick = Main.GameUpdateCount + SeverSoundGapTicks;
                SoundEngine.PlaySound(
                    aged ? SoundID.Item71 with { Pitch = -0.55f, Volume = 0.5f }
                         : CWRSound.KatanaHitB with { Pitch = 0.22f, Volume = 0.62f },
                    target.Center);
            }
            if (!steelBody) {
                PRTLoader.NewParticle<PRT_CrimsonBloodStain>(target.Center
                    , perp * Main.rand.NextFloat(-2f, 2f) + Vector2.UnitY * 2f
                    , new Color(150, 20, 26), Main.rand.NextFloat(0.5f, 0.75f))
                    ?.Configure(Main.rand.Next(20, 30));
            }
        }

        /// <summary>
        /// 断首了结的返势:势沿刀路被吸回鞘中。一串错开抵达的纸白细屑连成流,
        /// 而不是两粒孤零零的火花(owner 客户端)
        /// </summary>
        public static void SpawnExecuteRefundFleck(Player player, Vector2 from) {
            if (Main.dedServ || player == null) {
                return;
            }
            Vector2 toPlayer = player.Center - from;
            float distance = toPlayer.Length();
            if (distance < 1f) {
                return;
            }
            Vector2 dir = toPlayer / distance;
            SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = 0.35f, Volume = 0.28f }, player.Center);
            //已在路上的一串:各自起点不同、速度不同,读作一道流而非一次爆
            for (int i = 0; i < 6; i++) {
                float t = i / 5f;
                Vector2 at = from + toPlayer * (t * 0.55f)
                    + dir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-9f, 9f);
                float speed = distance / MathHelper.Lerp(16f, 9f, t);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(at
                    , (player.Center - at).SafeNormalize(dir) * speed
                    , i == 5 ? GoldSpark : PaperSteel, Main.rand.NextFloat(0.20f, 0.36f))
                    ?.Configure(Main.rand.Next(13, 19), affectedByGravity: false);
            }
            //入鞘的一记收束
            PRTLoader.NewParticle<PRT_CrimsonHitFlash>(player.Center, Vector2.Zero, GoldSpark, 0.35f);
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
    /// 断口的每目标视觉门闩。斩杀线内连段每 5~16 帧就命中一次,断口若跟着刷,
    /// 残血 boss 身上会常驻两三条重叠白线，既毁掉"决定性一刀"的稀有度,
    /// 也遮住二阶段的弹幕。了结帧不受门闩约束(那一刀本来就该看见)
    /// </summary>
    internal sealed class OniSeverCutGate : GlobalNPC
    {
        private const int VisualCooldownTicks = 24;

        private ulong nextTick;

        public override bool InstancePerEntity => true;

        public override void SetDefaults(NPC entity) => nextTick = 0;

        internal bool TryClaim(bool killed) {
            if (!killed && Main.GameUpdateCount < nextTick) {
                return false;
            }
            nextTick = Main.GameUpdateCount + VisualCooldownTicks;
            return true;
        }
    }

    /// <summary>
    /// 断口:被切开的那一线。前段自中心向两端揭开,随后两片沿切面法向错开、
    /// 缝里透出更暗的断面内部,两端按噪声侵蚀成毛口。<br/>
    /// aged=旧首的旧钢钝断(无白热芯、毛口更重);跟随目标以免大位移时脱节
    /// </summary>
    internal class PRT_OniSeverCut : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override bool CanPool => true;

        private const int Steps = 9;

        private float cutAngle;
        private float halfLength;
        private bool aged;
        private bool deep;
        private int followNPC = -1;
        private Vector2 followOffset;
        private float seed;

        public PRT_OniSeverCut Configure(float angle, float length, bool agedSteel, bool killed,
            int follow = -1) {
            cutAngle = angle;
            halfLength = MathF.Max(length, 6f);
            aged = agedSteel;
            deep = killed;
            followNPC = follow;
            Lifetime = killed ? 18 : 12;
            seed = Main.rand.NextFloat();
            if (follow >= 0 && follow < Main.maxNPCs) {
                followOffset = Position - Main.npc[follow].Center;
            }
            return this;
        }

        public override void Reset() {
            base.Reset();
            followNPC = -1;
            followOffset = default;
            aged = false;
            deep = false;
            halfLength = 12f;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AlphaBlend;

        public override void AI() {
            if (followNPC < 0 || followNPC >= Main.maxNPCs) {
                return;
            }
            NPC npc = Main.npc[followNPC];
            if (npc.active) {
                Position = npc.Center + followOffset;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            float t = LifetimeCompletion;
            //揭开→错位→淡出
            float reveal = MathHelper.Clamp(t / 0.28f, 0f, 1f);
            float part = MathHelper.Clamp((t - 0.18f) / 0.82f, 0f, 1f);
            part = 1f - (1f - part) * (1f - part) * (1f - part);
            float fade = 1f - MathHelper.Clamp((t - 0.45f) / 0.55f, 0f, 1f);
            if (fade <= 0.01f) {
                return false;
            }

            Vector2 dir = cutAngle.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            Vector2 center = Position - Main.screenPosition;
            float gap = part * (deep ? 4.2f : 2.2f);
            Color steel = aged ? new Color(214, 196, 170) : new Color(255, 243, 226);
            Color inner = new(6, 2, 4);
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);

            for (int i = 0; i < Steps; i++) {
                float u0 = i / (float)Steps * 2f - 1f;
                float u1 = (i + 1) / (float)Steps * 2f - 1f;
                float mid = (u0 + u1) * 0.5f;
                //两端侵蚀:包络 + 每段各自的噪声缺口,毛口比齐头更像断
                float envelope = MathF.Sqrt(MathF.Max(0f, 1f - mid * mid));
                float notch = 0.55f + 0.45f * OniBrush.Hash01((int)(seed * 977f) + i * 31);
                if (aged) {
                    notch *= 0.55f + 0.45f * OniBrush.Hash01((int)(seed * 613f) + i * 17);
                }
                float weight = envelope * notch * reveal;
                if (weight <= 0.05f) {
                    continue;
                }
                Vector2 a = center + dir * (u0 * halfLength * reveal);
                Vector2 b = center + dir * (u1 * halfLength * reveal);
                Vector2 edge = b - a;
                float len = edge.Length();
                if (len < 0.5f) {
                    continue;
                }
                float rot = edge.ToRotation();
                Vector2 origin = new(0f, 0.5f);
                //缝:两片之间露出的断面内部
                spriteBatch.Draw(pixel, a, src, inner * (fade * 0.9f * weight), rot, origin,
                    new Vector2(len, MathF.Max(gap * 2f, 1f)), SpriteEffects.None, 0f);
                //两片的断缘
                spriteBatch.Draw(pixel, a + perp * gap, src, steel * (fade * 0.85f * weight), rot, origin,
                    new Vector2(len, 1.1f), SpriteEffects.None, 0f);
                spriteBatch.Draw(pixel, a - perp * gap, src, steel * (fade * 0.85f * weight), rot, origin,
                    new Vector2(len, 1.1f), SpriteEffects.None, 0f);
                //白热芯只给锐断,且只在最初两帧,不常驻
                if (!aged && t < 0.16f) {
                    spriteBatch.Draw(pixel, a, src,
                        Color.White * (fade * 0.7f * weight * (1f - t / 0.16f)), rot, origin,
                        new Vector2(len, 0.9f), SpriteEffects.None, 0f);
                }
            }
            return false;
        }
    }

    /// <summary>
    /// 空鸣压力波前:外扩的一圈压缩环,不是等角喷雾。<br/>
    /// 半径按 EaseOutCubic 冲出后减速,环厚随之变薄,环形靠整数谐波起伏
    /// (整数倍角保证跨接缝连续),内缘压一层暗把它读成压缩而非发光圈
    /// </summary>
    internal class PRT_OniHollowWave : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override bool CanPool => true;

        private const int Arcs = 30;

        private float maxRadius;
        private float phase;

        public PRT_OniHollowWave Configure(float radius, int lifetime, float phaseOffset) {
            maxRadius = MathF.Max(radius, 32f);
            Lifetime = Math.Max(lifetime, 6);
            phase = phaseOffset;
            return this;
        }

        public override void Reset() {
            base.Reset();
            maxRadius = 128f;
            phase = 0f;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AlphaBlend;

        public override void AI() { }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            float t = LifetimeCompletion;
            float ease = 1f - (1f - t) * (1f - t) * (1f - t);
            float radius = maxRadius * ease * Scale;
            //越远越薄:冲出去的那口气在摊开
            float thick = MathHelper.Lerp(4.6f, 1.1f, ease) * Scale;
            float fade = (1f - t) * (1f - t) * Scale;
            if (fade <= 0.01f || radius < 6f) {
                return false;
            }

            Vector2 center = Position - Main.screenPosition;
            Color front = new Color(255, 243, 226) * (fade * 0.5f);
            Color inner = new Color(24, 10, 14) * (fade * 0.55f);
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 origin = new(0f, 0.5f);

            Vector2 previous = RingPoint(center, radius, 0f);
            for (int i = 1; i <= Arcs; i++) {
                float angle = MathHelper.TwoPi * i / Arcs;
                Vector2 current = RingPoint(center, radius, angle);
                Vector2 edge = current - previous;
                float len = edge.Length();
                if (len >= 0.5f) {
                    float rot = edge.ToRotation();
                    Vector2 pull = (previous - center).SafeNormalize(Vector2.UnitX);
                    //内缘的暗压在前,亮的波面压在上
                    spriteBatch.Draw(pixel, previous - pull * thick * 0.8f, src, inner, rot, origin,
                        new Vector2(len + 1f, thick * 1.5f), SpriteEffects.None, 0f);
                    spriteBatch.Draw(pixel, previous, src, front, rot, origin,
                        new Vector2(len + 1f, thick), SpriteEffects.None, 0f);
                }
                previous = current;
            }
            return false;
        }

        /// <summary>环形起伏只用整数倍角,跨 0/2π 接缝连续</summary>
        private Vector2 RingPoint(Vector2 center, float radius, float angle) {
            float wobble = 1f
                + 0.085f * MathF.Sin(3f * angle + phase)
                + 0.05f * MathF.Sin(5f * angle - phase * 1.4f);
            return center + angle.ToRotationVector2() * radius * wobble;
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
