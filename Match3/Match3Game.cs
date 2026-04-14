using System.Text.RegularExpressions;
using Match3Easter.Models;
using Match3Easter.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Match = Match3Easter.Models.Match;

namespace Match3Easter;

public class Match3Game : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private Texture2D _pixel;
    private Board _board;


    private GameState _state = GameState.Idle;
    private ScreenState _screenState = ScreenState.MainMenu;

    private Dictionary<GemType, Texture2D> _gemTextures;
    private Dictionary<(GemType, BonusType), Texture2D> _bonusGemTextures;
    private Dictionary<DestroyerType, Texture2D> _destroyerTextures;
    private Rectangle _playButtonRect;
    private Texture2D _playButtonTexture;
    private Rectangle _newGameButtonRect;
    private Texture2D _newGameButtonTexture;
    private Texture2D _gameOverTexture;
    private Texture2D _yourScoreTexture;
    private Texture2D _scoreTexture;
    private Texture2D _timeTexture;

    private Texture2D _digitTexture;
    private const int DigitWidth = 32;
    private const int DigitHeight = 48;


    private readonly List<GemAnimation> _gemAnimations = [];
    private readonly List<DestroyerAnimation> _destroyerAnimations = [];

    private float _animTimer;
    private const float AnimDuration = 0.25f;
    private const float RemovalAnimDuration = 0.1f;
    private const float ActivationDuration = 0.25f;

    private readonly Dictionary<(int x, int y), float> _removalStartTimes = new();
    private float _lastRemovalTime;
    private readonly HashSet<(int x, int y)> _processedActivations = new();


    private MouseState _previousMouseState;
    private (int x, int y)? _selected = null;

    private int _swapX1, _swapY1, _swapX2, _swapY2;

    private const int GridHeight = 8;
    private const int CellSize = 64;
    private const int HudHeight = 80;
    private const int BoardOffsetY = 0;
    private const int BoardOffsetX = 0;
    private const int WindowWidth = GridHeight * CellSize;
    private const int WindowHeight = GridHeight * CellSize + HudHeight;

    private const float _gameDuration = 5f;
    private float _timeRemaining = _gameDuration;

    public Match3Game()
    {
        _graphics = new GraphicsDeviceManager(this);
        _graphics.PreferredBackBufferWidth = WindowWidth;
        _graphics.PreferredBackBufferHeight = WindowHeight;
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        // TODO: Add your initialization logic here (e.g., non-content variables)
        _board = new Board();
        base.Initialize();
    }

    protected void StartNewGame()
    {
        _board = new Board();
        _state = GameState.Idle;
        _animTimer = 0f;
        _timeRemaining = _gameDuration;
    }

    protected override void LoadContent()
    {
        _pixel = new Texture2D(_graphics.GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);

        _spriteBatch = new SpriteBatch(GraphicsDevice);

        // TODO: use this.Content to load your game content here

        _gemTextures = new Dictionary<GemType, Texture2D>
        {
            [GemType.Circle] = Texture2D.FromFile(GraphicsDevice, "Assets/circle.png"),
            [GemType.Diamond] = Texture2D.FromFile(GraphicsDevice, "Assets/diamond.png"),
            [GemType.Square] = Texture2D.FromFile(GraphicsDevice, "Assets/rectangle.png"),
            [GemType.Pentagon] =
                Texture2D.FromFile(GraphicsDevice, "Assets/pentagon.png"), // Pentagon -> hexagon.png
            [GemType.Triangle] =
                Texture2D.FromFile(GraphicsDevice, "Assets/triangle.png"), // Triangle -> bipyramid.png
        };
        _bonusGemTextures = new Dictionary<(GemType, BonusType), Texture2D>
        {
            [(GemType.Circle, BonusType.Bomb)] =
                Texture2D.FromFile(GraphicsDevice, "Assets/circleBomb.png"),
            [(GemType.Circle, BonusType.LineHorizontal)] =
                Texture2D.FromFile(GraphicsDevice, "Assets/circleLineH.png"),
            [(GemType.Circle, BonusType.LineVertical)] =
                Texture2D.FromFile(GraphicsDevice, "Assets/circleLineV.png"),
            [(GemType.Diamond, BonusType.Bomb)] =
                Texture2D.FromFile(GraphicsDevice, "Assets/diamondBomb.png"),
            [(GemType.Diamond, BonusType.LineHorizontal)] =
                Texture2D.FromFile(GraphicsDevice, "Assets/diamondLineH.png"),
            [(GemType.Diamond, BonusType.LineVertical)] =
                Texture2D.FromFile(GraphicsDevice, "Assets/diamondLineV.png"),
            [(GemType.Pentagon, BonusType.Bomb)] =
                Texture2D.FromFile(GraphicsDevice, "Assets/pentagonBomb.png"),
            [(GemType.Pentagon, BonusType.LineHorizontal)] =
                Texture2D.FromFile(GraphicsDevice, "Assets/pentagonLineH.png"),
            [(GemType.Pentagon, BonusType.LineVertical)] =
                Texture2D.FromFile(GraphicsDevice, "Assets/pentagonLineV.png"),
            [(GemType.Square, BonusType.Bomb)] =
                Texture2D.FromFile(GraphicsDevice, "Assets/rectangleBomb.png"),
            [(GemType.Square, BonusType.LineHorizontal)] =
                Texture2D.FromFile(GraphicsDevice, "Assets/rectangleLineH.png"),
            [(GemType.Square, BonusType.LineVertical)] =
                Texture2D.FromFile(GraphicsDevice, "Assets/rectangleLineV.png"),
            [(GemType.Triangle, BonusType.Bomb)] =
                Texture2D.FromFile(GraphicsDevice, "Assets/triangleBomb.png"),
            [(GemType.Triangle, BonusType.LineHorizontal)] =
                Texture2D.FromFile(GraphicsDevice, "Assets/triangleLineH.png"),
            [(GemType.Triangle, BonusType.LineVertical)] =
                Texture2D.FromFile(GraphicsDevice, "Assets/triangleLineV.png"),
        };
        _destroyerTextures = new Dictionary<DestroyerType, Texture2D>
        {
            [DestroyerType.West] = Texture2D.FromFile(GraphicsDevice, "Assets/destroyerWest.png"),
            [DestroyerType.East] = Texture2D.FromFile(GraphicsDevice, "Assets/destroyerEast.png"),
            [DestroyerType.South] = Texture2D.FromFile(GraphicsDevice, "Assets/destroyerSouth.png"),
            [DestroyerType.North] = Texture2D.FromFile(GraphicsDevice, "Assets/destroyerNorth.png"),
        };

        _playButtonRect = new Rectangle(
            GraphicsDevice.Viewport.Width / 2 - 100,
            GraphicsDevice.Viewport.Height / 2 - 30,
            200, 60);

        _playButtonTexture = Texture2D.FromFile(GraphicsDevice, "Assets/playButton.png");

        _newGameButtonRect = new Rectangle(GraphicsDevice.Viewport.Width / 2 - 100,
            GraphicsDevice.Viewport.Height / 2 - 30,
            200, 60);

        _newGameButtonTexture = Texture2D.FromFile(GraphicsDevice, "Assets/newGameButton.png");
        _gameOverTexture = Texture2D.FromFile(GraphicsDevice, "Assets/gameOver.png");
        _scoreTexture = Texture2D.FromFile(GraphicsDevice, "Assets/score.png");
        _timeTexture = Texture2D.FromFile(GraphicsDevice, "Assets/time.png");
        _digitTexture = Texture2D.FromFile(GraphicsDevice, "Assets/digits.png");
        _yourScoreTexture = Texture2D.FromFile(GraphicsDevice, "Assets/yourScore.png");
    }

    private static Vector2 CellToScreen(int x, int y)
        => new(BoardOffsetX + x * CellSize, BoardOffsetY + y * CellSize);

    private static (int x, int y) ScreenToCell(int screenX, int screenY)
        => new((screenX - BoardOffsetX) / CellSize, (screenY - BoardOffsetY) / CellSize);

    // TODO: REWRITE WITH STATES
    private void OnCellClicked(int x, int y)
    {
        if (_selected is null)
        {
            _selected = (x, y);
            return;
        }

        var (sx, sy) = _selected.Value;
        _selected = null;

        if (!Board.IsSwappable(sx, sy, x, y))
        {
            return;
        }

        _swapX1 = sx;
        _swapY1 = sy;
        _swapX2 = x;
        _swapY2 = y;


        EnterGameState(GameState.Swapping);
    }

    // TODO: REWRITE WITH STATES
    private void HandleInput()
    {
        var mouse = Mouse.GetState();
        if (_previousMouseState.LeftButton == ButtonState.Pressed && mouse.LeftButton == ButtonState.Released)
        {
            var (x, y) = ScreenToCell(mouse.X, mouse.Y);

            if (x is >= 0 and < GridHeight && y is >= 0 and < GridHeight)
                OnCellClicked(x, y);
        }

        _previousMouseState = mouse;
    }

    private static float ComputeRadialDelay(int x, int y, int originX, int originY)
    {
        var dist = MathF.Sqrt(MathF.Pow(x - originX, 2) + MathF.Pow(y - originY, 2));
        return dist * RemovalAnimDuration;
    }

    private static float ComputeLineDelay(int x, int y, int originX, int originY)
    {
        var dist = Math.Max(Math.Abs(x - originX), Math.Abs(y - originY));
        return dist * RemovalAnimDuration;
    }

    private float CalculateLocalDelay(DestructionCause cause, int x, int y, int originX, int originY)
    {
        return (cause) switch
        {
            DestructionCause.Bomb or DestructionCause.DoubleBomb => ComputeRadialDelay(x, y, originX, originY),
            DestructionCause.LineHorizontal
                or DestructionCause.LineCross
                or DestructionCause.LineVertical
                => ComputeLineDelay(x, y, originX, originY),
            _ => 0f
        };
    }

    private void EnterScreenState(ScreenState state)
    {
    }

    private void EnterGameState(GameState newState)
    {
        _state = newState;
        _animTimer = 0f;

        switch (newState)
        {
            case GameState.Swapping or GameState.SwappingBack:
                _gemAnimations.Clear();

                _gemAnimations.Add(new GemAnimation // CALL BEFORE SWAP -> tile will be on FROM_POSITION
                {
                    From = CellToScreen(_swapX1, _swapY1),
                    To = CellToScreen(_swapX2, _swapY2),
                    GridX = _swapX1, GridY = _swapY1
                });

                _gemAnimations.Add(new GemAnimation // CALL BEFORE SWAP -> tile will be on FROM_POSITION
                {
                    From = CellToScreen(_swapX2, _swapY2),
                    To = CellToScreen(_swapX1, _swapY1),
                    GridX = _swapX2, GridY = _swapY2
                });
                break;

            case GameState.CreateBonuses:
                _gemAnimations.Clear();

                foreach (var (toX, toY, fromX, fromY) in
                         _board.CalcBonusAnimPositions(_swapX1, _swapY1, _swapX2, _swapY2))
                    _gemAnimations.Add(new GemAnimation // CALL BEFORE SWAP -> tile will be on FROM_POSITION
                    {
                        From = CellToScreen(fromX, fromY),
                        To = CellToScreen(toX, toY),
                        GridX = fromX, GridY = fromY
                    });

                break;

            case GameState.Removing:
                _removalStartTimes.Clear();

                _lastRemovalTime = 0f;
                _processedActivations.Clear();

                // while (_board._destructionEventQueue.TryDequeue(out var ev))
                while (_board.TryDequeueDestructionEvent(out var ev))
                {
                    var triggerTime = ev.BaseTime + CalculateLocalDelay(ev.Cause, ev.X, ev.Y, ev.XOrigin, ev.YOrigin);

                    if (triggerTime > _lastRemovalTime) _lastRemovalTime = triggerTime;

                    if (_removalStartTimes.TryGetValue((ev.X, ev.Y), out var existing))
                        triggerTime = Math.Min(existing, triggerTime);

                    _removalStartTimes[(ev.X, ev.Y)] = triggerTime;

                    var gem = _board.GetGem(ev.X, ev.Y);
                    if (gem is null) continue;

                    if (gem._getBonusType() == BonusType.None || _processedActivations.Contains((ev.X, ev.Y)) ||
                        _board.IsProtectedForRemoval(ev.X, ev.Y)) continue;

                    _processedActivations.Add((ev.X, ev.Y));

                    var cascadeBaseTime = triggerTime + ActivationDuration;

                    var bonusActivator = _board.ResolveBonusActivator(gem._getBonusType());

                    switch (gem._getBonusType())
                    {
                        case BonusType.LineHorizontal:
                            //West destroyer
                            _destroyerAnimations.Add(new DestroyerAnimation
                            {
                                LaunchTime = cascadeBaseTime,
                                Type = DestroyerType.West,
                                From = CellToScreen(ev.X, ev.Y),
                                To = CellToScreen(-1, ev.Y),
                                TravelDuration = (ev.X + 1) * RemovalAnimDuration
                            });
                            // East destroyer
                            _destroyerAnimations.Add(new DestroyerAnimation
                            {
                                LaunchTime = cascadeBaseTime,
                                Type = DestroyerType.East,
                                From = CellToScreen(ev.X, ev.Y),
                                To = CellToScreen(_board.GetXSize(), ev.Y),
                                TravelDuration = (_board.GetXSize() - ev.X) * RemovalAnimDuration
                            });
                            break;
                        case BonusType.LineVertical:
                            //North destroyer
                            _destroyerAnimations.Add(new DestroyerAnimation
                            {
                                LaunchTime = cascadeBaseTime,
                                Type = DestroyerType.North,
                                From = CellToScreen(ev.X, ev.Y),
                                To = CellToScreen(ev.X, -1),
                                TravelDuration = (ev.Y + 1) * RemovalAnimDuration
                            });

                            // South destroyer
                            _destroyerAnimations.Add(new DestroyerAnimation
                            {
                                LaunchTime = cascadeBaseTime,
                                Type = DestroyerType.South,
                                From = CellToScreen(ev.X, ev.Y),
                                To = CellToScreen(ev.X, _board.GetYSize()),
                                TravelDuration = (_board.GetYSize() - ev.Y) * RemovalAnimDuration
                            });
                            break;

                        case BonusType.Cross:
                            // To North
                            _destroyerAnimations.Add(new DestroyerAnimation
                            {
                                LaunchTime = cascadeBaseTime,
                                Type = DestroyerType.North,
                                From = CellToScreen(ev.X, ev.Y),
                                To = CellToScreen(ev.X, -1),
                                TravelDuration = (ev.Y + 1) * RemovalAnimDuration
                            });


                            // To West
                            _destroyerAnimations.Add(new DestroyerAnimation
                            {
                                LaunchTime = cascadeBaseTime,
                                Type = DestroyerType.West,
                                From = CellToScreen(ev.X, ev.Y),
                                To = CellToScreen(-1, ev.Y),
                                TravelDuration = (ev.X + 1) * RemovalAnimDuration
                            });

                            // To South
                            _destroyerAnimations.Add(new DestroyerAnimation
                            {
                                LaunchTime = cascadeBaseTime,
                                Type = DestroyerType.South,
                                From = CellToScreen(ev.X, ev.Y),
                                To = CellToScreen(ev.X, _board.GetYSize()),
                                TravelDuration = (_board.GetYSize() - ev.Y) * RemovalAnimDuration
                            });

                            // To East
                            _destroyerAnimations.Add(new DestroyerAnimation
                            {
                                LaunchTime = cascadeBaseTime,
                                Type = DestroyerType.East,
                                From = CellToScreen(ev.X, ev.Y),
                                To = CellToScreen(_board.GetXSize(), ev.Y),
                                TravelDuration = (_board.GetXSize() - ev.X) * RemovalAnimDuration
                            });
                            break;
                        case BonusType.TripleLineVertical:
                            for (var i = ev.X - 1; i <= ev.X + 1; i++)
                            {
                                if (!_board.InBounds(i, ev.Y)) continue;
                                _destroyerAnimations.Add(new DestroyerAnimation
                                {
                                    LaunchTime = cascadeBaseTime,
                                    Type = DestroyerType.North,
                                    From = CellToScreen(i, ev.Y),
                                    To = CellToScreen(i, -1),
                                    TravelDuration = (ev.Y + 1) * RemovalAnimDuration
                                });

                                // South destroyer
                                _destroyerAnimations.Add(new DestroyerAnimation
                                {
                                    LaunchTime = cascadeBaseTime,
                                    Type = DestroyerType.South,
                                    From = CellToScreen(i, ev.Y),
                                    To = CellToScreen(i, _board.GetYSize()),
                                    TravelDuration = (_board.GetYSize() - ev.Y) * RemovalAnimDuration
                                });
                            }

                            break;
                        case BonusType.TripleLineHorizontal:
                            for (var j = ev.Y - 1; j <= ev.Y + 1; j++)
                            {
                                if (!_board.InBounds(ev.X, j)) continue;
                                //West destroyer
                                _destroyerAnimations.Add(new DestroyerAnimation
                                {
                                    LaunchTime = cascadeBaseTime,
                                    Type = DestroyerType.West,
                                    From = CellToScreen(ev.X, j),
                                    To = CellToScreen(-1, j),
                                    TravelDuration = (ev.X + 1) * RemovalAnimDuration
                                });
                                // East destroyer
                                _destroyerAnimations.Add(new DestroyerAnimation
                                {
                                    LaunchTime = cascadeBaseTime,
                                    Type = DestroyerType.East,
                                    From = CellToScreen(ev.X, j),
                                    To = CellToScreen(_board.GetXSize(), j),
                                    TravelDuration = (_board.GetXSize() - ev.X) * RemovalAnimDuration
                                });
                            }

                            break;
                    }

                    bonusActivator!(ev.X, ev.Y, cascadeBaseTime);
                }

                break;

            case GameState.Falling:

                _gemAnimations.Clear();
                // собрать анимации ДО перемещения данных
                var fallData = _board.CalcFallPositions();
                foreach (var (fx, fromY, toY) in fallData)
                {
                    _gemAnimations.Add(new GemAnimation
                    {
                        From = CellToScreen(fx, fromY),
                        To = CellToScreen(fx, toY),
                        GridX = fx, GridY = fromY // рисуем до падения -> фишка будет на fromY
                    });
                }

                break;

            case GameState.Filling:

                _board.FillCols(); // CALLING BEFORE ANIMATION

                _gemAnimations.Clear();

                var fillData = _board.CalcFillPositions();
                foreach (var (fx, fromY, toY) in fillData)
                {
                    _gemAnimations.Add(new GemAnimation
                    {
                        From = CellToScreen(fx, fromY),
                        To = CellToScreen(fx, toY),
                        GridX = fx, GridY = toY // рисуем после падения -> фишка будет на toY
                    });
                }

                break;
        }
    }


    protected override void Update(GameTime gameTime)
    {
        // Exit game if Back button (Controller) or Escape (Keyboard) is pressed
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
            Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        if (_state == GameState.Idle && _screenState == ScreenState.Playing)
        {
            _timeRemaining -= (float)gameTime.ElapsedGameTime.TotalSeconds;
        }

        if (_timeRemaining <= 0)
        {
            _timeRemaining = 0f;
            _screenState = ScreenState.GameOver;
        }
        // TODO: Add your update logic here (input handling, physics, etc.)

        switch (_screenState)
        {
            case ScreenState.MainMenu:
                UpdateMainMenu(gameTime);
                break;

            case ScreenState.Playing:
                UpdateGame(gameTime);
                break;

            case ScreenState.GameOver:
                UpdateGameOver(gameTime);
                break;
        }

        _previousMouseState = Mouse.GetState();
        base.Update(gameTime);
    }

    protected void UpdateMainMenu(GameTime gameTime)
    {
        var mouse = Mouse.GetState();
        bool clicked = mouse.LeftButton == ButtonState.Released
                       && _previousMouseState.LeftButton == ButtonState.Pressed;

        if (clicked && _playButtonRect.Contains(mouse.Position))
        {
            StartNewGame();
            _screenState = ScreenState.Playing;
        }
    }

    protected void UpdateGameOver(GameTime gameTime)
    {
        var mouse = Mouse.GetState();
        bool clicked = mouse.LeftButton == ButtonState.Released
                       && _previousMouseState.LeftButton == ButtonState.Pressed;

        if (clicked && _newGameButtonRect.Contains(mouse.Position))
        {
            // StartNewGame();
            _screenState = ScreenState.MainMenu;
        }
    }

    protected void UpdateGame(GameTime gameTime)
    {
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        switch (_state)
        {
            //Wait for input
            case GameState.Idle:
                _animTimer += dt;
                if (_animTimer >= AnimDuration)
                {
                    HandleInput();
                }

                break;

            // Wait till Swap ANIM will end then go to matching or SwapBack
            case GameState.Swapping:
                _animTimer += dt;
                if (_animTimer >= AnimDuration)
                {
                    _board.Swap(_swapX1, _swapY1, _swapX2, _swapY2);
                    _board.MarkDirty(_swapX1, _swapY1);
                    _board.MarkDirty(_swapX2, _swapY2);
                    EnterGameState(_board.FindMatches() ? GameState.Matching : GameState.SwappingBack);
                }

                break;

            // Wait till SwapBack ANIM will end then go Idle
            case GameState.SwappingBack:
                _animTimer += dt;
                if (_animTimer >= AnimDuration)
                {
                    _board.Swap(_swapX1, _swapY1, _swapX2, _swapY2);
                    EnterGameState(GameState.Idle);
                }

                break;

            case GameState.CreateBonuses:
                _animTimer += dt;
                if (_animTimer >= AnimDuration)
                {
                    _board.CreateBonuses(_swapX1, _swapY1, _swapX2, _swapY2);
                    _board.MarkMatches();
                    EnterGameState(GameState.Removing);
                }

                break;

            // Wait until swap ANIM ends then mark matches and go to Removing 
            case GameState.Matching:
                _animTimer += dt;
                if (_animTimer >= AnimDuration)
                {
                    if (_board.HasBonusMatches())
                    {
                        EnterGameState(GameState.CreateBonuses);
                    }
                    else
                    {
                        _board.MarkMatches();
                        EnterGameState(GameState.Removing);
                    }
                }

                break;

            // Wait till all gems disappear ANIM will end: it happens after _lastRemovalTime + AnimDuration
            // then update logic
            case GameState.Removing:
                _animTimer += dt;

                if (_animTimer >= _lastRemovalTime + AnimDuration)
                {
                    _destroyerAnimations.Clear();
                    _board.CallDestruction();
                    _board.ReshuffleFreeGems();
                    EnterGameState(GameState.Falling);
                }

                break;

            // Wait till Fall ANIM will end then 
            case GameState.Falling:
                _animTimer += dt;
                if (_animTimer >= AnimDuration)
                {
                    _board.FallCols(); // CALLING AFTER ANIMATION
                    EnterGameState(GameState.Filling);
                }

                break;
            case GameState.Filling:
                _animTimer += dt;
                if (_animTimer >= AnimDuration)
                {
                    EnterGameState(_board.FindMatches() ? GameState.Matching : GameState.Idle);
                }

                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private Color GetGemColor(Gem gem)
    {
        var color = gem._getGemType() switch
        {
            GemType.Pentagon => Color.Red,
            GemType.Triangle => Color.Blue,
            GemType.Square => Color.Green,
            GemType.Diamond => Color.Yellow,
            GemType.Circle => Color.Purple,
            _ => Color.White
        };
        return color;
    }

    protected override void Draw(GameTime gameTime)
    {
        // Clear the screen with a specific color (standard is CornflowerBlue)
        GraphicsDevice.Clear(new Color(252, 248, 230));

        // TODO: Add your drawing code here

        _spriteBatch.Begin(samplerState: SamplerState.LinearClamp);
        switch (_screenState)
        {
            case ScreenState.MainMenu:
                DrawMainMenu(gameTime);
                break;
            case ScreenState.Playing:
                DrawGame(gameTime);
                DrawHud(gameTime);
                break;
            case ScreenState.GameOver:
                DrawGameOver(gameTime);
                break;
        }

        _spriteBatch.End();
        base.Draw(gameTime);
    }

    protected void DrawMainMenu(GameTime gameTIme)
    {
        DrawNotebookBackground();

        var mouse = Mouse.GetState();
        var hover = _playButtonRect.Contains(mouse.Position);
        var color = hover ? Color.LightGray * 0.15f : Color.White * 0f;

        _spriteBatch.Draw(_pixel, _playButtonRect, color);
        _spriteBatch.Draw(_playButtonTexture,
            new Vector2(_playButtonRect.Center.X - 100, _playButtonRect.Center.Y - 30),
            Color.White);
    }

    protected void DrawGameOver(GameTime gameTIme)
    {
        DrawNotebookBackground();

        var mouse = Mouse.GetState();
        var hover = _newGameButtonRect.Contains(mouse.Position);
        var color = hover ? Color.LightGray * 0.15f : Color.White * 0f;

        _spriteBatch.Draw(_gameOverTexture,
            new Vector2(_newGameButtonRect.Center.X - 100,
                _newGameButtonRect.Center.Y - 30 - _newGameButtonRect.Height - 60 - 10),
            Color.White);

        _spriteBatch.Draw(_yourScoreTexture,
            new Vector2(_newGameButtonRect.Center.X - 100,
                _newGameButtonRect.Center.Y - 30 - _newGameButtonRect.Height),
            Color.White);
        DrawNumber(_board.GetScore(),
            new Vector2(_newGameButtonRect.Center.X + 100,
                _newGameButtonRect.Center.Y - 30 - _newGameButtonRect.Height + 5),
            1f); // Y + score texture + offset 

        _spriteBatch.Draw(_pixel, _newGameButtonRect, color);
        _spriteBatch.Draw(_newGameButtonTexture,
            new Vector2(_newGameButtonRect.Center.X - 100, _newGameButtonRect.Center.Y - 30),
            Color.White);
    }

    private void DrawNotebookBackground()
    {
        int w = _graphics.PreferredBackBufferWidth;
        int h = _graphics.PreferredBackBufferHeight;
        // _spriteBatch.Draw(_pixel, new Rectangle(0, 0, w, h), new Color(252, 248, 230));

        var lineColor = new Color(150, 180, 220);
        for (var y = BoardOffsetY + CellSize; y <= BoardOffsetY + GridHeight * CellSize; y += CellSize)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(0, y, w, 1), lineColor);
        }

        for (var x = BoardOffsetX; x < BoardOffsetX + GridHeight * CellSize; x += CellSize)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(x, 0, 1, h - HudHeight), lineColor);
        }

        _spriteBatch.Draw(_pixel, new Rectangle(BoardOffsetX + CellSize, 0, 1, h), Color.DarkRed);
    }

    private void DrawNumber(int number, Vector2 position, float scale = 1f)
    {
        var strNumber = number.ToString();
        var x = position.X;

        foreach (var ch in strNumber)
        {
            int digit = ch - '0'; // ASCII ch ('0':48 , '1':49, '2': 50, etc.) - ASCII '0':48 = int digit
            var sourceRect = new Rectangle(digit * DigitWidth, 0, DigitWidth, DigitHeight);
            var destRect = new Rectangle(
                (int)x, (int)position.Y,
                (int)(DigitWidth * scale), (int)(DigitHeight * scale));

            _spriteBatch.Draw(_digitTexture, destRect, sourceRect, Color.White);
            x += DigitWidth * scale;
        }
    }

    private void DrawHud(GameTime gameTime)
    {
        //Background
        _spriteBatch.Draw(_pixel,
            new Rectangle(0, CellSize * GridHeight + 1, CellSize * GridHeight - 1, HudHeight),
            new Color(252, 248, 230));

        //Redline
        _spriteBatch.Draw(_pixel, new Rectangle(BoardOffsetX + CellSize, GridHeight * CellSize, 1, HudHeight),
            Color.DarkRed);

        _spriteBatch.Draw(_scoreTexture, new Vector2(CellSize + 10, CellSize * GridHeight + 10), Color.Black);
        // Score
        DrawNumber(_board.GetScore(),
            new Vector2(_scoreTexture.Width + CellSize + 10 + 10, CellSize * GridHeight + 20),
            0.7f); // Y + score texture + offset 

        // "TIME" lined on right
        var timeLabelX = _graphics.PreferredBackBufferWidth - _timeTexture.Width - DigitWidth * 2 - 20;
        _spriteBatch.Draw(_timeTexture, new Vector2(timeLabelX, CellSize * GridHeight + 10), Color.Black);
        // TIMER
        int seconds = (int)Math.Ceiling(_timeRemaining);
        DrawNumber(seconds,
            new Vector2(timeLabelX + _timeTexture.Width,
                CellSize * GridHeight + 20), 0.7f); // Y + score texture + offset
    }

    protected void DrawGame(GameTime gameTIme)
    {
        var t = Math.Clamp(_animTimer / AnimDuration, 0f, 1f);

        DrawNotebookBackground();

        // Gems
        for (var x = 0; x < GridHeight; x++)
        for (var y = 0; y < GridHeight; y++)
        {
            var gem = _board.GetGem(x, y);
            if (gem is null) continue;

            // var color = GetGemColor(gem);
            var pos = CellToScreen(x, y);

            var scale = 1f;
            var alpha = 1f;

            Texture2D texture;

            if (gem._getBonusType() != BonusType.None &&
                _bonusGemTextures.TryGetValue((gem._getGemType(), gem._getBonusType()), out var bonusTex))
            {
                texture = bonusTex;
            }
            else if (gem._getGemType() != GemType.None)
            {
                texture = _gemTextures[gem._getGemType()];
            }
            else
            {
                continue;
            }

            // ???Creating interpolating Vector to Animate Tile Path???
            var animIndex = _gemAnimations.FindIndex(a => a.GridX == x && a.GridY == y);


            switch (_state)
            {
                case GameState.Swapping or GameState.SwappingBack:
                    if (animIndex >= 0)
                        pos = Vector2.Lerp(_gemAnimations[animIndex].From,
                            _gemAnimations[animIndex].To, t);
                    break;
                case GameState.CreateBonuses:
                    if (animIndex >= 0)
                    {
                        pos = Vector2.Lerp(_gemAnimations[animIndex].From,
                            _gemAnimations[animIndex].To, t);

                        if (!(_gemAnimations[animIndex].From == _gemAnimations[animIndex].To))
                        {
                            scale = 1f - t;
                            alpha = 1f - t;
                        }
                    }

                    break;

                case GameState.Removing:
                    if (_removalStartTimes.TryGetValue((x, y), out var startTime))
                    {
                        var elapsed = _animTimer - startTime;
                        if (elapsed < 0) scale = 1f;
                        else
                        {
                            var localT = Math.Clamp(elapsed / RemovalAnimDuration, 0f, 1f);
                            scale = 1f - localT;
                            alpha = 1f - localT;
                        }
                    }

                    break;
                case GameState.Falling:
                    var fallIdx = _gemAnimations.FindIndex(a => a.GridX == x && a.GridY == y);
                    if (fallIdx >= 0)
                        pos = Vector2.Lerp(_gemAnimations[fallIdx].From,
                            _gemAnimations[fallIdx].To, t);
                    break;
                case GameState.Filling:
                    var fillIdx = _gemAnimations.FindIndex(a => a.GridX == x && a.GridY == y);
                    if (fillIdx >= 0)
                        pos = Vector2.Lerp(_gemAnimations[fillIdx].From,
                            _gemAnimations[fillIdx].To, t);
                    break;
            }


            //Disappearing -> decrease size

            var size = (int)(60 * scale);
            var offset = (60 - size) / 2;

            _spriteBatch.Draw(
                texture,
                position: pos + new Vector2(2 + offset, 2 + offset),
                sourceRectangle: null,
                color: Color.White * alpha,
                rotation: 0f,
                origin: Vector2.Zero,
                scale: new Vector2(size / (float)texture.Width),
                effects: SpriteEffects.None,
                layerDepth: 0f
            );
        }

        // Destroyers


        if (_destroyerAnimations.Count > 0)
        {
            foreach (var animation in _destroyerAnimations)
            {
                var elapsed = _animTimer - animation.LaunchTime;
                // if (elapsed < 0) continue;

                // if (animation.Type != BonusType.LineHorizontal || animation.Type != BonusType.LineVertical)
                //     continue;

                var t1 = Math.Clamp(elapsed / animation.TravelDuration, 0f, 1f);
                var pos = Vector2.Lerp(animation.From, animation.To, t1);
                var texture = _destroyerTextures[animation.Type];

                _spriteBatch.Draw(
                    texture,
                    position: pos + new Vector2(2, 2),
                    sourceRectangle: null,
                    color: Color.White,
                    rotation: 0f,
                    origin: Vector2.Zero,
                    scale: 60f / texture.Width,
                    effects: SpriteEffects.None,
                    layerDepth: 0f
                );
            }
        }

        // Selection
        if (_selected is not null)
        {
            var (sx, sy) = _selected.Value;
            var screenPos = CellToScreen(sx, sy);
            _spriteBatch.Draw(_pixel,
                new Rectangle((int)screenPos.X, (int)screenPos.Y, CellSize, CellSize),
                Color.White * 0.25f); // полупрозрачная подсветка поверх
        }
    }
}