// 파일명: UpgradeLogic.cs
using System;
using System.Collections.Generic;
using WeaponData;
using CombatSystem;
using EntityGroup;

namespace UpgradeLogic
{
    public class LevelSystem
    {
        public int Level = 1; public int CurrentExp = 0; public int MaxExp = 20; public bool IsLevelUpReady = false;
        // ★ 메타 업그레이드: 경험치 획득 배율 (기본 1.0, 빠른성장 업그레이드로 증가)
        public float ExpMult = 1.0f;

        public void AddExp(int amount)
        {
            // ExpMult 반영: 소수점은 확률로 처리
            float scaled = amount * ExpMult;
            int final    = (int)scaled;
            // 소수 부분을 확률로 반올림 (예: 1.3 → 70% 확률로 1, 30% 확률로 2)
            if ((float)(new Random().NextDouble()) < (scaled - final)) final++;

            CurrentExp += final;
            if (CurrentExp >= MaxExp) { CurrentExp -= MaxExp; Level++; MaxExp += 10; IsLevelUpReady = true; }
        }
    }

    // 풀강 시 등장하는 보너스 카드 종류
    public enum BonusCardType { None, HealSmall, HealLarge, GoldSmall, GoldLarge, Shield }

    public class UpgradeCard
    {
        public CardType      CardType;
        public WeaponType    WeaponType;
        public AccessoryType AccessoryType;
        public BonusCardType BonusType = BonusCardType.None; // 풀강 보너스 카드용
        public int           NextLevel;
        public string        Title;
        public string        Description;
        public bool          IsNewWeapon;

        public bool IsBonus => BonusType != BonusCardType.None;

        public Raylib_cs.Color CardColor
        {
            get {
                if (IsBonus) return new Raylib_cs.Color(20, 80, 40, 220);
                return CardType == CardType.Weapon
                    ? new Raylib_cs.Color(30, 60, 130, 220)
                    : new Raylib_cs.Color(130, 60, 30, 220);
            }
        }
        public Raylib_cs.Color BorderColor
        {
            get {
                if (IsBonus) return new Raylib_cs.Color(80, 220, 120, 255);
                return CardType == CardType.Weapon
                    ? new Raylib_cs.Color(80, 140, 255, 255)
                    : new Raylib_cs.Color(255, 140, 80, 255);
            }
        }
        public string Icon
        {
            get {
                if (IsBonus) return BonusType switch {
                    BonusCardType.HealSmall => "HP",
                    BonusCardType.GoldSmall => "G",
                    _ => "?"
                };
                return CardType == CardType.Weapon ? "W" : "A";
            }
        }
    }

    public class CardDeck
    {
        // 진화 무기(MagicCircle 등)도 초기값 0으로 미리 등록
        public Dictionary<WeaponType, int> WeaponLevels = new() {
            { WeaponType.Staff, 0 }, { WeaponType.Garlic, 0 }, { WeaponType.Orbital, 0 },
            { WeaponType.Axe, 0 }, { WeaponType.Shuriken, 0 },
            { WeaponType.MagicCircle, 0 }, { WeaponType.HellFire, 0 },
            { WeaponType.BlackHole, 0 }, { WeaponType.AxeStorm, 0 }, { WeaponType.InfiniteShuriken, 0 }
        };
        public Dictionary<AccessoryType, int> AccessoryLevels = new() {
            { AccessoryType.Shoes, 0 }, { AccessoryType.Armor, 0 },
            { AccessoryType.Ring, 0 }, { AccessoryType.Glove, 0 }, { AccessoryType.Necklace, 0 }
        };

        private Random _rng = new Random();
        public List<UpgradeCard> CurrentCards = new List<UpgradeCard>();

        // 진화가 완료된 원본 무기 목록 — 이 목록에 있으면 카드 후보에서 영구 제외
        public HashSet<WeaponType> EvolvedWeapons = new HashSet<WeaponType>();

        public void InitStartingWeapons(bool hasStaff, bool hasGarlic, bool hasOrbital)
        {
            if (hasStaff) WeaponLevels[WeaponType.Staff] = 1;
            if (hasGarlic) WeaponLevels[WeaponType.Garlic] = 1;
            if (hasOrbital) WeaponLevels[WeaponType.Orbital] = 1;
        }

        public void DrawCards()
        {
            CurrentCards.Clear();
            List<UpgradeCard> candidates = new List<UpgradeCard>();

            // 1. 업그레이드 가능한 무기/장신구를 먼저 스캔
            foreach (var pair in WeaponLevels)
            {
                if (IsEvolution(pair.Key)) continue;
                if (EvolvedWeapons.Contains(pair.Key)) continue;
                if (pair.Value == 0) candidates.Add(new UpgradeCard { CardType = CardType.Weapon, WeaponType = pair.Key, NextLevel = 1, IsNewWeapon = true, Title = WeaponName(pair.Key) + " 해금", Description = WeaponTable.GetWeapon(pair.Key, 1).Description });
                else if (pair.Value < 5) candidates.Add(new UpgradeCard { CardType = CardType.Weapon, WeaponType = pair.Key, NextLevel = pair.Value + 1, IsNewWeapon = false, Title = WeaponName(pair.Key) + $" Lv.{pair.Value + 1}", Description = WeaponTable.GetWeapon(pair.Key, pair.Value + 1).Description });
            }
            foreach (var pair in AccessoryLevels)
            {
                if (pair.Value == 0) candidates.Add(new UpgradeCard { CardType = CardType.Accessory, AccessoryType = pair.Key, NextLevel = 1, IsNewWeapon = true, Title = AccName(pair.Key) + " 획득", Description = WeaponTable.GetAcc(pair.Key, 1).Description });
                else if (pair.Value < 5) candidates.Add(new UpgradeCard { CardType = CardType.Accessory, AccessoryType = pair.Key, NextLevel = pair.Value + 1, IsNewWeapon = false, Title = AccName(pair.Key) + $" Lv.{pair.Value + 1}", Description = WeaponTable.GetAcc(pair.Key, pair.Value + 1).Description });
            }

            // 2. 업그레이드 후보가 단 1개라도 있으면 그것만 보여줌 (남은 빈자리를 골드/회복으로 채우지 않음)
            if (candidates.Count > 0)
            {
                Shuffle(candidates);
                int count = Math.Min(3, candidates.Count);
                for (int i = 0; i < count; i++) CurrentCards.Add(candidates[i]);
            }
            // 3. 업그레이드 할 것이 "전혀 없을 때"만 빵과 금화 보따리 고정 출현
            else
            {
                CurrentCards.Add(new UpgradeCard {
                    CardType = CardType.Weapon, BonusType = BonusCardType.HealSmall,
                    Title = "빵", Description = "체력을 30% 회복합니다.", IsNewWeapon = false
                });

                CurrentCards.Add(new UpgradeCard {
                    CardType = CardType.Weapon, BonusType = BonusCardType.GoldSmall,
                    Title = "금화 보따리", Description = "100 골드를 획득합니다.", IsNewWeapon = false
                });
            }
        }

        public UpgradeCard SelectCard(int index, LevelSystem levelSystem = null)
        {
            if (index < 0 || index >= CurrentCards.Count) return null;
            var card = CurrentCards[index];
            if (!card.IsBonus)
            {
                if (card.CardType == CardType.Weapon)
                    WeaponLevels[card.WeaponType] = card.NextLevel;
                else
                {
                    AccessoryLevels[card.AccessoryType] = card.NextLevel;
                    // 목걸이: ExpMult 즉시 갱신
                    if (card.AccessoryType == AccessoryType.Necklace && levelSystem != null)
                    {
                        var data = WeaponTable.GetAcc(AccessoryType.Necklace, card.NextLevel);
                        levelSystem.ExpMult = data.ValueFloat;
                    }
                }
            }
            CurrentCards.Clear();
            return card;
        }

        // 보물상자 열기 로직
        public List<string> OpenChest(Weapon weapon, Player player)
        {
            List<string> results = new List<string>();
            int roll = _rng.Next(100);
            int chestCount = (roll < 60) ? 1 : (roll < 90) ? 3 : 5;

            for (int i = 0; i < chestCount; i++)
            {
                // 1순위: 진화 가능한 무기가 있다면 최우선으로 진화시킴
                if (TryEvolveWeapon(weapon, out string evoName))
                {
                    // 확실하게 진화한 무기 이름이 넘어가도록 설정
                    results.Add($"★ 진화: {evoName} ★");
                    continue;
                }

                // 2순위: 현재 가지고 있는 장비 중 5레벨(만렙)이 아닌 것들 수집
                List<UpgradeCard> upgradables = new List<UpgradeCard>();
                foreach (var w in WeaponLevels) {
                    if (w.Value > 0 && w.Value < 5 && !IsEvolution(w.Key) && !EvolvedWeapons.Contains(w.Key))
                        upgradables.Add(new UpgradeCard { CardType = CardType.Weapon, WeaponType = w.Key, NextLevel = w.Value + 1 });
                }
                foreach (var a in AccessoryLevels) {
                    if (a.Value > 0 && a.Value < 5)
                        upgradables.Add(new UpgradeCard { CardType = CardType.Accessory, AccessoryType = a.Key, NextLevel = a.Value + 1 });
                }

                if (upgradables.Count > 0)
                {
                    var pick = upgradables[_rng.Next(upgradables.Count)];
                    if (pick.CardType == CardType.Weapon) {
                        WeaponLevels[pick.WeaponType] = pick.NextLevel;
                        weapon.ApplyLevel(pick.WeaponType, pick.NextLevel);
                        results.Add(WeaponName(pick.WeaponType) + " Lv." + pick.NextLevel);
                    } else {
                        AccessoryLevels[pick.AccessoryType] = pick.NextLevel;
                        weapon.ApplyAccessory(pick.AccessoryType, pick.NextLevel, player);
                        results.Add(AccName(pick.AccessoryType) + " Lv." + pick.NextLevel);
                    }
                }
                else
                {
                    player.Gold += 100;
                    results.Add("금화 주머니 (+100 골드)");
                }
            }
            return results;
        }

        // 진화 여부 체크
        private bool TryEvolveWeapon(Weapon weapon, out string evoName)
        {
            evoName = "";
            if (CheckEvo(WeaponType.Staff,    AccessoryType.Shoes,    WeaponType.HellFire,        weapon, out evoName)) return true;
            if (CheckEvo(WeaponType.Garlic,   AccessoryType.Armor,    WeaponType.MagicCircle,     weapon, out evoName)) return true;
            if (CheckEvo(WeaponType.Orbital,  AccessoryType.Ring,     WeaponType.BlackHole,       weapon, out evoName)) return true;
            if (CheckEvo(WeaponType.Axe,      AccessoryType.Glove,    WeaponType.AxeStorm,        weapon, out evoName)) return true;
            if (CheckEvo(WeaponType.Shuriken, AccessoryType.Necklace, WeaponType.InfiniteShuriken,weapon, out evoName)) return true;
            return false;
        }

        private bool CheckEvo(WeaponType w, AccessoryType a, WeaponType evo, Weapon weapon, out string evoName)
        {
            evoName = "";
            if (WeaponLevels.GetValueOrDefault(w, 0) == 5 && AccessoryLevels.GetValueOrDefault(a, 0) >= 1)
            {
                if (!WeaponLevels.ContainsKey(evo) || WeaponLevels[evo] == 0)
                {
                    WeaponLevels[evo] = 1;
                    WeaponLevels[w]   = 0;
                    EvolvedWeapons.Add(w);
                    weapon.ApplyEvolution(w, evo);
                    evoName = WeaponName(evo);
                    return true;
                }
            }
            return false;
        }

        private bool IsEvolution(WeaponType t) =>
            t == WeaponType.MagicCircle || t == WeaponType.HellFire ||
            t == WeaponType.BlackHole   || t == WeaponType.AxeStorm  ||
            t == WeaponType.InfiniteShuriken;

        private string WeaponName(WeaponType t) => t switch {
            WeaponType.Staff             => "지팡이",
            WeaponType.Garlic            => "영창",
            WeaponType.Orbital           => "궤도구체",
            WeaponType.Axe               => "도끼",
            WeaponType.Shuriken          => "표창",
            WeaponType.MagicCircle       => "마법진",
            WeaponType.HellFire          => "헬파이어",
            WeaponType.BlackHole         => "블랙홀",
            WeaponType.AxeStorm          => "도끼폭풍",
            WeaponType.InfiniteShuriken  => "무한표창",
            _                            => "???"
        };
        private string AccName(AccessoryType t) => t switch {
            AccessoryType.Shoes    => "신발(투사체+)",
            AccessoryType.Armor    => "갑옷(방어+)",
            AccessoryType.Ring     => "반지(범위+)",
            AccessoryType.Glove    => "장갑(데미지+)",
            AccessoryType.Necklace => "목걸이(경험치+)",
            _                      => "???"
        };
        private void Shuffle<T>(List<T> list) { for (int i = list.Count - 1; i > 0; i--) { int j = _rng.Next(i + 1); (list[i], list[j]) = (list[j], list[i]); } }
    }
}