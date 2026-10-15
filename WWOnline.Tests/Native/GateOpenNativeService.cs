using WWOnline.Data;
using WWOnline.Services;
using WWOnline.Tests.Services;

namespace WWOnline.Tests.Native;

/// <summary>
/// The native shim (<see cref="NativeGameService"/> over a <see cref="FakeNativeGame"/>) with the scene gate faked
/// open: the save window reaches the game, every other address (Link's pointer, the stage name, the pending stage
/// change, the event mode, the pause menu: <see cref="SceneStabilityGate"/>'s reads) answers from <see cref="Gate"/>.
/// </summary>
internal sealed class GateOpenNativeService : GameMemoryService
{
    private readonly NativeGameService _native;

    public GateOpenNativeService(FakeNativeGame game, string stage)
    {
        _native = new NativeGameService(game);
        Gate.SetU32(GameMemoryAddresses.Player.LinkActorPointer.Address, 0x80400000);
        Gate.Set(GameMemoryAddresses.Stage.CurrentStageName.Address, [.. System.Text.Encoding.ASCII.GetBytes(stage), 0]);
    }

    public FakeDolphin Gate { get; } = new();

    public override bool IsConnected => true;
    public override int? ConnectedProcessId => null;
    public override long? EmulatedMemorySize => null;
    public override event EventHandler<bool>? ConnectionChanged { add { } remove { } }
    public override Task<bool> ConnectAsync() => Task.FromResult(true);
    public override Task<bool> ConnectAsync(int processId) => Task.FromResult(true);
    public override IReadOnlyList<DolphinProcessInfo> EnumerateProcesses() => [];
    public override void Disconnect() { }
    public override void Dispose() => _native.Dispose();

    public override byte[]? ReadMemory(uint address, int size) =>
        InSaveWindow(address, size) ? _native.ReadMemory(address, size) : Gate.ReadMemory(address, size);

    public override bool WriteMemory(uint address, byte[] data) =>
        InSaveWindow(address, data.Length) ? _native.WriteMemory(address, data) : Gate.WriteMemory(address, data);

    private static bool InSaveWindow(uint address, int size) =>
        address >= GameMemoryAddresses.GameInfo && address + (uint)size <= GameMemoryAddresses.GameInfo + FakeNativeGame.SaveSizeBytes;
}
