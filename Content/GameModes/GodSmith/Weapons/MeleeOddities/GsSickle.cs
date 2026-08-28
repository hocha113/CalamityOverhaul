using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MeleeOddities
{
    /// <summary>
    /// 【农夫麦秋镰】材质：铁灰镰身、麦金刃口的农具镰。签名：①割草掉干草的原版身份完整镜像
    /// （Player.ItemCheck_CutTiles 的 sItem.type==1786 路径）②丰收层：斩草成功/命中敌人各攒一层、
    /// 每层挥速 +4%，满 5 层下一斩化为加宽大横扫 ③刃口收割弧薄光随层数点亮 + 挥砍麦壳碎屑
    /// </summary>
    internal class GsSickle : GsOdditiesComboScheme
    {
        public override int TargetItemID => ItemID.Sickle;

        protected override int HeldProjID => ModContent.ProjectileType<GsSickleHeld>();

        protected override string GsDescFallback =>
            "Reforged: still harvests hay from grass.\n" +
            "Slashes build Harvest, each stack swinging a little faster;\n" +
            "at 5 stacks the next slash becomes a widened reaping sweep";

        //麦秋色板
        internal static readonly Color StrawGold = new(232, 210, 140);    //麦金刃口
        internal static readonly Color IronGray = new(150, 148, 140);     //铁灰镰身
        internal static readonly Color HarvestOrange = new(255, 178, 80); //丰收橙强调
        internal static readonly Color DeepChaff = new(40, 36, 26);       //深糠暗

        /// <summary>满层大横扫的特殊拍号（自然循环只走 0/1/2，本拍只由满层注入）</summary>
        internal const int SweepBeat = 3;

        /// <summary>出手前记账：满层改打大横扫并清层；层数打包进拍号高位随生成包过线</summary>
        protected override void ModifyLocalSwing(Item item, Player player, ref int beat, ref float swingSign) {
            GsSicklePlayer mp = player.GetModPlayer<GsSicklePlayer>();
            mp.NotifySlash();
            int tier = mp.HarvestTier;
            if (tier >= GsSicklePlayer.HarvestMax) {
                beat = SweepBeat;
                mp.ClearHarvest();
            }
            //held 首帧解码：低位拍号还给基类，高位层数只作刃光显示（大横扫因此带满层辉光出手）
            beat += tier * 8;
        }

        /// <summary>丰收攻速：每层 +4%。钩子各端都跑，远端读本端 ModPlayer 恒 0 层得 1f，仅动画观感差异</summary>
        public override float GsUseSpeedMultiplier(Item item, Player player)
            => 1f + 0.04f * player.GetModPlayer<GsSicklePlayer>().HarvestTier;

        //9 伤商店农具，公认弱势（公约 §5 允许至 135%）：×1.30 与快割拍（约 20f/拍对原版 24f、拍伤 0.95）
        //合计常驻约 130%~135%；丰收攻速与大横扫是要主动斩草/命中才有的条件收益，不计常驻底账
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.30f;

        /// <summary>
        /// 压掉原版挥舞的物理尾巴：held 每帧强撑 itemAnimation&gt;0，而镰刀 noMelee=false（SetDefaults 已密封），
        /// Player.ItemCheck 的近战尾巴（挥舞碰撞箱命中 + ItemCheck_CutTiles 的 1786 干草路径）在 owner 端仍会逐帧执行，
        /// 不压会与 held 双发干草、双份直击。noHitbox=true 令 GetMeleeHitbox 置 dontAttack，整段尾巴跳过
        /// </summary>
        public override void GsUseItemHitbox(Item item, Player player, ref Rectangle hitbox, ref bool noHitbox)
            => noHitbox = true;
    }

    /// <summary>
    /// 丰收层每玩家状态：上限 5，4 秒（240 帧）无斩击衰减一层。
    /// 只在 myPlayer 路径写读，跨端不同步——远端观感由 held 的 ai[0] 高位打包承载
    /// </summary>
    internal class GsSicklePlayer : ModPlayer
    {
        internal const int HarvestMax = 5;
        private const int DecayFrames = 240;

        /// <summary>当前丰收层数</summary>
        internal int HarvestTier;
        private int decayTimer;

        /// <summary>斩草成功/命中敌人各 +1 层；攒满响一记提示</summary>
        internal void AddHarvest() {
            decayTimer = 0;
            if (HarvestTier >= HarvestMax) {
                return;
            }
            HarvestTier++;
            if (HarvestTier == HarvestMax && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.55f, Pitch = 0.35f }, Player.Center);
            }
        }

        /// <summary>挥镰刷新衰减计时（不加层）</summary>
        internal void NotifySlash() => decayTimer = 0;

        internal void ClearHarvest() {
            HarvestTier = 0;
            decayTimer = 0;
        }

        public override void PostUpdateMiscEffects() {
            if (Player.whoAmI != Main.myPlayer || HarvestTier <= 0) {
                return;
            }
            if (++decayTimer >= DecayFrames) {
                decayTimer = 0;
                HarvestTier--;
            }
        }

        /// <summary>PostUpdate 系钩子死后不跑、衰减会冻结，死亡直接清层</summary>
        public override void UpdateDead() => ClearHarvest();
    }

    /// <summary>
    /// 麦秋镰手持：四拍。0/1/2 快割三连（Raise 逐拍 -1 加快），3 = 满层大横扫（加宽判定、前压、重顿帧）。
    /// ai[0] = 拍号 + 丰收层×8（首帧解码），ai[1] = 交替符号。<br/>
    /// 割草掉干草：引擎切割的盒切（CutTilesAt）跑在模组钩子之前，会把刃心的草无干草地清掉，
    /// 故 CanCutTiles=false 封掉引擎路径，AI 里自调 CutTiles——先按原版 1786 路径收割干草类，
    /// 再走基类线切清其余可切物
    /// </summary>
    internal class GsSickleHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.Sickle;
        protected override Color EdgeBright => GsSickle.StrawGold;
        protected override Color BodyMain => GsSickle.IronGray;
        protected override Color HotAccent => GsSickle.HarvestOrange;
        protected override Color DeepShadow => GsSickle.DeepChaff;

        protected override int BeatCount => 4;

        /// <summary>大横扫拍加宽贪婪判定线宽</summary>
        protected override float CollisionWidth => ComboStage == GsSickle.SweepBeat ? 52f : 40f;

        /// <summary>首帧从 ai[0] 高位解出的丰收层数（纯显示量，各端同式解码）</summary>
        private int harvestShown;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == GsSickle.SweepBeat) {
                //满层大横扫：大弧前压重顿帧，判定线宽由 CollisionWidth 分流
                return new GsBroadBeat {
                    Raise = 8, Hold = 3, Slash = 6, Recover = 12,
                    RaiseBack = 2.4f, Follow = 1.5f, ReachScale = 1.3f, LeanAmp = 0.1f,
                    DamageMult = 1.6f, Hitstop = 3, LungeSpeed = 2.5f, SwingPitch = -0.25f,
                };
            }
            GsBroadBeat b = GsBroadBeat.Standard;
            b.Raise = 6 - stage;                  //快割三连逐拍加快
            b.DamageMult = 0.95f;
            b.SwingPitch = 0.05f + stage * 0.06f; //轻农具，音高偏亮且逐拍上扬
            return b;
        }

        protected override Color BodyTint(Color lightColor) => Color.Lerp(lightColor, GsSickle.IronGray, 0.15f);

        public override void AI() {
            //首帧解包：方案把丰收层打进拍号高位（beat + tier*8），低位还给基类当拍号；
            //各端都在自己的首帧做同式解码，后续 netUpdate 重发的干净拍号对已解码端幂等
            if (timer == 0) {
                int packed = (int)Projectile.ai[0];
                harvestShown = Math.Clamp(packed / 8, 0, GsSicklePlayer.HarvestMax);
                Projectile.ai[0] = packed % 8;
            }
            base.AI();
            //引擎切割路径已被 CanCutTiles=false 封掉，改由这里自调（内部有伤害窗与 owner 守门）
            CutTiles();
        }

        /// <summary>命中敌人 +1 层（owner 独占量，守 myPlayer）</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.owner == Main.myPlayer) {
                Owner.GetModPlayer<GsSicklePlayer>().AddHarvest();
            }
        }

        //==================== 割草掉干草（原版镜像） ====================

        /// <summary>封掉引擎切割：其盒切跑在模组钩子之前，会把刃心的草无干草地清掉</summary>
        public override bool? CanCutTiles() => false;

        /// <summary>
        /// 先丰收后常规：干草类物块走原版 Player.ItemCheck_CutTiles 的 sItem.type==1786 路径，
        /// 其余可切物交还基类线切。只在 owner 端执行——引擎路径本也只跑 owner 端
        /// （Projectile.cs Damage→CutTiles 守 owner == Main.myPlayer），自调后这里补同款守门
        /// </summary>
        public override void CutTiles() {
            if (!sweepDamageActive || Projectile.owner != Main.myPlayer) {
                return;
            }
            //镜像原版：镰刀非再生工具 allowRegrowth=false、非机关 fromTrap=false（dontHurtNature 由该方法内部接管）
            bool[] ignore = Owner.GetTileCutIgnorance(allowRegrowth: false, fromTrap: false);
            Vector2 hand = Hand;
            const int samples = 2;
            for (int i = 0; i <= samples; i++) {
                float ang = MathHelper.Lerp(lastAngle, mainAngle, i / (float)samples);
                Vector2 tip = hand + ang.ToRotationVector2() * (mainReach * 1.02f);
                Utils.PlotTileLine(hand, tip, CollisionWidth * 0.85f, (x, y) => CutHayTile(x, y, ignore));
            }
            //引擎路径原本会在进钩子前设好 tileCutIgnore，自调后补上再走基类常规线切
            DelegateMethods.tileCutIgnore = ignore;
            base.CutTiles();
        }

        /// <summary>
        /// 单格干草路径，镜像 Player.cs ItemCheck_CutTiles（40820~40875）：
        /// 守门链 tileCut/ignore/CanCutTile(AttackMelee) → 先记类型再 KillTile → 打空才掉干草
        /// （草 3/24/61/110/201/529/637 掉 1~2 捆，高草 73/74/113 掉 2~4 捆，ItemID.Hay=1727）
        /// → 客户端按原版补 SendData(21, noGrabDelay=1f) 与 SendData(17)
        /// </summary>
        private bool CutHayTile(int x, int y, bool[] ignore) {
            if (!WorldGen.InWorld(x, y, 1)) {
                return true;
            }
            Tile tile = Main.tile[x, y];
            if (!tile.HasTile || !Main.tileCut[tile.TileType] || ignore[tile.TileType]
                || !WorldGen.CanCutTile(x, y, Terraria.Enums.TileCuttingContext.AttackMelee)) {
                return true;
            }
            //Plants/CorruptPlants/JunglePlants/HallowedPlants/CrimsonPlants/SeaOats/AshPlants → 1~2 捆
            //Plants2/JunglePlants2/HallowedPlants2（高草）→ 2~4 捆；非干草源交还基类切割
            int band = tile.TileType switch {
                3 or 24 or 61 or 110 or 201 or 529 or 637 => 1,
                73 or 74 or 113 => 2,
                _ => 0,
            };
            if (band == 0) {
                return true;
            }
            WorldGen.KillTile(x, y);
            if (!Main.tile[x, y].HasTile) {
                int stack = band == 1 ? Main.rand.Next(1, 3) : Main.rand.Next(2, 5);
                int number = Item.NewItem(new EntitySource_ItemUse(Owner, Item), x * 16, y * 16, 16, 16, ItemID.Hay, stack);
                if (Main.netMode == NetmodeID.MultiplayerClient) {
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, number, 1f);
                }
                //斩草成功 +1 层（本方法只跑 owner 端）
                Owner.GetModPlayer<GsSicklePlayer>().AddHarvest();
            }
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 0, x, y);
            }
            return true;
        }

        //==================== 演出 ====================

        /// <summary>挥砍期追加麦壳碎屑：短命暗黄小屑带重力，低密度（已在非服务器端调用）</summary>
        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            if (phase != PhaseSlash || !Main.rand.NextBool(2)) {
                return;
            }
            Vector2 at = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.5f, 0.95f));
            Vector2 vel = ((mainAngle + swingDir * MathHelper.PiOver2).ToRotationVector2() * Main.rand.NextFloat(1.2f, 2.6f))
                + new Vector2(0f, -0.4f);
            PRTLoader.NewParticle<PRT_Spark>(at, vel, new Color(186, 158, 88), Main.rand.NextFloat(0.22f, 0.34f))
                ?.Configure(true, Main.rand.Next(8, 14));
        }

        /// <summary>刃口收割弧薄光：沿刀角一线软光，亮度与厚度随丰收层数（确定量，不掷绘制 rand）</summary>
        protected override void DrawExtra(SpriteBatch sb, Color lightColor) {
            if (harvestShown <= 0 || fanFade <= 0.02f) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            float t = harvestShown / (float)GsSicklePlayer.HarvestMax;
            Vector2 dir = mainAngle.ToRotationVector2();
            Vector2 at = Hand + dir * (mainReach * 0.68f) - Main.screenPosition;
            float len = mainReach * (0.5f + 0.22f * t);
            //宽暗一笔打底
            Color under = Color.Lerp(GsSickle.StrawGold, GsSickle.HarvestOrange, 0.45f) * ((0.08f + 0.26f * t) * fanFade);
            under.A = 0;
            sb.Draw(glow, at, null, under, mainAngle, glow.Size() / 2f,
                new Vector2(len / glow.Width, (7f + 3f * harvestShown) / glow.Height), SpriteEffects.None, 0f);
            //刃口亮芯一线
            Color core = GsSickle.StrawGold * ((0.12f + 0.30f * t) * fanFade);
            core.A = 0;
            sb.Draw(glow, at + dir * (mainReach * 0.08f), null, core, mainAngle, glow.Size() / 2f,
                new Vector2(len * 0.7f / glow.Width, 3.5f / glow.Height), SpriteEffects.None, 0f);
        }
    }
}
