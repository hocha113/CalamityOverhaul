using CalamityOverhaul.Content.Cyberwares;
using CalamityOverhaul.Content.Cyberwares.Implementation.Sandevistans;
using CalamityOverhaul.Content.NPCs.Victors.UIs;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.Victors
{
    /// <summary>
    /// 分桶台词池：一次性语境（初见/术后）绝对优先，其余按当前语境
    /// （心情/玩家状态/进度/时段天气/群系）聚合成候选池，以概率压过通用 Greet 轮转
    /// </summary>
    internal static class VictorDialogue
    {
        /// <summary>语境候选池命中时替代通用池的概率</summary>
        private const float ContextChance = 0.65f;
        /// <summary>心情极好阈值（Love 一档即 0.88）</summary>
        private const double HappyFactor = 0.9;
        /// <summary>心情差阈值</summary>
        private const double GrumpyFactor = 1.15;

        private static LocalizedText[] generic;
        private static LocalizedText[] firstMeet;
        private static LocalizedText[] postSurgery;
        private static LocalizedText[] moodHomeless;
        private static LocalizedText[] moodHappy;
        private static LocalizedText[] moodGrumpy;
        private static LocalizedText[] noChrome;
        private static LocalizedText[] fullChrome;
        private static LocalizedText[] lowHealth;
        private static LocalizedText[] rich;
        private static LocalizedText[] poor;
        private static LocalizedText[] sandevistan;
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
        private static bool postSurgeryPending;
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

        /// <summary>手术成功后置位，下次开对话吃掉</summary>
        internal static void NoteSurgeryDone() => postSurgeryPending = true;

        internal static void ResetSession() {
            firstMeetPending = false;
            postSurgeryPending = false;
            lastLine = null;
        }

        /// <summary>由 <see cref="VictorTalkUI.SetStaticDefaults"/> 调用，注册全部台词键</summary>
        internal static void Register(VictorTalkUI ui) {
            generic = [
                ui.GetLocalization("Greet0",  () => "Another customer? Come in, before the draft scatters your spare parts."),
                ui.GetLocalization("Greet1",  () => "Want a stronger body? Steel never betrays you - only your wallet does."),
                ui.GetLocalization("Greet2",  () => "Brain, eyes, limbs - if the price is right, there is nothing I cannot replace."),
                ui.GetLocalization("Greet3",  () => "Sit on the table. Let me see what flesh of yours is still worth keeping."),
                ui.GetLocalization("Greet4",  () => "You still have both original eyes. Interesting. Most people fix that first."),
                ui.GetLocalization("Greet5",  () => "Don't touch anything on that tray. Half of it is sterile. The other half is worse."),
                ui.GetLocalization("Greet6",  () => "I don't ask where you got the damage. I just make sure it does not happen the same way twice."),
                ui.GetLocalization("Greet7",  () => "Last customer came in missing an arm. Left with two better ones. That is progress."),
                ui.GetLocalization("Greet8",  () => "Flesh rots. The right chrome does not. Keep that in mind before you walk out of here unchanged."),
                ui.GetLocalization("Greet9",  () => "Time is money. Mine, specifically. Tell me what you need and skip the small talk."),
                ui.GetLocalization("Greet10", () => "New parts need breaking in. Try not to take heavy fire for a few days."),
                ui.GetLocalization("Greet11", () => "The body is just a tool. Yours looks like it has skipped maintenance for a while."),
            ];
            firstMeet = [
                ui.GetLocalization("TalkFirstMeet0", () => "A new face. Let me guess - you're not here for a doctor, you're here for a miracle. Sit down, I sell both."),
                ui.GetLocalization("TalkFirstMeet1", () => "Welcome, customer. Three house rules: no credit, no questions about your past, and no critiquing my work."),
            ];
            postSurgery = [
                ui.GetLocalization("TalkPostSurgery0", () => "The sutures are still warm. Don't block any blades with the new parts for twenty-four hours."),
                ui.GetLocalization("TalkPostSurgery1", () => "The surgery went perfectly. Of course, mine always do."),
                ui.GetLocalization("TalkPostSurgery2", () => "How does it feel? Any rattling, current leaks or phantom pain, come straight back - repairs cost extra."),
            ];
            moodHomeless = [
                ui.GetLocalization("TalkMoodHomeless0", () => "Until you people clear out a clinic room for me, the prices include an open-air operating fee."),
                ui.GetLocalization("TalkMoodHomeless1", () => "I can't do precision work with the table strapped to my back. Get me a house and we can talk prices."),
            ];
            moodHappy = [
                ui.GetLocalization("TalkMoodHappy0", () => "I'm in a good mood today, so I rounded your quote down. Keep it to yourself."),
                ui.GetLocalization("TalkMoodHappy1", () => "I like this place. Enough to give you a discount, even."),
            ];
            moodGrumpy = [
                ui.GetLocalization("TalkMoodGrumpy0", () => "Don't complain about the prices. Move me somewhere agreeable and they'll drop on their own."),
                ui.GetLocalization("TalkMoodGrumpy1", () => "Bad mood, unsteady scalpel. The risk surcharge is in the bill - and it's your fault."),
            ];
            noChrome = [
                ui.GetLocalization("TalkNoChrome0", () => "A fully original body... rare. Like an old machine that has never had a firmware update."),
                ui.GetLocalization("TalkNoChrome1", () => "Not a single implant? Are you here to browse, or to feel nostalgic?"),
            ];
            fullChrome = [
                ui.GetLocalization("TalkFullChrome0", () => "There's more chrome than flesh on you. Even your footsteps sound like my cash register."),
                ui.GetLocalization("TalkFullChrome1", () => "Your capacity is almost full, customer. Install any more and you'll need a power upgrade first."),
            ];
            lowHealth = [
                ui.GetLocalization("TalkLowHealth0", () => "You're bleeding. Sit down first - customers dying in my doorway is bad for business."),
                ui.GetLocalization("TalkLowHealth1", () => "That blood pressure is setting off alarms. Wounds first, invoice later."),
            ];
            rich = [
                ui.GetLocalization("TalkRich0", () => "I smell platinum. What are we replacing today? A full set works too."),
            ];
            poor = [
                ui.GetLocalization("TalkPoor0", () => "Pockets cleaner than my sterile tray? Go earn some coin first - cyberware doesn't come on credit."),
                ui.GetLocalization("TalkPoor1", () => "No money? No matter. Sell some loot and come back - we're open all night."),
            ];
            sandevistan = [
                ui.GetLocalization("TalkSandevistan0", () => "Is the Sandevistan running smooth? Don't get greedy with it - fried nerves bill as a full rebuild."),
            ];
            preHardmode = [
                ui.GetLocalization("TalkPreHardmode0", () => "This world's technology is still wood and stone. Good thing I brought my own tools."),
                ui.GetLocalization("TalkPreHardmode1", () => "Upgrade the body before the calamity upgrades itself. That advice was free."),
            ];
            hardmode = [
                ui.GetLocalization("TalkHardmode0", () => "Business tripled since the world cracked open. Light and blight chew through flesh all the same."),
                ui.GetLocalization("TalkHardmode1", () => "The new monsters out there tear people apart fast. Can your reflexes keep up? If not, I have stock."),
            ];
            postMoonlord = [
                ui.GetLocalization("TalkPostMoonlord0", () => "Even the Moon Lord went down. Seems some of my customers really are monsters."),
                ui.GetLocalization("TalkPostMoonlord1", () => "A god's remains make excellent material. If you picked any up, save me a share."),
            ];
            postSupCal = [
                ui.GetLocalization("TalkPostSupCal0", () => "You beat the Calamity herself and still keep this much flesh. I'm starting to think my chrome is just jewelry to you."),
            ];
            bloodMoon = [
                ui.GetLocalization("TalkBloodMoon0", () => "A blood moon? Just an overtime shift for me. There'll be a queue of severed limbs at the door soon."),
                ui.GetLocalization("TalkBloodMoon1", () => "Stay away from the doorway tonight. Under a blood moon even the rabbits want a bite of you."),
            ];
            eclipse = [
                ui.GetLocalization("TalkEclipse0", () => "Only hard cases go out during an eclipse. Be well-equipped or be fast."),
            ];
            rain = [
                ui.GetLocalization("TalkRain0", () => "Oil your exposed joints in the rain. I've fixed too many rusted elbows."),
                ui.GetLocalization("TalkRain1", () => "Rain. Humidity is bad for circuits and good for business."),
            ];
            deepNight = [
                ui.GetLocalization("TalkDeepNight0", () => "Still awake at this hour? Fair enough - cybereyes don't need to close."),
                ui.GetLocalization("TalkDeepNight1", () => "Deep night is my favorite business hour. Quiet, and the customers are always in a hurry."),
            ];
            underground = [
                ui.GetLocalization("TalkUnderground0", () => "The underground is good. Stable temperature, stable humidity, no signal, no interruptions mid-surgery."),
                ui.GetLocalization("TalkUnderground1", () => "My first clinic was underground too. The customers feared daylight just like these ones."),
            ];
            snow = [
                ui.GetLocalization("TalkSnow0", () => "Cold is good. Parts don't overheat, and the bodies - I mean the stock - keep longer."),
            ];
            jungle = [
                ui.GetLocalization("TalkJungle0", () => "Jungle damp gets into sealed casings within three days. Book a maintenance check when you leave."),
            ];
            beach = [
                ui.GetLocalization("TalkBeach0", () => "The coast? Salt spray is the number one killer of cyberware. I'd advise against settling here."),
            ];
            evil = [
                ui.GetLocalization("TalkEvil0", () => "This land is corroding your parts and you alike. I can only fix one of the two."),
            ];
        }

        /// <summary>取一句台词；调用方负责打字机重置</summary>
        internal static string Pick() {
            if (generic == null || generic.Length == 0) {
                return string.Empty;
            }

            //一次性语境绝对优先：初见 > 术后
            if (firstMeetPending) {
                firstMeetPending = false;
                string line = PickFrom(firstMeet);
                if (line != null) {
                    return Commit(line);
                }
            }
            if (postSurgeryPending) {
                postSurgeryPending = false;
                string line = PickFrom(postSurgery);
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
            NPC victor = GetBoundVictor();
            if (victor != null) {
                if (victor.homeless) {
                    Add(pool, moodHomeless);
                }
                double factor = VictorMood.PriceAdjustment;
                if (factor <= HappyFactor) {
                    Add(pool, moodHappy);
                }
                else if (factor >= GrumpyFactor) {
                    Add(pool, moodGrumpy);
                }
            }

            //玩家状态
            CyberwarePlayer cyber = player.GetModPlayer<CyberwarePlayer>();
            if (cyber.UsedCapacity <= 0) {
                Add(pool, noChrome);
            }
            else if (cyber.RemainingCapacity <= 2) {
                Add(pool, fullChrome);
            }
            if (cyber.HasCyberware<SandevistansItem>()) {
                Add(pool, sandevistan);
            }
            if (player.statLifeMax2 > 0
                && player.statLife < player.statLifeMax2 * 0.3f) {
                Add(pool, lowHealth);
            }
            long coins = VictorUIStyle.CountCoins(player);
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

        private static NPC GetBoundVictor() {
            int who = VictorSession.BoundWhoAmI;
            if (who < 0 || who >= Main.maxNPCs) {
                return null;
            }
            NPC npc = Main.npc[who];
            return npc?.active == true
                && npc.type == ModContent.NPCType<Victor>() ? npc : null;
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
