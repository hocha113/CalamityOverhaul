using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.NPCs.TBUGs.UIs;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.TBUGs
{
    /// <summary>
    /// TBUG 分桶台词池：一次性语境（初见/首单成交）绝对优先，其余按当前语境
    /// （心情/玩家状态/进度/时段天气/群系）聚合成候选池，以概率压过通用 Greet 轮转
    /// </summary>
    internal static class TBUGDialogue
    {
        /// <summary>语境候选池命中时替代通用池的概率</summary>
        private const float ContextChance = 0.65f;
        /// <summary>心情极好阈值</summary>
        private const double HappyFactor = 0.9;
        /// <summary>心情差阈值</summary>
        private const double GrumpyFactor = 1.15;

        private static LocalizedText[] generic;
        private static LocalizedText[] firstMeet;
        private static LocalizedText[] firstPurchase;
        private static LocalizedText[] moodHomeless;
        private static LocalizedText[] moodHappy;
        private static LocalizedText[] moodGrumpy;
        private static LocalizedText[] hackUnlocked;
        private static LocalizedText[] hackLocked;
        private static LocalizedText[] lowHealth;
        private static LocalizedText[] rich;
        private static LocalizedText[] poor;
        private static LocalizedText[] preHardmode;
        private static LocalizedText[] hardmode;
        private static LocalizedText[] postMoonlord;
        private static LocalizedText[] postSupCal;
        private static LocalizedText[] bloodMoon;
        private static LocalizedText[] eclipse;
        private static LocalizedText[] rain;
        private static LocalizedText[] deepNight;
        private static LocalizedText[] underground;
        private static LocalizedText[] snow;
        private static LocalizedText[] jungle;
        private static LocalizedText[] beach;
        private static LocalizedText[] evil;

        private static bool firstMeetPending;
        private static bool firstPurchasePending;
        private static string lastLine;

        //通用池洗牌队列，一轮内不重复
        private static int[] shuffleQueue;
        private static int shufflePos;

        /// <summary>右键交互时若图鉴尚无交谈记录则置位，下次开对话吃掉</summary>
        internal static void NoteFirstMeet(bool firstMeet) {
            if (firstMeet) {
                firstMeetPending = true;
            }
        }

        /// <summary>本次会话第一笔成交后置位，下次开对话吃掉</summary>
        internal static void NoteFirstPurchase() => firstPurchasePending = true;

        /// <summary>整套重置（换世界用）；一次性标记也一并清掉</summary>
        internal static void ResetSession() {
            firstMeetPending = false;
            firstPurchasePending = false;
            lastLine = null;
        }

        /// <summary>
        /// 会话收尾只清防重复；一次性标记（首单成交）要活过界面开合，
        /// 留到下次开对话再吃掉
        /// </summary>
        internal static void ResetLastLine() => lastLine = null;

        /// <summary>由 <see cref="TBUGTalkUI.SetStaticDefaults"/> 调用，注册全部台词键</summary>
        internal static void Register(TBUGTalkUI ui) {
            generic = [
                ui.GetLocalization("Greet0",  () => "You're here? Browse away. Don't touch the bundle of cables behind the rig - that's my spine."),
                ui.GetLocalization("Greet1",  () => "Your walk cycle has a tiny loop. Repeats every seven steps. Don't worry, most people have one."),
                ui.GetLocalization("Greet2",  () => "This world's physics is decently written. The water part was clearly rushed."),
                ui.GetLocalization("Greet3",  () => "Everything's on the shelf. I set the prices. Save the haggling breath."),
                ui.GetLocalization("Greet4",  () => "Daylight glares all over my screens. Come at night - I'm nicer at night."),
                ui.GetLocalization("Greet5",  () => "Don't ask where I'm from. What's the hometown of an error message?"),
                ui.GetLocalization("Greet6",  () => "Coins are funny. Heavy, waste pocket space - but you all trust them, so I do too."),
                ui.GetLocalization("Greet7",  () => "I don't do repairs. I sell. If it breaks, that's a user-side issue."),
                ui.GetLocalization("Greet8",  () => "I fell out the night Skeletron died. Not thanking him, but his grip did slip first."),
                ui.GetLocalization("Greet9",  () => "You've got empty equipment slots. Empty slots are wasted slots."),
                ui.GetLocalization("Greet10", () => "Sleep is a patch, and a lazy one. I mostly skip it."),
                ui.GetLocalization("Greet11", () => "Make it quick. I've got three processes hanging and waiting for me."),
            ];
            firstMeet = [
                ui.GetLocalization("TalkFirstMeet0", () => "New face. I'm TBUG - the one who fell out of that seam. Drop the look. My landing wasn't THAT bad."),
                ui.GetLocalization("TalkFirstMeet1", () => "Oh, it talks. Better than the slimes. Shelf's there if you're buying; talk fast if you're not."),
            ];
            firstPurchase = [
                ui.GetLocalization("TalkFirstPurchase0", () => "Deal. No receipt - my memory is more reliable than paper."),
                ui.GetLocalization("TalkFirstPurchase1", () => "Hold it tight. If it misbehaves, review your own usage habits first."),
            ];
            moodHomeless = [
                ui.GetLocalization("TalkMoodHomeless0", () => "I'm sleeping under open sky and my hardware dews up every morning. Get me a room, prices drop a notch."),
                ui.GetLocalization("TalkMoodHomeless1", () => "A shop without a roof is a street stall. I don't enjoy being a street vendor."),
            ];
            moodHappy = [
                ui.GetLocalization("TalkMoodHappy0", () => "Good mood today. My hand slipped on the quotes - downward. Keep it quiet."),
                ui.GetLocalization("TalkMoodHappy1", () => "This place grows on me. Your architecture beats your physics engine."),
            ];
            moodGrumpy = [
                ui.GetLocalization("TalkMoodGrumpy0", () => "Don't whine about prices. Move me somewhere decent and they'll fall on their own."),
                ui.GetLocalization("TalkMoodGrumpy1", () => "Bad mood, spiky quotes. That's market dynamics, not my problem."),
            ];
            hackUnlocked = [
                ui.GetLocalization("TalkHackUnlocked0", () => "That rig in your eyes is decent work. Who installed it? Above this world's average."),
                ui.GetLocalization("TalkHackUnlocked1", () => "Customers who can breach are good customers. At least they never ask which end the chip goes in."),
            ];
            hackLocked = [
                ui.GetLocalization("TalkHackLocked0", () => "Half my stock is useless to you right now. Get yourself breach-capable first."),
            ];
            lowHealth = [
                ui.GetLocalization("TalkLowHealth0", () => "You're dripping. Nurse first - my shelf doesn't stock blood bags."),
                ui.GetLocalization("TalkLowHealth1", () => "Health bars only matter near the bottom. Yours is nearly there."),
            ];
            rich = [
                ui.GetLocalization("TalkRich0", () => "I smell platinum. Which shelf are we emptying today?"),
            ];
            poor = [
                ui.GetLocalization("TalkPoor0", () => "Pockets cleaner than my recycle bin. Go earn something - I don't do credit."),
                ui.GetLocalization("TalkPoor1", () => "No money? Then it's an exhibition. Looking is free. Touching isn't."),
            ];
            preHardmode = [
                ui.GetLocalization("TalkPreHardmode0", () => "The world is still early-game. Monster stats are written politely. Stock up while it lasts."),
                ui.GetLocalization("TalkPreHardmode1", () => "That wall of flesh hasn't fallen yet. When it does, you'll be back to clear my shelves."),
            ];
            hardmode = [
                ui.GetLocalization("TalkHardmode0", () => "The world took a major patch. Stronger monsters, better drops. Good for business."),
                ui.GetLocalization("TalkHardmode1", () => "Light and dark are tearing up the map out there. Don't stand next to fresh seams - I fell out of one."),
            ];
            postMoonlord = [
                ui.GetLocalization("TalkPostMoonlord0", () => "You dismantled the Moon Lord. I'd like a copy of your combat log. For study."),
                ui.GetLocalization("TalkPostMoonlord1", () => "You fight gods and still shop at my little stall. Should I raise prices or be touched?"),
            ];
            postSupCal = [
                ui.GetLocalization("TalkPostSupCal0", () => "Even the Calamity herself is down. At this point YOU are this world's biggest exploit."),
            ];
            bloodMoon = [
                ui.GetLocalization("TalkBloodMoon0", () => "Blood moon. Spawn rates doubled tonight - the things at the door are in more of a hurry than you."),
                ui.GetLocalization("TalkBloodMoon1", () => "All red out there tonight. I shrank the door's hitbox a little. Safety first."),
            ];
            eclipse = [
                ui.GetLocalization("TalkEclipse0", () => "An eclipse. Day got louder than night. Hard not to suspect someone flipped a config flag."),
            ];
            rain = [
                ui.GetLocalization("TalkRain0", () => "Rain. My gear is bagged. Yours is your call."),
                ui.GetLocalization("TalkRain1", () => "The raindrops fall in perfectly straight lines. Not even a wind term. Lazy."),
            ];
            deepNight = [
                ui.GetLocalization("TalkDeepNight0", () => "Still up at this hour? Fine. Night owls get five percent off - kidding, full price."),
                ui.GetLocalization("TalkDeepNight1", () => "Deep night. Fewer processes running. The world genuinely runs smoother."),
            ];
            underground = [
                ui.GetLocalization("TalkUnderground0", () => "Underground is good. Stable temperature, quiet, and the rock blocks all the signal noise."),
                ui.GetLocalization("TalkUnderground1", () => "Smell that? Server-room air. I suspect this world's host machine is buried somewhere down here."),
            ];
            snow = [
                ui.GetLocalization("TalkSnow0", () => "The tundra is honestly cold. Hardware lasts two extra years out here."),
            ];
            jungle = [
                ui.GetLocalization("TalkJungle0", () => "Jungle humidity rides the alarm line all day. My stock wouldn't survive a week here."),
            ];
            beach = [
                ui.GetLocalization("TalkBeach0", () => "The coast? My gear took seawater once. That repair bill would've bought three new boards."),
            ];
            evil = [
                ui.GetLocalization("TalkEvil0", () => "The corrosion here genuinely chews circuits. Your gear and your skin - it isn't picky."),
            ];
        }

        /// <summary>取一句台词；调用方负责打字机重置</summary>
        internal static string Pick() {
            if (generic == null || generic.Length == 0) {
                return string.Empty;
            }

            //一次性语境绝对优先：初见 > 首单成交
            if (firstMeetPending) {
                firstMeetPending = false;
                string line = PickFrom(firstMeet);
                if (line != null) {
                    return Commit(line);
                }
            }
            if (firstPurchasePending) {
                firstPurchasePending = false;
                string line = PickFrom(firstPurchase);
                if (line != null) {
                    return Commit(line);
                }
            }

            List<LocalizedText> pool = [];
            CollectContext(Main.LocalPlayer, pool);
            if (pool.Count > 0 && Main.rand.NextFloat() < ContextChance) {
                //至多重抽三次避开上一句
                for (int attempt = 0; attempt < 3; attempt++) {
                    string line = pool[Main.rand.Next(pool.Count)].Value;
                    if (line != lastLine || pool.Count == 1) {
                        return Commit(line);
                    }
                }
            }
            return Commit(PickGeneric());
        }

        private static void CollectContext(Player player, List<LocalizedText> pool) {
            if (player?.active != true) {
                return;
            }

            //心情：无家 / 极好 / 差
            NPC tbug = GetBoundTBUG();
            if (tbug != null) {
                if (tbug.homeless) {
                    Add(pool, moodHomeless);
                }
                double factor = TBUGMood.PriceAdjustment;
                if (factor <= HappyFactor) {
                    Add(pool, moodHappy);
                }
                else if (factor >= GrumpyFactor) {
                    Add(pool, moodGrumpy);
                }
            }

            //玩家状态：骇入能力 / 血量 / 财力
            if (HackTimeAccess.CanUse(player)) {
                Add(pool, hackUnlocked);
            }
            else {
                Add(pool, hackLocked);
            }
            if (player.statLifeMax2 > 0
                && player.statLife < player.statLifeMax2 * 0.3f) {
                Add(pool, lowHealth);
            }
            long coins = TBUGRenderer.CountCoins(player);
            if (coins >= Item.buyPrice(platinum: 1)) {
                Add(pool, rich);
            }
            else if (coins < Item.buyPrice(gold: 1)) {
                Add(pool, poor);
            }

            //进度：只取最高阶段
            if (CWRRef.Has && CWRRef.GetDownedCalamitas()) {
                Add(pool, postSupCal);
            }
            else if (NPC.downedMoonlord) {
                Add(pool, postMoonlord);
            }
            else if (Main.hardMode) {
                Add(pool, hardmode);
            }
            else {
                Add(pool, preHardmode);
            }

            //时段与天气
            if (Main.bloodMoon) {
                Add(pool, bloodMoon);
            }
            if (Main.eclipse) {
                Add(pool, eclipse);
            }
            if (Main.raining) {
                Add(pool, rain);
            }
            //夜半 16200 之后算深夜
            if (!Main.dayTime && Main.time > 16200.0) {
                Add(pool, deepNight);
            }

            //群系
            if (player.ZoneDirtLayerHeight || player.ZoneRockLayerHeight) {
                Add(pool, underground);
            }
            if (player.ZoneSnow) {
                Add(pool, snow);
            }
            if (player.ZoneJungle) {
                Add(pool, jungle);
            }
            if (player.ZoneBeach) {
                Add(pool, beach);
            }
            if (player.ZoneCorrupt || player.ZoneCrimson) {
                Add(pool, evil);
            }
        }

        private static void Add(List<LocalizedText> pool, LocalizedText[] lines) {
            if (lines != null) {
                pool.AddRange(lines);
            }
        }

        private static NPC GetBoundTBUG() {
            int who = TBUGSession.BoundWhoAmI;
            if (who < 0 || who >= Main.maxNPCs) {
                return null;
            }
            NPC npc = Main.npc[who];
            return npc?.active == true
                && npc.type == ModContent.NPCType<TBUG>() ? npc : null;
        }

        private static string PickFrom(LocalizedText[] lines)
            => lines == null || lines.Length == 0
                ? null
                : lines[Main.rand.Next(lines.Length)].Value;

        private static string PickGeneric() {
            if (shuffleQueue == null || shufflePos >= shuffleQueue.Length) {
                shuffleQueue = new int[generic.Length];
                for (int i = 0; i < shuffleQueue.Length; i++) {
                    shuffleQueue[i] = i;
                }
                for (int i = shuffleQueue.Length - 1; i > 0; i--) {
                    int j = Main.rand.Next(i + 1);
                    (shuffleQueue[i], shuffleQueue[j]) = (shuffleQueue[j], shuffleQueue[i]);
                }
                shufflePos = 0;
            }
            return generic[shuffleQueue[shufflePos++]].Value;
        }

        private static string Commit(string line) {
            lastLine = line;
            return line;
        }
    }
}
