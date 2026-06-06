// 파일명: SaveData.cs
// 역할: 게임 종료 후에도 유지되는 영구 데이터 (골드, 메타 업그레이드)
//   - 파일 저장/불러오기 (save.dat)
//   - MetaUpgrade: 타이틀 상점에서 골드로 구매하는 영구 강화

using System;
using System.IO;
using System.Collections.Generic;

namespace SaveData
{
    // ─────────────────────────────────────────────────────────────
    // 메타 업그레이드 종류
    // ─────────────────────────────────────────────────────────────
    public enum MetaUpgradeType
    {
        MaxHP,          // 최대 체력 +20 (최대 5단계)
        MoveSpeed,      // 이동 속도 +10 (최대 5단계)
        StartDamage,    // 시작 데미지 +10% (최대 5단계)
        StartGold,      // 시작 골드 +50 (최대 3단계)
        ExpBonus,       // 경험치 획득량 +15% (최대 5단계)
        Revive,         // 부활 1회 추가 (최대 2단계)
    }

    // ─────────────────────────────────────────────────────────────
    // 메타 업그레이드 데이터 정의
    // ─────────────────────────────────────────────────────────────
    public class MetaUpgradeDef
    {
        public MetaUpgradeType Type;
        public string          Name;
        public string          Description;
        public int             MaxLevel;
        public int[]           Costs;      // 레벨별 구매 비용
        public float[]         Values;     // 레벨별 효과값

        public int Cost(int currentLevel) =>
            (currentLevel < Costs.Length) ? Costs[currentLevel] : 9999;

        public float TotalValue(int level)
        {
            float total = 0f;
            for (int i = 0; i < level && i < Values.Length; i++) total += Values[i];
            return total;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 메타 업그레이드 테이블 (정적)
    // ─────────────────────────────────────────────────────────────
    public static class MetaTable
    {
        public static readonly List<MetaUpgradeDef> All = new List<MetaUpgradeDef>
        {
            new MetaUpgradeDef {
                Type = MetaUpgradeType.MaxHP,
                Name = "강인한 몸",
                Description = "시작 최대 체력이 영구적으로 증가합니다.",
                MaxLevel = 5,
                Costs  = new[] { 80, 160, 280, 450, 700 },
                Values = new[] { 20f, 20f, 20f, 30f, 50f }
            },
            new MetaUpgradeDef {
                Type = MetaUpgradeType.MoveSpeed,
                Name = "날랜 발",
                Description = "시작 이동 속도가 영구적으로 증가합니다.",
                MaxLevel = 5,
                Costs  = new[] { 60, 130, 220, 350, 550 },
                Values = new[] { 10f, 10f, 15f, 15f, 20f }
            },
            new MetaUpgradeDef {
                Type = MetaUpgradeType.StartDamage,
                Name = "타고난 공격력",
                Description = "모든 무기의 시작 데미지가 영구적으로 증가합니다.",
                MaxLevel = 5,
                Costs  = new[] { 100, 200, 350, 550, 900 },
                Values = new[] { 0.1f, 0.1f, 0.15f, 0.15f, 0.2f }  // AccDamageMult에 더함
            },
            new MetaUpgradeDef {
                Type = MetaUpgradeType.StartGold,
                Name = "금수저",
                Description = "게임 시작 시 보유 골드가 증가합니다.",
                MaxLevel = 3,
                Costs  = new[] { 50, 150, 400 },
                Values = new[] { 50f, 100f, 200f }
            },
            new MetaUpgradeDef {
                Type = MetaUpgradeType.ExpBonus,
                Name = "빠른 성장",
                Description = "경험치 획득량이 영구적으로 증가합니다.",
                MaxLevel = 5,
                Costs  = new[] { 70, 150, 260, 400, 620 },
                Values = new[] { 0.15f, 0.15f, 0.15f, 0.2f, 0.25f }  // 배율 보너스
            },
            new MetaUpgradeDef {
                Type = MetaUpgradeType.Revive,
                Name = "불굴의 의지",
                Description = "사망 시 체력 30%로 1회 부활합니다.",
                MaxLevel = 2,
                Costs  = new[] { 500, 1500 },
                Values = new[] { 1f, 1f }  // 부활 횟수
            },
        };

        public static MetaUpgradeDef Get(MetaUpgradeType type) =>
            All.Find(d => d.Type == type);
    }

    // ─────────────────────────────────────────────────────────────
    // 세이브 데이터 본체
    // ─────────────────────────────────────────────────────────────
    public class SaveFile
    {
        public int PermanentGold = 0;
        public Dictionary<MetaUpgradeType, int> MetaLevels = new Dictionary<MetaUpgradeType, int>();

        // 편의 메서드
        public int GetMetaLevel(MetaUpgradeType t) =>
            MetaLevels.TryGetValue(t, out int v) ? v : 0;

        public bool CanBuy(MetaUpgradeType t, MetaUpgradeDef def) =>
            GetMetaLevel(t) < def.MaxLevel && PermanentGold >= def.Cost(GetMetaLevel(t));

        // 영구 골드 획득 (게임 종료 후 정산 시 호출)
        public void EarnGold(int amount) => PermanentGold += amount;

        // 메타 업그레이드 구매
        public bool BuyUpgrade(MetaUpgradeType t)
        {
            var def = MetaTable.Get(t);
            if (def == null) return false;
            int level = GetMetaLevel(t);
            if (level >= def.MaxLevel) return false;
            int cost = def.Cost(level);
            if (PermanentGold < cost) return false;

            PermanentGold -= cost;
            MetaLevels[t] = level + 1;
            return true;
        }

        // ── 저장 / 불러오기 ──
        private const string SavePath = "save.dat";

        public void Save()
        {
            try
            {
                using var sw = new StreamWriter(SavePath);
                sw.WriteLine(PermanentGold);
                foreach (var kv in MetaLevels)
                    sw.WriteLine($"{(int)kv.Key}:{kv.Value}");
            }
            catch { /* 저장 실패 시 무시 */ }
        }

        public static SaveFile Load()
        {
            var sf = new SaveFile();
            try
            {
                if (!File.Exists(SavePath)) return sf;
                using var sr = new StreamReader(SavePath);
                sf.PermanentGold = int.Parse(sr.ReadLine() ?? "0");
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    var parts = line.Split(':');
                    if (parts.Length == 2 &&
                        int.TryParse(parts[0], out int k) &&
                        int.TryParse(parts[1], out int v))
                        sf.MetaLevels[(MetaUpgradeType)k] = v;
                }
            }
            catch { sf = new SaveFile(); }
            return sf;
        }
    }
}