namespace ShalevOhad.DCS.SRS.Recorder.Core
{
    public static class Constants
    {
        /// <summary>
        /// The version of the SRS Recording Client.
        /// </summary>
        public const string VERSION = "0.9.0";

        /// <summary>
        /// The minimum required SRS server version for compatibility.
        /// </summary>
        public const string MINIMUM_SERVER_VERSION = "2.3.2.1";

        /// <summary>
        /// Output sample rate for audio processing (in Hz).
        /// </summary>
        public const int OUTPUT_SAMPLE_RATE = 48000;

        /// <summary>
        /// OPUS frame size in samples for 20ms at 48kHz (mono)
        /// </summary>
        public const int OPUS_FRAME_SIZE = 960;

        /// <summary>
        /// OPUS frame duration in milliseconds
        /// </summary>
        public const int OPUS_FRAME_DURATION_MS = 20;
        
        /// <summary>
        /// Default volume level (100%)
        /// </summary>
        public const float DEFAULT_VOLUME = 1.0f;
        
        /// <summary>
        /// Maximum volume level (200%)
        /// </summary>
        public const float MAX_VOLUME = 2.0f;
    }
}
