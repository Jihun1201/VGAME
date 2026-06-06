// 파일명: FieldItem.cs
// 역할: 맵 위에 존재하는 모든 상호작용 오브젝트 정의
//   - DropItem  : 적 처치 시 확률 드랍 (음식, 자석, 방패 포션)
//   - MapChest  : 맵 랜덤 스폰 보물상자 → 접근 시 DropItem 방출

using System;
using Raylib_cs;
using GameCore;

namespace FieldItem
{
    // ──────────────────────────────────────────────────────────────
    // 드랍 아이템 종류
    // ──────────────────────────────────────────────────────────────
    public enum DropItemType
    {
        Food,       // 체력 회복 (초록 사각형 → 나중에 item_food.png)
        Magnet,     // 맵 전체 경험치/골드 흡수 (파랑 마름모 → item_magnet.png)
        Shield,     // 3초 무적 포션 (노랑 원 → item_shield.png)
    }

    // ──────────────────────────────────────────────────────────────
    // 드랍 아이템
    // ──────────────────────────────────────────────────────────────
    public class DropItem
    {
        public Vector2      Position;
        public DropItemType Type;
        public bool         IsCollected = false;

        // 동동 떠오르는 애니메이션용
        public float BobTimer = 0f;

        // 픽업 범위
        public float PickupRadius => 25f;

        public void Update(float dt)
        {
            BobTimer += dt;
        }

        // 렌더링 (도형 placeholder)
        public void Draw()
        {
            // 위아래로 살짝 떠다니는 오프셋
            float bobOffset = (float)Math.Sin(BobTimer * 3f) * 3f;
            int x = (int)Position.X;
            int y = (int)(Position.Y + bobOffset);

            switch (Type)
            {
                case DropItemType.Food:
                    // 초록 사각형 (나중에 item_food.png 로 교체)
                    Raylib.DrawRectangle(x - 8, y - 8, 16, 16, new Color(80, 220, 80, 255));
                    Raylib.DrawRectangleLines(x - 8, y - 8, 16, 16, new Color(40, 160, 40, 255));
                    Raylib.DrawText("+", x - 4, y - 8, 16, Color.White);
                    break;

                case DropItemType.Magnet:
                    // 파랑 마름모 (나중에 item_magnet.png 로 교체)
                    DrawDiamond(x, y, 10, new Color(60, 160, 255, 255));
                    Raylib.DrawText("M", x - 4, y - 7, 13, Color.White);
                    break;

                case DropItemType.Shield:
                    // 노랑 원 (나중에 item_shield.png 로 교체)
                    Raylib.DrawCircle(x, y, 9, new Color(255, 220, 40, 255));
                    Raylib.DrawCircleLines(x, y, 9, new Color(200, 160, 0, 255));
                    Raylib.DrawText("S", x - 4, y - 7, 13, Color.Black);
                    break;
            }
        }

        private void DrawDiamond(int cx, int cy, int r, Color color)
        {
            // 마름모: 위/오른/아래/왼 꼭짓점
            var top   = new System.Numerics.Vector2(cx,     cy - r);
            var right = new System.Numerics.Vector2(cx + r, cy    );
            var bot   = new System.Numerics.Vector2(cx,     cy + r);
            var left  = new System.Numerics.Vector2(cx - r, cy    );
            Raylib.DrawTriangle(top, left,  bot,   color);
            Raylib.DrawTriangle(top, bot,   right, color);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // 맵 상자 상태
    // ──────────────────────────────────────────────────────────────
    public enum ChestState { Closed, Opening, Done }

    // ──────────────────────────────────────────────────────────────
    // 맵 보물상자
    // ──────────────────────────────────────────────────────────────
    // ──────────────────────────────────────────────────────────────
    // 맵 보물상자 (FieldItem.cs 내부)
    // ──────────────────────────────────────────────────────────────
    public class MapChest
    {
        public Vector2    Position;
        public ChestState State     = ChestState.Closed;
        public float      OpenTimer = 0f;          
        public float      OpenDelay = 0.4f;        
        public bool       IsDone    => State == ChestState.Done;

        public float TriggerRadius => 30f;

        // ★ 기존의 SpawnedItems 리스트와 흩뿌리는 로직 완전히 삭제됨

        public void Update(float dt)
        {
            if (State == ChestState.Opening)
            {
                OpenTimer += dt;
                if (OpenTimer >= OpenDelay) State = ChestState.Done;
            }
        }

        public void Open()
        {
            if (State != ChestState.Closed) return;
            State = ChestState.Opening;
            // 상자가 열리는 애니메이션 시작! (아이템 방출은 GameCore가 UI로 처리함)
        }

        public void Draw()
        {
            int x = (int)Position.X; int y = (int)Position.Y;
            if (State == ChestState.Closed)
            {
                Raylib.DrawRectangle(x - 14, y - 12, 28, 24, new Color(139, 90, 43, 255));
                Raylib.DrawRectangleLines(x - 14, y - 12, 28, 24, new Color(80, 50, 20, 255));
                Raylib.DrawRectangle(x - 4, y - 4, 8, 8, new Color(220, 180, 50, 255));
                Raylib.DrawRectangleLines(x - 15, y - 13, 30, 26, new Color(255, 220, 80, 180));
            }
            else if (State == ChestState.Opening)
            {
                float openRatio = OpenTimer / OpenDelay;
                int lidOffset = (int)(openRatio * 8f);
                Raylib.DrawRectangle(x - 14, y - 4, 28, 16, new Color(139, 90, 43, 255));
                Raylib.DrawRectangle(x - 14, y - 12 - lidOffset, 28, 10, new Color(160, 110, 55, 255));
                Raylib.DrawRectangleLines(x - 14, y - 12 - lidOffset, 28, 10, new Color(80, 50, 20, 255));
                Raylib.DrawCircle(x, y, (int)(openRatio * 20f), new Color((byte)255, (byte)230, (byte)100, (byte)(int)(80 * (1f - openRatio))));
            }
        }
    }
}