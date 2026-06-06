// 파일명: GameCore.cs
using System;
using System.Collections.Generic;
using Raylib_cs;
using EntityGroup;
using CombatSystem;
using UpgradeLogic;

namespace GameCore
{
    public struct Vector2 { public float X; public float Y; public Vector2(float x, float y) { X = x; Y = y; } public static float Distance(Vector2 a, Vector2 b) { float dx = a.X - b.X; float dy = a.Y - b.Y; return (float)Math.Sqrt(dx * dx + dy * dy); } }
    public enum GameState { Title, Playing, LevelUp, GameOver, Victory }

    public class Engine
    {
        private Player _player; private List<Enemy> _enemies; private Weapon _weapon; private List<ExpGem> _gems; private LevelSystem _levelSystem;
        private GameState _currentState = GameState.Title;
        private float _spawnTimer = 0f;
        private float _survivalTime = 0f;
        private bool _boss1Spawned = false; 
        private bool _boss2Spawned = false; 

        private List<DamageText> _damageTexts; private Camera2D _camera; 
        private Texture2D _texIdle, _texTitleIdle, _texWalk, _texEnemy, _texFloor; 
        private List<Texture2D> _gemTextures = new List<Texture2D>();
        private string[] _gemFileNames = { "image/MonedaP.png", "image/MonedaD.png", "image/MonedaR.png", "image/spr_coin_gri.png", "image/spr_coin_strip4.png", "image/spr_coin_azu.png", "image/spr_coin_ama.png", "image/spr_coin_roj.png" };

        public Engine()
        {
            _player = new Player { Position = new Vector2(400, 300) }; _enemies = new List<Enemy>(); _weapon = new Weapon(); _gems = new List<ExpGem>(); _levelSystem = new LevelSystem(); _damageTexts = new List<DamageText>();
            _camera = new Camera2D(); _camera.Offset = new System.Numerics.Vector2(800f / 2f, 600f / 2f); _camera.Zoom = 1.0f;
        }

        public void Run()
        {
            Raylib.InitWindow(800, 600, "MONSTER SURVIVOR"); Raylib.SetTargetFPS(60);
            
            _texIdle = Raylib.LoadTexture("image/idle.png"); 
            _texTitleIdle = Raylib.LoadTexture("image/ups_idle.png"); 
            
            _texWalk = Raylib.LoadTexture("image/walk.png"); 
            _texEnemy = Raylib.LoadTexture("image/Basic 1x.png"); 
            _texFloor = Raylib.LoadTexture("image/floor.png"); 
            
            Raylib.SetTextureFilter(_texIdle, TextureFilter.Point);
            Raylib.SetTextureFilter(_texWalk, TextureFilter.Point);
            Raylib.SetTextureFilter(_texEnemy, TextureFilter.Point);
            Raylib.SetTextureFilter(_texTitleIdle, TextureFilter.Bilinear);

            foreach (var fileName in _gemFileNames) _gemTextures.Add(Raylib.LoadTexture(fileName));
            
            while (!Raylib.WindowShouldClose()) { Update(Raylib.GetFrameTime()); Render(); }
            
            Raylib.UnloadTexture(_texIdle); Raylib.UnloadTexture(_texTitleIdle); 
            Raylib.UnloadTexture(_texWalk); Raylib.UnloadTexture(_texEnemy); Raylib.UnloadTexture(_texFloor); foreach (var tex in _gemTextures) Raylib.UnloadTexture(tex); Raylib.CloseWindow();
        }

        private void Update(float dt)
        {
            if (_currentState == GameState.Title) { if (Raylib.IsKeyPressed(KeyboardKey.Enter)) _currentState = GameState.Playing; return; }
            if (_currentState == GameState.GameOver || _currentState == GameState.Victory) return;
            if (_currentState == GameState.LevelUp) { if (Raylib.IsKeyPressed(KeyboardKey.One)) { _player.Speed += 30f; ResumeGame(); } else if (Raylib.IsKeyPressed(KeyboardKey.Two)) { _weapon.Damage += 5; ResumeGame(); } else if (Raylib.IsKeyPressed(KeyboardKey.Three)) { _weapon.FireCooldown *= 0.8f; ResumeGame(); } return; }

            _survivalTime += dt;
            if (_survivalTime >= 300f) { _currentState = GameState.Victory; return; }

            _player.Update(dt); _camera.Target = new System.Numerics.Vector2(_player.Position.X, _player.Position.Y);
            foreach (var enemy in _enemies) { if (enemy.IsDead) continue; if (Vector2.Distance(_player.Position, enemy.Position) < 25.0f) { _player.CurrentHP -= enemy.Damage * dt; _player.HitTimer = 0.1f; } }
            if (_player.IsDead) { _player.CurrentHP = 0; _currentState = GameState.GameOver; return; }

            float currentSpawnDelay = Math.Max(0.2f, 0.8f - (_survivalTime / 300f) * 0.6f);
            _spawnTimer += dt;
            if (_spawnTimer >= currentSpawnDelay) 
            {
                _spawnTimer = 0f; Random rand = new Random();
                float spawnX = _player.Position.X + (rand.Next(0, 2) == 0 ? rand.Next(-450, -400) : rand.Next(400, 450));
                float spawnY = _player.Position.Y + (rand.Next(0, 2) == 0 ? rand.Next(-350, -300) : rand.Next(300, 450));
                _enemies.Add(new Enemy { Position = new Vector2(spawnX, spawnY), HP = 10 + (_survivalTime / 60f) * 5f }); 
            }

            if (_survivalTime >= 90f && !_boss1Spawned) { _boss1Spawned = true; _enemies.Add(new Enemy { Position = new Vector2(_player.Position.X + 450, _player.Position.Y), HP = 300, Damage = 20, Speed = 110, Scale = 6f, TintColor = Color.Purple, IsBoss = true }); }
            if (_survivalTime >= 180f && !_boss2Spawned) { _boss2Spawned = true; _enemies.Add(new Enemy { Position = new Vector2(_player.Position.X - 450, _player.Position.Y), HP = 800, Damage = 30, Speed = 130, Scale = 8f, TintColor = Color.DarkPurple, IsBoss = true }); }

            foreach (var enemy in _enemies) enemy.Update(dt, _player.Position);
            _weapon.Update(dt, _player, _enemies, _damageTexts);
            foreach (var text in _damageTexts) text.Update(dt); _damageTexts.RemoveAll(t => t.Timer >= t.Lifetime);

            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                if (_enemies[i].IsDead)
                {
                    Random rand = new Random(); int dropIndex = 3; 
                    if (_enemies[i].IsBoss) { dropIndex = (rand.Next(0, 2) == 0) ? 7 : 2; }
                    else { if (rand.Next(0, 100) < 10) { int coinRoll = rand.Next(0, 100); if (coinRoll < 70) dropIndex = 0; else if (coinRoll < 95) dropIndex = 1; else dropIndex = 2; } else { int expRoll = rand.Next(0, 100); if (expRoll < 60) dropIndex = 3; else if (expRoll < 85) dropIndex = 4; else if (expRoll < 97) dropIndex = 5; else dropIndex = 6; } }
                    _gems.Add(new ExpGem { Position = _enemies[i].Position, GemTypeIndex = dropIndex }); _enemies.RemoveAt(i);
                }
            }
            foreach (var gem in _gems) { gem.Update(dt); if (Vector2.Distance(_player.Position, gem.Position) < (gem.IsCoin ? 30f : 50f)) { gem.IsCollected = true; if (gem.IsCoin) _player.Gold += gem.GetValue(); else _levelSystem.AddExp(gem.GetValue()); } }
            _gems.RemoveAll(g => g.IsCollected);
            if (_levelSystem.IsLevelUpReady) _currentState = GameState.LevelUp;
        }

        private void ResumeGame() { _levelSystem.IsLevelUpReady = false; _currentState = GameState.Playing; }

        // 파일명: GameCore.cs (Render 메서드 렌더링 수정본)

        private void Render()
        {
            Raylib.BeginDrawing(); 
            if (_currentState == GameState.Title)
            {
                Raylib.ClearBackground(new Color(20, 20, 35, 255));
                if (_texTitleIdle.Width > 0)
                {
                    int currentTitleFrame = (int)(Raylib.GetTime() * 10) % 10; 
                    float frameWidth = _texTitleIdle.Width / 10f; float frameHeight = _texTitleIdle.Height;
                    Rectangle sourceRec = new Rectangle(currentTitleFrame * frameWidth, 0, frameWidth, frameHeight);
                    float scale = 1.5f; 
                    System.Numerics.Vector2 origin = new System.Numerics.Vector2((frameWidth * scale) / 2, (frameHeight * scale) / 2);
                    Raylib.DrawTexturePro(_texTitleIdle, sourceRec, new Rectangle(400 + 15, 300 + 15, frameWidth * scale, frameHeight * scale), origin, 0f, new Color(0, 0, 0, 150));
                    Raylib.DrawTexturePro(_texTitleIdle, sourceRec, new Rectangle(400, 300, frameWidth * scale, frameHeight * scale), origin, 0f, Color.White);
                }
                Raylib.DrawText("ASDF SURVIVOR", 140, 50, 60, Color.Gold); 
                if ((int)(Raylib.GetTime() * 2) % 2 == 0) Raylib.DrawText("- Press ENTER to Start -", 220, 500, 28, Color.White);
                Raylib.EndDrawing(); return;
            }

            Raylib.ClearBackground(Color.Black); Raylib.BeginMode2D(_camera);
            if (_texFloor.Width > 0 && _texFloor.Height > 0) { int tileW = _texFloor.Width; int tileH = _texFloor.Height; float startX = (float)Math.Floor((_camera.Target.X - 400) / tileW) * tileW; float startY = (float)Math.Floor((_camera.Target.Y - 300) / tileH) * tileH; for (float x = startX; x < _camera.Target.X + 400 + tileW; x += tileW) for (float y = startY; y < _camera.Target.Y + 300 + tileH; y += tileH) Raylib.DrawTexture(_texFloor, (int)x, (int)y, Color.White); }
            foreach (var gem in _gems) { Texture2D gemTex = _gemTextures[gem.GemTypeIndex]; if (gemTex.Width > 0) { float frameWidth = (float)gemTex.Width / gem.MaxFrames; Rectangle sourceRec = new Rectangle(gem.CurrentFrame * frameWidth, 0, frameWidth, gemTex.Height); Rectangle destRec = new Rectangle(gem.Position.X, gem.Position.Y, frameWidth, gemTex.Height); System.Numerics.Vector2 origin = new System.Numerics.Vector2(frameWidth / 2, gemTex.Height / 2); Raylib.DrawTexturePro(gemTex, sourceRec, destRec, origin, 0f, Color.White); } else Raylib.DrawCircle((int)gem.Position.X, (int)gem.Position.Y, 5, Color.SkyBlue); }
            
            // ★ 무기 시각 효과 렌더링
            if (_weapon.HasGarlic) Raylib.DrawCircle((int)_player.Position.X, (int)_player.Position.Y, _weapon.GarlicRadius, new Color(150, 255, 150, 80));
            if (_weapon.HasOrbital) 
            { 
                for (int i = 0; i < _weapon.OrbitalCount; i++) 
                { 
                    float currentAngle = _weapon.OrbitalAngle + (i * ((float)Math.PI * 2 / _weapon.OrbitalCount)); 
                    int orbX = (int)(_player.Position.X + Math.Cos(currentAngle) * _weapon.OrbitalRadius); 
                    int orbY = (int)(_player.Position.Y + Math.Sin(currentAngle) * _weapon.OrbitalRadius); 
                    Raylib.DrawCircle(orbX, orbY, 8, new Color(0, 255, 255, 255)); // 궤도 구체
                } 
            }
            foreach (var p in _weapon.Projectiles) Raylib.DrawCircle((int)p.Position.X, (int)p.Position.Y, 5, Color.Yellow);
            
            foreach (var e in _enemies) { if (_texEnemy.Width > 0) { float frameWidth = (float)_texEnemy.Width / 5; float frameHeight = (float)_texEnemy.Height / 3; Rectangle sourceRec = new Rectangle(0, 0, frameWidth, frameHeight); Rectangle destRec = new Rectangle(e.Position.X, e.Position.Y, frameWidth * e.Scale, frameHeight * e.Scale); System.Numerics.Vector2 origin = new System.Numerics.Vector2((frameWidth * e.Scale) / 2, (frameHeight * e.Scale) / 2); Color renderColor = (e.HitTimer > 0) ? Color.Red : e.TintColor; Raylib.DrawTexturePro(_texEnemy, sourceRec, destRec, origin, 0f, renderColor); } else Raylib.DrawRectangle((int)e.Position.X - 10, (int)e.Position.Y - 10, 20, 20, Color.Red); }
            
            if (_texIdle.Width > 0 && _texWalk.Width > 0) { Texture2D currentTex = _player.IsMoving ? _texWalk : _texIdle; int maxFrames = _player.IsMoving ? 24 : 10; int cols = _player.IsMoving ? 4 : 10; int rows = _player.IsMoving ? 6 : 1; int currentFrameNum = _player.CurrentFrame % maxFrames; float frameWidth = (float)currentTex.Width / cols; float frameHeight = (float)currentTex.Height / rows; float sourceX = (currentFrameNum % cols) * frameWidth; float sourceY = (currentFrameNum / cols) * frameHeight; float renderWidth = _player.IsFacingLeft ? frameWidth : -frameWidth; Rectangle sourceRec = new Rectangle(sourceX, sourceY, renderWidth, frameHeight); Rectangle destRec = new Rectangle(_player.Position.X, _player.Position.Y, frameWidth * 1.5f, frameHeight * 1.5f); System.Numerics.Vector2 origin = new System.Numerics.Vector2((frameWidth * 1.5f) / 2, (frameHeight * 1.5f) / 2); Color playerColor = (_player.IsDead || _player.HitTimer > 0) ? Color.Red : Color.White; Raylib.DrawTexturePro(currentTex, sourceRec, destRec, origin, 0f, playerColor); } else Raylib.DrawCircle((int)_player.Position.X, (int)_player.Position.Y, 15, Color.Blue);

            float hpRatio = _player.CurrentHP / _player.MaxHP; int barWidth = 40; int barHeight = 6; int barX = (int)_player.Position.X - (barWidth / 2); 
            int barY = (int)_player.Position.Y + 50; 
            Raylib.DrawRectangle(barX, barY, barWidth, barHeight, Color.DarkGray); Raylib.DrawRectangle(barX, barY, (int)(barWidth * hpRatio), barHeight, Color.Red); 
            foreach (var text in _damageTexts) Raylib.DrawText(text.Damage.ToString(), (int)text.Position.X - 10, (int)text.Position.Y - 20, 20, Color.Yellow);
            Raylib.EndMode2D();
            // ... (하단 HUD 및 상태창 코드는 기존과 동일)

            float expRatio = (float)_levelSystem.CurrentExp / _levelSystem.MaxExp;
            Raylib.DrawRectangle(0, 0, 800, 20, Color.Black); Raylib.DrawRectangle(0, 0, (int)(800 * expRatio), 20, Color.Blue);
            Raylib.DrawText($"LV: {_levelSystem.Level}", 10, 25, 20, Color.White); Raylib.DrawText($"ATK: {_weapon.Damage}", 10, 50, 15, Color.LightGray);
            Raylib.DrawText($"GOLD: {_player.Gold}", 650, 25, 20, Color.Gold);
            int minutes = (int)_survivalTime / 60; int seconds = (int)_survivalTime % 60; string timeString = $"{minutes:D2}:{seconds:D2}";
            Raylib.DrawText(timeString, 360, 25, 28, Color.White);

            if (_currentState == GameState.LevelUp) { Raylib.DrawRectangle(0, 0, 800, 600, new Color(0, 0, 0, 150)); Raylib.DrawText("LEVEL UP! (1, 2, 3)", 300, 200, 30, Color.Gold); Raylib.DrawRectangle(100, 280, 180, 100, Color.DarkBlue); Raylib.DrawText("[1] Speed +", 135, 320, 20, Color.White); Raylib.DrawRectangle(310, 280, 180, 100, Color.DarkPurple); Raylib.DrawText("[2] Damage +", 345, 320, 20, Color.White); Raylib.DrawRectangle(520, 280, 180, 100, Color.DarkGreen); Raylib.DrawText("[3] Atk Speed +", 540, 320, 20, Color.White); }
            if (_currentState == GameState.GameOver) { Raylib.DrawRectangle(0, 0, 800, 600, new Color(150, 0, 0, 200)); Raylib.DrawText("YOU DIED", 260, 240, 60, Color.Red); Raylib.DrawText("Game Over", 340, 320, 24, Color.LightGray); }
            if (_currentState == GameState.Victory) { Raylib.DrawRectangle(0, 0, 800, 600, new Color(0, 100, 255, 200)); Raylib.DrawText("VICTORY!", 260, 240, 60, Color.Gold); Raylib.DrawText($"Survived 5 Minutes / Gold Earned: {_player.Gold}", 180, 320, 24, Color.White); }
            Raylib.EndDrawing();
        }
    }
}