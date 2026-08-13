using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 元素弹幕：火=三波弯弧火扇；冰=环阵冰枪涟漪+寒雾；雷=行进天柱列；
    /// npc.ai[3]=布阵种子
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.ElementBarrage, typeof(CultistStateContext))]
    internal class CultistElementBarrageState : CultistStateBase
    {
        public override string StateName => "ElementBarrage";
        public override CultistStateIndex StateIndex => CultistStateIndex.ElementBarrage;

        public override void OnEnter(CultistStateContext context) {
            base.OnEnter(context);
            if (!VaultUtils.isClient) {
                context.Npc.ai[3] = Main.rand.Next(1000);
                context.Npc.netUpdate = true;
            }
        }

        private int Duration(CultistStateContext ctx) => ctx.Element switch {
            CultistElement.Fire => 178,
            CultistElement.Ice => 196,
            _ => 208,
        };

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            FaceTarget(context);
            context.ElementAura = 1f;

            //侧位悬浮，随波次轻移
            int side = (int)npc.ai[3] % 2 == 0 ? 1 : -1;
            if (player.Alives()) {
                float drift = (float)Math.Sin(Timer * 0.02f) * 90f;
                SetHover(context, player.Center + new Vector2(side * (430f + drift), -270f));
            }

            switch (context.Element) {
                case CultistElement.Fire:
                    UpdateFire(context, npc, player);
                    break;
                case CultistElement.Ice:
                    UpdateIce(context, npc, player);
                    break;
                default:
                    UpdateThunder(context, npc, player);
                    break;
            }

            if (Timer >= Duration(context)) {
                return new CultistWeaveState();
            }
            return null;
        }

        #region 焚焰灼弧
        private void UpdateFire(CultistStateContext context, NPC npc, Player player) {
            //三波：30/80/130，波前20帧起手辉光
            int[] waves = [30, 80, 130];
            foreach (int wave in waves) {
                if (Timer >= wave - 20 && Timer < wave) {
                    context.CastPose = CultistPose.CastForward;
                    context.CastGlow = (Timer - (wave - 20)) / 20f;
                }
                if ((int)Timer == wave) {
                    context.CastPose = CultistPose.CastForward;
                    context.CastGlow = 1f;
                    Vector2 hand = HandPos(npc);
                    Vector2 aim = AimWithLead(npc, player, 18f);
                    if (!VaultUtils.isServer) {
                        CultistRenderHelper.CastBurst(hand, aim, CultistElement.Fire, 1.3f);
                        SoundEngine.PlaySound(SoundID.Item73 with { Volume = 0.85f, Pitch = -0.1f, MaxInstances = 4 }, hand);
                    }
                    if (!VaultUtils.isClient && player.Alives()) {
                        int count = context.IsDeathMode ? 7 : 5;
                        int damage = ProjDamage(npc, 38f, 27f);
                        for (int i = 0; i < count; i++) {
                            float spread = MathHelper.Lerp(-0.45f, 0.45f, count <= 1 ? 0.5f : i / (float)(count - 1));
                            Vector2 vel = aim.RotatedBy(spread) * 6.2f;
                            //中路火球落地留焚地
                            float cinder = i == count / 2 ? 1f : 0f;
                            Projectile.NewProjectile(npc.GetSource_FromAI(), hand, vel,
                                ModContent.ProjectileType<CultistFireBolt>(), damage, 0f, Main.myPlayer, 45f, cinder);
                        }
                    }
                }
            }
        }
        #endregion

        #region 霜牢枪阵
        private void UpdateIce(CultistStateContext context, NPC npc, Player player) {
            //布阵期施法向上
            if (Timer >= 10 && Timer <= 60) {
                context.CastPose = CultistPose.CastUp;
                context.CastGlow = MathHelper.Clamp((Timer - 10) / 30f, 0f, 1f);
            }

            if ((int)Timer == 20 && !VaultUtils.isClient && player.Alives()) {
                int count = context.IsDeathMode ? 12 : 10;
                int damage = ProjDamage(npc, 40f, 28f);
                float baseAngle = npc.ai[3] * 0.37f;
                for (int i = 0; i < count; i++) {
                    float angle = baseAngle + MathHelper.TwoPi * i / count;
                    Vector2 pos = player.Center + angle.ToRotationVector2() * 430f;
                    Vector2 aim = (player.Center - pos).SafeNormalize(Vector2.UnitY);
                    //成对涟漪刺出：相位错拍
                    float telegraph = 55f + (i % 5) * 8f;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), pos, aim,
                        ModContent.ProjectileType<CultistIceLance>(), damage, 0f, Main.myPlayer, telegraph, 19f);
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item120 with { Volume = 0.7f, Pitch = 0.4f }, player.Center);
                }
            }

            //寒雾两团从侧翼漂入
            if ((int)Timer == 92 && !VaultUtils.isClient && player.Alives()) {
                for (int s = -1; s <= 1; s += 2) {
                    Vector2 pos = player.Center + new Vector2(s * 560f, -120f);
                    Vector2 vel = new(-s * 1.6f, 0.2f);
                    Projectile.NewProjectile(npc.GetSource_FromAI(), pos, vel,
                        ModContent.ProjectileType<CultistFrostMistZone>(), 0, 0f, Main.myPlayer);
                }
            }
        }
        #endregion

        #region 雷枢天柱
        private void UpdateThunder(CultistStateContext context, NPC npc, Player player) {
            if (Timer >= 8 && Timer <= 70) {
                context.CastPose = CultistPose.CastUp;
                context.CastGlow = MathHelper.Clamp((Timer - 8) / 30f, 0f, 1f);
                //臂间电花
                if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                    CultistRenderHelper.SpawnElementMote(HandPos(npc), Main.rand.NextVector2Circular(2f, 2f),
                        CultistElement.Thunder, 0.8f, 14);
                }
            }

            //主波：行进柱列，从远侧碾向玩家
            if ((int)Timer == 22 && !VaultUtils.isClient && player.Alives()) {
                int count = context.IsDeathMode ? 7 : 5;
                int damage = ProjDamage(npc, 44f, 30f);
                int dir = Math.Sign(player.Center.X - npc.Center.X);
                if (dir == 0) {
                    dir = 1;
                }
                for (int i = 0; i < count; i++) {
                    float offsetX = (i - (count - 1) * 0.5f) * 190f * dir;
                    Vector2 ground = FindGround(player.Center + new Vector2(offsetX, 0f));
                    float telegraph = 50f + i * 9f;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), ground, Vector2.Zero,
                        ModContent.ProjectileType<CultistThunderColumn>(), damage, 0f, Main.myPlayer, telegraph, 1400f);
                }
            }

            //补刀波：贴脚双柱
            if ((int)Timer == 128 && !VaultUtils.isClient && player.Alives()) {
                int damage = ProjDamage(npc, 44f, 30f);
                for (int s = -1; s <= 1; s += 2) {
                    Vector2 ground = FindGround(player.Center + new Vector2(s * 95f, 0f));
                    Projectile.NewProjectile(npc.GetSource_FromAI(), ground, Vector2.Zero,
                        ModContent.ProjectileType<CultistThunderColumn>(), damage, 0f, Main.myPlayer, 46f, 1400f);
                }
            }
        }
        #endregion

        /// <summary>下探地面，找不到则玩家脚下400px</summary>
        internal static Vector2 FindGround(Vector2 from) {
            int tileX = (int)(from.X / 16f);
            int tileY = Math.Max((int)(from.Y / 16f), 10);
            for (int i = 0; i < 70; i++) {
                int y = tileY + i;
                if (y >= Main.maxTilesY - 10) {
                    break;
                }
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (tile.HasUnactuatedTile && Main.tileSolid[tile.TileType]) {
                    return new Vector2(from.X, y * 16f);
                }
            }
            return from + new Vector2(0f, 400f);
        }
    }
}
