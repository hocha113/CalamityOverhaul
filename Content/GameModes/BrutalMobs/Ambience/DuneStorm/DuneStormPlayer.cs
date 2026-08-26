using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.DuneStorm
{
    /// <summary>
    /// 「正午灼热」：晴昼正午站在沙地上不动会累积热浪（热闪粒子渐强 + 灼烤声渐密的双通道预告），
    /// 满值短暂施加原版 Slow 并部分回落；移动快速散热，入水立即清空。
    /// 逐玩家状态放 ModPlayer（禁 static），只在本机端为自己的玩家结算，
    /// 减益经 AddBuff 骑原版同步，零自定义网络包
    /// </summary>
    internal class DuneStormPlayer : ModPlayer
    {
        /// <summary>热浪满值</summary>
        private const float HeatFull = 100f;
        /// <summary>正午窗口（tick）：约 9:45 ~ 14:15，正午 12:00 = 27000</summary>
        private const double NoonStart = 18900;
        private const double NoonEnd = 35100;
        /// <summary>触发后的回落值（重新灼满仍需数秒，不会连发）</summary>
        private const float HeatAfterTrigger = 25f;
        /// <summary>移动散热速率（每帧）</summary>
        private const float CoolRate = 1.5f;
        /// <summary>灼烤声脉冲的起始热度（距满值 ≥130 帧，构成听觉预告）</summary>
        private const float SizzleFrom = 55f;

        /// <summary>当前热浪 0~100（逐玩家，本机结算）</summary>
        private float heat;
        private int sizzleCooldown;

        public override void PostUpdateMiscEffects() {
            //只为本机自己的玩家结算：热浪是本地状态，减益走原版 buff 同步
            if (Main.dedServ || Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (GameModeSystem.EffectiveTier <= 0) {
                heat = 0f;
                return;
            }

            if (sizzleCooldown > 0) {
                sizzleCooldown--;
            }

            //入水立即清热（带一口蒸汽的反馈）
            if (Player.wet) {
                if (heat > 30f) {
                    SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.4f, Pitch = 0.3f, MaxInstances = 3 }, Player.Center);
                    for (int i = 0; i < 6; i++) {
                        Dust dust = Dust.NewDustPerfect(Player.Top + new Vector2(Main.rand.NextFloat(-10f, 10f), 4f),
                            DustID.Cloud, new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.5f)), 160, default, 1.1f);
                        dust.noGravity = true;
                    }
                }
                heat = 0f;
                return;
            }

            if (Scorching()) {
                heat += DuneStorm.HeatRateByTier[GameModeSystem.EffectiveTier - 1];
            }
            else {
                heat = Math.Max(0f, heat - CoolRate);
                return;
            }

            //视觉通道：热闪粒子随热度渐强（满值前上限约 0.35 粒/帧）
            float k = heat / HeatFull;
            if (Main.rand.NextFloat() < k * k * 0.35f) {
                Dust mote = Dust.NewDustPerfect(
                    Player.Bottom + new Vector2(Main.rand.NextFloat(-Player.width * 0.6f, Player.width * 0.6f),
                        -Main.rand.NextFloat(0f, Player.height * 0.7f)),
                    DustID.Torch, new Vector2(0f, -Main.rand.NextFloat(0.4f, 1.2f)),
                    160, default, Main.rand.NextFloat(0.6f, 1f));
                mote.noGravity = true;
            }
            if (k > 0.35f) {
                Lighting.AddLight(Player.Center, new Vector3(0.30f, 0.20f, 0.06f) * k);
            }

            //听觉通道：过半后灼烤声渐密（脉冲间隔随热度收紧）
            if (heat >= SizzleFrom && sizzleCooldown <= 0) {
                SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.22f, Pitch = 0.5f, MaxInstances = 3 }, Player.Center);
                sizzleCooldown = (int)MathHelper.Lerp(34f, 18f, (heat - SizzleFrom) / (HeatFull - SizzleFrom));
            }

            if (heat < HeatFull) {
                return;
            }

            //灼热落地：短暂 Slow + 灼响 + 热闪爆发，热度回落
            heat = HeatAfterTrigger;
            Player.AddBuff(BuffID.Slow, 110);
            SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.65f, Pitch = 0.15f, MaxInstances = 3 }, Player.Center);
            for (int i = 0; i < 10; i++) {
                Dust burst = Dust.NewDustPerfect(Player.Center + new Vector2(Main.rand.NextFloat(-12f, 12f), Main.rand.NextFloat(-16f, 16f)),
                    DustID.Torch, new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(1f, 2.6f)),
                    140, default, Main.rand.NextFloat(0.8f, 1.3f));
                burst.noGravity = true;
            }
        }

        /// <summary>灼热条件：残酷地表沙漠 + 晴昼正午 + 站定在沙系地块上 + 无 Boss + 无城镇安宁</summary>
        private bool Scorching() {
            if (!DuneStorm.MechanicsAllowed || !DuneStorm.InSurfaceDesert(Player)) {
                return false;
            }
            if (!Main.dayTime || Main.time < NoonStart || Main.time > NoonEnd) {
                return false;
            }
            if (Main.raining || Sandstorm.Happening) {
                return false;//沙暴遮天与雨天都不积热
            }
            if (Player.velocity.Y != 0f || Math.Abs(Player.velocity.X) > 0.1f) {
                return false;//站定才积热，移动即散
            }
            if (!StandingOnSand()) {
                return false;
            }
            return !DuneStorm.TownCalm(Player.Center);
        }

        /// <summary>脚下两列任一为沙系实体地块</summary>
        private bool StandingOnSand() {
            int y = (int)(Player.Bottom.Y / 16f);
            for (int side = 0; side < 2; side++) {
                int x = (int)((Player.position.X + (side == 0 ? 4f : Player.width - 4f)) / 16f);
                for (int dy = 0; dy <= 1; dy++) {
                    if (!WorldGen.InWorld(x, y + dy, 10) || !WorldGen.SolidTile(x, y + dy)) {
                        continue;
                    }
                    if (DuneStorm.IsSandFamily(Framing.GetTileSafely(x, y + dy).TileType)) {
                        return true;
                    }
                    break;//先撞到非沙实体块就不再向下看
                }
            }
            return false;
        }

        public override void UpdateDead() => heat = 0f;
    }
}
