// 파일명: UpgradeLogic.cs
namespace UpgradeLogic
{
    public class LevelSystem 
    { 
        public int Level = 1; 
        public int CurrentExp = 0; 
        public int MaxExp = 20; 
        public bool IsLevelUpReady = false;

        public void AddExp(int amount) 
        { 
            CurrentExp += amount; 
            if (CurrentExp >= MaxExp) 
            { 
                CurrentExp -= MaxExp; 
                Level++; 
                
                // ★ 1.5배 곱셈에서 선형 덧셈으로 변경 (레벨업 속도 대폭 증가!)
                // 레벨이 오를 때마다 다음 요구량이 10씩만 늘어납니다. (20 -> 30 -> 40 -> 50...)
                MaxExp += 10; 
                
                IsLevelUpReady = true; 
            } 
        }
    }
}