using CalamityOverhaul.Content.Items.Magic.Elysiums.Disciples;
using CalamityOverhaul.Content.Items.Magic.Elysiums.Revelations;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    /// <summary>
    /// 天国极乐的门徒席位权威：转化登记、殉道追踪、增益结算、彼得圣盾接线
    /// 席位状态(已转化/已殉道)随角色存档；门徒弹幕由席位登记在进世界后自动重聚
    /// </summary>
    internal class ElysiumPlayer : ModPlayer
    {
        public const int SeatCount = DiscipleCatalog.SeatCount;

        /// <summary>席位已转化(存档)：该位使徒已从城镇居民中唤出</summary>
        public bool[] SeatConverted = new bool[SeatCount];
        /// <summary>席位已殉道(存档)：殉道者化为殉道之力，不可再唤</summary>
        public bool[] Martyred = new bool[SeatCount];

        //运行时：席位→门徒弹幕索引(由门徒AI逐帧登记，读取时校验存活)
        private readonly int[] seatProj = new int[SeatCount];

        //殉道触发限频(避免Boss多段命中把门徒一秒内连环烧光)
        private int martyrCooldown;
        //重聚计时：进世界/门徒缺位后错峰重唤
        private int resummonTimer;

        /// <summary>神圣天雷技能冷却(帧)</summary>
        public int ThunderCooldown;
        /// <summary>彼得圣盾冷却(帧)，0=就绪</summary>
        public int PeterGuardCooldown;
        /// <summary>最近一次圣盾格挡发生的帧号(彼得弹幕读取以演出)</summary>
        public ulong PeterBlockAt;

        //犹大背叛：演出倒计时(-1=未发动)与再触发冷却
        private int judasBetrayCountdown = -1;
        private int judasBetrayCooldown;
        private const int BetrayDuration = 38;
        //穿刺帧(与犹大弹幕的StabFrame同拍)
        private const int BetrayHurtAt = BetrayDuration - 18;

        /// <summary>启示录是否已降临(主人端权威；旁观端以领域弹幕存活为真相)</summary>
        public bool IsRevelationActive;
        /// <summary>四骑士召唤位：0瘟疫 1战争 2饥荒 3死亡</summary>
        public bool[] HorsemenSummoned = new bool[4];
        /// <summary>天体陨石冷却</summary>
        public int MeteorCooldown;

        /// <summary>殉道之力(约翰席位不计)，上限11</summary>
        public int MartyrdomEnergy {
            get {
                int count = 0;
                for (int i = 0; i < SeatCount; i++) {
                    if (i != DiscipleCatalog.JohnSeat && Martyred[i]) {
                        count++;
                    }
                }
                return count;
            }
        }

        public override void Initialize() {
            SeatConverted = new bool[SeatCount];
            Martyred = new bool[SeatCount];
            for (int i = 0; i < SeatCount; i++) {
                seatProj[i] = -1;
            }
        }

        #region 席位登记与查询
        /// <summary>门徒弹幕每帧自报席位</summary>
        public void RegisterSeat(int seat, int projIndex) {
            if (seat >= 0 && seat < SeatCount) {
                seatProj[seat] = projIndex;
            }
        }

        /// <summary>席位上是否有在职门徒(弹幕存活且未殉道中)</summary>
        public bool IsSeatAlive(int seat) => TryGetDisciple(seat, out _);

        public bool TryGetDisciple(int seat, out BaseDisciple disciple) {
            disciple = null;
            if (seat < 0 || seat >= SeatCount) {
                return false;
            }
            int idx = seatProj[seat];
            if (idx < 0 || idx >= Main.maxProjectiles) {
                return false;
            }
            Projectile proj = Main.projectile[idx];
            if (!proj.active || proj.owner != Player.whoAmI
                || proj.ModProjectile is not BaseDisciple d || d.Seat != seat || d.IsMartyring) {
                return false;
            }
            disciple = d;
            return true;
        }

        public int AliveDiscipleCount {
            get {
                int count = 0;
                for (int i = 0; i < SeatCount; i++) {
                    if (IsSeatAlive(i)) {
                        count++;
                    }
                }
                return count;
            }
        }

        /// <summary>该席位在存活门徒中的序号(供环绕布位)</summary>
        public int GetSeatOrder(int seat) {
            int order = 0;
            for (int i = 0; i < seat; i++) {
                if (IsSeatAlive(i)) {
                    order++;
                }
            }
            return order;
        }

        /// <summary>下一个可转化的空席(未转化且该门徒类型已实装)</summary>
        public bool TryGetFreeSeat(out int seat) {
            for (int i = 0; i < SeatCount; i++) {
                if (!SeatConverted[i] && !Martyred[i] && SeatToProjType(i) > 0) {
                    seat = i;
                    return true;
                }
            }
            seat = -1;
            return false;
        }

        /// <summary>席位→门徒弹幕类型</summary>
        public static int SeatToProjType(int seat) => seat switch {
            0 => ModContent.ProjectileType<SimonPeter>(),
            1 => ModContent.ProjectileType<Andrew>(),
            2 => ModContent.ProjectileType<James>(),
            3 => ModContent.ProjectileType<John>(),
            4 => ModContent.ProjectileType<Philip>(),
            5 => ModContent.ProjectileType<Bartholomew>(),
            6 => ModContent.ProjectileType<Thomas>(),
            7 => ModContent.ProjectileType<Matthew>(),
            8 => ModContent.ProjectileType<Lesser>(),
            9 => ModContent.ProjectileType<Thaddaeus>(),
            10 => ModContent.ProjectileType<Zealot>(),
            11 => ModContent.ProjectileType<JudasIscariot>(),
            _ => 0,
        };

        public bool HasElysiumInInventory() {
            int elysiumType = ModContent.ItemType<Elysium>();
            if (Player.HeldItem != null && Player.HeldItem.type == elysiumType) {
                return true;
            }
            foreach (Item item in Player.inventory) {
                if (item != null && !item.IsAir && item.type == elysiumType) {
                    return true;
                }
            }
            return false;
        }

        #region 启示录
        /// <summary>启示录就绪：殉道之力满盈且约翰在世</summary>
        public bool RevelationReady => !IsRevelationActive && MartyrdomEnergy >= 11 && IsSeatAlive(DiscipleCatalog.JohnSeat);

        /// <summary>后三印审判进行中(以弹幕存活为真相)</summary>
        public bool IsSealJudgmentActive
            => Player.ownedProjectileCounts[ModContent.ProjectileType<Revelations.RevelationSealJudgment>()] > 0;

        public int HorsemenCount {
            get {
                int count = 0;
                for (int i = 0; i < HorsemenSummoned.Length; i++) {
                    if (HorsemenSummoned[i]) {
                        count++;
                    }
                }
                return count;
            }
        }

        public bool HasDeathHorseman => IsRevelationActive && HorsemenSummoned[3];

        /// <summary>
        /// 揭开启示录(主人端)：约翰殉道升天，天国领域展开。
        /// 白光吞屏与领域开幕由领域弹幕承担
        /// </summary>
        public void ActivateRevelation() {
            if (Player.whoAmI != Main.myPlayer || !RevelationReady) {
                return;
            }

            Vector2 johnPos = Player.Center;
            if (TryGetDisciple(DiscipleCatalog.JohnSeat, out BaseDisciple john)) {
                johnPos = john.Projectile.Center;
            }

            IsRevelationActive = true;
            Array.Clear(HorsemenSummoned, 0, HorsemenSummoned.Length);
            MeteorCooldown = 0;
            MartyrSeat(DiscipleCatalog.JohnSeat);

            //约翰升天：三道白光自他的位置冲天
            if (!Main.dedServ) {
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_SkyBolt>(johnPos, Vector2.Zero, new Color(235, 240, 255), 1f - i * 0.2f)
                        ?.Configure(johnPos - new Vector2((i - 1) * 46f, 700f), johnPos, 30 + i * 4);
                }
            }
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 1.2f, Pitch = 0.5f }, johnPos);

            ShootState shootState = Player.GetShootState();
            Projectile.NewProjectile(shootState.Source, Player.Center, Vector2.Zero,
                ModContent.ProjectileType<Revelations.RevelationDomain>(), 0, 0f, Player.whoAmI);
        }

        /// <summary>
        /// 启示录落幕(主人端)：殉道者随终幕升天(席位空置待再聚)，约翰归来
        /// </summary>
        public void DeactivateRevelation() {
            if (!IsRevelationActive) {
                return;
            }
            IsRevelationActive = false;
            Array.Clear(HorsemenSummoned, 0, HorsemenSummoned.Length);
            MeteorCooldown = 0;

            for (int i = 0; i < SeatCount; i++) {
                if (!Martyred[i]) {
                    continue;
                }
                Martyred[i] = false;
                if (i != DiscipleCatalog.JohnSeat) {
                    //殉道者已随审判升天，圣位空置
                    SeatConverted[i] = false;
                }
            }
        }

        /// <summary>召唤下一位骑士(主人端，右键入口)</summary>
        public void SummonNextHorseman() {
            if (Player.whoAmI != Main.myPlayer || !IsRevelationActive) {
                return;
            }
            int next = HorsemenCount;
            if (next >= HorsemanCatalog.Count || HorsemenSummoned[next]) {
                return;
            }

            var style = HorsemanCatalog.Get(next);
            Vector2 dir = (MathHelper.PiOver2 * next - MathHelper.PiOver4).ToRotationVector2();
            Vector2 summonPos = Player.Center + dir * style.OrbitRadius + new Vector2(0f, -60f);

            ShootState shootState = Player.GetShootState();
            Projectile.NewProjectile(shootState.Source, summonPos, Vector2.Zero,
                ModContent.ProjectileType<Revelations.ApocalypseHorseman>(), 0, 0f, Player.whoAmI, next);
            HorsemenSummoned[next] = true;
        }

        /// <summary>启示录逐帧维护：骑士消亡除名、异常状态兜底(主人端)</summary>
        private void TickRevelation() {
            if (MeteorCooldown > 0) {
                MeteorCooldown--;
            }
            if (Player.whoAmI != Main.myPlayer || !IsRevelationActive) {
                return;
            }

            if (Player.dead || !HasElysiumInInventory()) {
                DeactivateRevelation();
                return;
            }

            //骑士在场校验(死亡/丢失即除名)
            Span<bool> alive = stackalloc bool[HorsemanCatalog.Count];
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.owner == Player.whoAmI
                    && proj.ModProjectile is Revelations.ApocalypseHorseman horseman) {
                    int idx = horseman.HorsemanIndex;
                    if (idx >= 0 && idx < HorsemanCatalog.Count) {
                        alive[idx] = true;
                    }
                }
            }
            for (int i = 0; i < HorsemanCatalog.Count; i++) {
                if (HorsemenSummoned[i] && !alive[i]) {
                    HorsemenSummoned[i] = false;
                }
            }
        }
        #endregion

        /// <summary>
        /// 圣职调换(主人端)：互换两个席位的身份，在职门徒就地转变。
        /// 弹幕的杀与生自动同步，席位登记为主人本机真相
        /// </summary>
        public bool SwapSeats(int a, int b) {
            if (Player.whoAmI != Main.myPlayer || a == b
                || a < 0 || a >= SeatCount || b < 0 || b >= SeatCount
                || Martyred[a] || Martyred[b]
                || (!SeatConverted[a] && !SeatConverted[b])) {
                return false;
            }

            Vector2 posA = Player.Center + new Vector2(-40f, -40f);
            Vector2 posB = Player.Center + new Vector2(40f, -40f);
            if (TryGetDisciple(a, out BaseDisciple da)) {
                posA = da.Projectile.Center;
                da.Projectile.Kill();
            }
            if (TryGetDisciple(b, out BaseDisciple db)) {
                posB = db.Projectile.Center;
                db.Projectile.Kill();
            }

            (SeatConverted[a], SeatConverted[b]) = (SeatConverted[b], SeatConverted[a]);

            //身位交叉继承：新身份在旧躯体站过的地方成形
            if (SeatConverted[a] && SeatToProjType(a) > 0) {
                Projectile.NewProjectile(Player.GetSource_Misc("ElysiumSwap"),
                    posB, Vector2.Zero, SeatToProjType(a), 0, 0f, Player.whoAmI);
            }
            if (SeatConverted[b] && SeatToProjType(b) > 0) {
                Projectile.NewProjectile(Player.GetSource_Misc("ElysiumSwap"),
                    posA, Vector2.Zero, SeatToProjType(b), 0, 0f, Player.whoAmI);
            }
            return true;
        }

        /// <summary>门徒能力的伤害基准：主人当前天国极乐的实际武器伤害(主人端调用)</summary>
        public static int GetElysiumDamage(Player player) {
            int elysiumType = ModContent.ItemType<Elysium>();
            Item elysium = null;
            if (player.HeldItem != null && player.HeldItem.type == elysiumType) {
                elysium = player.HeldItem;
            }
            else {
                foreach (Item item in player.inventory) {
                    if (item != null && !item.IsAir && item.type == elysiumType) {
                        elysium = item;
                        break;
                    }
                }
            }
            return elysium != null ? player.GetWeaponDamage(elysium) : 320;
        }
        #endregion

        #region 殉道
        public override void PostUpdate() {
            if (martyrCooldown > 0) {
                martyrCooldown--;
            }
            if (ThunderCooldown > 0) {
                ThunderCooldown--;
            }
            if (PeterGuardCooldown > 0) {
                PeterGuardCooldown--;
            }
            if (judasBetrayCooldown > 0) {
                judasBetrayCooldown--;
            }
            TickRevelation();
            TickJudasBetrayal();
            TickResummon();
        }

        /// <summary>
        /// 犹大的背叛(主人端权威)：十二圣位齐聚且主人濒危时，
        /// 犹大起身穿刺，穿刺拍结算近乎必死的一击，席位随之空置
        /// </summary>
        private void TickJudasBetrayal() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            if (judasBetrayCountdown >= 0) {
                judasBetrayCountdown--;
                if (judasBetrayCountdown == BetrayDuration - BetrayHurtAt) {
                    SeatConverted[DiscipleCatalog.JudasSeat] = false;
                    int betrayDamage = Player.statLife + 200;
                    Player.Hurt(Terraria.DataStructures.PlayerDeathReason.ByCustomReason(
                        Terraria.Localization.NetworkText.FromKey(
                            "Mods.CalamityOverhaul.Items.Elysium.JudasDeathReasonText", Player.name)),
                        betrayDamage, 0);
                }
                return;
            }

            if (judasBetrayCooldown > 0 || Player.dead
                || Player.statLife > Player.statLifeMax2 * 0.3f
                || AliveDiscipleCount < SeatCount
                || !TryGetDisciple(DiscipleCatalog.JudasSeat, out BaseDisciple judas)
                || judas is not Disciples.JudasIscariot judasProj) {
                return;
            }

            judasBetrayCountdown = BetrayDuration;
            judasBetrayCooldown = 900;
            judasProj.BeginBetrayal();
        }

        /// <summary>Boss的伤害会烧掉一位门徒(约翰除外)，化为殉道之力</summary>
        public override void OnHurt(Player.HurtInfo info) {
            if (Player.whoAmI != Main.myPlayer || martyrCooldown > 0) {
                return;
            }
            if (!info.DamageSource.TryGetCausingEntity(out var entity)
                || entity is not NPC npc
                || (!npc.boss && !NPCID.Sets.ShouldBeCountedAsBoss[npc.type])) {
                return;
            }

            //收集可殉道席位(在职且非约翰)
            Span<int> eligible = stackalloc int[SeatCount];
            int eligibleCount = 0;
            for (int i = 0; i < SeatCount; i++) {
                if (i != DiscipleCatalog.JohnSeat && IsSeatAlive(i)) {
                    eligible[eligibleCount++] = i;
                }
            }
            if (eligibleCount == 0) {
                return;
            }

            martyrCooldown = 90;
            MartyrSeat(eligible[Main.rand.Next(eligibleCount)]);
        }

        /// <summary>殉道结算：席位点殉，门徒本体进入化光演出(状态经弹幕ai同步各端)</summary>
        public void MartyrSeat(int seat) {
            if (seat < 0 || seat >= SeatCount || Martyred[seat]) {
                return;
            }
            Martyred[seat] = true;
            if (TryGetDisciple(seat, out BaseDisciple disciple)) {
                disciple.BeginMartyrdom();
            }
        }
        #endregion

        #region 重聚
        /// <summary>已转化未殉道的席位若无在职门徒(进世界/意外丢失)，错峰重唤</summary>
        private void TickResummon() {
            if (Player.whoAmI != Main.myPlayer || Player.dead || !HasElysiumInInventory()) {
                return;
            }
            if (resummonTimer > 0) {
                resummonTimer--;
                return;
            }
            for (int i = 0; i < SeatCount; i++) {
                if (!SeatConverted[i] || Martyred[i] || IsSeatAlive(i) || SeatToProjType(i) <= 0) {
                    continue;
                }
                Projectile.NewProjectile(Player.GetSource_Misc("ElysiumResummon"),
                    Player.Center + new Vector2(Main.rand.NextFloat(-60f, 60f), -40f), Vector2.Zero,
                    SeatToProjType(i), 0, 0f, Player.whoAmI);
                resummonTimer = 45;
                return;
            }
        }
        #endregion

        #region 增益与彼得圣盾
        public override void UpdateEquips() {
            int count = AliveDiscipleCount;
            if (count > 0) {
                //基线圣恩：随门徒数目缓增
                Player.GetDamage(DamageClass.Generic) += 0.012f * count;
                Player.GetCritChance(DamageClass.Generic) += count;
                Player.statDefense += count;
            }

            //彼得在职：磐石之护的常驻份
            if (IsSeatAlive(0)) {
                Player.statDefense += 6;
            }

            //犹大在职：出卖者献上的全面厚礼
            if (IsSeatAlive(DiscipleCatalog.JudasSeat)) {
                Player.GetDamage(DamageClass.Generic) += 0.06f;
                Player.GetCritChance(DamageClass.Generic) += 4;
                Player.statDefense += 4;
                Player.lifeRegen += 2;
            }

            //十一人同席：圣恩涌溢
            if (count >= 11) {
                Player.GetDamage(DamageClass.Generic) += 0.06f;
                Player.lifeRegen += 3;
            }

            //十二圣位齐聚：荣光满溢，但犹大的刀也已备好
            if (count >= SeatCount) {
                Player.GetDamage(DamageClass.Generic) += 0.1f;
                Player.GetCritChance(DamageClass.Generic) += 8;
                Player.statDefense += 10;
                Player.moveSpeed += 0.1f;
            }

            //启示录圣恩与骑士威能
            if (IsRevelationActive) {
                Player.GetDamage(DamageClass.Generic) += 0.08f;
                Player.statDefense += 10;
                Player.lifeRegen += 4;

                bool death = HasDeathHorseman;
                if (HorsemenSummoned[1]) {
                    //战争：伤害与暴击
                    Player.GetDamage(DamageClass.Generic) += death ? 0.3f : 0.18f;
                    Player.GetCritChance(DamageClass.Generic) += death ? 20 : 12;
                }
                if (HorsemenSummoned[2]) {
                    //饥荒：穿甲
                    Player.GetArmorPenetration(DamageClass.Generic) += death ? 80 : 40;
                }
            }
        }

        /// <summary>多马的验证：验证之目生效时，主人的攻击必然暴击</summary>
        public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref NPC.HitModifiers modifiers) {
            if (proj.owner == Player.whoAmI && Player.HasBuff<Disciples.VerificationBuff>()) {
                modifiers.SetCrit();
            }
        }

        public override void ModifyHitNPCWithItem(Item item, NPC target, ref NPC.HitModifiers modifiers) {
            if (Player.HasBuff<Disciples.VerificationBuff>()) {
                modifiers.SetCrit();
            }
        }

        /// <summary>瘟疫骑士在场：主人的攻击给敌人烙上瘟疫印</summary>
        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone) {
            if (proj.owner == Player.whoAmI) {
                TryApplyPlague(target);
            }
        }

        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone) {
            TryApplyPlague(target);
        }

        private void TryApplyPlague(NPC target) {
            if (!IsRevelationActive || !HorsemenSummoned[0]
                || target == null || !target.active || target.friendly) {
                return;
            }
            target.AddBuff(ModContent.BuffType<Disciples.PlagueMarkDebuff>(), HasDeathHorseman ? 420 : 240);
        }

        /// <summary>
        /// 进世界兜底：若上次存档停在启示录中(约翰席位带殉道标)，视作启示录已随下线落幕，
        /// 殉道者升天消耗、约翰归来
        /// </summary>
        public override void OnEnterWorld() {
            if (!Martyred[DiscipleCatalog.JohnSeat]) {
                return;
            }
            for (int i = 0; i < SeatCount; i++) {
                if (!Martyred[i]) {
                    continue;
                }
                Martyred[i] = false;
                if (i != DiscipleCatalog.JohnSeat) {
                    SeatConverted[i] = false;
                }
            }
        }

        /// <summary>彼得圣盾：就绪时替主人挡下一击的两成半伤害</summary>
        public override void ModifyHurt(ref Player.HurtModifiers modifiers) {
            if (PeterGuardCooldown > 0 || !IsSeatAlive(0)) {
                return;
            }
            modifiers.FinalDamage *= 0.72f;
            PeterGuardCooldown = 540;
            PeterBlockAt = Main.GameUpdateCount;
            SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.9f, Pitch = 0.35f }, Player.Center);
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.8f, Pitch = 0.15f }, Player.Center);
        }
        #endregion

        #region 存档
        public override void SaveData(TagCompound tag) {
            byte[] converted = new byte[SeatCount];
            byte[] martyred = new byte[SeatCount];
            for (int i = 0; i < SeatCount; i++) {
                converted[i] = SeatConverted[i] ? (byte)1 : (byte)0;
                martyred[i] = Martyred[i] ? (byte)1 : (byte)0;
            }
            tag["elysiumSeats"] = converted;
            tag["elysiumMartyrs"] = martyred;
        }

        public override void LoadData(TagCompound tag) {
            if (tag.TryGet("elysiumSeats", out byte[] converted)) {
                for (int i = 0; i < SeatCount && i < converted.Length; i++) {
                    SeatConverted[i] = converted[i] != 0;
                }
            }
            if (tag.TryGet("elysiumMartyrs", out byte[] martyred)) {
                for (int i = 0; i < SeatCount && i < martyred.Length; i++) {
                    Martyred[i] = martyred[i] != 0;
                }
            }
        }
        #endregion
    }
}
