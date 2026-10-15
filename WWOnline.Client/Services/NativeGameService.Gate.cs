using WWOnline.Native;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

// The scene facts and the warp through tww.h's "gate" calls instead of play addresses.
public sealed partial class NativeGameService : IGameSceneSource, IGameWarp
{
    public GameScene? ReadScene() => ReadSceneAndLink()?.Scene;

    public bool MoveLink(Vector3 pos, short angleY) =>
        IsConnected && Call(api => api is ITwwGateApi gate && gate.WarpLocal(pos.X, pos.Y, pos.Z, angleY));

    public bool ChangeStage(StageEntry entry) =>
        IsConnected && Call(api => api is ITwwGateApi gate && gate.ChangeStage(entry.Name, entry.Point, entry.Room, entry.Layer));

    public Vector3? LinkPosition() => IsConnected
        ? Call<Vector3?>(api => api.GetLocalState(out var local) ? new Vector3(local.Pos[0], local.Pos[1], local.Pos[2]) : null)
        : null;

    protected override void ReadPlayerScene(GameState state)
    {
        if (ReadSceneAndLink() is not var (scene, local))
            return;
        if (local.Valid != 0)
        {
            state.Player.PositionX = local.Pos[0];
            state.Player.PositionY = local.Pos[1];
            state.Player.PositionZ = local.Pos[2];
        }
        // The GameCube client reads the status word's top two bytes (big-endian).
        state.Player.StatusFlags1 = (byte)(scene.PlayerStatus0 >> 24);
        state.Player.StatusFlags2 = (byte)(scene.PlayerStatus0 >> 16);
        // The Great Sea's rooms are its sectors.
        state.Player.CurrentSector = scene.Stage == PuppetVisibility.SeaStage && scene.Room > 0 ? (byte)scene.Room : (byte)0;
        state.StageName = scene.Stage;
    }

    private (GameScene Scene, TwwLocalState Local)? ReadSceneAndLink()
    {
        if (!IsConnected) return null;
        return Call<(GameScene, TwwLocalState)?>(api =>
        {
            if (api is not ITwwGateApi gate) return null;
            api.GetLocalState(out var local);
            return (GameScene.FromNative(gate.GetGateState(), local), local);
        });
    }
}
