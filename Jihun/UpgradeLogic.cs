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
        public void AddExp(int amount)
        {
            CurrentExp += amount;
            if (CurrentExp >= MaxExp) { CurrentExp -= MaxExp; Level++; MaxExp += 10; IsLevelUpReady = true; }
        }
    }

    public class UpgradeCard
    {
        public CardType      CardType;
        public WeaponType    WeaponType;
        public AccessoryType AccessoryType;
        public int           NextLevel;
        public string        Title;
        public string        Description;
        public bool          IsNewWeapon;

        public Raylib_cs.Color CardColor => CardType == CardType.Weapon ? new Raylib_cs.Color(30, 60, 130, 220) : new Raylib_cs.Color(130, 60, 30, 220);
        public Raylib_cs.Color BorderColor => CardType == CardType.Weapon ? new Raylib_cs.Color(80, 140, 255, 255) : new Raylib_cs.Color(255, 140, 80, 255);
        public string Icon => CardType == CardType.Weapon ? "W" : "A";
    }

    public class CardDeck
    {
        public Dictionary<WeaponType, int> WeaponLevels = new() { { WeaponType.Staff, 0 }, { WeaponType.Garlic, 0 }, { WeaponType.Orbital, 0 }, { WeaponType.Axe, 0 } };
        public Dictionary<AccessoryType, int> AccessoryLevels = new() { { AccessoryType.Wings, 0 }, { AccessoryType.Armor, 0 }, { AccessoryType.Ring, 0 }, { AccessoryType.Glove, 0 } };

        private Random _rng = new Random();
        public List<UpgradeCard> CurrentCards = new List<UpgradeCard>();

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

            foreach (var pair in WeaponLevels)
            {
                if (IsEvolution(pair.Key)) continue;
                if (pair.Value == 0) candidates.Add(new UpgradeCard { CardType = CardType.Weapon, WeaponType = pair.Key, NextLevel = 1, IsNewWeapon = true, Title = WeaponName(pair.Key) + " 해금", Description = WeaponTable.GetWeapon(pair.Key, 1).Description });
                else if (pair.Value < 5) candidates.Add(new UpgradeCard { CardType = CardType.Weapon, WeaponType = pair.Key, NextLevel = pair.Value + 1, IsNewWeapon = false, Title = WeaponName(pair.Key) + $" Lv.{pair.Value + 1}", Description = WeaponTable.GetWeapon(pair.Key, pair.Value + 1).Description });
            }
            foreach (var pair in AccessoryLevels)
            {
                if (pair.Value == 0) candidates.Add(new UpgradeCard { CardType = CardType.Accessory, AccessoryType = pair.Key, NextLevel = 1, IsNewWeapon = true, Title = AccName(pair.Key) + " 획득", Description = WeaponTable.GetAcc(pair.Key, 1).Description });
                else if (pair.Value < 5) candidates.Add(new UpgradeCard { CardType = CardType.Accessory, AccessoryType = pair.Key, NextLevel = pair.Value + 1, IsNewWeapon = false, Title = AccName(pair.Key) + $" Lv.{pair.Value + 1}", Description = WeaponTable.GetAcc(pair.Key, pair.Value + 1).Description });
            }

            Shuffle(candidates);
            int count = Math.Min(3, candidates.Count);
            for (int i = 0; i < count; i++) CurrentCards.Add(candidates[i]);
        }

        public UpgradeCard SelectCard(int index)
        {
            if (index < 0 || index >= CurrentCards.Count) return null;
            var card = CurrentCards[index];
            if (card.CardType == CardType.Weapon) WeaponLevels[card.WeaponType] = card.NextLevel;
            else AccessoryLevels[card.AccessoryType] = card.NextLevel;
            CurrentCards.Clear();
            return card;
        }

        // ★ [신규] 보물상자 열기 로직 (1, 3, 5 아이템 드랍)
        public List<string> OpenChest(Weapon weapon, Player player)
        {
            List<string> results = new List<string>();
            int roll = _rng.Next(100);
            
            // 확률: 80% = 1상자 / 15% = 3상자 / 5% = 5상자
            int chestCount = (roll < 80) ? 1 : (roll < 95) ? 3 : 5;

            for (int i = 0; i < chestCount; i++)
            {
                // 1순위: 진화 가능한 무기가 있다면 최우선으로 진화시킴
                if (TryEvolveWeapon(weapon, out string evoName))
                {
                    results.Add($"★ 진화: {evoName} ★");
                    continue;
                }

                // 2순위: 현재 가지고 있는 장비 중 5레벨(만렙)이 아닌 것들 수집
                List<UpgradeCard> upgradables = new List<UpgradeCard>();
                foreach (var w in WeaponLevels) {
                    if (w.Value > 0 && w.Value < 5 && !IsEvolution(w.Key))
                        upgradables.Add(new UpgradeCard { CardType = CardType.Weapon, WeaponType = w.Key, NextLevel = w.Value + 1 });
                }
                foreach (var a in AccessoryLevels) {
                    if (a.Value > 0 && a.Value < 5)
                        upgradables.Add(new UpgradeCard { CardType = CardType.Accessory, AccessoryType = a.Key, NextLevel = a.Value + 1 });
                }

                // 업그레이드 할 게 남아있다면 랜덤으로 1업
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
                // 진화도 못하고, 모든 장비가 만렙이라면 골드 뭉치 지급
                else
                {
                    player.Gold += 100;
                    results.Add("금화 주머니 (+100 골드)");
                }
            }
            return results;
        }

        // 진화 여부 체크 및 실행
        private bool TryEvolveWeapon(Weapon weapon, out string evoName)
        {
            evoName = "";
            if (CheckEvo(WeaponType.Staff, AccessoryType.Wings, WeaponType.MagicCircle, weapon, out evoName)) return true;
            if (CheckEvo(WeaponType.Garlic, AccessoryType.Armor, WeaponType.HolyWater, weapon, out evoName)) return true;
            if (CheckEvo(WeaponType.Orbital, AccessoryType.Ring, WeaponType.BlackHole, weapon, out evoName)) return true;
            if (CheckEvo(WeaponType.Axe, AccessoryType.Glove, WeaponType.AxeStorm, weapon, out evoName)) return true;
            return false;
        }

        private bool CheckEvo(WeaponType w, AccessoryType a, WeaponType evo, Weapon weapon, out string evoName)
        {
            evoName = "";
            if (WeaponLevels.GetValueOrDefault(w, 0) == 5 && AccessoryLevels.GetValueOrDefault(a, 0) == 5)
            {
                if (!WeaponLevels.ContainsKey(evo) || WeaponLevels[evo] == 0)
                {
                    WeaponLevels[evo] = 1; WeaponLevels[w] = 0; // 원본 삭제
                    weapon.ApplyEvolution(w, evo);
                    evoName = WeaponName(evo);
                    return true;
                }
            }
            return false;
        }

        private bool IsEvolution(WeaponType t) => t == WeaponType.MagicCircle || t == WeaponType.HolyWater || t == WeaponType.BlackHole || t == WeaponType.AxeStorm;
        private string WeaponName(WeaponType t) => t switch { WeaponType.Staff => "지팡이", WeaponType.Garlic => "마늘", WeaponType.Orbital => "궤도구체", WeaponType.Axe => "도끼", WeaponType.MagicCircle => "마법진", WeaponType.HolyWater => "성수", WeaponType.BlackHole => "블랙홀", WeaponType.AxeStorm => "도끼폭풍", _ => "???" };
        private string AccName(AccessoryType t) => t switch { AccessoryType.Wings => "날개(투사체+)", AccessoryType.Armor => "갑옷(방어+)", AccessoryType.Ring => "반지(범위+)", AccessoryType.Glove => "장갑(데미지+)", _ => "???" };
        private void Shuffle<T>(List<T> list) { for (int i = list.Count - 1; i > 0; i--) { int j = _rng.Next(i + 1); (list[i], list[j]) = (list[j], list[i]); } }
    }
}