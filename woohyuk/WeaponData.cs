// 파일명: WeaponData.cs
namespace WeaponData
{
    public enum CardType { Weapon, Accessory }
    public enum WeaponType { Staff, Garlic, Orbital, Axe, MagicCircle, HolyWater, BlackHole, AxeStorm }
    public enum AccessoryType { Wings, Armor, Ring, Glove }

    public class WeaponLevelData
    {
        public int Level; public string Description;
        public float StaffDamage; public float StaffCooldown; public int StaffProjectileCount;
        public float GarlicDamage; public float GarlicRadius; public float GarlicCooldown;
        public float OrbitalDamage; public int OrbitalCount; public float OrbitalRadius; public float OrbitalSpeed;
        public float AxeDamage; public int AxeCount; public float AxeSpeed;
    }

    public class AccLevelData
    {
        public int Level; public string Description;
        public int ValueInt; public float ValueFloat; 
    }

    public static class WeaponTable
    {
        // ── 무기 테이블 ──
        public static readonly WeaponLevelData[] Staff = new[] {
            new WeaponLevelData { Level=1, StaffDamage=15f, StaffCooldown=0.8f, StaffProjectileCount=1, Description="마법 투사체 발사" },
            new WeaponLevelData { Level=2, StaffDamage=22f, StaffCooldown=0.7f, StaffProjectileCount=1, Description="데미지 +7, 연사력 증가" },
            new WeaponLevelData { Level=3, StaffDamage=30f, StaffCooldown=0.6f, StaffProjectileCount=2, Description="투사체 2개 동시 발사!" },
            new WeaponLevelData { Level=4, StaffDamage=40f, StaffCooldown=0.5f, StaffProjectileCount=2, Description="강력한 데미지, 빠른 연사" },
            new WeaponLevelData { Level=5, StaffDamage=55f, StaffCooldown=0.35f,StaffProjectileCount=3, Description="투사체 3개! 최대 강화" },
        };
        public static readonly WeaponLevelData[] Garlic = new[] {
            new WeaponLevelData { Level=1, GarlicDamage=5f, GarlicRadius=70f, GarlicCooldown=0.5f, Description="주변 근접 적 지속 피해" },
            new WeaponLevelData { Level=2, GarlicDamage=8f, GarlicRadius=80f, GarlicCooldown=0.45f,Description="범위 & 데미지 소폭 증가" },
            new WeaponLevelData { Level=3, GarlicDamage=12f,GarlicRadius=95f, GarlicCooldown=0.4f, Description="더 넓은 범위 피해" },
            new WeaponLevelData { Level=4, GarlicDamage=18f,GarlicRadius=110f,GarlicCooldown=0.35f,Description="강력한 넉백 추가" },
            new WeaponLevelData { Level=5, GarlicDamage=28f,GarlicRadius=130f,GarlicCooldown=0.25f,Description="광역 피해 최대 강화" },
        };
        public static readonly WeaponLevelData[] Orbital = new[] {
            new WeaponLevelData { Level=1, OrbitalDamage=15f, OrbitalCount=2, OrbitalRadius=80f, OrbitalSpeed=3f, Description="구체 2개가 주위 선회" },
            new WeaponLevelData { Level=2, OrbitalDamage=20f, OrbitalCount=2, OrbitalRadius=85f, OrbitalSpeed=3.5f,Description="데미지 & 회전 속도 증가" },
            new WeaponLevelData { Level=3, OrbitalDamage=28f, OrbitalCount=3, OrbitalRadius=90f, OrbitalSpeed=4f, Description="구체 3개로 증가!" },
            new WeaponLevelData { Level=4, OrbitalDamage=38f, OrbitalCount=3, OrbitalRadius=100f,OrbitalSpeed=5f, Description="더 넓은 궤도, 빠른 회전" },
            new WeaponLevelData { Level=5, OrbitalDamage=55f, OrbitalCount=4, OrbitalRadius=110f,OrbitalSpeed=6f, Description="구체 4개! 최대 강화" },
        };
        public static readonly WeaponLevelData[] Axe = new[] {
            new WeaponLevelData { Level=1, AxeDamage=25f, AxeCount=1, AxeSpeed=400f, Description="포물선으로 떨어지는 도끼 투척" },
            new WeaponLevelData { Level=2, AxeDamage=35f, AxeCount=1, AxeSpeed=450f, Description="데미지 증가, 더 높이 투척" },
            new WeaponLevelData { Level=3, AxeDamage=45f, AxeCount=2, AxeSpeed=450f, Description="도끼 2개 연속 투척!" },
            new WeaponLevelData { Level=4, AxeDamage=60f, AxeCount=2, AxeSpeed=500f, Description="묵직한 데미지" },
            new WeaponLevelData { Level=5, AxeDamage=80f, AxeCount=3, AxeSpeed=500f, Description="도끼 3개 투척! 최대 강화" },
        };

        // ── 장신구 테이블 ──
        // 날개: Lv1=투사체 속도+20%(ValueFloat=1.2f), Lv2=이동속도+20(ValueFloat=20f, ValueInt=-1 플래그),
        //       Lv3~5=투사체 개수+n(ValueInt)
        // ApplyAccessory에서 ValueInt >= 0이면 투사체 보너스, == -1이면 이동속도, ValueFloat > 1이면 속도배율
        public static readonly AccLevelData[] Wings = new[] {
            // 버그3 수정: Lv1/2에 실제 효과값 부여
            new AccLevelData { Level=1, ValueInt=0, ValueFloat=1.2f, Description="투사체 속도 20% 증가" },
            new AccLevelData { Level=2, ValueInt=-1, ValueFloat=20f, Description="이동 속도 +20 증가" },
            new AccLevelData { Level=3, ValueInt=1, ValueFloat=1.0f, Description="모든 투사체 개수 +1" },
            new AccLevelData { Level=4, ValueInt=1, ValueFloat=1.0f, Description="투사체 개수 +1, 크기 증가" },
            new AccLevelData { Level=5, ValueInt=2, ValueFloat=1.0f, Description="모든 투사체 개수 +2" }
        };
        public static readonly AccLevelData[] Armor = new[] {
            new AccLevelData { Level=1, ValueFloat=20f, Description="최대 체력 20 증가 & 회복" },
            new AccLevelData { Level=2, ValueFloat=20f, Description="최대 체력 20 증가 & 회복" },
            new AccLevelData { Level=3, ValueFloat=20f, Description="최대 체력 20 증가 & 회복" },
            new AccLevelData { Level=4, ValueFloat=20f, Description="최대 체력 20 증가 & 회복" },
            new AccLevelData { Level=5, ValueFloat=50f, Description="최대 체력 50 대폭 증가!" }
        };
        public static readonly AccLevelData[] Ring = new[] {
            new AccLevelData { Level=1, ValueFloat=1.1f, Description="모든 무기 공격 범위 10% 증가" },
            new AccLevelData { Level=2, ValueFloat=1.2f, Description="모든 무기 공격 범위 20% 증가" },
            new AccLevelData { Level=3, ValueFloat=1.3f, Description="모든 무기 공격 범위 30% 증가" },
            new AccLevelData { Level=4, ValueFloat=1.4f, Description="모든 무기 공격 범위 40% 증가" },
            new AccLevelData { Level=5, ValueFloat=1.6f, Description="모든 무기 공격 범위 60% 증가!" }
        };
        public static readonly AccLevelData[] Glove = new[] {
            new AccLevelData { Level=1, ValueFloat=1.1f, Description="모든 무기 데미지 10% 증가" },
            new AccLevelData { Level=2, ValueFloat=1.2f, Description="모든 무기 데미지 20% 증가" },
            new AccLevelData { Level=3, ValueFloat=1.3f, Description="모든 무기 데미지 30% 증가" },
            new AccLevelData { Level=4, ValueFloat=1.4f, Description="모든 무기 데미지 40% 증가" },
            new AccLevelData { Level=5, ValueFloat=1.6f, Description="모든 무기 데미지 60% 증가!" }
        };

        public static WeaponLevelData GetWeapon(WeaponType type, int level)
        {
            int idx = System.Math.Clamp(level - 1, 0, 4);
            return type switch { WeaponType.Staff => Staff[idx], WeaponType.Garlic => Garlic[idx], WeaponType.Orbital => Orbital[idx], WeaponType.Axe => Axe[idx], _ => Staff[idx] };
        }
        public static AccLevelData GetAcc(AccessoryType type, int level)
        {
            int idx = System.Math.Clamp(level - 1, 0, 4);
            return type switch { AccessoryType.Wings => Wings[idx], AccessoryType.Armor => Armor[idx], AccessoryType.Ring => Ring[idx], AccessoryType.Glove => Glove[idx], _ => Wings[idx] };
        }
    }
}