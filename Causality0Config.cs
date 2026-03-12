using System.ComponentModel;

namespace Causality0;

public sealed class Causality0Config
{
    [Description("默认录制帧率 只影响新开始的录制，不影响加载已有回放。推荐 15-120 / Default recording FPS. Only affects new recordings and does not affect loading existing replays. Recommended range: 15-120.")]
    public int DefaultRecordFps { get; set; } = 60;

    [Description("是否录制玩家语音 关闭时仍可正常录制与回放其他数据，只是不保存语音包 / Whether to record player voice. When disabled, other data can still be recorded and replayed normally, but voice packets will not be saved.")]
    public bool RecordVoice { get; set; } = false;
}
