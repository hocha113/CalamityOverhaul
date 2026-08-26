using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
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

        /// <summary>确定性微闪相位</summary>
        private float Seed => Projectile.identity * 0.6173f % MathHelper.TwoPi;

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

            //旗焰微光与粒子（identity 定相，各端本地）
            if (!VaultUtils.isServer) {
                Color hue = Command == MinionDoctrine.CommandAssault
                    ? MinionDoctrine.AssaultRed : MinionDoctrine.RallyCyan;
                Lighting.AddLight(Projectile.Center, hue.ToVector3() * 0.28f);
                if (Main.rand.NextBool(9)) {
                    PRTLoader.NewParticle<PRT_Light>(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-5f, 5f), -20f),
                        new Vector2(0f, -Main.rand.NextFloat(0.4f, 0.9f)),
                        hue, Main.rand.NextFloat(0.08f, 0.13f))?.Configure(14, 0.65f);
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
            if (VaultUtils.isServer) {
                return;
            }
            //撤旗散光
            Color hue = Command == MinionDoctrine.CommandAssault
                ? MinionDoctrine.AssaultRed : MinionDoctrine.RallyCyan;
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3f),
                    hue, Main.rand.NextFloat(0.25f, 0.4f))?.Configure(false, Main.rand.Next(10, 18));
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

        //==================== 旗桩绘制（全程序化，禁新增贴图；绘制路径禁 Main.rand） ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D pole = CWRAsset.MaskLaserLine?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D flare = CWRAsset.StarFlare01?.Value;
            if (pole == null || glow == null || flare == null) {
                return false;
            }
            Color hue = Command == MinionDoctrine.CommandAssault
                ? MinionDoctrine.AssaultRed : MinionDoctrine.RallyCyan;
            float flick = 0.82f + 0.18f * (float)Math.Sin(
                Main.GlobalTimeWrappedHourly * 7.3f + Seed);
            Vector2 basePos = Projectile.Center + new Vector2(0f, 10f) - Main.screenPosition;

            //底光（黑底贴图走 A=0 加色）
            Color under = hue with { A = 0 };
            Main.EntitySpriteDraw(glow, basePos, null, under * (0.35f * flick), 0f,
                glow.Size() / 2f, new Vector2(0.9f, 0.45f), SpriteEffects.None, 0);
            //旗桩光柱：竖立短柱，底锚地面
            Vector2 poleScale = new(46f / pole.Width, 5f / pole.Height);
            Main.EntitySpriteDraw(pole, basePos - new Vector2(0f, 23f), null, under * (0.85f * flick),
                MathHelper.PiOver2, pole.Size() / 2f, poleScale, SpriteEffects.None, 0);
            //顶部旗焰星芒
            float breathe = 0.9f + 0.1f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3.4f + Seed * 1.7f);
            Main.EntitySpriteDraw(flare, basePos - new Vector2(0f, 46f), null, under * (0.9f * flick),
                Seed, flare.Size() / 2f, 0.34f * breathe, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, basePos - new Vector2(0f, 46f), null,
                (Color.White with { A = 0 }) * (0.4f * flick), 0f,
                glow.Size() / 2f, 0.22f * breathe, SpriteEffects.None, 0);
            return false;
        }
    }
}
