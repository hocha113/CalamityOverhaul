using CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.BossRooms
{
    /// <summary>
    /// 禁室封门砖：战斗期由 GaolBossRoomWatcher 填进门洞的实心柱，
    /// 不可采掘、防爆、无掉落，唯一解除方式是看守的解封事务。
    /// 零贴图：暗铁底 + 幽粉锁链栅；shader 就绪时演出交给
    /// GaolRoomAmbienceRender 的能量栅 pass，此处只收集格子。
    /// </summary>
    internal class GaolRoomSealTile : ModTile
    {
        public override bool IsLoadingEnabled(Mod mod) => DeepGaolWraithGate.Enabled;

        public override string Texture => CWRConstant.VaultPlaceholder2;

        //囚粉主题色（数值对齐 DeepGaolWraith 的 GaolPink/GaolPinkDeep/IronDeep，
        //本地留副本避免耦合 A1 在改的文件）
        private static readonly Color SealPink = new(236, 116, 156);
        private static readonly Color SealPinkDeep = new(118, 34, 66);
        private static readonly Color SealIron = new(60, 54, 66);

        public override void SetStaticDefaults() {
            Main.tileSolid[Type] = true;
            Main.tileBlockLight[Type] = false;
            Main.tileLighted[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            //不可采掘：唯一开门方式是看守解封
            MinPick = 999;
            MineResist = 30f;
            AddMapEntry(new Color(140, 52, 84), CreateMapEntryName());
        }

        public override bool CanExplode(int i, int j) => false;

        public override bool CanKillTile(int i, int j, ref bool blockDamaged) => false;

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            //幽粉脉动：整根门柱共相位，行间错峰读作能量在栅上流
            float pulse = 0.65f + 0.35f * MathF.Sin(Main.GlobalTimeWrappedHourly * 1.8f + j * 0.7f);
            r = 0.26f * pulse;
            g = 0.10f * pulse;
            b = 0.17f * pulse;
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            //shader 路径：能量栅由氛围层按门洞整块出（GaolRoom.fx TechGrate），格子不自绘
            if (GaolRoomAmbienceRender.GrateShaderReady) {
                return false;
            }

            //CPU 回退：暗铁底 + 双缘幽粉线 + 上浮亮段（怨气上涌，与警戒扫描反向）
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return false;
            }
            float t = Main.GlobalTimeWrappedHourly;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 tl = new Vector2(i * 16, j * 16) - Main.screenPosition + offset;

            Vector2 Size(float w, float h) => new(w / px.Width, h / px.Height);

            spriteBatch.Draw(px, tl, null, SealIron, 0f, Vector2.Zero,
                Size(16f, 16f), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, tl + new Vector2(1f, 0f), null, SealPinkDeep * 0.8f, 0f, Vector2.Zero,
                Size(1.6f, 16f), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, tl + new Vector2(13.4f, 0f), null, SealPinkDeep * 0.8f, 0f, Vector2.Zero,
                Size(1.6f, 16f), SpriteEffects.None, 0f);

            //横档：每格中带一道铁档，读作锁链栅的横链
            spriteBatch.Draw(px, tl + new Vector2(2f, 6.5f), null, SealPinkDeep * 0.55f, 0f, Vector2.Zero,
                Size(12f, 3f), SpriteEffects.None, 0f);

            //上浮怨气亮段：相位向上游走
            float phase = 1f - (t * 0.6f + i * 0.17f) % 1f;
            float local = phase * 128f - j * 16 % 128;
            if (local > -6f && local < 16f) {
                float yClamped = MathHelper.Clamp(local, 0f, 13f);
                spriteBatch.Draw(px, tl + new Vector2(2f, yClamped), null, SealPink * 0.7f, 0f,
                    Vector2.Zero, Size(12f, 3f), SpriteEffects.None, 0f);
                spriteBatch.Draw(px, tl + new Vector2(2f, yClamped + 1f), null, Color.White * 0.4f, 0f,
                    Vector2.Zero, Size(12f, 1f), SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
