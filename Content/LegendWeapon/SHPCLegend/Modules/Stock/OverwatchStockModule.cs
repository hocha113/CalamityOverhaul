using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock
{
    /// <summary>守望枪托，静止叠哨戒层，注入 ShootContext</summary>
    internal sealed class OverwatchStockModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Stock;
        //哨戒蓝白
        public override Color TintColor => new(150, 220, 255);

        private const int MaxStacks = 5;
        private const float StationaryThreshold = 0.6f;
        private const int StackUpInterval = 18;
        private const float DamagePerStack = 0.03f;
        private const float CritPerStack = 2f;

        private int _stacks;
        private int _stackUpTimer;

        public override void Apply(ref ShootContext ctx) {
            //基础，精确但攻速略减
            ctx.SpreadMul += -0.35f;
            ctx.AttackSpeedMul += -0.1f;
            //层数注入
            ctx.DamageMul += _stacks * DamagePerStack;
            ctx.CritAdd += (int)(_stacks * CritPerStack);
        }

        public override void OnPlayerUpdate(Player player) {
            if (player == null || !player.active) return;
            float speed = player.velocity.Length();
            if (speed < StationaryThreshold) {
                _stackUpTimer++;
                if (_stackUpTimer >= StackUpInterval && _stacks < MaxStacks) {
                    _stackUpTimer = 0;
                    _stacks++;
                    SpawnStackVFX(player);
                }
            }
            else {
                _stackUpTimer = 0;
                if (_stacks > 0 && Main.GameUpdateCount % 6 == 0) {
                    _stacks--;
                }
            }
        }

        private void SpawnStackVFX(Player player) {
            if (Main.netMode == NetmodeID.Server) return;
            if (player.whoAmI != Main.myPlayer) return;
            //逐层方粒读作贴纸已删，蓄层期零表现，只留满层锁定一拍
            if (_stacks < MaxStacks) return;
            Vector2 anchor = player.Center + new Vector2(0f, -player.height * 0.5f);
            //满层锁定拍，细锐环收拢+轻就位音
            PRTLoader.NewParticle<PRT_StarPulseRing>(anchor, Vector2.Zero,
                new Color(200, 240, 255), 0.3f).Configure(0.3f, 0.05f, 12);
            SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.2f, Pitch = 0.65f }, player.Center);
        }
    }
}
