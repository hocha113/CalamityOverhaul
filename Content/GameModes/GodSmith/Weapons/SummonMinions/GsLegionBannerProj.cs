using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions
{
    /// <summary>
    /// 军团战旗：指挥系统的唯一同步面。全部指令状态装在 ai[0..2] 里随弹幕生成包与
    /// netUpdate 免费广播，禁自定义包：<br/>
    /// ai[0] = 指令（1 突击 / 2 集结；护卫 = 无旗）<br/>
    /// ai[1..2] = 载荷（突击：NPC 索引 + 类型校验；集结：世界坐标 X + Y）<br/>
    /// timeLeft 由仆从钩子在各端一致续命（<see cref="MinionDoctrine.MinionUpkeep"/>）：
    /// 无在编仆从、owner 掉线、模式关闭都会令续命停止，全端同步自然过期
    /// </summary>
    internal class GsLegionBannerProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithSummonMinionsA";

        /// <summary>无人续命时的余命帧数（续命阈值同值）</summary>
        internal const int LingerFrames = 90;

        //指令提示文案（护卫/突击/集结，右键切换时的个人读数）
        internal static LocalizedText GuardTip { get; private set; }
        internal static LocalizedText AssaultTip { get; private set; }
        internal static LocalizedText RallyTip { get; private set; }
        /// <summary>tooltip 右键指挥说明行（全族共享，由 GsMinionScheme 注入）</summary>
        internal static LocalizedText CommandHint { get; private set; }

        private int Command => (int)Projectile.ai[0];

        public override void SetStaticDefaults() {
            GuardTip = this.GetLocalization("GuardTip", () => "Guard");
            AssaultTip = this.GetLocalization("AssaultTip", () => "Assault");
            RallyTip = this.GetLocalization("RallyTip", () => "Rally");
            CommandHint = this.GetLocalization("CommandHint",
                () => "Right click: assault a foe, rally at a spot, or point near yourself to recall");
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LingerFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            //晚加入者必须能看到指挥状态：走原版重要弹幕快照
            Projectile.netImportant = true;
        }

        public override void AI() {
            //立旗音放 AI 首帧：OnSpawn 只在生成端跑，远端听不到（localAI 各端本地起于 0）
            if (Projectile.localAI[2] == 0f) {
                Projectile.localAI[2] = 1f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.55f, Pitch = 0.2f },
                        Projectile.Center);
                }
            }
            //各端本地登记，供 MinionDoctrine 查询
            MinionDoctrine.NoticeBanner(Projectile);
            Projectile.velocity = Vector2.Zero;

            if (Command == MinionDoctrine.CommandAssault) {
                NPC target = MinionDoctrine.ResolveAssaultTarget(Projectile);
                if (target != null) {
                    //旗随焦点：各端从同步的 NPC 位置推导，确定性跟随无需发包
                    Projectile.Center = target.Top - new Vector2(0f, 30f);
                    //owner 端把原版仆从集火目标钉在焦点上（变化才发原版同步包）
                    if (Projectile.IsOwnedByLocalPlayer()) {
                        Player player = Main.player[Projectile.owner];
                        if (player.MinionAttackTargetNPC != target.whoAmI) {
                            player.MinionAttackTargetNPC = target.whoAmI;
                            if (Main.netMode == NetmodeID.MultiplayerClient) {
                                NetMessage.SendData(MessageID.MinionAttackTargetUpdate,
                                    number: Projectile.owner);
                            }
                        }
                    }
                }
                else if (Projectile.IsOwnedByLocalPlayer()) {
                    //焦点失效：owner 撤旗回护卫（Kill 自动广播；远端读取侧已即时回退）
                    Projectile.Kill();
                    return;
                }
            }
        }

        public override void OnKill(int timeLeft) {
            //撤旗时归还原版集火目标（仅当仍指着我们的焦点，不打扰玩家自己的鞭标记）
            if (Projectile.IsOwnedByLocalPlayer() && Command == MinionDoctrine.CommandAssault) {
                Player player = Main.player[Projectile.owner];
                if (player.MinionAttackTargetNPC == (int)Projectile.ai[1]) {
                    player.MinionAttackTargetNPC = -1;
                    if (Main.netMode == NetmodeID.MultiplayerClient) {
                        NetMessage.SendData(MessageID.MinionAttackTargetUpdate,
                            number: Projectile.owner);
                    }
                }
            }
        }

        /// <summary>指令切换的个人读数（owner 本地 CombatText + 层音，个人反馈合法）</summary>
        internal static void PopCommandText(Player player, int command) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            (LocalizedText text, Color color) = command switch {
                MinionDoctrine.CommandAssault => (AssaultTip, MinionDoctrine.AssaultRed),
                MinionDoctrine.CommandRally => (RallyTip, MinionDoctrine.RallyCyan),
                _ => (GuardTip, MinionDoctrine.GuardGold),
            };
            CombatText.NewText(player.getRect(), color, text.Value);
            if (command == MinionDoctrine.CommandGuard) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.6f }, player.Center);
            }
        }

        /// <summary>纯编排弹幕，不画本体</summary>
        public override bool PreDraw(ref Color lightColor) => false;
    }
}
