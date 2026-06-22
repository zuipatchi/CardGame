namespace Common.Cpu
{
    // 選んだ CPU 対戦相手を Home から Main へ受け渡す Common 常駐モデル。
    // Main シーンを再ロードする再戦でも同じ index が残るため、同じ相手と戦える。
    public sealed class CpuBattleModel
    {
        // CpuRosterStore のロスターに対する相手の index。
        public int OpponentIndex { get; set; }
    }
}
