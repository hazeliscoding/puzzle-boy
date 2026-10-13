using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PuzzleBoy.Rules;

namespace PuzzleBoy;

public sealed class PuzzleBoyGame : Game
{
    public PuzzleBoyGame(IReadOnlyList<Stage> stages)
    {
        // The manager registers itself as a game service, which keeps it alive.
        _ = new GraphicsDeviceManager(this);
        Window.Title = $"Puzzle Boy ({stages.Count} stages)";
        IsMouseVisible = true;
    }

    protected override void Update(GameTime gameTime)
    {
        if (Keyboard.GetState().IsKeyDown(Keys.Escape))
        {
            Exit();
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);
        base.Draw(gameTime);
    }
}
