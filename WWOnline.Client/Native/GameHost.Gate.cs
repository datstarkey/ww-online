namespace WWOnline.Native;

public sealed unsafe partial class GameHost
{
    private sealed partial class TwwApi : ITwwGateApi
    {
        public TwwGateState GetGateState()
        {
            TwwGateState state;
            TwwNative.tww_gate_get_state(&state);
            return state;
        }

        public bool WarpLocal(float x, float y, float z, short angleY)
        {
            float* pos = stackalloc float[3] { x, y, z };
            return TwwNative.tww_gate_warp_local(pos, angleY) == 1;
        }

        public bool ChangeStage(string stage, short point, sbyte room, sbyte layer) =>
            TwwNative.tww_gate_change_stage(stage, point, room, layer) == 1;
    }
}
