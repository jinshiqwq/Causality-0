using System.ComponentModel;

namespace Causality0
{

    public enum C0CompressionMode
    {
        None,
        Lzma
    }

    public enum C0CompressionPreset
    {
        FastestSpeed,
        FastSpeed,
        Normal,
        HighCompression,
        MaximumCompression
    }

    public sealed class Causality0Config
    {
        [Description("默认录制帧率 只影响新开始的录制，不影响加载已有回放。推荐 15-120 / Default recording FPS. Only affects new recordings and does not affect loading existing replays. Recommended range: 15-120.")]
        public int DefaultRecordFps { get; set; } = 60;

        [Description("是否录制玩家语音 关闭时仍可正常录制与回放其他数据，只是不保存语音包 / Whether to record player voice. When disabled, other data can still be recorded and replayed normally, but voice packets will not be saved.")]
        public bool RecordVoice { get; set; } = false;

        [Description("回放文件压缩模式 可选 None、Lzma。保存时按所选协议写入，加载时会自动识别格式，不影响回放解码 / Replay file compression mode. Available values: None, Lzma. Saving uses the selected format, and loading auto-detects the format so playback decoding remains compatible.")]
        public C0CompressionMode ReplayCompression { get; set; } = C0CompressionMode.Lzma;

        [Description("回放文件压缩档位 可选 FastestSpeed、FastSpeed、Normal、HighCompression、MaximumCompression。当前仅影响 Lzma / Replay compression preset. Available values: FastestSpeed, FastSpeed, Normal, HighCompression, MaximumCompression. Currently only affects Lzma.")]
        public C0CompressionPreset ReplayCompressionPreset { get; set; } = C0CompressionPreset.Normal;
    }
}
